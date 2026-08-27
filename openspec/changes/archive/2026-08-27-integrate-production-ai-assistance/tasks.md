# Tasks

## 1. Adapter and processing
- [x] 1.1 Extend `IAiAssistant` with typed, versioned requests/results and implement a configured production adapter.
- [x] 1.2 Execute imports/translations with timeout, retry, cancellation, and schema validation through the durable job retry envelope.
- [x] 1.3 Add forward-only provenance/status fields and preserve original source content.

## 2. Safety and UX
- [x] 2.1 Enforce input/output limits, tenant quotas, owner scope, and explicit approval before guide mutation or publication.
- [x] 2.2 Label AI output and expose queued, processing, invalid-output, provider-unavailable, retry, and review states in Flutter.
- [x] 2.3 Prevent secrets and unrelated personal data from entering requests, logs, telemetry, or responses.

## 3. Verification
- [x] 3.1 Add adapter contract tests and API tests for ownership, quota, malformed output, timeout, retry, duplicate execution, and disabled configuration.
- [x] 3.2 Test human edits and approval without automatic publication.
- [x] 3.3 Document provider configuration/data handling and run backend and Flutter gates.
