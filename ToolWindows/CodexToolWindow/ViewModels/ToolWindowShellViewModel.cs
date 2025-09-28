namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    /// <summary>
    /// Root shell view-model responsible for orchestrating module view-models.
    /// </summary>
    public class ToolWindowShellViewModel
    {
        public ToolWindowShellViewModel(
            ChatTranscriptViewModel chatTranscriptViewModel,
            DiffReviewViewModel diffReviewViewModel,
            ExecConsoleViewModel execConsoleViewModel,
            ApprovalsBannerViewModel approvalsBannerViewModel,
            McpToolsViewModel mcpToolsViewModel)
        {
            ChatTranscript = chatTranscriptViewModel;
            DiffReview = diffReviewViewModel;
            ExecConsole = execConsoleViewModel;
            ApprovalsBanner = approvalsBannerViewModel;
            McpTools = mcpToolsViewModel;
        }

        public ChatTranscriptViewModel ChatTranscript { get; }

        public DiffReviewViewModel DiffReview { get; }

        public ExecConsoleViewModel ExecConsole { get; }

        public ApprovalsBannerViewModel ApprovalsBanner { get; }

        public McpToolsViewModel McpTools { get; }
    }
}
