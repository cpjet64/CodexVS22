Legend
- [ ] Not started
- [/] In progress (annotate when partial)
- [x] Completed
- [!] Blocked (add blocker note)

Rules
- Lines must be 100 characters or fewer.
- Files must be 300 lines or fewer.
- Functions must serve a single purpose.
- Break long functions into smaller functions.
- Create a Git commit for each subtask after its post-test passes.
- Commit message format: "[T<task>.<sub>] <short>; post-test=<pass>; compare=<summary>".
- Example: "[T1.3] Create UI crate; post-test=pass; compare=N/A".

Execution Order
- Complete tasks from top to bottom. Do not skip ahead.

## T1. Bootstrap VSIX project and environment
- [x] [T1.1] Install Visual Studio 2022 with the Extensibility workload.
- [x] [T1.2] Create a VSIX project with a Tool Window using the Community template.
- [x] [T1.3] Set InstallationTarget to [17.0,18.0) in source.extension.vsixmanifest.
- [x] [T1.4] Add Microsoft.VisualStudio.SDK and VSSDK.BuildTools NuGet packages.
- [x] [T1.5] Add VSCT file and place commands under Tools and IDM_VS_CTXT_CODEWIN.
- [x] [T1.6] Wire Open Codex and Add to Codex chat commands in AsyncPackage.
- [x] [T1.7] Add Codex Options page (CLI path, Use WSL, Open on startup).
- [x] [T1.8] Add Diagnostics command and route to a diagnostics handler.
- [x] [T1.9] Configure debugging to launch Experimental instance (/rootsuffix Exp).
- [x] [T1.10] Add .editorconfig and code analysis rules to enforce style.
- [!] [T1.11] Verify build succeeds and VSIX loads in the experimental instance. (Requires VS)
- [x] [T1.12] Document prerequisites in README and verify steps manually.

## T2. Codex CLI process management
- [x] [T2.1] Resolve CLI path from Options or PATH; log the resolved path.
- [x] [T2.2] Implement WSL fallback using `wsl.exe -- codex proto` when opted in.
- [x] [T2.3] Start `codex proto` as a long-lived process in the solution directory.
- [x] [T2.4] Read stdout line-by-line; treat each line as a JSON event envelope.
- [x] [T2.5] Write submissions to stdin; flush after each JSON line.
- [x] [T2.6] Pump stderr to a Diagnostics tab with timestamps and level hints.
- [x] [T2.7] Implement graceful shutdown on tool window close or VS shutdown.
- [x] [T2.8] Add whoami check; if unauthenticated, run `codex login` and report.
- [x] [T2.9] Add a one-shot reconnect on broken pipe; surface status in UI.
- [x] [T2.10] Record CLI version and rollout path on startup for telemetry.
- [x] [T2.11] Throttle logs to avoid flooding ActivityLog; cap rate per second.
- [x] [T2.12] Post-test: simulate CLI absence and bad path; show friendly errors.

## T3. Protocol envelopes and correlation
- [x] [T3.1] Define Submission and Op models; Event and EventMsg models with tolerant parsing.
- [x] [T3.2] Create a UUID helper for Submission.id values.
- [x] [T3.3] Maintain an in-flight map id→turn; remove on TaskComplete or error.
- [x] [T3.4] Handle SessionConfigured first; update session header and status bar.
- [x] [T3.5] Stream AgentMessageDelta; finalize on AgentMessage.
- [ ] [T3.6] Capture TokenCount and show usage counters in the footer.
- [ ] [T3.7] Render StreamError as non-fatal banner; allow retry button.
- [x] [T3.8] Log unknown EventMsg kinds without crashing; keep UI responsive.
- [ ] [T3.9] Add compact/no-op submission for heartbeat if CLI supports it.
- [ ] [T3.10] Unit-test correlation with canned event streams.
- [x] [T3.11] Persist last rollout_path observed for diagnostics.
- [ ] [T3.12] Post-test with parallel turns and ensure correct routing.

## T4. Tool Window chat UI
- [x] [T4.1] Create WPF Tool Window layout with header, transcript, and input box.
- [ ] [T4.2] Add model and reasoning selectors; persist choices to Options.
- [x] [T4.3] Submit user_input ops from Send button and Ctrl+Enter.
- [ ] [T4.4] Append user bubbles; stream assistant tokens into a live bubble.
- [x] [T4.5] Disable Send while a turn is active; re-enable on TaskComplete.
- [ ] [T4.6] Add Clear Chat with confirmation dialog to avoid accidental loss.
- [ ] [T4.7] Add Copy to Clipboard per message and Copy All in transcript.
- [ ] [T4.8] Add accessibility labels and keyboard navigation for controls.
- [ ] [T4.9] Show a small spinner or status text while streaming.
- [ ] [T4.10] Persist window state and last size across sessions.
- [ ] [T4.11] Telemetry: count chats, average tokens/sec, average turn time.
- [ ] [T4.12] Post-test: long inputs, non-ASCII, and paste flows behave correctly.

## T5. Approvals (exec and patch) UX
- [x] [T5.1] Add approval policy toggle: Chat, Agent, Agent (Full Access).
- [x] [T5.2] On ExecApprovalRequest show modal with command, cwd, and risk note.
- [x] [T5.3] Send ExecApproval with decision; remember per-session rule if chosen.
- [x] [T5.4] On ApplyPatchApprovalRequest show file list and summary of changes.
- [x] [T5.5] Send PatchApproval with decision and optional rationale.
- [ ] [T5.6] Provide 'Reset approvals' to clear remembered session decisions.
- [ ] [T5.7] Add a warning banner when in Full Access mode.
- [ ] [T5.8] Log denied approvals with reason code and context.
- [ ] [T5.9] Make approval prompts non-blocking and focus-safe.
- [ ] [T5.10] Unit-test mapping from request events to approval submissions.
- [ ] [T5.11] Persist last approval mode in Options and restore at startup.
- [ ] [T5.12] Post-test: repeated prompts honor remembered decisions.

