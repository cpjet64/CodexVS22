using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;

namespace CodexVS22.Core.Protocol
{
    public enum EventKind
    {
        Unknown,
        SessionConfigured,
        AgentMessageDelta,
        AgentMessage,
        TokenCount,
        StreamError,
        ExecApprovalRequest,
        ApplyPatchApprovalRequest,
        ExecCommandBegin,
        ExecCommandOutputDelta,
        ExecCommandEnd,
        TurnDiff,
        TaskComplete
    }

    public class EventMsg
    {
        public EventKind Kind { get; set; }
        public string Id { get; set; }
        public JsonElement Raw { get; set; }
    }

    public static class EventParser
    {
        public static EventMsg Parse(string line)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var kind = root.TryGetProperty("kind", out var k) ? k.GetString() : root.TryGetProperty("event", out var e) ? e.GetProperty("kind").GetString() : null;
                var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                return new EventMsg
                {
                    Kind = ToKind(kind),
                    Id = id,
                    Raw = root.Clone()
                };
            }
            catch
            {
                return new EventMsg { Kind = EventKind.Unknown, Raw = default };
            }
        }

        private static EventKind ToKind(string s)
        {
            return s switch
            {
                "SessionConfigured" => EventKind.SessionConfigured,
                "AgentMessageDelta" => EventKind.AgentMessageDelta,
                "AgentMessage" => EventKind.AgentMessage,
                "TokenCount" => EventKind.TokenCount,
                "StreamError" => EventKind.StreamError,
                "ExecApprovalRequest" => EventKind.ExecApprovalRequest,
                "ApplyPatchApprovalRequest" => EventKind.ApplyPatchApprovalRequest,
                "ExecCommandBegin" => EventKind.ExecCommandBegin,
                "ExecCommandOutputDelta" => EventKind.ExecCommandOutputDelta,
                "ExecCommandEnd" => EventKind.ExecCommandEnd,
                "TurnDiff" => EventKind.TurnDiff,
                "TaskComplete" => EventKind.TaskComplete,
                _ => EventKind.Unknown
            };
        }
    }

    public sealed class CorrelationMap
    {
        private readonly ConcurrentDictionary<string, object> _map = new();
        public void Add(string id, object state) { if (!string.IsNullOrEmpty(id)) _map[id] = state; }
        public bool TryGet(string id, out object state) => _map.TryGetValue(id, out state);
        public void Remove(string id) { if (!string.IsNullOrEmpty(id)) _map.TryRemove(id, out _); }
    }
}

