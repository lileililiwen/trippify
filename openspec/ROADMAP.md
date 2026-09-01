# Delivery roadmap

Implement and archive one change at a time. A later change may be refined
before implementation, but must not silently absorb an earlier change. A
validated spec is planned work; it is not shipped until implementation,
verification, archive, and a related-only commit are complete.

## Foundation

1. `establish-platform-foundation`
2. `add-user-and-creator-identity`

## Marketplace MVP

3. `author-structured-travel-guides`
4. `plan-routes-maps-and-budgets`
5. `publish-and-discover-guides`
6. `sell-and-unlock-guides`
7. `manage-personal-library-and-forks`
8. `review-guides-and-report-updates`
9. `operate-creator-and-admin-workspaces`

## Trust and growth

10. `version-guides-and-track-freshness`
11. `support-commercial-remixes`
12. `verify-real-trips`
13. `assist-guide-import-and-translation`
14. `add-follows-and-notifications`

## Distribution

15. `package-self-hosted-edition`
16. `offer-managed-saas-hosting`
17. `introduce-integration-plugin-system`

## Completed production slices

18. `add-development-demo-data`
19. `integrate-production-payment-gateway`
20. `implement-restorable-self-hosted-backups`
21. `execute-durable-background-jobs`
22. `enforce-managed-saas-quotas`
23. `integrate-production-ai-assistance`
24. `integrate-map-and-object-storage-providers`
25. `upload-verified-trip-evidence-attachments`
26. `fix-flutter-session-controls`

## Audit backlog — next delivery sequence

These nine changes were created from the September 2026 repository audit.
They are independent active changes and must be delivered sequentially.

27. `wire-production-provider-credentials`
    - Next change. Replace `REDACTED` outbound credentials for payment, AI,
      and remote object storage. Verify missing credentials and provider
      rejection without leaking secrets.
28. `harden-cross-origin-and-runtime-secrets`
    - Restrict credentialed CORS to exact configured origins and reject weak
      production signing secrets.
29. `make-commerce-checkout-idempotent`
    - Replace timestamp-based checkout keys and prove one order/session under
      sequential and concurrent retries.
30. `fix-provider-health-probes`
    - Correct the object-storage probe key mismatch and verify cleanup and
      failure reporting.
31. `fail-closed-evidence-malware-scanning`
    - Add a scanner boundary and prevent unscanned attachments becoming
      downloadable `Ready` evidence.
32. `align-flutter-api-methods-and-error-states`
    - Implement Flutter PATCH dispatch, complete error taxonomy, and restore
      meaningful skipped client coverage.
33. `complete-flutter-workspace-ux`
    - Make creator, admin, tenant, notification, import, and licensing
      workflows discoverable and responsive.
34. `complete-flutter-localization-and-accessibility`
    - Add English/Chinese resources and verify WCAG-oriented keyboard,
      semantics, focus, contrast, target-size, and reduced-motion behavior.
35. `add-rendered-browser-and-release-quality-gates`
    - Add browser workflow evidence, skip accountability, PostgreSQL upgrade,
      provider outage, and backup/restore release gates.

Current next item: `wire-production-provider-credentials`. See `HANDOFF.md`.

## Cross-change gates

- ASP.NET Core API authorization tests cover anonymous, wrong-user, creator, and administrator paths.
- PostgreSQL migrations are forward-only and tested from an empty database and the previous release.
- Flutter tests cover loading, empty, success, validation, offline/retry, and denied states where applicable.
- Public and paid fields are separated in server projections; clients never enforce entitlements alone.
- OpenAPI, operational documentation, telemetry, accessibility, localization, and privacy behavior are updated with each vertical slice.
- Provider credentials are injected from server-side configuration and never
  use placeholder authentication.
- Rendered Flutter web workflows and PostgreSQL/recovery evidence are required
  before claiming release readiness.