## T6. Diff preview and apply
- [x] [T6.1] Parse TurnDiff unified diff into file→hunks data structures.
- [ ] [T6.2] Render side-by-side diffs using Visual Studio diff services.
- [ ] [T6.3] Show a tree of files with checkboxes to include/exclude patches.
- [ ] [T6.4] Apply edits using ITextBuffer / ITextEdit transactions per file.
- [ ] [T6.5] Show progress (PatchApplyBegin) and final result (PatchApplyEnd).
- [ ] [T6.6] Offer Discard Patch to abandon changes and close the turn.
- [ ] [T6.7] Auto-open changed files option in Options page.
- [ ] [T6.8] Detect conflicts and warn; suggest manual merge if needed.
- [ ] [T6.9] Telemetry: record apply success/failure and duration.
- [ ] [T6.10] Unit tests on diff parsing and patch application on temp files.
- [ ] [T6.11] Post-test: empty diffs and binary files handled gracefully.
- [ ] [T6.12] Ensure edits respect read-only files and source control locks.

## T7. Exec console
- [x] [T7.1] Create a console panel for ExecCommandBegin with command and cwd header.
- [x] [T7.2] Append ExecCommandOutputDelta chunks to the console view.
- [x] [T7.3] On ExecCommandEnd show exit code, duration, and summary line.
- [ ] [T7.4] Add Cancel action if the CLI supports aborting the command.
- [ ] [T7.5] Implement basic ANSI color interpretation for readability.
- [ ] [T7.6] Add Copy All and Clear Output actions on the console.
- [ ] [T7.7] Auto-open console when exec starts; hide when finished (option).
- [ ] [T7.8] Cap buffer size to prevent memory growth; allow exporting to file.
- [ ] [T7.9] Persist console visibility and last height across sessions.
- [ ] [T7.10] Unit tests with canned exec event streams and edge cases.
- [ ] [T7.11] Telemetry: number of execs, average runtime, non-zero exits.
- [ ] [T7.12] Post-test: long outputs and rapid updates remain responsive.

## T8. MCP tools and prompts
- [ ] [T8.1] Send ListMcpTools and render tools with name and description.
- [ ] [T8.2] If tool call events appear, show running and completed states.
- [ ] [T8.3] Add prompt library panel via ListCustomPrompts response.
- [ ] [T8.4] Insert a prompt into the input box on click with preview.
- [ ] [T8.5] Persist last used tool and prompt across sessions.
- [ ] [T8.6] Handle missing MCP servers gracefully with guidance text.
- [ ] [T8.7] Add Refresh Tools button with debounce to limit traffic.
- [ ] [T8.8] Unit tests: synthetic ListMcpTools and prompts responses.
- [ ] [T8.9] Telemetry: count tool invocations and prompt inserts.
- [ ] [T8.10] Provide link or help text to configure MCP servers.
- [ ] [T8.11] Add hover help for tool parameters if available.
- [ ] [T8.12] Post-test: no UI freezes while loading large lists.

## T9. Options and configuration
- [ ] [T9.1] Add fields: CLI path, Use WSL, Open on startup, defaults for model and effort.
- [ ] [T9.2] Add approval mode default and sandbox policy presets.
- [ ] [T9.3] Validate CLI path and version when saving Options.
- [ ] [T9.4] Add 'Test connection' button to run whoami in a background task.
- [ ] [T9.5] Persist Options per user; store solution-specific overrides if needed.
- [ ] [T9.6] Export/import Options as JSON; validate schema on import.
- [ ] [T9.7] Unit tests for Options serialization and validation rules.
- [ ] [T9.8] Guard invalid values with clear error messages and hints.
- [ ] [T9.9] Add Reset to defaults button and confirmation dialog.
- [ ] [T9.10] Ensure Options work off the UI thread to keep VS responsive.
- [ ] [T9.11] Log all Option changes for diagnostics with timestamps.
- [ ] [T9.12] Post-test: reopen VS restores settings and session behavior.

## T10. Packaging, parity, and release
- [ ] [T10.1] Update vsixmanifest metadata, icon, and tags for Marketplace.
- [ ] [T10.2] Verify InstallationTarget supports VS 17.x; test on 17.latest.
- [ ] [T10.3] Build the VSIX in Release; capture artifacts with symbols.
- [ ] [T10.4] Run smoke tests for chat, diff, exec, approvals in clean instance.
- [ ] [T10.5] Check parity against VS Code manifest-derived checklist.
- [ ] [T10.6] Resolve keybinding conflicts; document defaults and overrides.
- [ ] [T10.7] Add EULA, branding notes, and disclaimers to README.
- [ ] [T10.8] Write CHANGELOG for 0.1.0 with notable features and limits.
- [ ] [T10.9] Tag release, attach VSIX, and publish draft notes.
- [ ] [T10.10] Confirm post-tests passed for all tasks before release.
- [ ] [T10.11] Create short demo GIFs and embed in README.
- [ ] [T10.12] Open a tracking issue list for v0.2.0 improvements.
