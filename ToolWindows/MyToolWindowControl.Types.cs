using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private sealed class AssistantTurn
    {
      public AssistantTurn(ChatBubbleElements elements)
      {
        Container = elements.Container;
        Header = elements.Header;
        Bubble = elements.Body;
      }

      public Border Container { get; }
      public TextBlock Header { get; }
      public TextBlock Bubble { get; }
      public StringBuilder Buffer { get; } = new StringBuilder();
    }

    private sealed class ChatBubbleElements
    {
      public ChatBubbleElements(Border container, TextBlock header, TextBlock body)
      {
        Container = container;
        Header = header;
        Body = body;
      }

      public Border Container { get; }
      public TextBlock Header { get; }
      public TextBlock Body { get; }
    }

    private enum ApprovalKind
    {
      Exec,
      Patch
    }

    private sealed class ApprovalRequest
    {
      public ApprovalRequest(ApprovalKind kind, string callId, string message, string signature, bool canRemember)
      {
        Kind = kind;
        CallId = callId;
        Message = message;
        Signature = signature;
        CanRemember = canRemember;
      }

      public ApprovalKind Kind { get; }
      public string CallId { get; }
      public string Message { get; }
      public string Signature { get; }
      public bool CanRemember { get; }
    }

    private sealed class ExecTurn
    {
      public ExecTurn(Border container, TextBlock body, TextBlock header, Button cancelButton, Button copyButton, Button clearButton, Button exportButton, string normalizedCommand)
      {
        Container = container;
        Body = body;
        Header = header;
        CancelButton = cancelButton;
        CopyButton = copyButton;
        ClearButton = clearButton;
        ExportButton = exportButton;
        NormalizedCommand = normalizedCommand;
        DefaultForeground = body?.Foreground;
      }

      public Border Container { get; }
      public TextBlock Body { get; }
      public TextBlock Header { get; }
      public Button CancelButton { get; }
      public Button CopyButton { get; }
      public Button ClearButton { get; }
      public Button ExportButton { get; }
      public Brush DefaultForeground { get; }
      public string ExecId { get; set; } = string.Empty;
      public bool CancelRequested { get; set; }
      public bool IsRunning { get; set; }
      public string NormalizedCommand { get; set; }
      public StringBuilder Buffer { get; } = new StringBuilder();
    }

    private sealed class McpToolInfo
    {
      public McpToolInfo(string name, string description, string server)
      {
        Name = string.IsNullOrWhiteSpace(name) ? "(tool)" : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Server = server?.Trim() ?? string.Empty;
      }

      public string Name { get; }
      public string Description { get; }
      public string Server { get; }
    }

    private sealed class McpToolRun : INotifyPropertyChanged
    {
      private readonly StringBuilder _outputBuffer = new();
      private string _statusDisplay;
      private string _detail = string.Empty;
      private string _timingDisplay;
      private bool _isRunning = true;
      private DateTimeOffset? _completedUtc;

      public McpToolRun(string callId, string toolName, string server)
      {
        CallId = string.IsNullOrEmpty(callId) ? Guid.NewGuid().ToString() : callId;
        ToolName = string.IsNullOrWhiteSpace(toolName) ? "(tool)" : toolName.Trim();
        Server = server?.Trim() ?? string.Empty;
        StartedUtc = DateTimeOffset.UtcNow;
        _statusDisplay = "Running...";
        _timingDisplay = $"Started {StartedUtc.ToLocalTime():HH:mm:ss}";
      }

      public string CallId { get; }
      public string ToolName { get; }
      public string Server { get; }
      public DateTimeOffset StartedUtc { get; }

      public string StatusDisplay
      {
        get => _statusDisplay;
        private set => SetProperty(ref _statusDisplay, value, nameof(StatusDisplay));
      }

      public string Detail
      {
        get => _detail;
        private set => SetProperty(ref _detail, value, nameof(Detail));
      }

      public string TimingDisplay
      {
        get => _timingDisplay;
        private set => SetProperty(ref _timingDisplay, value, nameof(TimingDisplay));
      }

      public bool IsRunning
      {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value, nameof(IsRunning));
      }

      public event PropertyChangedEventHandler PropertyChanged;

      public void UpdateRunning(string statusText, string detail)
      {
        IsRunning = true;
        _completedUtc = null;
        StatusDisplay = string.IsNullOrWhiteSpace(statusText) ? "Running..." : statusText.Trim();
        TimingDisplay = $"Started {StartedUtc.ToLocalTime():HH:mm:ss}";
        _outputBuffer.Clear();
        if (!string.IsNullOrWhiteSpace(detail))
        {
          _outputBuffer.Append(detail.Trim());
          Detail = BuildSummary();
        }
        else
        {
          Detail = string.Empty;
        }
      }

      public void AppendOutput(string text)
      {
        if (string.IsNullOrWhiteSpace(text))
          return;

        if (_outputBuffer.Length > 0)
          _outputBuffer.AppendLine();
        _outputBuffer.Append(text.Trim());
        Detail = BuildSummary();
      }

      public void Complete(string statusText, bool? success, string detail)
      {
        IsRunning = false;
        _completedUtc = DateTimeOffset.UtcNow;

        var fallback = success.HasValue
          ? (success.Value ? "Completed" : "Failed")
          : "Completed";

        StatusDisplay = string.IsNullOrWhiteSpace(statusText)
          ? fallback
          : statusText.Trim();

        if (!string.IsNullOrWhiteSpace(detail))
        {
          if (_outputBuffer.Length > 0)
            _outputBuffer.AppendLine();
          _outputBuffer.Append(detail.Trim());
        }

        Detail = BuildSummary();

        var duration = _completedUtc.Value - StartedUtc;
        if (duration < TimeSpan.Zero)
          duration = TimeSpan.Zero;

        TimingDisplay = duration.TotalSeconds < 1
          ? $"Finished in {duration.TotalMilliseconds:F0} ms"
          : $"Finished in {duration.TotalSeconds:F1} s";
      }

      private string BuildSummary()
      {
        var summary = _outputBuffer.ToString().Trim();
        if (summary.Length > 500)
          summary = summary.Substring(0, 500) + "...";
        return summary;
      }

      private void SetProperty<T>(ref T field, T value, string propertyName)
      {
        if (EqualityComparer<T>.Default.Equals(field, value))
          return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    private sealed class CustomPromptInfo
    {
      public CustomPromptInfo(string id, string name, string description, string body, string source)
      {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? "(prompt)" : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Body = body ?? string.Empty;
        Source = source?.Trim() ?? string.Empty;
      }

      public string Id { get; }
      public string Name { get; }
      public string Description { get; }
      public string Body { get; }
      public string Source { get; }

      public string SourceDisplay => string.IsNullOrEmpty(Source) ? string.Empty : Source;
    }

  }
}
