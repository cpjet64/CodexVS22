using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using CodexVS22.Core;
using CodexVS22.Core.Protocol;
using System.Text.Json;

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
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (this.FindName("Transcript") is StackPanel t)
                        {
                            TextBlock tb = null;
                            if (t.Children.Count > 0)
                                tb = t.Children[t.Children.Count - 1] as TextBlock;
                            if (tb == null || !object.Equals(tb.Tag, "assistant"))
                            {
                                tb = new TextBlock { Text = string.Empty, Tag = "assistant", TextWrapping = TextWrapping.Wrap };
                                t.Children.Add(tb);
                            }
                            var delta = TryGetString(evt.Raw, "text_delta") ?? string.Empty;
                            tb.Text += delta;
                        }
                    });
                    break;
                case EventKind.AgentMessage:
                    break;
                case EventKind.TokenCount:
                    break;
                case EventKind.StreamError:
                    await VS.Notifications.ShowWarningAsync("Codex stream error. You can retry.");
                    break;
                case EventKind.ApplyPatchApprovalRequest:
                    {
                        var result = MessageBox.Show("Apply patch from Codex?", "Codex Patch Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        var approved = result == MessageBoxResult.Yes;
                        var approval = new { id = evt.Id, ops = new object[] { new { kind = "patch_approval", approved = approved } } };
                        await _host.SendAsync(JsonSerializer.Serialize(approval));
                    }
                    break;
                case EventKind.ExecApprovalRequest:
                    {
                        var cmd = TryGetString(evt.Raw, "command") ?? "(unknown)";
                        var cwd = TryGetString(evt.Raw, "cwd") ?? string.Empty;
                        var result = MessageBox.Show($"Approve exec?\n{cmd}\nCWD: {cwd}", "Codex Exec Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        var approved = result == MessageBoxResult.Yes;
                        var approval = new { id = evt.Id, ops = new object[] { new { kind = "exec_approval", approved = approved } } };
                        await _host.SendAsync(JsonSerializer.Serialize(approval));
                    }
                    break;
                case EventKind.ExecCommandBegin:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (this.FindName("ExecContainer") is FrameworkElement c) c.Visibility = Visibility.Visible;
                        if (this.FindName("ExecConsole") is TextBox t)
                        {
                            t.Text += "\n$ exec started\n";
                        }
                    });
                    break;
                case EventKind.ExecCommandOutputDelta:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (this.FindName("ExecConsole") is TextBox t)
                        {
                            t.AppendText(evt.Raw.ToString());
                            t.AppendText("\n");
                            t.ScrollToEnd();
                        }
                    });
                    break;
                case EventKind.ExecCommandEnd:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (this.FindName("ExecConsole") is TextBox t)
                        {
                            t.Text += "\n$ exec finished\n";
                        }
                    });
                    break;
                case EventKind.TurnDiff:
                    {
                        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
                        await pane.WriteLineAsync("[diff] Received diff from Codex");
                    }
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
                    break;
            }
        }

        private async void HandleStderr(string line)
        {
            var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
            await pane.WriteLineAsync($"[stderr] {line}");
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clear chat?", "Codex", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (this.FindName("Transcript") is StackPanel t)
                {
                    t.Children.Clear();
                }
                if (this.FindName("InputBox") is TextBox box) box.Clear();
            }
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
            var submission = new { id = id, ops = new object[] { new { kind = "user_input", text = text } } };
            await _host.SendAsync(JsonSerializer.Serialize(submission));
            if (this.FindName("Transcript") is StackPanel t)
            {
                t.Children.Add(new TextBlock { Text = text, Tag = "user", TextWrapping = TextWrapping.Wrap });
            }
            var btn = this.FindName("SendButton") as Button;
            var status = this.FindName("StatusText") as TextBlock;
            if (btn != null) btn.IsEnabled = false;
            if (status != null) status.Text = "Streaming...";
        }
    }
}

