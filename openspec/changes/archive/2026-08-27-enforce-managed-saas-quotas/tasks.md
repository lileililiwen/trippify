# Tasks

## 1. Quota model
- [x] 1.1 Define metered metrics, plan defaults, periods, reservations, adjustments, and immutable usage history.
- [x] 1.2 Add forward-only schema/index changes and idempotent default seeding.
- [x] 1.3 Implement atomic reserve/finalize/release and period rollover services.

## 2. Enforcement surfaces
- [x] 2.1 Enforce relevant guide, AI import, media storage, and background-job limits server-side.
- [x] 2.2 Return stable quota problems and display actionable denied/reset states in Flutter.
- [x] 2.3 Add audited administrator adjustments without silently rewriting history.

## 3. Verification
- [x] 3.1 Test concurrent last-slot requests, failed-operation release, rollover, plan change, suspension, unknown metric, and cross-tenant isolation.
- [x] 3.2 Verify anonymous/member/admin negative paths and telemetry privacy.
- [x] 3.3 Update managed SaaS documentation and run backend, migration, and Flutter gates.
