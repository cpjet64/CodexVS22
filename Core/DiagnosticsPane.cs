using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace CodexVS22.Core
{
    internal static class DiagnosticsPane
    {
        private static readonly object Gate = new();
        private static Task<OutputWindowPane> _cachedPane;

        public static Task<OutputWindowPane> GetAsync()
        {
            lock (Gate)
            {
                return _cachedPane ??= CreateAsync();
            }
        }

        private static async Task<OutputWindowPane> CreateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        }
    }
}
