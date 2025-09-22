using System;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Core
{
  internal static class ApprovalSubmissionFactory
  {
    private static long _counter;

    public static string CreateExec(string requestId, bool approved)
    {
      var decision = approved ? "approved" : "denied";
      var callId = requestId ?? string.Empty;
      var submissionId = !string.IsNullOrEmpty(callId)
        ? $"{callId}:exec_{Interlocked.Increment(ref _counter)}"
        : Guid.NewGuid().ToString();

      var submission = new JObject
      {
        ["id"] = submissionId,
        ["op"] = new JObject
        {
          ["type"] = "exec_approval",
          ["id"] = callId,
          ["call_id"] = callId,
          ["decision"] = decision,
          ["approved"] = approved
        }
      };

      return submission.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static string CreatePatch(string requestId, bool approved)
    {
      var decision = approved ? "approved" : "denied";
      var callId = requestId ?? string.Empty;
      var submissionId = !string.IsNullOrEmpty(callId)
        ? $"{callId}:patch_{Interlocked.Increment(ref _counter)}"
        : Guid.NewGuid().ToString();

      var submission = new JObject
      {
        ["id"] = submissionId,
        ["op"] = new JObject
        {
          ["type"] = "patch_approval",
          ["id"] = callId,
          ["call_id"] = callId,
          ["decision"] = decision,
          ["approved"] = approved
        }
      };

      return submission.ToString(Newtonsoft.Json.Formatting.None);
    }
  }
}
