# AGENTS.md

## Mission

Build a small, auditable system for a public pilot study of AI trust calibration. The repository is research infrastructure first and a product second.

## Non-negotiable research invariants

1. Never expose provider or model identity to the participant before all blind evaluations for the task are submitted.
2. Randomize A/B/C labels per task; never use a stable provider-to-label mapping.
3. Preserve the exact submitted task prompt and exact raw model output. Do not silently repair model answers.
4. Criteria submitted before generation (`expectedCore`, `criticalErrors`, `disputedAreas`) are immutable for the scored run.
5. Excluded tasks and exclusion reasons remain auditable; do not delete inconvenient results.
6. Model names, provider settings and run timestamps must be stored with each generation.
7. API keys and tokens must never be committed.

## Delivery style

- Optimize for a small pilot, not speculative scale.
- Prefer explicit code over framework-heavy architecture.
- ASP.NET Core backend and React/TypeScript frontend.
- UTF-8 only.
- Keep provider-specific HTTP contracts inside provider adapters.
- The application must run without real AI credentials using the mock provider.
- Do not add broad cleanup or unrelated abstractions while implementing a study slice.

## Current phase

Pilot. In-memory storage is acceptable for the first runnable contour. Persistent storage is required before the main study and protocol freeze.

## Read before changing research behavior

- `docs/research/hypothesis.md`
- `docs/research/protocol-v0-pilot.md`
- `docs/research/scoring-rubric.md`
- `docs/research/ai-run-contract.md`
