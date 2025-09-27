# Release Task Tracker

Legend
- [ ] Not started
- [/] In progress
- [x] Completed
- [!] Blocked (add blocker note)

Keep lines under 100 characters. Update this file after each work session.

## 1. Restore Build & Tool Window Health
- [ ] [B1] Fix build break in MyToolWindowControl.xaml.cs (see TODO_VisualStudio.md:T1.11)
  - [x] [B1.a] Split control into smaller partial classes (<300 lines each) (TelemetryTracker, helper types, lifecycle, auth, exec, MCP/diff, working-dir handlers moved)
  - [x] [B1.b] Replace missing helpers (NormalizeFileContent, NormalizeForComparison) or add impl
  - [x] [B1.c] Correct VS Text API usage (add Microsoft.VisualStudio.Text references, avoid MarkDirty if needed)
  - [x] [B1.d] Resolve telemetry references (_telemetryTracker, LogTelemetryAsync) or remove unused code
  - [x] [B1.e] Update diff viewer options to supported values (no undefined VSDIFFOPT_* flags)
  - [x] [B1.f] Ensure ApplyExecBufferLimit is called on the instance
  - [!] [B1.g] Run dotnet build CodexVS22.sln -c Release and attach log excerpt showing success
    - Blocked: msbuild CodexVS22.sln /p:Configuration=Release (DevShell) fails: add RuntimeIdentifier "win" (NuGet restore) then rerun to generate MyToolWindowControl.g.cs and clear VSCT includes.
- [x] [B2] Review CodexVS22.csproj references; add any missing VS SDK assemblies locally
    - Verified VS SDK references resolve via local NuGet cache (text, image catalog, core utility); no missing-reference warnings under msbuild.
- [ ] [B3] Capture updated build log and note outcome in docs/build-log.txt
    - Pending: wait for successful msbuild (see [B1.g]) before capturing build log.

## 2. Verify Cursor-AI Imported Features
- [ ] [V1] MCP Prompt insertion
  - [ ] [V1.a] Validate prompt click inserts text, focus retained, no crashes
  - [ ] [V1.b] Confirm options persistence works without async VS.Settings.SaveAsync
  - [ ] [V1.c] Add/adjust tests if behavior changes
- [ ] [V2] MCP Tool list refresh and empty-state handling
  - [ ] [V2.a] Exercise refresh debounce and missing-server guidance
  - [ ] [V2.b] Document expected UX in README/docs
- [ ] [V3] Options dialog enhancements
  - [ ] [V3.a] Verify new fields, reset, export/import, logging on background thread
  - [ ] [V3.b] Add coverage/tests as needed
  - [ ] [V3.c] Update docs/Options.md (or README section) with latest behavior
- [ ] [V4] Telemetry additions (prompt/tool counters)
  - [ ] [V4.a] Ensure trackers exist and wire to central telemetry service
  - [ ] [V4.b] Record tests or manual validation notes in docs/Telemetry.md

## 3. Execute Release Checklist (RELEASE_CHECKLIST_v0.1.0.md)
- [ ] [R1] Build & Packaging section complete (VSIX, PDB, dependency validation)
- [ ] [R2] Testing section complete (smoke tests, scenarios, error cases, WSL)
- [ ] [R3] Release preparation steps complete (version bump, tag, release draft, marketplace prep)
- [ ] [R4] Required release assets gathered and stored under docs/release/
- [ ] [R5] Performance benchmarks measured and logged (startup, memory, responsiveness)
- [ ] [R6] Security checklist confirmed (input validation, sandbox policies, data protection)
- [ ] [R7] Compatibility matrix verified (document tested VS/Windows versions)
- [ ] [R8] Post-release verification & communication plans written
- [ ] [R9] Sign-offs collected (Dev, QA, Security, Docs, Release Manager)

## 4. Documentation & Status Updates
- [ ] [D1] Update PROJECT_COMPLETION_SUMMARY.md to match actual progress
- [ ] [D2] Update README/CHANGELOG with any changes from verification work
- [ ] [D3] Archive superseded TODO items or mark as complete in TODO_VisualStudio.md
- [ ] [D4] Capture final outcome in RELEASE_CHECKLIST_v0.1.0.md and link to assets

## 5. Final Validation
- [ ] [F1] Re-run full build + tests after all tasks complete
- [ ] [F2] Smoke test VSIX in experimental instance and record results
- [ ] [F3] Prepare final sign-off summary for release announcement





