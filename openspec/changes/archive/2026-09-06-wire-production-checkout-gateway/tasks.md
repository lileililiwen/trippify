# Tasks: Wire production checkout gateway

- [x] Inspect `Program.cs` composition, `PaymentProviderOptions`, and `CommerceEndpoints.Checkout`; confirm `IPaymentGateway` resolves to `LocalPaymentGateway` unconditionally and that `HttpPaymentGateway` is only used as a webhook verifier.
- [x] Make `IPaymentGateway` resolve to `HttpPaymentGateway` when the payment provider is enabled and not local, mirroring the existing AI and webhook-verifier conditional registration. Keep `LocalPaymentGateway` for local/disabled mode.
- [x] Extend `PaymentProviderOptions.Validate` to reject a disabled or local payment provider in Staging/Production so a misconfigured host fails startup.
- [x] Add red tests that run the real `HttpPaymentGateway` (no service swap) against a request-capturing fake server and assert an order is persisted only after the provider accepts session creation.
- [x] Add negative-path tests: provider disabled/local returns a controlled unavailable result and creates no order; provider 401/403/timeout fails safe; configured credential absent from logs and responses.
- [x] Remove or convert the `RemoveAll<IPaymentGateway>(); AddSingleton<FakeGateway>` swap in `CommerceApiTests` so the throwing local-gateway path is exercised by a disabled-provider test rather than masked.
- [x] Run `dotnet build Trippify.sln` and `dotnet test Trippify.sln`; archive only after every task and scenario is verified.
