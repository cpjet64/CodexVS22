# UI Element Event Map
| Element | Type | Event Bindings |
| --- | --- | --- |
| MyToolWindow | UserControl | Loaded=OnLoaded, Unloaded=OnUnloaded |
| LogoutButton | Button | Click=OnLogoutClick |
| ExecConsoleToggle | ToggleButton | Checked=OnExecConsoleToggleChanged, Unchecked=OnExecConsoleToggleChanged |
| CopyAllButton | Button | Click=OnCopyAllClick |
| ResetApprovalsButton | Button | Click=OnResetApprovalsClick |
| McpToolsContainer | Border |  |
| McpToolsEmptyText | StackPanel |  |
| McpHelpLink | Hyperlink | Click=OnMcpHelpClick |
| McpToolsList | ItemsControl |  |
| McpToolRunsContainer | Border |  |
| McpToolRunsList | ItemsControl |  |
| CustomPromptsContainer | Border |  |
| CustomPromptsEmptyText | TextBlock |  |
| CustomPromptsList | ItemsControl |  |
| PromptPreview | TextBlock |  |
| AuthBanner | Border |  |
| AuthMessage | TextBlock |  |
| LoginButton | Button | Click=OnLoginClick |
| FullAccessBanner | Border |  |
| FullAccessText | TextBlock |  |
| ApprovalPromptBanner | Border |  |
| ApprovalPromptText | TextBlock |  |
| ApprovalRememberCheckBox | CheckBox |  |
| ApprovalApproveButton | Button | Click=OnApprovalApproveClick |
| ApprovalDenyButton | Button | Click=OnApprovalDenyClick |
| ApprovalCombo | ComboBox | SelectionChanged=OnApprovalModeChanged |
| ModelCombo | ComboBox | SelectionChanged=OnModelSelectionChanged |
| ReasoningCombo | ComboBox | SelectionChanged=OnReasoningSelectionChanged |
| DiffTreeContainer | Border |  |
| DiffSelectionSummary | TextBlock |  |
| DiscardPatchButton | Button | Click=OnDiscardPatchClick |
| DiffTreeView | TreeView |  |
| TranscriptScrollViewer | ScrollViewer |  |
| Transcript | StackPanel |  |
| StreamErrorBanner | Border |  |
| StreamErrorText | TextBlock |  |
| StreamRetryButton | Button | Click=OnStreamRetryClick |
| TokenUsageText | TextBlock |  |
| TelemetryText | TextBlock |  |
| StreamingIndicator | ProgressBar |  |
| StatusText | TextBlock |  |
| SendButton | Button | Click=OnSendClick |
| InputBox | TextBox | PreviewKeyDown=OnInputPreviewKeyDown |