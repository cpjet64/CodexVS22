using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using CodexVS22.Core;
using CodexVS22.Core.Protocol;

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
            await _host.CheckAuthenticationAsync(options, dir);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _host?.Dispose();
            _host = null;
        }

        private async void HandleStdout(string line)
        {
            var evt = EventParser.Parse(line);
            switch (evt.Kind)
            {
                case EventKind.SessionConfigured:
                    CodexVS22.Core.CodexCliHost.LastRolloutPath = TryGetString(evt.Raw, "rollout_path");
                    break;
                case EventKind.AgentMessageDelta:
                    // TODO: stream token delta into current assistant bubble
                    break;
                case EventKind.AgentMessage:
                    // TODO: finalize assistant bubble
                    break;
                case EventKind.TokenCount:
                    // TODO: update token counters in footer
                    break;
                case EventKind.StreamError:
                    await VS.Notifications.ShowWarningAsync("Codex stream error. You can retry.");
                    break;
                case EventKind.TaskComplete:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        var btn = this.FindName("SendButton") as Button;
                        var status = this.FindName("StatusText") as TextBlock;
                        if (btn != null) btn.IsEnabled = true;
                        if (status != null) status.Text = string.Empty;
                    });
                    break;
                default:
                    // Ignore unknowns; keep UI responsive
                    break;
            }
        }

        private async void HandleStderr(string line)
        {
            var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
            await pane.WriteLineAsync($"[stderr] {line}");
        }

        private static string TryGetString(System.Text.Json.JsonElement el, string name)
        {
            try { return el.TryGetProperty(name, out var v) ? v.GetString() : null; } catch { return null; }
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
            var btn = this.FindName("SendButton") as Button;
            var status = this.FindName("StatusText") as TextBlock;
            if (btn != null) btn.IsEnabled = false;
            if (status != null) status.Text = "Streaming...";
        }
    }
}
