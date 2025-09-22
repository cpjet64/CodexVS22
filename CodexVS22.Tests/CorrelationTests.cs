using System.Collections.Generic;
using System.Text;
using CodexVS22.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Tests
{
  [TestClass]
  public class CorrelationTests
  {
    [TestMethod]
    public void AgentMessageEvents_CorrelateDeltaAndFinal()
    {
      var lines = new[]
      {
        "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"Hello \"}}",
        "{\"msg\":{\"kind\":\"AgentMessageDelta\",\"id\":\"turn-1\",\"text_delta\":\"World\"}}",
        "{\"msg\":{\"kind\":\"AgentMessage\",\"id\":\"turn-1\",\"text\":\"Hello World!\"}}"
      };

      var tracker = new TranscriptTracker();
      tracker.Process(lines);

      Assert.AreEqual("Hello World!", tracker.GetTranscript("turn-1"));
      Assert.IsFalse(tracker.HasInFlight("turn-1"));
    }

    [TestMethod]
    public void ParallelTurns_CorrelateIndependently()
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

      Assert.AreEqual("beta!", tracker.GetTranscript("turn-B"));
      Assert.IsFalse(tracker.HasTranscript("turn-A"));
      Assert.IsFalse(tracker.HasInFlight("turn-A"));
      Assert.IsFalse(tracker.HasInFlight("turn-B"));
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
      {
        return _completed.TryGetValue(id, out var text) ? text : string.Empty;
      }

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
}
