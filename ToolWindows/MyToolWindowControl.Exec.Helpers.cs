using System;
using System.Linq;
using System.Windows.Controls.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CodexVS22.Core.Protocol;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private void ApplyExecConsoleToggleState()
    {
      if (FindName("ExecConsoleToggle") is not ToggleButton toggle)
        return;

      _suppressExecToggleEvent = true;
      toggle.IsChecked = _options?.ExecConsoleVisible ?? true;
      _suppressExecToggleEvent = false;

      foreach (var turn in _execConsoleTurns)
        ApplyExecConsoleVisibility(turn);
    }

    private string ResolveExecId(EventMsg evt)
    {
      var id = GetExecEventId(evt);
      if (string.IsNullOrEmpty(id))
        return string.Empty;

      if (_execIdRemap.TryGetValue(id, out var mapped))
        return mapped;

      return id;
    }

    private static string GetExecEventId(EventMsg evt)
    {
      var callId = TryGetString(evt.Raw, "call_id");
      if (!string.IsNullOrEmpty(callId))
        return callId;
      if (!string.IsNullOrEmpty(evt.Id))
        return evt.Id;
      return string.Empty;
    }

    private string RegisterExecFallbackId()
    {
      var id = Guid.NewGuid().ToString();
      _execIdRemap[id] = id;
      _lastExecFallbackId = id;
      return id;
    }

    private static string BuildExecHeader(string command, string cwd)
    {
      var hasCommand = !string.IsNullOrWhiteSpace(command);
      var hasCwd = !string.IsNullOrWhiteSpace(cwd);

      if (hasCommand && hasCwd)
        return $"$ {command} (cwd: {cwd})";
      if (hasCommand)
        return $"$ {command}";
      if (hasCwd)
        return $"cwd: {cwd}";
      return "$ exec";
    }

    private static (string display, string normalized) ExtractExecCommandInfo(JToken commandToken)
    {
      if (commandToken == null)
        return (string.Empty, string.Empty);

      if (commandToken.Type == JTokenType.Array)
      {
        var array = (JArray)commandToken;
        var parts = array
          .Select(t => TrimQuotes(t?.ToString() ?? string.Empty))
          .Where(p => !string.IsNullOrEmpty(p))
          .ToList();
        if (parts.Count == 0)
          return (string.Empty, string.Empty);

        if (parts.Count >= 3 && string.Equals(parts[1], "-lc", StringComparison.OrdinalIgnoreCase))
        {
          var script = parts[2];
          return (script, script);
        }

        var joined = string.Join(" ", parts);
        var tail = parts.LastOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? joined;
        return (joined, tail);
      }

      if (commandToken.Type == JTokenType.Object)
      {
        return (commandToken.ToString(Formatting.None), string.Empty);
      }

      var text = TrimQuotes(commandToken.ToString());
      return (text, text);
    }

    private static string TrimQuotes(string text)
    {
      if (string.IsNullOrEmpty(text))
        return string.Empty;

      var trimmed = text.Trim();
      if (trimmed.Length >= 2)
      {
        var first = trimmed[0];
        var last = trimmed[trimmed.Length - 1];
        if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
          return trimmed.Substring(1, trimmed.Length - 2);
      }

      return trimmed;
    }

    private static bool ShouldOfferRemember(string signature)
      => !string.IsNullOrWhiteSpace(signature);
  }
}
