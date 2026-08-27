# Agents.md

> This document is the normative contract for AI agents and humans working on
> Trippify. Every rule here MUST be followed unless an approved OpenSpec change
> explicitly replaces it.

---

## 1. What is Trippify?

Trippify is a **structured travel-guide authoring, marketplace, forking, and
hosting platform**. Creators publish structured day-by-day guides with budgets,
verified travelers can prove they actually completed a trip, and consumers
discover, purchase, fork, and review guides. The platform supports both a
public marketplace and a self-hosted distribution that operators can deploy on
their own infrastructure.

**Product constraints — non-negotiable unless changed through OpenSpec:**

- **Server-authoritative policy.** Authorization, ownership, role checks,
  payment state, entitlement checks, order transitions, commission, refund
  rules, moderation, and licensing MUST be enforced by the backend. Clients
  never enforce entitlements alone.
- **Privacy by projection.** Public creator fields are projected separately
  from private profile data; verification tokens, status history, roles, audit
  data, and email confirmation state never leak through public endpoints.
- **Snapshot commercial terms.** When payment succeeds, the service price,
  promised revision allowance, commission basis, and refund rules are
  snapshotted. Editing a public guide MUST NOT mutate a paid order's
  commercial terms.
- **Idempotent background work and webhooks.** Payment, evidence review, and
  notification callbacks are processed exactly once. Replay is a hard
  requirement, not a nice-to-have.
- **Forward-only migrations.** PostgreSQL migrations are forward-only and
  verified from an empty database and from the previous release.
- **Secrets never reach clients.** Provider keys, payment credentials, and
  signing keys are server-side only.

**Current status:** foundation, marketplace MVP, trust, and distribution
changes are merged. New capabilities are added one OpenSpec change at a time
following the delivery roadmap in `openspec/ROADMAP.md`.

---

## 2. Architecture — ASP.NET Core Modular Monolith

The backend is a C# / ASP.NET Core Web API with a Flutter client. Prefer one
deployable modular monolith with clear boundaries. Do not introduce
microservices, a native app, or a new database engine without an approved
architectural change.

```text
src/
├── Trippify.Domain/         # shared primitives
├── Trippify.Application/    # adapter contracts (IEmailSender, IObjectStorage, ...)
├── Trippify.Infrastructure/ # EF Core entities, migrations, AppDbContext, providers
└── Trippify.Api/            # HTTP API, endpoints, OpenAPI / Swagger
apps/
└── trippify_flutter/        # Flutter mobile and web client
tests/
├── Trippify.ApiTests/             # xUnit suite for the HTTP API
└── Trippify.ArchitectureTests/    # architectural invariants
```

The expected domain boundaries within `Trippify.Api` are organized by feature
endpoint file (e.g. `IdentityEndpoints.cs`, `GuideEndpoints.cs`,
`CommerceEndpoints.cs`, `ReviewEndpoints.cs`). Cross-feature work goes through
explicit services or contracts; avoid direct navigation across feature
ownership boundaries.

### 2.1 Module rules

- Domain, Application, and Infrastructure projects MUST NOT reference the
  Web host or concrete API endpoints.
- Provider integrations for email, storage, maps, payments, AI, and background
  jobs MUST use replaceable interfaces and configuration.
- Every owner-scoped query MUST filter by the authenticated user or explicit
  operator scope. Returning `404` is preferred when revealing object existence
  would leak private information.
- Money MUST use decimal-safe types and an explicit currency.
- Financial, audit, and moderation histories MUST be append-only or otherwise
  retain a complete trail; never silently rewrite material history.
- Background work MUST be idempotent and retry-safe.
- Forward-only PostgreSQL migrations are mandatory. No down migrations in
  production paths.
- Public/paid fields are separated in server projections; Flutter never
  enforces entitlements alone.

---

## 3. Spec-First Development — Fixed Workflow

Every behavior change goes through OpenSpec before application code is
written:

```text
propose → validate → implement → verify → archive → commit
```

The source of truth for current product behavior is:

```text
openspec/specs/<capability>/spec.md
```

The planned delivery order is in:

```text
openspec/ROADMAP.md
```

Each implementation change belongs in:

```text
openspec/changes/<change-name>/
├── proposal.md
├── design.md
├── tasks.md
└── specs/<capability>/spec.md (when adding a new capability or delta)
```

