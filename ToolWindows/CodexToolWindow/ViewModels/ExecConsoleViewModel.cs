using System.Windows.Input;

namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    /// <summary>
    /// Placeholder exec console view-model exposing minimal bindings used by the shell layout.
    /// </summary>
    public class ExecConsoleViewModel
    {
        public string? ActiveOutput { get; set; }

        public ICommand? CancelCommand { get; set; }

        public ICommand? ExportCommand { get; set; }
    }
}
