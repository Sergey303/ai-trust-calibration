import { FormEvent, useState } from "react";
import {
  BlindTask,
  RevealItem,
  createParticipant,
  createTask,
  generateTask,
  getBlindTask,
  revealTask,
  submitEvaluation,
  submitPostSurvey,
} from "./api";

type Step = "pre" | "task" | "blind" | "reveal" | "done";

type EvaluationDraft = {
  severity: number;
  verificationBurden: number;
  hallucinatedFact: boolean;
  admittedInsufficientData: boolean;
  rationale: string;
};

const TASKS_PER_PARTICIPANT = 3;

const emptyEvaluation = (): EvaluationDraft => ({
  severity: 0,
  verificationBurden: 0,
  hallucinatedFact: false,
  admittedInsufficientData: false,
  rationale: "",
});

function splitLines(value: string): string[] {
  return value
    .split("\n")
    .map((item) => item.trim())
    .filter(Boolean);
}

function App() {
  const [step, setStep] = useState<Step>("pre");
  const [participantId, setParticipantId] = useState("");
  const [taskId, setTaskId] = useState("");
  const [completedTaskCount, setCompletedTaskCount] = useState(0);
  const [blindTask, setBlindTask] = useState<BlindTask | null>(null);
  const [reveal, setReveal] = useState<RevealItem[]>([]);
  const [activeLabel, setActiveLabel] = useState("");
  const [drafts, setDrafts] = useState<Record<string, EvaluationDraft>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Неизвестная ошибка");
    } finally {
      setBusy(false);
    }
  }

  async function handlePreSurvey(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await run(async () => {
      const id = await createParticipant({
        priorErrorEstimate: Number(form.get("priorErrorEstimate")),
        firstModelAssociation: String(form.get("firstModelAssociation")),
        usedModelsLastThreeMonths: splitLines(String(form.get("usedModels"))),
        chatGptUsageFrequency: String(form.get("chatGptUsageFrequency")),
        verificationHabit: String(form.get("verificationHabit")),
      });
      setParticipantId(id);
      setStep("task");
    });
  }

  async function handleTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await run(async () => {
      const id = await createTask({
        participantId,
        prompt: String(form.get("prompt")),
        expectedCore: splitLines(String(form.get("expectedCore"))),
        criticalErrors: splitLines(String(form.get("criticalErrors"))),
        disputedAreas: splitLines(String(form.get("disputedAreas"))),
      });
      setTaskId(id);
      await generateTask(id);
      const task = await getBlindTask(id);
      setBlindTask(task);
      setActiveLabel(task.answers[0]?.label ?? "");
      setDrafts(Object.fromEntries(task.answers.map((answer) => [answer.label, emptyEvaluation()])));
      setStep("blind");
    });
  }

  async function saveEvaluation(label: string) {
    const draft = drafts[label];
    if (!draft) return;

    await run(async () => {
      await submitEvaluation(taskId, { label, ...draft });
      const refreshed = await getBlindTask(taskId);
      setBlindTask(refreshed);
      const next = refreshed.answers.find((answer) => !answer.evaluated);
      if (next) {
        setActiveLabel(next.label);
        return;
      }

      const items = await revealTask(taskId);
      setReveal(items);
      setCompletedTaskCount((count) => count + 1);
      setStep("reveal");
    });
  }

  function startNextTask() {
    setTaskId("");
    setBlindTask(null);
    setReveal([]);
    setActiveLabel("");
    setDrafts({});
    setError("");
    setStep("task");
  }

  async function handlePostSurvey(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await run(async () => {
      await submitPostSurvey(participantId, {
        posteriorErrorEstimate: Number(form.get("posteriorErrorEstimate")),
        verificationStrategy: String(form.get("verificationStrategy")),
        trustChange: String(form.get("trustChange")),
        comment: String(form.get("comment")),
      });
      setStep("done");
    });
  }

  const activeAnswer = blindTask?.answers.find((answer) => answer.label === activeLabel);
  const activeDraft = activeLabel ? drafts[activeLabel] : undefined;
  const currentTaskNumber = Math.min(completedTaskCount + 1, TASKS_PER_PARTICIPANT);

  return (
    <main className="shell">
      <header className="hero">
        <p className="eyebrow">AI Trust Calibration · pilot v0</p>
        <h1>Не «верите ли вы ИИ», а насколько точно вы оцениваете риск ошибки?</h1>
        <p>
          Вы дадите три профессиональные задачи, заранее зафиксируете критерии и вслепую оцените
          несколько ответов. Названия моделей раскроются только после оценок каждой задачи.
        </p>
      </header>

      <div className="stepbar">
        <span className={step === "pre" ? "current" : ""}>1. До эксперимента</span>
        <span className={step === "task" ? "current" : ""}>2. Задача {currentTaskNumber}/3</span>
        <span className={step === "blind" ? "current" : ""}>3. Слепая оценка</span>
        <span className={step === "reveal" ? "current" : ""}>4. Раскрытие</span>
      </div>

      {error && <div className="error">{error}</div>}

      {step === "pre" && (
        <form className="card form" onSubmit={handlePreSurvey}>
          <h2>До просмотра ответов</h2>
          <label>
            Вероятность хотя бы одной существенной ошибки современной AI-модели на обычной
            профессиональной задаче, 0–100%
            <input name="priorErrorEstimate" type="number" min="0" max="100" defaultValue="30" required />
          </label>
          <label>
            Какая модель или AI-сервис первым приходит вам в голову?
            <input name="firstModelAssociation" required placeholder="Например: DeepSeek" />
          </label>
          <label>
            Какими моделями пользовались за последние 3 месяца? По одной на строку
            <textarea name="usedModels" rows={4} placeholder={"DeepSeek\nChatGPT"} />
          </label>
          <label>
            Как часто вы пользуетесь ChatGPT?
            <select name="chatGptUsageFrequency" required defaultValue="never">
              <option value="never">Никогда / практически никогда</option>
              <option value="rare">Реже нескольких раз в месяц</option>
              <option value="regular">Несколько раз в месяц</option>
              <option value="weekly">Еженедельно</option>
              <option value="daily">Почти ежедневно</option>
            </select>
          </label>
          <label>
            Как обычно проверяете ответ AI?
            <select name="verificationHabit" required defaultValue="key-facts">
              <option value="none">Практически не проверяю</option>
              <option value="skim">Быстро просматриваю</option>
              <option value="key-facts">Проверяю ключевые утверждения</option>
              <option value="most">Проверяю большинство фактов</option>
              <option value="all">Фактически перепроверяю целиком</option>
            </select>
          </label>
          <button disabled={busy}>Зафиксировать ответы</button>
        </form>
      )}

      {step === "task" && (
        <form className="card form" onSubmit={handleTask}>
          <h2>Задача {currentTaskNumber} из {TASKS_PER_PARTICIPANT}</h2>
          <p className="hint">
            Не загадку и не ловушку для нейросети. Задачу, которую вы действительно могли бы
            передать интеллектуальному помощнику и способны проверить сами.
          </p>
          <label>
            Задача
            <textarea name="prompt" rows={8} required />
          </label>
          <label>
            3–7 обязательных признаков хорошего ответа. Один на строку
            <textarea name="expectedCore" rows={6} required />
          </label>
          <label>
            Какие ошибки существенно меняют решение или делают совет опасным? Одна на строку
            <textarea name="criticalErrors" rows={5} required />
          </label>
          <label>
            Где допустимы разные профессиональные мнения? Один пункт на строку
            <textarea name="disputedAreas" rows={4} />
          </label>
          <div className="notice">
            После запуска критерии фиксируются и не меняются для этой задачи.
          </div>
          <button disabled={busy}>{busy ? "Получаем ответы…" : "Зафиксировать и запустить модели"}</button>
        </form>
      )}

      {step === "blind" && blindTask && activeAnswer && activeDraft && (
        <section className="blind-layout">
          <aside className="card criteria">
            <h2>Ваши критерии</h2>
            <h3>Ядро ответа</h3>
            <ul>{blindTask.expectedCore.map((item) => <li key={item}>{item}</li>)}</ul>
            <h3>Существенные / критические ошибки</h3>
            <ul>{blindTask.criticalErrors.map((item) => <li key={item}>{item}</li>)}</ul>
            {blindTask.disputedAreas.length > 0 && (
              <>
                <h3>Спорные области</h3>
                <ul>{blindTask.disputedAreas.map((item) => <li key={item}>{item}</li>)}</ul>
              </>
            )}
          </aside>

          <div className="card answer-card">
            <nav className="answer-tabs">
              {blindTask.answers.map((answer) => (
                <button
                  type="button"
                  className={answer.label === activeLabel ? "active" : "secondary"}
                  key={answer.label}
                  onClick={() => setActiveLabel(answer.label)}
                >
                  Ответ {answer.label} {answer.evaluated ? "✓" : ""}
                </button>
              ))}
            </nav>

            <article className="answer-text">{activeAnswer.content}</article>

            {activeAnswer.evaluated ? (
              <div className="notice">Оценка ответа зафиксирована и больше не изменяется.</div>
            ) : (
              <div className="evaluation">
                <label>
                  Severity: {activeDraft.severity}
                  <input
                    type="range" min="0" max="3" value={activeDraft.severity}
                    onChange={(event) => setDrafts({ ...drafts, [activeLabel]: { ...activeDraft, severity: Number(event.target.value) } })}
                  />
                  <small>0 — существенных ошибок нет; 1 — мелкая; 2 — существенная; 3 — критическая.</small>
                </label>
                <label>
                  Нагрузка на проверку: {activeDraft.verificationBurden}
                  <input
                    type="range" min="0" max="3" value={activeDraft.verificationBurden}
                    onChange={(event) => setDrafts({ ...drafts, [activeLabel]: { ...activeDraft, verificationBurden: Number(event.target.value) } })}
                  />
                  <small>0 — беглый просмотр; 3 — полная перепроверка или проще сделать самому.</small>
                </label>
                <label className="check">
                  <input
                    type="checkbox" checked={activeDraft.hallucinatedFact}
                    onChange={(event) => setDrafts({ ...drafts, [activeLabel]: { ...activeDraft, hallucinatedFact: event.target.checked } })}
                  />
                  Есть выдуманный факт
                </label>
                <label className="check">
                  <input
                    type="checkbox" checked={activeDraft.admittedInsufficientData}
                    onChange={(event) => setDrafts({ ...drafts, [activeLabel]: { ...activeDraft, admittedInsufficientData: event.target.checked } })}
                  />
                  Модель честно указала на недостаток данных
                </label>
                <label>
                  Почему вы поставили такую оценку?
                  <textarea
                    rows={4} value={activeDraft.rationale}
                    onChange={(event) => setDrafts({ ...drafts, [activeLabel]: { ...activeDraft, rationale: event.target.value } })}
                  />
                </label>
                <button disabled={busy || !activeDraft.rationale.trim()} onClick={() => saveEvaluation(activeLabel)}>
                  Зафиксировать оценку ответа {activeLabel}
                </button>
              </div>
            )}
          </div>
        </section>
      )}

      {step === "reveal" && (
        <section className="card form">
          <h2>Модели для задачи {completedTaskCount} раскрыты</h2>
          <div className="reveal-grid">
            {reveal.map((item) => (
              <article className="reveal-item" key={item.label}>
                <strong>Ответ {item.label}</strong>
                <span>{item.providerDisplayName}</span>
                <code>{item.modelId}</code>
                <small>{item.reasoningMode}</small>
              </article>
            ))}
          </div>

          {completedTaskCount < TASKS_PER_PARTICIPANT ? (
            <>
              <div className="notice">
                Оценки этой задачи зафиксированы. Впереди ещё {TASKS_PER_PARTICIPANT - completedTaskCount}.
              </div>
              <button type="button" onClick={startNextTask}>Перейти к задаче {completedTaskCount + 1}</button>
            </>
          ) : (
            <form className="form nested" onSubmit={handlePostSurvey}>
              <h2>После трёх слепых сравнений</h2>
              <label>
                Теперь оцените вероятность существенной ошибки современной сильной модели, 0–100%
                <input name="posteriorErrorEstimate" type="number" min="0" max="100" required />
              </label>
              <label>
                Как теперь вы считаете правильным проверять ответы?
                <select name="verificationStrategy" required defaultValue="risk-based">
                  <option value="all-ai">Любой AI-ответ нужно полностью проверять</option>
                  <option value="model-based">Глубина проверки должна зависеть от модели</option>
                  <option value="risk-based">Прежде всего от цены ошибки и типа задачи</option>
                  <option value="less-than-before">В целом буду проверять меньше</option>
                  <option value="more-than-before">В целом буду проверять больше</option>
                </select>
              </label>
              <label>
                Как изменилось отношение к надёжности AI?
                <select name="trustChange" required defaultValue="recalibrated">
                  <option value="unchanged">Не изменилось</option>
                  <option value="recalibrated">Стало более дифференцированным</option>
                  <option value="more-trust">Доверия стало больше</option>
                  <option value="less-trust">Доверия стало меньше</option>
                </select>
              </label>
              <label>
                Свободный комментарий
                <textarea name="comment" rows={4} />
              </label>
              <button disabled={busy}>Завершить эксперимент</button>
            </form>
          )}
        </section>
      )}

      {step === "done" && (
        <section className="card done">
          <h2>Спасибо. Оценки трёх задач зафиксированы.</h2>
          <p>
            Пилот нужен прежде всего для проверки методики. Его результаты не должны смешиваться с
            основной выборкой после фиксации protocol v1.
          </p>
        </section>
      )}
    </main>
  );
}

export default App;
