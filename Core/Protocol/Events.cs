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

                var payload =
                    root["msg"] as JObject ??
                    root["event"] as JObject ??
                    root["payload"] as JObject ??
                    root;

                var kindValue =
                    payload?["kind"]?.ToString() ??
                    payload?["type"]?.ToString() ??
                    root["kind"]?.ToString() ??
                    root["event"]?["kind"]?.ToString();

                var id =
                    root["id"]?.ToString() ??
                    payload?["id"]?.ToString();

                return new EventMsg
                {
                    Kind = ToKind(kindValue),
                    Id = id,
                    Raw = payload ?? root
                };
            }
            catch
            {
                return new EventMsg { Kind = EventKind.Unknown, Raw = null };
            }
        }

        private static EventKind ToKind(string s)
        {
            if (string.IsNullOrEmpty(s))
                return EventKind.Unknown;

            return s switch
            {
                "SessionConfigured" or "session_configured" => EventKind.SessionConfigured,
                "AgentMessageDelta" or "agent_message_delta" => EventKind.AgentMessageDelta,
                "AgentMessage" or "agent_message" => EventKind.AgentMessage,
                "TokenCount" or "token_count" => EventKind.TokenCount,
                "StreamError" or "stream_error" => EventKind.StreamError,
                "ExecApprovalRequest" or "exec_approval_request" => EventKind.ExecApprovalRequest,
                "ApplyPatchApprovalRequest" or "apply_patch_approval_request" => EventKind.ApplyPatchApprovalRequest,
                "ExecCommandBegin" or "exec_command_begin" => EventKind.ExecCommandBegin,
                "ExecCommandOutputDelta" or "exec_command_output_delta" => EventKind.ExecCommandOutputDelta,
                "ExecCommandEnd" or "exec_command_end" => EventKind.ExecCommandEnd,
                "TurnDiff" or "turn_diff" => EventKind.TurnDiff,
                "TaskComplete" or "task_complete" => EventKind.TaskComplete,
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
