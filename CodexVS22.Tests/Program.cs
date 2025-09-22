using System;
using System.Collections.Generic;
using System.Text;
using CodexVS22.Core.Protocol;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Tests;

internal static class Program
{
  private static readonly List<string> Failures = new();

  private static int Main()
  {
    RunTest(nameof(AgentMessageEvents_CorrelateDeltaAndFinal), AgentMessageEvents_CorrelateDeltaAndFinal);
    RunTest(nameof(ParallelTurns_CorrelateIndependently), ParallelTurns_CorrelateIndependently);
    RunTest(nameof(ParallelTurns_TaskCompleteCleansState), ParallelTurns_TaskCompleteCleansState);

    if (Failures.Count == 0)
    {
      Console.WriteLine("Correlation tests passed.");
      return 0;
    }

    foreach (var failure in Failures)
      Console.Error.WriteLine(failure);

    return 1;
  }

  private static void RunTest(string name, Action action)
  {
    try
    {
      action();
    }
    catch (Exception ex)
    {
      Failures.Add($"{name} failed: {ex.Message}");
    }
  }

  private static void AgentMessageEvents_CorrelateDeltaAndFinal()
  {
    var lines = new[]
    {
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"Hello \"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"World\"}}",
      "{\"msg\":{\"kind\":\"AgentMessage\",\"id\":\"turn-1\",\"text\":\"Hello World!\"}}"
    };

    var tracker = new TranscriptTracker();
    tracker.Process(lines);

    AssertEqual("Hello World!", tracker.GetTranscript("turn-1"), "Final transcript mismatch");
    AssertFalse(tracker.HasInFlight("turn-1"), "turn-1 should be completed");
  }

  private static void ParallelTurns_CorrelateIndependently()
  {
    var lines = new[]
    {
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-A\",\"text_delta\":\"alpha\"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-B\",\"text_delta\":\"beta\"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-A\",\"text_delta\":\" one\"}}",
      "{\"msg\":{\"kind\":\"AgentMessage\",\"id\":\"turn-B\",\"text\":\"beta!\"}}",
      "{\"msg\":{\"kind\":\"StreamError\",\"id\":\"turn-A\",\"message\":\"cancelled\"}}"
    };

    var tracker = new TranscriptTracker();
    tracker.Process(lines);

    AssertEqual("beta!", tracker.GetTranscript("turn-B"), "turn-B transcript mismatch");
    AssertFalse(tracker.HasTranscript("turn-A"), "turn-A should not have a final transcript");
    AssertFalse(tracker.HasInFlight("turn-A"), "turn-A should be cleared");
    AssertFalse(tracker.HasInFlight("turn-B"), "turn-B should be cleared");
  }

  private static void ParallelTurns_TaskCompleteCleansState()
  {
    var lines = new[]
    {
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"foo\"}}",
      "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-2\",\"text_delta\":\"bar\"}}",
      "{\"msg\":{\"kind\":\"TaskComplete\",\"id\":\"turn-1\"}}",
      "{\"msg\":{\"kind\":\"AgentMessage\",\"id\":\"turn-2\",\"text\":\"bar!\"}}",
      "{\"msg\":{\"kind\":\"TaskComplete\",\"id\":\"turn-2\"}}"
    };

    var tracker = new TranscriptTracker();
    tracker.Process(lines);

    AssertFalse(tracker.HasTranscript("turn-1"), "turn-1 transcript should be cleared");
    AssertFalse(tracker.HasInFlight("turn-1"), "turn-1 in-flight state should be cleared");
    AssertEqual("bar!", tracker.GetTranscript("turn-2"), "turn-2 transcript mismatch");
    AssertFalse(tracker.HasInFlight("turn-2"), "turn-2 in-flight state should be cleared");
  }

  private static void AssertEqual(string expected, string actual, string message)
  {
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
      throw new InvalidOperationException($"{message} (expected: '{expected}', actual: '{actual}')");
  }

  private static void AssertFalse(bool condition, string message)
  {
    if (condition)
      throw new InvalidOperationException(message);
  }

  private sealed class TranscriptTracker
  {
    private readonly CorrelationMap _map = new();
    private readonly Dictionary<string, StringBuilder> _buffers = new();
    private readonly Dictionary<string, string> _completed = new();

    public void Process(IEnumerable<string> jsonLines)
    {
      foreach (var line in jsonLines)
      {
        var evt = EventParser.Parse(line);
        switch (evt.Kind)
        {
          case EventKind.AgentMessageDelta:
            AppendDelta(evt);
            break;
          case EventKind.AgentMessage:
            CompleteTurn(evt);
            break;
          case EventKind.StreamError:
          case EventKind.TaskComplete:
            Cleanup(evt.Id);
            break;
        }
      }
    }

    public string GetTranscript(string id)
      => _completed.TryGetValue(id, out var text) ? text : string.Empty;

    public bool HasTranscript(string id) => _completed.ContainsKey(id);

    public bool HasInFlight(string id) => _map.TryGet(id, out _);

    private void AppendDelta(EventMsg evt)
    {
      var id = evt.Id;
      if (string.IsNullOrEmpty(id))
        return;

      if (!_map.TryGet(id, out var state))
      {
        var buffer = new StringBuilder();
        _map.Add(id, buffer);
        _buffers[id] = buffer;
        state = buffer;
      }

      if (state is StringBuilder sb)
      {
        var text = ExtractTextDelta(evt.Raw);
        sb.Append(text);
      }
    }

    private void CompleteTurn(EventMsg evt)
    {
      var id = evt.Id;
      if (string.IsNullOrEmpty(id))
        return;

      var finalText = ExtractFinalText(evt.Raw);

      if (_map.TryGet(id, out var state) && state is StringBuilder sb)
      {
        if (!string.IsNullOrEmpty(finalText))
        {
          sb.Clear();
          sb.Append(finalText);
        }

        _completed[id] = sb.ToString();
      }
      else if (!string.IsNullOrEmpty(finalText))
      {
        _completed[id] = finalText;
      }

      Cleanup(id);
    }

    private void Cleanup(string id)
    {
      if (string.IsNullOrEmpty(id))
        return;

      _map.Remove(id);
      _buffers.Remove(id);
    }

    private static string ExtractTextDelta(JObject? obj)
    {
      if (obj == null)
        return string.Empty;

      var direct = obj["text_delta"]?.ToString();
      if (!string.IsNullOrEmpty(direct))
        return direct;

      if (obj["delta"] is JObject deltaObj)
        return deltaObj["text_delta"]?.ToString() ?? string.Empty;

      return string.Empty;
    }

    private static string ExtractFinalText(JObject? obj)
    {
      if (obj == null)
        return string.Empty;

      var direct = obj["text"]?.ToString();
      if (!string.IsNullOrEmpty(direct))
        return direct;

      if (obj["message"] is JObject messageObj)
        return messageObj["text"]?.ToString() ?? string.Empty;

      return string.Empty;
    }
  }
}
