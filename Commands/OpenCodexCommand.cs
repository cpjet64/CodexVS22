using Community.VisualStudio.Toolkit;

namespace CodexVS22
{
    [Command(PackageGuids.CodexVS22String, PackageIds.OpenCodexCommand)]
    internal sealed class OpenCodexCommand : BaseCommand<OpenCodexCommand>
    {
        protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            return MyToolWindow.ShowAsync();
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Command == null) return;
            Command.Visible = true;
            Command.Enabled = true;
            Command.Supported = true;
        }
    }
}
