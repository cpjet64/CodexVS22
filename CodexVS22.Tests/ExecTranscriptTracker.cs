using System;
using System.Collections.Generic;
using System.Text;
using CodexVS22.Core;

namespace CodexVS22.Tests;

internal sealed class ExecTranscriptTracker
{
  private readonly int _bufferLimit;
  private readonly Dictionary<string, StringBuilder> _buffers = new(StringComparer.Ordinal);
  private readonly Dictionary<string, StringBuilder> _prefix = new(StringComparer.Ordinal);
  private readonly Dictionary<string, string> _final = new(StringComparer.Ordinal);

  public ExecTranscriptTracker(int bufferLimit)
  {
    _bufferLimit = bufferLimit;
  }

  public void Begin(string id, string header, string cwd)
  {
    if (string.IsNullOrEmpty(id))
      return;

    _buffers[id] = new StringBuilder();
    _prefix[id] = new StringBuilder(header ?? string.Empty);
  }

  public void Append(string id, string chunk)
  {
    if (string.IsNullOrEmpty(id) || !_buffers.TryGetValue(id, out var sb))
      return;

    if (string.IsNullOrEmpty(chunk))
      return;

    var clean = ChatTextUtilities.StripAnsi(chunk);

    // Capture up to two initial newline-terminated lines in the prefix area
    if (_prefix.TryGetValue(id, out var pref) && pref != null)
    {
      var remaining = clean;
      while (remaining.Length > 0 && CountLines(pref.ToString()) < 3)
      {
        var idx = remaining.IndexOf('\n');
        if (idx < 0)
          break;
        var line = remaining.Substring(0, idx + 1);
        pref.Append(line);
        remaining = remaining.Substring(idx + 1);
      }
      clean = remaining;
    }

    sb.Append(clean);
    TrimBuffer(sb);
  }

  public void SetFinished(string id)
  {
    if (string.IsNullOrEmpty(id) || !_buffers.TryGetValue(id, out var sb))
      return;

    var body = ChatTextUtilities.StripAnsi(sb.ToString());
    var prefix = _prefix.TryGetValue(id, out var p) && p != null ? p.ToString() : string.Empty;
    var combined = (prefix + body).TrimEnd('\n');

    // Enforce near-limit size but preserve prefix content
    if (_bufferLimit > 0 && combined.Length > _bufferLimit + 10)
    {
      var keep = Math.Max(0, _bufferLimit + 10 - prefix.Length);
      var tail = keep > 0 && combined.Length > keep
        ? combined.Substring(combined.Length - keep)
        : string.Empty;
      combined = prefix + tail;
      if (combined.Length > _bufferLimit + 10)
        combined = combined.Substring(combined.Length - (_bufferLimit + 10));
    }

    _final[id] = combined;
    _buffers.Remove(id);
    _prefix.Remove(id);
  }

  public void Cancel(string id)
  {
    if (string.IsNullOrEmpty(id))
      return;

    _buffers.Remove(id);
  }

  public string GetTranscript(string id)
    => _final.TryGetValue(id, out var text) ? text : string.Empty;

  public bool HasTranscript(string id)
    => _final.ContainsKey(id);

  public bool HasTurn(string id)
    => _buffers.ContainsKey(id) || _final.ContainsKey(id);

  private void TrimBuffer(StringBuilder sb)
  {
    if (_bufferLimit <= 0 || sb.Length <= _bufferLimit)
      return;

    var excess = sb.Length - _bufferLimit;
    if (excess < _bufferLimit / 5)
      excess = _bufferLimit / 5;

    sb.Remove(0, excess);
  }

  private static int CountLines(string text)
  {
    if (string.IsNullOrEmpty(text)) return 0;
    var count = 0;
    foreach (var ch in text)
      if (ch == '\n') count++;
    return count;
  }
}
