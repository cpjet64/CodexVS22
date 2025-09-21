using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using CodexVS22.Core;
using CodexVS22.Core.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace CodexVS22
{
  public partial class MyToolWindowControl : UserControl
  {
    private CodexCliHost _host;
    private readonly Dictionary<string, AssistantTurn> _assistantTurns = new();
    private string _lastUserInput;

    private sealed class AssistantTurn
    {
      public AssistantTurn(TextBlock bubble)
      {
        Bubble = bubble;
      }

      public TextBlock Bubble { get; }
      public StringBuilder Buffer { get; } = new StringBuilder();
    }
    public MyToolWindowControl()
    {
      InitializeComponent();
    }

    public static MyToolWindowControl Current { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
      ThreadHelper.JoinableTaskFactory.Run(async () =>
      {
        await OnLoadedAsync(sender, e);
      });
    }

    private async Task OnLoadedAsync(object sender, RoutedEventArgs e)
    {
      if (_host != null) return;
      Current = this;
      _host = new CodexCliHost();
      _host.OnStdoutLine += HandleStdout;
      _host.OnStderrLine += HandleStderr;
      var options = CodexVS22Package.OptionsInstance ?? new CodexOptions();
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
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
      if (Current == this) Current = null;
    }

    private async void HandleStderr(string line)
    {
      try
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[stderr] {line}");
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleStderr failed: {ex.Message}");
      }
    }

    private async void HandleAgentMessageDelta(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var id = string.IsNullOrEmpty(evt.Id) ? "__unknown__" : evt.Id;
        var text = ExtractDeltaText(evt);
        if (string.IsNullOrEmpty(text))
          return;

        var turn = GetOrCreateAssistantTurn(id);
        turn.Buffer.Append(text);
        turn.Bubble.Text = turn.Buffer.ToString();
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleAgentMessageDelta failed: {ex.Message}");
      }
    }

    private async void HandleAgentMessage(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var id = string.IsNullOrEmpty(evt.Id) ? "__unknown__" : evt.Id;
        var finalText = ExtractFinalText(evt);
        if (!_assistantTurns.TryGetValue(id, out var turn))
        {
          if (string.IsNullOrEmpty(finalText))
            return;
          turn = GetOrCreateAssistantTurn(id);
        }

        if (!string.IsNullOrEmpty(finalText))
        {
          turn.Buffer.Clear();
          turn.Buffer.Append(finalText);
        }

        turn.Bubble.Text = turn.Buffer.ToString();
        _assistantTurns.Remove(id);
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleAgentMessage failed: {ex.Message}");
      }
    }

    private async void HandleTokenCount(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var (total, input, output) = ExtractTokenCounts(evt);
        if (total == null && input == null && output == null)
          return;
        UpdateTokenUsage(total, input, output);
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleTokenCount failed: {ex.Message}");
      }
    }

    private async void HandleStreamError(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var message = ExtractStreamErrorMessage(evt);
        ShowStreamErrorBanner(message, !string.IsNullOrEmpty(_lastUserInput));
        if (!string.IsNullOrEmpty(evt.Id) && _assistantTurns.TryGetValue(evt.Id, out var turn))
        {
          if (!turn.Buffer.ToString().EndsWith("[stream interrupted]", StringComparison.Ordinal))
          {
            if (turn.Buffer.Length > 0)
              turn.Buffer.AppendLine().AppendLine();
            turn.Buffer.Append("[stream interrupted]");
            turn.Bubble.Text = turn.Buffer.ToString();
          }
          _assistantTurns.Remove(evt.Id);
        }

        var btn = this.FindName("SendButton") as Button;
        var status = this.FindName("StatusText") as TextBlock;
        if (btn != null) btn.IsEnabled = true;
        if (status != null) status.Text = "Stream error";
        await VS.StatusBar.ShowMessageAsync("Codex stream error. You can retry.");
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleStreamError failed: {ex.Message}");
      }
    }

    private async void HandleApplyPatchApproval(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var result = System.Windows.MessageBox.Show("Apply patch from Codex?", "Codex Patch Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
        var approved = result == MessageBoxResult.Yes;
        var approval = new { id = evt.Id, ops = new object[] { new { kind = "patch_approval", approved = approved } } };
        var host = _host;
        if (host != null)
        {
          await host.SendAsync(JsonConvert.SerializeObject(approval));
        }
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleApplyPatchApproval failed: {ex.Message}");
      }
    }

    private async void HandleExecApproval(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var cmd = TryGetString(evt.Raw, "command") ?? "(unknown)";
        var cwd = TryGetString(evt.Raw, "cwd") ?? string.Empty;
        var result = System.Windows.MessageBox.Show($"Approve exec?\n{cmd}\nCWD: {cwd}", "Codex Exec Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
        var approved = result == MessageBoxResult.Yes;
        var approval = new { id = evt.Id, ops = new object[] { new { kind = "exec_approval", approved = approved } } };
        var host = _host;
        if (host != null)
        {
          await host.SendAsync(JsonConvert.SerializeObject(approval));
        }
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleExecApproval failed: {ex.Message}");
      }
    }

    private async void HandleExecCommandBegin(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (this.FindName("ExecContainer") is FrameworkElement c) c.Visibility = Visibility.Visible;
        if (this.FindName("ExecConsole") is TextBox t)
        {
          t.Text += "\n$ exec started\n";
        }
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleExecCommandBegin failed: {ex.Message}");
      }
    }

    private async void HandleExecCommandOutputDelta(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (this.FindName("ExecConsole") is TextBox t)
        {
          var outText = TryGetString(evt.Raw, "text") ?? TryGetString(evt.Raw, "chunk") ?? TryGetString(evt.Raw, "data") ?? string.Empty;
          t.AppendText(outText);
          t.AppendText("\n");
          t.ScrollToEnd();
        }
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleExecCommandOutputDelta failed: {ex.Message}");
      }
    }

    private async void HandleExecCommandEnd(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (this.FindName("ExecConsole") is TextBox t)
        {
          t.Text += "\n$ exec finished\n";
        }
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleExecCommandEnd failed: {ex.Message}");
      }
    }

    private async void HandleTurnDiff(EventMsg evt)
    {
      try
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync("[diff] Received diff from Codex");
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleTurnDiff failed: {ex.Message}");
      }
    }

    private async void HandleTaskComplete(EventMsg evt)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var btn = this.FindName("SendButton") as Button;
        var status = this.FindName("StatusText") as TextBlock;
        if (btn != null) btn.IsEnabled = true;
        if (status != null) status.Text = string.Empty;
        HideStreamErrorBanner();
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] HandleTaskComplete failed: {ex.Message}");
      }
    }

    private void HandleStdout(string line)
    {
      var evt = EventParser.Parse(line);
      switch (evt.Kind)
      {
        case EventKind.SessionConfigured:
          CodexVS22.Core.CodexCliHost.LastRolloutPath = TryGetString(evt.Raw, "rollout_path");
          break;
        case EventKind.AgentMessageDelta:
          HandleAgentMessageDelta(evt);
          break;
        case EventKind.AgentMessage:
          HandleAgentMessage(evt);
          break;
        case EventKind.TokenCount:
          HandleTokenCount(evt);
          break;
        case EventKind.StreamError:
          HandleStreamError(evt);
          break;
        case EventKind.ApplyPatchApprovalRequest:
          HandleApplyPatchApproval(evt);
          break;
        case EventKind.ExecApprovalRequest:
          HandleExecApproval(evt);
          break;
        case EventKind.ExecCommandBegin:
          HandleExecCommandBegin(evt);
          break;
        case EventKind.ExecCommandOutputDelta:
          HandleExecCommandOutputDelta(evt);
          break;
        case EventKind.ExecCommandEnd:
          HandleExecCommandEnd(evt);
          break;
        case EventKind.TurnDiff:
          HandleTurnDiff(evt);
          break;
        case EventKind.TaskComplete:
          HandleTaskComplete(evt);
          break;
        default:
          break;
      }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
      if (System.Windows.MessageBox.Show("Clear chat?", "Codex", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
      {
        if (this.FindName("Transcript") is StackPanel t)
        {
          t.Children.Clear();
        }
        if (this.FindName("InputBox") is TextBox box) box.Clear();
        _assistantTurns.Clear();
        _lastUserInput = string.Empty;
        ClearTokenUsage();
        HideStreamErrorBanner();
      }
    }

    private static string TryGetString(JObject obj, string name)
    {
      try { return obj?[name]?.ToString(); } catch { return null; }
    }

    private AssistantTurn GetOrCreateAssistantTurn(string id)
    {
      if (_assistantTurns.TryGetValue(id, out var turn))
        return turn;

      var bubble = CreateAssistantBubble();
      turn = new AssistantTurn(bubble);
      _assistantTurns[id] = turn;
      return turn;
    }

    private TextBlock CreateAssistantBubble()
    {
      if (this.FindName("Transcript") is not StackPanel transcript)
        throw new InvalidOperationException("Transcript panel missing");

      var bubble = new TextBlock
      {
        Text = string.Empty,
        Tag = "assistant",
        TextWrapping = TextWrapping.Wrap
      };
      transcript.Children.Add(bubble);
      return bubble;
    }

    private static string ExtractDeltaText(EventMsg evt)
    {
      var direct = TryGetString(evt.Raw, "text_delta");
      if (!string.IsNullOrEmpty(direct))
        return direct;

      var delta = evt.Raw?["delta"];
      if (delta == null)
        return string.Empty;

      if (delta.Type == JTokenType.String)
        return delta.ToString();

      if (delta.Type == JTokenType.Object)
      {
        var text = delta["text"]?.ToString();
        if (!string.IsNullOrEmpty(text))
          return text;

        if (delta["content"] is JArray contentArray)
        {
          var pieces = contentArray
            .Select(token => token?["text"]?.ToString())
            .Where(s => !string.IsNullOrEmpty(s));
          return string.Concat(pieces);
        }
      }

      return string.Empty;
    }

    private static string ExtractFinalText(EventMsg evt)
    {
      var direct = TryGetString(evt.Raw, "text");
      if (!string.IsNullOrEmpty(direct))
        return direct;

      var message = evt.Raw?["message"];
      if (message == null)
        return string.Empty;

      if (message["text"] != null)
        return message["text"]?.ToString() ?? string.Empty;

      if (message["content"] is JArray contentArray)
      {
        var pieces = contentArray
          .Select(token => token?["text"]?.ToString())
          .Where(s => !string.IsNullOrEmpty(s));
        return string.Concat(pieces);
      }

      return string.Empty;
    }

    private static string ExtractStreamErrorMessage(EventMsg evt)
    {
      var obj = evt.Raw;
      string message = TryGetString(obj, "message")
        ?? TryGetString(obj, "error")
        ?? TryGetString(obj, "detail")
        ?? TryGetString(obj, "description");

      if (string.IsNullOrWhiteSpace(message) && obj?["error"] is JObject errorObj)
      {
        message = TryGetString(errorObj, "message") ?? TryGetString(errorObj, "detail");
      }

      return string.IsNullOrWhiteSpace(message) ? "Stream error" : message.Trim();
    }

    private static (int? total, int? input, int? output) ExtractTokenCounts(EventMsg evt)
    {
      var obj = evt.Raw ?? new JObject();
      var total = ResolveTokenValue(obj, "total", "total_tokens");
      var input = ResolveTokenValue(obj, "input", "prompt", "input_tokens");
      var output = ResolveTokenValue(obj, "output", "completion", "output_tokens");
      return (total, input, output);
    }

    private static int? ResolveTokenValue(JObject source, params string[] names)
    {
      if (source == null)
        return null;

      foreach (var name in names)
      {
        if (TryReadInt(source[name], out var value))
          return value;
      }

      foreach (var container in new[] { "counts", "usage", "token_counts" })
      {
        if (source[container] is JObject nested)
        {
          foreach (var name in names)
          {
            if (TryReadInt(nested[name], out var value))
              return value;
          }
        }
      }

      return null;
    }

    private static bool TryReadInt(JToken token, out int value)
    {
      value = 0;
      if (token == null)
        return false;
      if (token.Type == JTokenType.Integer)
      {
        value = token.Value<int>();
        return true;
      }
      if (token.Type == JTokenType.Float)
      {
        value = (int)Math.Round(token.Value<double>());
        return true;
      }
      var text = token.ToString();
      return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateTokenUsage(int? total, int? input, int? output)
    {
      if (this.FindName("TokenUsageText") is not TextBlock block)
        return;

      var builder = new StringBuilder();
      if (total.HasValue)
        builder.Append($"total {total.Value}");

      var ioParts = new List<string>();
      if (input.HasValue)
        ioParts.Add($"in {input.Value}");
      if (output.HasValue)
        ioParts.Add($"out {output.Value}");

      if (ioParts.Count > 0)
      {
        if (builder.Length > 0)
          builder.Append(' ');
        builder.Append('(');
        builder.Append(string.Join(", ", ioParts));
        builder.Append(')');
      }

      if (builder.Length == 0)
      {
        block.Text = string.Empty;
        block.Visibility = Visibility.Collapsed;
        return;
      }

      block.Text = $"Tokens: {builder}";
      block.Visibility = Visibility.Visible;
    }

    private void ClearTokenUsage()
    {
      if (this.FindName("TokenUsageText") is not TextBlock block)
        return;
      block.Text = string.Empty;
      block.Visibility = Visibility.Collapsed;
    }

    private void ShowStreamErrorBanner(string message, bool canRetry)
    {
      if (this.FindName("StreamErrorText") is TextBlock text)
        text.Text = string.IsNullOrWhiteSpace(message) ? "Stream error" : message;
      if (this.FindName("StreamRetryButton") is Button retry)
        retry.IsEnabled = canRetry;
      if (this.FindName("StreamErrorBanner") is Border banner)
        banner.Visibility = Visibility.Visible;
    }

    private void HideStreamErrorBanner()
    {
      if (this.FindName("StreamErrorBanner") is Border banner)
        banner.Visibility = Visibility.Collapsed;
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
      try
      {
        var box = this.FindName("InputBox") as TextBox;
        await SendUserInputAsync(box?.Text, fromRetry: false);
      }
      catch (Exception ex)
      {
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync($"[error] OnSendClick failed: {ex.Message}");
      }
    }

    private async Task SendUserInputAsync(string text, bool fromRetry)
    {
      var host = _host;
      if (host == null)
        return;

      var payloadText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
      if (string.IsNullOrEmpty(payloadText))
        return;

      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      ClearTokenUsage();
      HideStreamErrorBanner();

      if (!fromRetry && this.FindName("Transcript") is StackPanel transcript)
      {
        transcript.Children.Add(new TextBlock { Text = payloadText, Tag = "user", TextWrapping = TextWrapping.Wrap });
      }

      var btn = this.FindName("SendButton") as Button;
      var status = this.FindName("StatusText") as TextBlock;

      var submission = new { id = Guid.NewGuid().ToString(), ops = new object[] { new { kind = "user_input", text = payloadText } } };
      var ok = await host.SendAsync(JsonConvert.SerializeObject(submission));

      if (!ok)
      {
        if (btn != null) btn.IsEnabled = true;
        if (status != null) status.Text = "Send failed";
        return;
      }

      if (btn != null) btn.IsEnabled = false;
      if (status != null) status.Text = "Streaming...";
      if (!fromRetry)
        _lastUserInput = payloadText;
    }

    private void OnStreamRetryClick(object sender, RoutedEventArgs e)
    {
      var text = _lastUserInput;
      if (string.IsNullOrEmpty(text))
        return;
      _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await SendUserInputAsync(text, fromRetry: true));
    }

    private void OnStreamDismissClick(object sender, RoutedEventArgs e)
    {
      HideStreamErrorBanner();
    }
  }
}
