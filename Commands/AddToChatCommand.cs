using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;

namespace CodexVS22
{
    [Command(PackageIds.AddToChatCommand)]
    internal sealed class AddToChatCommand : BaseCommand<AddToChatCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await MyToolWindow.ShowAsync();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await VS.GetServiceAsync<DTE, DTE2>();
            var sel = dte?.ActiveDocument?.Selection as TextSelection;
            var text = sel?.Text;
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Forward selection into the tool window input box
            var ctrl = MyToolWindowControl.Current;
            if (ctrl != null)
                ctrl.AppendSelectionToInput(text);
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Command.Visible = Command.Enabled = HasSelection();
        }

        private static bool HasSelection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var dte = (DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE));
                var sel = dte?.ActiveDocument?.Selection as TextSelection;
                return sel != null && !string.IsNullOrEmpty(sel.Text);
            }
            catch { return false; }
        }
    }
}
