using System.Collections.ObjectModel;

namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    /// <summary>
    /// Placeholder diff review view-model for refactor scaffolding.
    /// </summary>
    public class DiffReviewViewModel
    {
        public ObservableCollection<string> DiffItems { get; } = new();
    }
}
