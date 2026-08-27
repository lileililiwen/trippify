# Delivery roadmap

Implement and archive one change at a time. A later change may be refined before implementation, but must not silently absorb an earlier change.

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

## Production completeness

18. `add-development-demo-data`
19. `integrate-production-payment-gateway`
20. `implement-restorable-self-hosted-backups`
21. `execute-durable-background-jobs`
22. `enforce-managed-saas-quotas`
23. `integrate-production-ai-assistance`
24. `integrate-map-and-object-storage-providers`
25. `upload-verified-trip-evidence-attachments`
26. `fix-flutter-session-controls`

## Cross-change gates

- ASP.NET Core API authorization tests cover anonymous, wrong-user, creator, and administrator paths.
- PostgreSQL migrations are forward-only and tested from an empty database and the previous release.
- Flutter tests cover loading, empty, success, validation, offline/retry, and denied states where applicable.
- Public and paid fields are separated in server projections; clients never enforce entitlements alone.
- OpenAPI, operational documentation, telemetry, accessibility, localization, and privacy behavior are updated with each vertical slice.
