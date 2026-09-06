## 1. Coverage
- [x] 1.1 Add a Coverlet threshold to the dotnet test projects. Added `coverlet.msbuild` (6.0.2) to Trippify.ApiTests and Trippify.ArchitectureTests csprojs. Used `coverlet.msbuild` rather than `coverlet.collector` because in this SDK the collector's `Threshold`/`FailOnError` cannot be targeted via CLI `--` override or runsettings; `coverlet.msbuild` enforces the gate reliably via MSBuild props.
- [x] 1.2 Add a --coverage threshold to the dotnet job in quality.yml. The API test run is gated at 80% line (`ThresholdStat=total`); verified locally it passes at 80% (actual ~92%) and fails at 99%. ArchitectureTests are run separately without the gate because they only reflect over assemblies and execute no source code (~0% executable coverage).
- [x] 1.3 Add Flutter coverage to the flutter job. `flutter test --coverage` generates `coverage/lcov.info`; a step parses it and fails below a 30% line floor (actual ~34.5%).
- [x] 1.4 Upload the coverage report artifact. `dotnet-coverage` (cobertura) and `flutter-coverage` (lcov) artifacts uploaded via upload-artifact@v4.
## 2. Verification
- [x] 2.1 Verified locally: dotnet gate passes at 80 / fails at 99; Flutter coverage 34.49% passes the 30% floor.
- [x] 2.2 `openspec validate ci-coverage-gates` -> valid.
