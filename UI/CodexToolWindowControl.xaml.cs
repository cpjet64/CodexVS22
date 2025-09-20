using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CodexVs.UI {
  public partial class CodexToolWindowControl : UserControl {
    private Core.CodexProcessService _process;
    public CodexToolWindowControl() {
      InitializeComponent();
      _process = Core.CodexProcessService.Instance;
      _process.OnMessage += OnAgentMessage;
      _ = EnsureStartedAsync();
    }
    private async Task EnsureStartedAsync() {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      await _process.TryStartAsync();
    }
    private void OnAgentMessage(string jsonLine) {
      Dispatcher.Invoke(() => {
        var tb = new TextBlock { Text = jsonLine, TextWrapping = TextWrapping.Wrap,
          Margin = new Thickness(0, 4, 0, 4) };
        Transcript.Children.Add(tb);
      });
    }
    private async void OnSendClick(object sender, RoutedEventArgs e) {
      var text = PromptBox.Text?.Trim();
      if (string.IsNullOrEmpty(text)) return;
      var model = ((ComboBoxItem)ModelCombo.SelectedItem)?.Content?.ToString() ?? "gpt-5-codex";
      var effort = ((ComboBoxItem)EffortCombo.SelectedItem)?.Content?.ToString() ?? "medium";
      var req = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{Guid.NewGuid()}\"," +
        "\"method\":\"UserMessage\",\"params\":{\"text\":" +
        $"{System.Text.Json.JsonSerializer.Serialize(text)},\"model\":\"{model}\"," +
        $"\"reasoning_effort\":\"{effort}\"}}}}";
      await _process.SendAsync(req);
      PromptBox.Clear();
    }
  }
}