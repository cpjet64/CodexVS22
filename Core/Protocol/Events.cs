using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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
        public JObject Raw { get; set; }
    }

    public static class EventParser
    {
        public static EventMsg Parse(string line)
        {
            try
            {
                var root = JObject.Parse(line);
                var kind = root["kind"]?.ToString() ?? root["event"]?["kind"]?.ToString();
                var id = root["id"]?.ToString();
                return new EventMsg
                {
                    Kind = ToKind(kind),
                    Id = id,
                    Raw = root
                };
            }
            catch
            {
                return new EventMsg { Kind = EventKind.Unknown, Raw = null };
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
