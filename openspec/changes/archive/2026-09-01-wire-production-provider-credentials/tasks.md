# Tasks: Wire production provider credentials

- [x] Inspect `PaymentProviderOptions`, `AiProviderOptions`, `ObjectStorageOptions`, and all outbound request construction; document the selected authentication contract for each adapter.
- [x] Add red tests proving configured credentials are present in the expected outbound authentication field and placeholder values are never sent.
- [x] Implement credential injection with provider-specific header/signature handling and preserve local disabled behavior.
- [x] Add tests for missing credentials, provider 401/403, timeout, and secret-safe logging.
- [x] Run `dotnet test Trippify.sln` and the provider-focused tests with a request-capturing fake server.
- [x] Update `docs/commerce.md`, `docs/assisted-import.md`, deployment documentation, and configuration examples.
- [x] Run `dotnet build Trippify.sln` and `dotnet test Trippify.sln`; archive only after every task and scenario is verified.
