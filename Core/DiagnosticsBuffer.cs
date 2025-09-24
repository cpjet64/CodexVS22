using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace CodexVS22.Core
{
  // Platform-agnostic diagnostics buffer for testable export/copy scenarios.
  internal static class DiagnosticsBuffer
  {
    private static readonly ConcurrentQueue<string> _lines = new();
    private const int MaxLines = 5000;

    public static void Add(string message)
    {
      if (string.IsNullOrWhiteSpace(message)) return;
      var line = $"{DateTime.Now:HH:mm:ss} {message.TrimEnd()}";
      _lines.Enqueue(line);
      while (_lines.Count > MaxLines && _lines.TryDequeue(out _)) { }
    }

    public static string ExportText()
    {
      var sb = new StringBuilder();
      foreach (var l in _lines)
        sb.AppendLine(l);
      return sb.ToString();
    }

    public static void Clear()
    {
      while (_lines.TryDequeue(out _)) { }
    }

    public static int Count => _lines.Count;
  }
}

