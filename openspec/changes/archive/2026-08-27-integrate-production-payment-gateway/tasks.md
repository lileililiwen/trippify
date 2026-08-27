# Tasks

## 1. Provider and configuration
- [x] 1.1 Implement a production `IPaymentGateway` adapter with typed options, checkout URL creation, timeouts, and secret-safe errors.
- [x] 1.2 Select local or production adapters through configuration and validate required production settings at startup.
- [x] 1.3 Add any forward-only provider-reference migration and verify empty and prior-release upgrades.

## 2. Commerce and Flutter
- [x] 2.1 Verify provider signatures over the raw webhook body before recording or applying events.
- [x] 2.2 Preserve idempotent paid/refunded transitions, commercial snapshots, entitlements, and ledger uniqueness.
- [x] 2.3 Redirect Flutter users to provider checkout and render cancelled, declined, offline, and retry states.

## 3. Verification and documentation
- [x] 3.1 Test anonymous, self-purchase, invalid discount, provider outage, invalid signature, duplicate event, paid, and refunded paths.
- [x] 3.2 Add adapter contract tests without contacting the live provider.
- [x] 3.3 Update commerce/deployment documentation and run backend and Flutter quality gates.