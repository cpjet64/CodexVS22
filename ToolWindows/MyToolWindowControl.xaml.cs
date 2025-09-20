using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using CodexVS22.Core;

namespace CodexVS22
{
    public partial class MyToolWindowControl : UserControl
    {
        private CodexCliHost _host;
        public MyToolWindowControl()
        {
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_host != null) return;
            _host = new CodexCliHost();
            _host.OnStdoutLine += HandleStdout;
            _host.OnStderrLine += HandleStderr;
            var pkg = await VS.Services.GetPackageAsync<CodexVS22Package>();
            var options = (CodexOptions)pkg.GetDialogPage(typeof(CodexOptions));
            var dte = await VS.GetServiceAsync<DTE, DTE2>();
            var solPath = dte?.Solution?.FullName;
            var dir = !string.IsNullOrEmpty(solPath) ? Path.GetDirectoryName(solPath) : string.Empty;
            await _host.StartAsync(options, dir);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _host?.Dispose();
            _host = null;
        }

        private async void HandleStdout(string line)
        {
            var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
            await pane.WriteLineAsync($"[json] {line}");
            // TODO(T3/T4): parse events and stream into chat transcript
        }

        private async void HandleStderr(string line)
        {
            var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
            await pane.WriteLineAsync($"[stderr] {line}");
        }

        public void AppendSelectionToInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var box = this.FindName("InputBox") as TextBox;
            if (box != null)
            {
                if (!string.IsNullOrEmpty(box.Text))
                    box.Text += "\n";
                box.Text += text;
                box.Focus();
                box.CaretIndex = box.Text.Length;
            }
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            var box = this.FindName("InputBox") as TextBox;
            var text = box?.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            var id = Guid.NewGuid().ToString();
            var payload = $"{{\"id\":\"{id}\",\"ops\":[{{\"kind\":\"user_input\",\"text\":{System.Text.Json.JsonSerializer.Serialize(text)}}]}}";
            await _host.SendAsync(payload);
        }
    }
}
