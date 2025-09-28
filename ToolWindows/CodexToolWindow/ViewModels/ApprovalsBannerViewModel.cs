using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    /// <summary>
    /// Placeholder approvals banner view-model for refactor scaffolding.
    /// </summary>
    public class ApprovalsBannerViewModel
    {
        public ObservableCollection<string> PendingApprovals { get; } = new();

        public ICommand? ApproveSelectedCommand { get; set; }

        public ICommand? DenySelectedCommand { get; set; }
    }
}
