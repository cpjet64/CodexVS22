# Build, Packaging, and CI Update Plan

## Current Snapshot
- Project file still enumerates every partial manually (`CodexVS22.csproj:203`), which will grow as we split modules (diff/exec/chat/etc.).
- VSIX manifest only ships the package and license assets today (`source.extension.vsixmanifest:25`).
- Windows CI workflow builds and packages the VSIX but stubs out signing and has no module-specific test gates (`.github/workflows/windows-vsix.yml:1`).
- Release workflows (`publish.yml:1`, `publish-tag.yml:1`) produce drafts/tags but share the same unsigned artefact.
- Coverage artifacts (`coverage.lcov`) and build logs already land in the root, yet CI does not surface them.

## Adjust csproj Includes for New Layout
1. Introduce wildcard ItemGroups grouped by namespace (e.g. `ToolWindows\**\*.cs`, `Modules\Diff\**\*.cs`) to remove the brittle manual list in `CodexVS22.csproj:203`.
2. Add explicit `None` includes for schema or sample files when we add protocol fixtures, so they flow to the VSIX as needed.
3. For shared SDK-style helpers, evaluate converting the VSIX project to the VS SDK `Microsoft.VisualStudio.Sdk` style; if not feasible immediately, centralise shared properties in a `Directory.Build.props` next to the solution.

## StyleCop and Analyzer Updates
1. Layer `Directory.Build.props` for solution-wide analyzers and add `PackageReference` for `StyleCop.Analyzers` plus `Microsoft.CodeAnalysis.NetAnalyzers` (or built-in `EnableNETAnalyzers`).
2. Extend `.editorconfig` guardrails to mirror refactor rules (≤100 chars, etc.) and map StyleCop diagnostic severities.
3. Provide a `stylecop.json` root for module-specific suppressions (diff tree, exec streaming) and document exemptions.

## MSBuild Targets for Module Resources
1. Create per-module `ItemGroup`s for XAML views and resource dictionaries; use `LogicalName` metadata to keep zipped layout deterministic.
2. Define a custom target `CopyModuleAssets` invoked before `Package` that stages prompt templates, diff icons, or CLI fixture files into `$(IntermediateOutputPath)`.
3. Hook the target into VSIX packaging via `<Target Name="BeforeBuild">` or `IncludeInVSIX` metadata to ensure new module assets flow without manual editing.

## Revise CI Scripts for Additional Test Suites
1. Expand `.github/workflows/windows-vsix.yml:24` to call `vstest.console.exe` against future `.runsettings` matrices (UI smoke, module unit tests, load tests).
2. Add Linux validation using `dotnet test` in a new workflow to keep protocol/unit coverage running cross-platform, sharing coverage outputs with Windows.
3. Cache `packages/` via `actions/cache` to reduce GitHub runner time and to accommodate new analyzer packages.
4. Publish structured test/artifact outputs (JUnit XML, coverage LCOV, perf.csv) by adding upload steps referencing repository artefacts.

## Coordinate VSIX Asset List
1. Update manifest assets when we split binaries (e.g. shared services library) so the VSIX contains additional assemblies/resources beyond the package (`source.extension.vsixmanifest:25`).
2. Audit `Resources` and planned module icons/spinners, ensuring they are referenced with `<Asset Type="..." Path="..." />` and flagged with `IncludeInVSIX`.
3. Document a checklist mapping module output → VSIX asset to avoid regressions during refactor.

## Symbol and Source Indexing Strategy
1. Enable SourceLink by adding `Microsoft.SourceLink.GitHub` to the project and setting `PublishRepositoryUrl`, `EmbedUntrackedSources` true, `ContinuousIntegrationBuild` metadata.
2. Generate `snupkg`/PDB symbol packages via MSBuild target that runs after Release builds, publish to GitHub artifacts for debugging.
3. For Windows CI, add `IndexSources` or call `sourcelink test` to validate mapping before release.

## Code Signing Implications
1. Replace the stub sign job (`.github/workflows/windows-vsix.yml:94`) with an environment-gated stage that retrieves certs from Azure Key Vault or GitHub OIDC secrets, logging the thumbprint used.
2. Document fallback for unsigned private builds and required approvals for enabling signing in forks.
3. Ensure release workflows reuse the same sign task so draft/tag builds do not diverge from CI-signed artefacts.

## Release Pipeline Validation
1. Extend `publish.yml:1` to consume artefacts from CI instead of rebuilding, guaranteeing parity between validation and release bits.
2. Add gating checks (coverage threshold, smoke test logs) before promoting drafts, with outputs from CI attached automatically.
3. Plan a nightly release-dry-run workflow that exercises packaging, signing, marketplace upload in staging mode to catch secret drift earlier.

## Coverage Alignment
1. Feed `coverage.lcov` into GitHub summaries by adding an action step (`lcov-to-cobertura`) so trends surface in PRs.
2. Update new test projects to emit coverage and merge via `reportgenerator`, publishing HTML in artefacts.
3. Enforce minimum coverage in CI (fail when drop > threshold) while allowing manual overrides via workflow input toggles.

## Deliverable Checklist
| Subtask | Status | Notes |
| --- | --- | --- |
| Adjust csproj includes | ✅ | Wildcard + SDK conversion plan targeting `CodexVS22.csproj:203`. |
| Update StyleCop/analyzers | ✅ | Analyzer + editorconfig strategy documented. |
| Plan msbuild targets | ✅ | Custom module asset target described. |
| Revise CI scripts | ✅ | Windows workflow expansion + Linux coverage noted. |
| Coordinate VSIX assets | ✅ | Manifest updates and audit process captured. |
| Ensure symbols/indexing | ✅ | SourceLink + symbol publication path defined. |
| Document signing | ✅ | Key vault driven signing pipeline outlined. |
| Plan release validation | ✅ | Draft promotion flow and nightly dry run described. |
| Align coverage reports | ✅ | Coverage publishing and gating approach defined. |
| Publish build-ci-update-plan.md | ✅ | This document. |

## Next Steps
1. Create tracking issues for csproj wildcard conversion and SourceLink enablement.
2. Prototype Linux `dotnet test` workflow and wire into coverage publishing.
3. Present plan at refactor sync to align module owners on packaging responsibilities.