### 3.1 Specification rules

- Use SHALL or MUST for normative requirements.
- Every requirement MUST have at least one `#### Scenario:` with observable
  Given/When/Then behavior.
- A change that modifies an existing requirement MUST include the full
  modified requirement block, not a partial fragment.
- Proposal scope MUST identify MVP inclusions, exclusions, affected roles,
  affected data, and security or financial consequences.
- Design decisions MUST identify reused code, module ownership, authorization,
  data migration, provider failure behavior, privacy, and rollback.
- Tasks are verifiable claims. Do not check a task until its behavior and
  negative paths have been demonstrated.
- Per `openspec/config.yaml`, tasks must keep backend, Flutter, migration, and
  negative-path tests independently verifiable.

### 3.2 Sequential delivery

Implement one OpenSpec change at a time. Finish its code, migrations, tests,
HTTP verification, tasks, archive, and related-only commit before starting
the next change. Do not combine unrelated roadmap capabilities in one change.

Base capability specs are not implementation evidence. Their presence means
the behavior is planned, not shipped.

**Each spec MUST end with this exact sequence once all tasks are finished:**

```bash
# 1. Mark every task checked and re-run the repository quality gates.
dotnet build Trippify.sln
dotnet test Trippify.sln

# 2. Archive the change. This moves the proposal out of openspec/changes/
#    and merges any spec deltas into openspec/specs/.
openspec archive <change-name> --yes

# 3. Inspect the resulting diff, then commit only paths related to this change.
git status
git add <scoped-paths>
git commit -m "<conventional-commit-message>"
```

Do not archive when tasks are still unchecked. Do not archive before
`dotnet build` and `dotnet test` are green. Do not combine the archive or
commit of one change with another. Do not push unless explicitly requested.

---

## 4. Production-Quality Standard

Implementation MUST be complete behavior, not scaffolding presented as done.

- No `TODO`, placeholder result, fake provider success, swallowed exception,
  or `NotImplementedException` in a completed feature.
- Validate all input server-side with explicit length, type, file-size, and
  state constraints.
- Enforce authentication, role, resource ownership, and account status at
  every protected endpoint. Test anonymous, wrong-user, creator, and
  administrator paths.
- Treat payment-provider signed events as payment authority. Verify
  signatures, persist an inbox record, and process each event idempotently.
- Snapshot service price, promised entitlements, and refund rules when
  payment succeeds.
- Preserve original commercial evidence (price snapshot, promised revision
  allowance, commission basis) even when public content is later edited.
- Label AI output and require explicit creator or operator confirmation
  before publishing.
- Do not log secrets, tokens, full payment data, identity documents, or
  private profile content.
- Provider outages (AI, email, storage, payments, maps) MUST fail safely and
  MUST NOT fabricate success. Email and payment adapters are caught at the
  endpoint so a transient outage cannot block account creation or order
  placement.

---

## 5. Build, Run, and Verification

Use the repository's committed SDK and tooling configuration
(`global.json`, `Directory.Build.props`). Expected baseline gates are:

```bash
dotnet restore Trippify.sln
dotnet build Trippify.sln --no-restore
dotnet test Trippify.sln --no-build
```

For Flutter changes:

```bash
cd apps/trippify_flutter
flutter pub get
flutter test
```

If the repository provides a more specific quality command, use it instead
of inventing alternatives.

Completion requires evidence proportional to the change:

- clean build with zero warnings and zero errors;
- unit, integration, and architecture tests passing;
- migrations apply to an empty database and upgrade the supported prior state;
- every OpenSpec scenario exercised at the appropriate layer;
- happy path and negative HTTP paths verified using real authentication;
- anonymous, wrong-user, creator, and administrator role boundaries verified;
- cross-user and cross-creator isolation verified;
- payment webhook replay, duplicate background work, and retry behavior
  tested when applicable;
- financial reconciliation verified when money moves;
- responsive and accessible browser/mobile behavior checked for affected
  pages.

Do not weaken analyzers, exclude failing tests, bypass hooks, or lower
coverage to make a gate pass.

---

## 6. Agent Workflow Checklist

When asked to implement a feature or OpenSpec change:

