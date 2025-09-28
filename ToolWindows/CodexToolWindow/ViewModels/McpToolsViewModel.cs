using System.Collections.ObjectModel;

namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    /// <summary>
    /// Placeholder MCP tools view-model for refactor scaffolding.
    /// </summary>
    public class McpToolsViewModel
    {
        public ObservableCollection<McpToolViewModel> Tools { get; } = new();

        public McpToolViewModel? SelectedTool { get; set; }
    }

    /// <summary>
    /// Minimal MCP tool descriptor used by the placeholder UI.
    /// </summary>
    public class McpToolViewModel
    {
        public string? Name { get; set; }

        public string? Description { get; set; }
    }
}
