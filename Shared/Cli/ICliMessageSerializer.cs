using Newtonsoft.Json.Linq;

namespace CodexVS22.Shared.Cli
{
    public interface ICliMessageSerializer
    {
        string CreateUserInputSubmission(string text);

        string CreateExecCancel(string execId);

        string CreateHeartbeatSubmission(JObject opTemplate);
    }
}
