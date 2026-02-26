using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Shared.Cli
{
    public sealed class CliSubmissionFactory : ICliMessageSerializer
    {
        public string CreateUserInputSubmission(string text)
        {
            var submission = new JObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["op"] = new JObject
                {
                    ["type"] = "user_input",
                    ["items"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "text",
                            ["text"] = text ?? string.Empty
                        }
                    }
                }
            };

            return submission.ToString(Formatting.None);
        }

        public string CreateExecCancel(string execId)
        {
            if (string.IsNullOrWhiteSpace(execId))
                return string.Empty;

            var submission = new JObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["op"] = new JObject
                {
                    ["type"] = "exec_cancel",
                    ["id"] = execId,
                    ["call_id"] = execId
                }
            };

            return submission.ToString(Formatting.None);
        }

        public string CreateHeartbeatSubmission(JObject opTemplate)
        {
            if (opTemplate == null)
                return string.Empty;

            var op = opTemplate.DeepClone() as JObject;
            if (op == null)
                return string.Empty;

            var type = op.Value<string>("type");
            if (string.IsNullOrWhiteSpace(type))
                return string.Empty;

            var submission = new JObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["op"] = op
            };

            return submission.ToString(Formatting.None);
        }
    }
}
