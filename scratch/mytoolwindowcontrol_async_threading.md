# Async Method Threading Summary
## MyToolWindowControl.Approvals.cs
- OnResetApprovalsClick (void) @ MyToolWindowControl.Approvals.cs:17 — UI thread switch, async void handler, VS UI API
- DisplayNextApprovalAsync (Task) @ MyToolWindowControl.Approvals.cs:70 — UI thread switch
- ResolveActiveApprovalAsync (Task) @ MyToolWindowControl.Approvals.cs:114 — UI thread switch, CLI send
- LogAutoApprovalAsync (Task) @ MyToolWindowControl.Approvals.cs:231 — background/default
- LogManualApprovalAsync (Task) @ MyToolWindowControl.Approvals.cs:247 — background/default
## MyToolWindowControl.Authentication.cs
- UpdateAuthenticationStateAsync (Task) @ MyToolWindowControl.Authentication.cs:15 — background/default
- RefreshAuthUiAsync (Task) @ MyToolWindowControl.Authentication.cs:28 — UI thread switch
- HandleAuthenticationResultAsync (Task) @ MyToolWindowControl.Authentication.cs:84 — background/default
- OnLoginClick (void) @ MyToolWindowControl.Authentication.cs:120 — async void handler, CLI send
- OnLogoutClick (void) @ MyToolWindowControl.Authentication.cs:161 — async void handler, CLI send
- HandleStderr (void) @ MyToolWindowControl.Authentication.cs:202 — async void handler
## MyToolWindowControl.Exec.cs
- HandleExecApproval (void) @ MyToolWindowControl.Exec.cs:18 — UI thread switch, async void handler, CLI send, VS UI API
- HandleExecCommandBegin (void) @ MyToolWindowControl.Exec.cs:67 — UI thread switch, async void handler
- HandleExecCommandOutputDelta (void) @ MyToolWindowControl.Exec.cs:127 — UI thread switch, async void handler
- HandleExecCommandEnd (void) @ MyToolWindowControl.Exec.cs:163 — UI thread switch, async void handler
## MyToolWindowControl.Heartbeat.cs
- SendHeartbeatAsync (Task) @ MyToolWindowControl.Heartbeat.cs:159 — CLI send
## MyToolWindowControl.Lifecycle.cs
- WaitForEnvironmentReadyAsync (Task<EnvironmentSnapshot>) @ MyToolWindowControl.Lifecycle.cs:48 — background/default
- OnLoadedAsync (Task) @ MyToolWindowControl.Lifecycle.cs:66 — RunAsync dispatch, CLI send
## MyToolWindowControl.Mcp.cs
- HandleListMcpTools (void) @ MyToolWindowControl.Mcp.cs:19 — UI thread switch, async void handler
- HandleListCustomPrompts (void) @ MyToolWindowControl.Mcp.cs:42 — UI thread switch, async void handler
- HandleToolCallBegin (void) @ MyToolWindowControl.Mcp.cs:69 — UI thread switch, async void handler
- HandleToolCallOutput (void) @ MyToolWindowControl.Mcp.cs:97 — UI thread switch, async void handler
- HandleToolCallEnd (void) @ MyToolWindowControl.Mcp.cs:122 — UI thread switch, async void handler
- HandleTurnDiff (void) @ MyToolWindowControl.Mcp.cs:152 — async void handler
- UpdateDiffTreeAsync (Task) @ MyToolWindowControl.Mcp.cs:187 — UI thread switch
- ProcessDiffDocumentsAsync (Task<List<DiffDocument>>) @ MyToolWindowControl.Mcp.cs:232 — background/default
## MyToolWindowControl.Options.cs
- InitializeSelectorsAsync (Task) @ MyToolWindowControl.Options.cs:14 — UI thread switch
- RestoreLastUsedItemsAsync (Task) @ MyToolWindowControl.Options.cs:56 — UI thread switch, RunAsync dispatch
## MyToolWindowControl.Transcript.cs
- OnCopyAllClick (void) @ MyToolWindowControl.Transcript.cs:18 — UI thread switch, async void handler, VS UI API
## MyToolWindowControl.WorkingDirectory.Environment.cs
- GetSolutionServiceAsync (Task<IVsSolution>) @ MyToolWindowControl.WorkingDirectory.Environment.cs:24 — UI thread switch, VS UI API
- GetSolutionDirectoryFromServiceAsync (Task<string>) @ MyToolWindowControl.WorkingDirectory.Environment.cs:35 — UI thread switch
- GetSolutionRootDirectoryAsync (Task<string>) @ MyToolWindowControl.WorkingDirectory.Environment.cs:87 — UI thread switch
- CaptureEnvironmentSnapshotAsync (Task<EnvironmentSnapshot>) @ MyToolWindowControl.WorkingDirectory.Environment.cs:128 — UI thread switch
- GetFolderWorkspaceRootAsync (Task<string>) @ MyToolWindowControl.WorkingDirectory.Environment.cs:188 — UI thread switch
## MyToolWindowControl.WorkingDirectory.Helpers.cs
- SafeInvokeAsync (Task<string>) @ MyToolWindowControl.WorkingDirectory.Helpers.cs:145 — background/default
## MyToolWindowControl.WorkingDirectory.Selection.cs
- SafeGetCurrentSolutionAsync (Task<SolutionItem>) @ MyToolWindowControl.WorkingDirectory.Selection.cs:140 — VS UI API
- GetActiveProjectAsync (Task<SolutionItem>) @ MyToolWindowControl.WorkingDirectory.Selection.cs:152 — VS UI API
- GetActiveSolutionItemsAsync (Task<IReadOnlyList<SolutionItem>>) @ MyToolWindowControl.WorkingDirectory.Selection.cs:178 — VS UI API
## MyToolWindowControl.WorkingDirectory.SolutionEvents.cs
- CleanupSolutionSubscriptionsAsync (Task) @ MyToolWindowControl.WorkingDirectory.SolutionEvents.cs:11 — background/default
## MyToolWindowControl.WorkingDirectory.Subscriptions.cs
- AdviseSolutionEventsAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:16 — UI thread switch
- UnadviseSolutionEventsAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:38 — UI thread switch
- SubscribeUiContextsAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:62 — UI thread switch, RunAsync dispatch
- UnsubscribeUiContextsAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:86 — UI thread switch
- EnsureWorkingDirectoryUpToDateAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:108 — CLI send
- OnSolutionReadyAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:143 — background/default
- OnWorkspaceReadyAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:156 — background/default
- OnSolutionFullyLoadedAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:199 — UI thread switch
- OnFolderWorkspaceReadyAsync (Task) @ MyToolWindowControl.WorkingDirectory.Subscriptions.cs:214 — UI thread switch
## MyToolWindowControl.WorkingDirectory.cs
- RefreshWorkingDirectoryAsync (Task) @ MyToolWindowControl.WorkingDirectory.cs:17 — background/default
- DetermineInitialWorkingDirectoryAsync (Task<string>) @ MyToolWindowControl.WorkingDirectory.cs:22 — background/default
- ResolveWorkingDirectoryAsync (Task<WorkingDirectoryResolution>) @ MyToolWindowControl.WorkingDirectory.cs:34 — UI thread switch, VS UI API
- LogWorkingDirectoryResolutionAsync (Task) @ MyToolWindowControl.WorkingDirectory.cs:97 — background/default
## MyToolWindowControl.xaml.cs
- RestartCliAsync (Task<bool>) @ MyToolWindowControl.xaml.cs:1473 — CLI send
- HandleAgentMessageDelta (void) @ MyToolWindowControl.xaml.cs:1625 — UI thread switch, async void handler
- HandleAgentMessage (void) @ MyToolWindowControl.xaml.cs:1785 — UI thread switch, async void handler
- HandleTokenCount (void) @ MyToolWindowControl.xaml.cs:2057 — UI thread switch, async void handler
- HandleStreamError (void) @ MyToolWindowControl.xaml.cs:2209 — UI thread switch, async void handler, VS UI API
- HandleApplyPatchApproval (void) @ MyToolWindowControl.xaml.cs:2481 — UI thread switch, async void handler, CLI send, VS UI API
- HandlePatchApplyBegin (void) @ MyToolWindowControl.xaml.cs:2817 — async void handler
- HandlePatchApplyEnd (void) @ MyToolWindowControl.xaml.cs:2945 — async void handler
- HandleTaskComplete (void) @ MyToolWindowControl.xaml.cs:3089 — UI thread switch, async void handler
- TryDiscardPendingPatchAsync (Task<bool>) @ MyToolWindowControl.xaml.cs:6569 — UI thread switch, CLI send
- DiscardPatchAsync (Task) @ MyToolWindowControl.xaml.cs:6929 — UI thread switch, CLI send, VS UI API
- OnDiscardPatchClick (void) @ MyToolWindowControl.xaml.cs:7217 — async void handler
- BeginPatchApplyProgressAsync (Task) @ MyToolWindowControl.xaml.cs:7321 — UI thread switch, VS UI API
- CompletePatchApplyProgressAsync (Task) @ MyToolWindowControl.xaml.cs:7593 — UI thread switch, VS UI API
- ApplySelectedDiffsAsync (Task<bool>) @ MyToolWindowControl.xaml.cs:8129 — UI thread switch, VS UI API
- ApplyDocumentTextAsync (Task<PatchApplyResult>) @ MyToolWindowControl.xaml.cs:8945 — UI thread switch, VS UI API
- LogPatchReadOnlyAsync (Task) @ MyToolWindowControl.xaml.cs:10233 — background/default
- OnExecCancelClick (void) @ MyToolWindowControl.xaml.cs:10337 — async void handler
- OnExecCopyAllClick (void) @ MyToolWindowControl.xaml.cs:10593 — UI thread switch, async void handler, VS UI API
- OnExecClearClick (void) @ MyToolWindowControl.xaml.cs:10857 — async void handler, VS UI API
- OnExecExportClick (void) @ MyToolWindowControl.xaml.cs:13109 — UI thread switch, async void handler, VS UI API
- ShowDiffAsync (Task) @ MyToolWindowControl.xaml.cs:14053 — UI thread switch, VS UI API
- UpdateFullAccessBannerAsync (Task) @ MyToolWindowControl.xaml.cs:14589 — UI thread switch
- RequestMcpToolsAsync (Task) @ MyToolWindowControl.xaml.cs:15685 — CLI send
- RequestCustomPromptsAsync (Task) @ MyToolWindowControl.xaml.cs:15941 — CLI send
- OnCopyMessageMenuItemClick (void) @ MyToolWindowControl.xaml.cs:18949 — UI thread switch, async void handler, VS UI API
- OnSendClick (void) @ MyToolWindowControl.xaml.cs:24245 — async void handler
- SendUserInputAsync (Task) @ MyToolWindowControl.xaml.cs:24357 — UI thread switch, CLI send
- SendExecCancelAsync (Task<bool>) @ MyToolWindowControl.xaml.cs:24773 — CLI send
- LogAssistantTextAsync (Task) @ MyToolWindowControl.xaml.cs:25317 — background/default
- LogTelemetryAsync (Task) @ MyToolWindowControl.xaml.cs:25453 — background/default
- WriteExecDiagnosticsAsync (Task) @ MyToolWindowControl.xaml.cs:25581 — background/default

Total methods: 84