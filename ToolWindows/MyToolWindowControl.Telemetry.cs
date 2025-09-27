using System;
using System.Collections.Generic;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private sealed class TelemetryTracker
    {
      private int _turns;
      private int _totalTokens;
      private double _totalSeconds;
      private int _currentTokens;
      private DateTime? _turnStart;
      private int _patchSuccesses;
      private int _patchFailures;
      private double _patchSeconds;
      private DateTime? _patchStart;
      private readonly Dictionary<string, DateTime> _execStarts = new(StringComparer.Ordinal);
      private int _execCount;
      private int _execNonZero;
      private double _execSeconds;
      private int _toolInvocations;
      private int _promptInserts;

      public void BeginTurn()
      {
        _turnStart = DateTime.UtcNow;
        _currentTokens = 0;
      }

      public void RecordTokens(int? total, int? input, int? output)
      {
        if (!_turnStart.HasValue)
          return;

        var candidate = 0;
        if (total.HasValue) candidate = Math.Max(candidate, total.Value);
        if (output.HasValue) candidate = Math.Max(candidate, output.Value);
        if (input.HasValue) candidate = Math.Max(candidate, input.Value);

        if (candidate > _currentTokens)
          _currentTokens = candidate;
      }

      public void CompleteTurn()
      {
        if (!_turnStart.HasValue)
          return;

        var elapsed = Math.Max(0.05, (DateTime.UtcNow - _turnStart.Value).TotalSeconds);
        _turns++;
        _totalTokens += _currentTokens;
        _totalSeconds += elapsed;
        _turnStart = null;
        _currentTokens = 0;
      }

      public void CancelTurn()
      {
        _turnStart = null;
        _currentTokens = 0;
      }

      public void BeginPatch()
      {
        _patchStart = DateTime.UtcNow;
      }

      public void CompletePatch(bool success, double durationSeconds)
      {
        if (durationSeconds <= 0 && _patchStart.HasValue)
          durationSeconds = Math.Max(0.01, (DateTime.UtcNow - _patchStart.Value).TotalSeconds);
        else if (durationSeconds <= 0)
          durationSeconds = 0.01;

        _patchSeconds += durationSeconds;
        if (success)
          _patchSuccesses++;
        else
          _patchFailures++;
        _patchStart = null;
      }

      public void CancelPatch()
      {
        _patchStart = null;
      }

      public void BeginExec(string id, string command)
      {
        if (string.IsNullOrEmpty(id))
          return;

        _execStarts[id] = DateTime.UtcNow;
      }

      public void CompleteExec(string id, int exitCode)
      {
        if (string.IsNullOrEmpty(id))
          return;

        if (_execStarts.TryGetValue(id, out var start))
        {
          var elapsed = Math.Max(0.05, (DateTime.UtcNow - start).TotalSeconds);
          _execSeconds += elapsed;
          _execCount++;
          if (exitCode != 0)
            _execNonZero++;
          _execStarts.Remove(id);
        }
      }

      public void CancelExec(string id)
      {
        if (string.IsNullOrEmpty(id))
          return;

        _execStarts.Remove(id);
      }

      public void RecordToolInvocation()
      {
        _toolInvocations++;
      }

      public void RecordPromptInsert()
      {
        _promptInserts++;
      }

      public void Reset()
      {
        _turns = 0;
        _totalTokens = 0;
        _totalSeconds = 0;
        _currentTokens = 0;
        _turnStart = null;
        _patchSuccesses = 0;
        _patchFailures = 0;
        _patchSeconds = 0;
        _patchStart = null;
        _execStarts.Clear();
        _execCount = 0;
        _execNonZero = 0;
        _execSeconds = 0;
        _toolInvocations = 0;
        _promptInserts = 0;
      }

      public string GetSummary()
      {
        var parts = new List<string>();

        if (_turns > 0)
        {
          var avgTokens = (double)_totalTokens / Math.Max(1, _turns);
          var rate = _totalSeconds > 0 ? _totalTokens / _totalSeconds : 0;
          parts.Add($"Turns {_turns} avg {avgTokens:F1} tok {rate:F1} tok/s");
        }

        var patchTotal = _patchSuccesses + _patchFailures;
        if (patchTotal > 0)
        {
          var avgSeconds = _patchSeconds / Math.Max(1, patchTotal);
          parts.Add($"Patch {_patchSuccesses}/{_patchFailures} avg {avgSeconds:F1}s");
        }

        if (_execCount > 0)
        {
          var avgSeconds = _execSeconds / Math.Max(1, _execCount);
          parts.Add($"Exec {_execCount} avg {avgSeconds:F1}s fail {_execNonZero}");
        }

        if (_toolInvocations > 0)
        {
          parts.Add($"Tools {_toolInvocations}");
        }

        if (_promptInserts > 0)
        {
          parts.Add($"Prompts {_promptInserts}");
        }

        return parts.Count == 0 ? string.Empty : string.Join(" • ", parts);
      }
    }
  }
}