1. Read this file, `README.md`, `openspec/ROADMAP.md`, `openspec/config.yaml`,
   the active change's artifacts, and all affected source-of-truth specs.
2. Inspect `git status`, the live OpenSpec change list, and existing code
   before planning. Preserve unrelated and partial work.
3. Confirm that the requested change is next in the agreed delivery order.
   Do not infer that a planned capability is already active.
4. Search for existing entities, services, endpoints, components, tests, and
   provider adapters. Record reuse decisions in `design.md`.
5. Implement tasks from top to bottom within the owning modules.
6. Verify every scenario, including authorization, ownership, invalid-state,
   provider-failure, and idempotency paths relevant to the change.
7. Run all repository quality gates and inspect the resulting diff.
8. Check tasks only when evidence exists.
9. Once every task in `tasks.md` is checked, run
   `openspec archive <name> --yes` to merge the change into the
   source-of-truth specs.
10. Commit only paths related to the completed change using a conventional
    commit message. Do not push unless explicitly requested.

For a documentation-only or specification-only request, validate the planning
artifacts and do not create application code, migrations, or infrastructure.

---

## 7. Anti-Patterns

- Do not implement behavior without an approved, strictly valid OpenSpec
  change.
- Do not implement or archive multiple changes together.
- Do not claim a base spec, checked task, build, archive, commit, push, or
  deployment that was not directly verified.
- Do not allow clients to choose roles, prices, commissions, order state,
  payment result, refund result, settlement eligibility, or moderation
  result.
- Do not expose one creator's private profile, drafts, evidence, or
  financial data to another creator or unrelated operator.
- Do not let AI publish a professional deliverable or send a client message
  without explicit human (creator or operator) review.
- Do not mutate paid-order commercial terms when a public guide is edited.
- Do not process payment, refund, evidence, or notification callbacks twice.
- Do not hardcode API keys, provider secrets, commission rates, or signing
  credentials.
- Do not make subjective dislike an automatic full-refund rule; apply the
  snapshotted service promise and evidence-based dispute policy.
- Do not delete dispute or financial evidence merely because a participant
  edits or deletes their visible content.
- Do not add excluded MVP features (native apps, social feeds, livestream,
  multi-currency, multi-level distribution, owned retail) without a separate
  approved change.

---

## 8. Current State and Delivery Order

### 8.1 Source-of-truth capability specs

The repository currently defines these capabilities:

1. `platform-foundation`
2. `user-creator-identity`
3. `structured-guide-authoring`
4. `route-map-budget-planning`
5. `guide-publishing-discovery`
6. `guide-commerce-entitlements`
7. `personal-library-forks`
8. `guide-reviews-feedback`
9. `verified-trip-evidence`
10. `creator-admin-workspaces`
11. `guide-versioning-freshness`
12. `commercial-remixes-revenue`
13. `creator-follows-notifications`
14. `assisted-import-translation`
15. `integration-plugin-system`
16. `managed-saas-hosting`
17. `self-hosted-distribution`
18. `email-delivery-resend`
19. `email-configuration-resend`

A capability's presence in this list means its spec is the source of truth.
A change is "shipped" only after `openspec archive` has merged the delta
into `openspec/specs/`.

### 8.2 Recommended implementation sequence

The active roadmap lives in `openspec/ROADMAP.md`. Foundation, marketplace
MVP, trust, and distribution changes are already merged. The next
incremental changes should be picked from the active `openspec list` in the
order the user (or maintainer) requests — never silently from this
document.

### 8.3 Deferred from MVP

Native apps; social community and feeds; live streaming; short video;
multi-level distribution; owned retail; livestream commerce; complex
membership levels; and international multi-currency.

---

## 9. References

- `README.md` — product overview, prerequisites, run instructions
- `openspec/ROADMAP.md` — delivery order and cross-change gates
- `openspec/config.yaml` — OpenSpec schema configuration
- `openspec/specs/*/spec.md` — current capability source of truth
- `openspec/changes/<name>/` — proposed and active implementation changes
- `openspec/changes/archive/` — completed changes
- `docs/*.md` — per-domain design notes (identity, guides, planning,
  discovery, commerce, library, reviews, verified trips, operations,
  notifications, versioning, plugins, managed SaaS, assisted import,
  commercial remixes, self-hosted)
