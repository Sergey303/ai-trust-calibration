const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000";

export type BlindAnswer = {
  label: string;
  content: string;
  evaluated: boolean;
};

export type BlindTask = {
  taskId: string;
  prompt: string;
  expectedCore: string[];
  criticalErrors: string[];
  disputedAreas: string[];
  answers: BlindAnswer[];
  canReveal: boolean;
};

export type RevealItem = {
  label: string;
  providerDisplayName: string;
  modelId: string;
  reasoningMode: string;
  completedAtUtc: string;
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export async function createParticipant(input: {
  priorErrorEstimate: number;
  firstModelAssociation: string;
  usedModelsLastThreeMonths: string[];
  chatGptUsageFrequency: string;
  verificationHabit: string;
}): Promise<string> {
  const result = await request<{ id: string }>("/api/participants", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return result.id;
}

export async function createTask(input: {
  participantId: string;
  prompt: string;
  expectedCore: string[];
  criticalErrors: string[];
  disputedAreas: string[];
}): Promise<string> {
  const result = await request<{ id: string }>("/api/tasks", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return result.id;
}

export async function generateTask(taskId: string): Promise<void> {
  await request(`/api/tasks/${taskId}/generate`, { method: "POST" });
}

export function getBlindTask(taskId: string): Promise<BlindTask> {
  return request(`/api/tasks/${taskId}/blind`);
}

export async function submitEvaluation(
  taskId: string,
  input: {
    label: string;
    severity: number;
    hallucinatedFact: boolean;
    admittedInsufficientData: boolean;
    verificationBurden: number;
    rationale: string;
  },
): Promise<{ canReveal: boolean }> {
  return request(`/api/tasks/${taskId}/evaluations`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function revealTask(taskId: string): Promise<RevealItem[]> {
  return request(`/api/tasks/${taskId}/reveal`);
}

export async function submitPostSurvey(
  participantId: string,
  input: {
    posteriorErrorEstimate: number;
    verificationStrategy: string;
    trustChange: string;
    comment: string;
  },
): Promise<void> {
  await request(`/api/participants/${participantId}/post-survey`, {
    method: "POST",
    body: JSON.stringify(input),
  });
}
