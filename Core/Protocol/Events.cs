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
        ListMcpTools,
        ToolCallBegin,
        ToolCallOutput,
        ToolCallEnd,
        ListCustomPrompts,
        TurnDiff,
        PatchApplyBegin,
        PatchApplyEnd,
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
                "ListMcpTools" or "list_mcp_tools" or "ListMcpToolsResponse" or "list_mcp_tools_response" or "McpToolsListed" or "mcp_tools_listed" => EventKind.ListMcpTools,
                "ToolCallBegin" or "tool_call_begin" or "ToolCallStarted" or "tool_call_started" or "ToolCallStart" or "tool_call_start" => EventKind.ToolCallBegin,
                "ToolCallOutput" or "tool_call_output" or "ToolCallOutputDelta" or "tool_call_output_delta" or "ToolCallDelta" or "tool_call_delta" => EventKind.ToolCallOutput,
                "ToolCallEnd" or "tool_call_end" or "ToolCallFinished" or "tool_call_finished" or "ToolCallComplete" or "tool_call_complete" or "ToolCallCompleted" or "tool_call_completed" or "ToolCallResult" or "tool_call_result" => EventKind.ToolCallEnd,
                "ListCustomPrompts" or "list_custom_prompts" or "CustomPromptsListed" or "custom_prompts_listed" or "ListPrompts" or "list_prompts" => EventKind.ListCustomPrompts,
                "TurnDiff" or "turn_diff" => EventKind.TurnDiff,
                "PatchApplyBegin" or "patch_apply_begin" => EventKind.PatchApplyBegin,
                "PatchApplyEnd" or "patch_apply_end" => EventKind.PatchApplyEnd,
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
