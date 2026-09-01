# Tasks: Harden cross-origin and runtime secrets

- [x] Define the production CORS and secret policy in `Program.cs`, `appsettings`, Compose documentation, and deployment docs.
- [x] Add failing tests for wildcard credentialed CORS, denied origins, exact allowed origins, and production missing/weak secrets.
- [x] Implement exact-origin CORS registration and fail-closed production validation.
- [x] Replace insecure Compose secret examples with environment/secret-store references and document local-only setup.
- [x] Verify public, protected, preflight, and signed-URL paths with safe headers and no secret leakage.
- [x] Run solution build/tests and strict OpenSpec validation; archive only after all scenarios pass.
