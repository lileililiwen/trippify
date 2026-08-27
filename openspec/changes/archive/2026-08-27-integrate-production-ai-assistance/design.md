# Context
The local AI adapter returns its input unchanged and processing completes synchronously.

# Goals / Non-goals
- Produce validated structured drafts while preserving explicit human approval.
- Keep deterministic local/test behavior clearly labeled as unavailable, not successful AI output.

# Decisions
- Run provider calls in durable jobs using server-side credentials, bounded input/output, timeout, retry, and cancellation.
- Validate provider output against a versioned schema before storing a private draft.
- Persist provider/model identifiers, prompt/schema version, source linkage, and timestamps without storing secrets or hidden reasoning.
- Charge quota only for accepted provider usage and surface provider-unavailable/invalid-output states.

# Authorization, privacy, and failure modes
- Only the owner can submit sources and review generated drafts; output never auto-publishes.
- Do not send unrelated private profile, purchase, or tenant data to the provider.

# Migration and rollback
- Add provenance/job fields forward-only. Configuration can disable the provider while retaining drafts and source records.
