# Why
Assisted imports and translations currently echo the source text, so no assistance occurs despite the product surface claiming AI-supported drafting.

# What Changes
Add a configurable AI adapter with structured output, safety limits, provenance, retry behavior, cost controls, and mandatory human review.

# Capabilities
## New Capabilities
- `production-ai-assistance`: provider-backed import drafting and translation under creator control.

# Dependencies and Non-goals
- Dependencies: assisted import/translation, quota enforcement, and durable jobs.
- Non-goals: autonomous publication, model training on private content, or client-held provider keys.

# Impact
Changes AI contracts/adapters, import processing, provenance, job execution, Flutter review states, and deployment configuration.
