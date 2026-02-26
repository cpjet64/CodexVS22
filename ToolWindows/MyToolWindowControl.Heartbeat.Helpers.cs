using CodexVS22.Shared.Cli;
using Newtonsoft.Json.Linq;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private static string CreateHeartbeatSubmission(JObject opTemplate)
    {
      var factory = new CliSubmissionFactory();
      return factory.CreateHeartbeatSubmission(opTemplate);
    }

    private static HeartbeatState ExtractHeartbeatState(JObject raw)
    {
      return CliHeartbeatHelper.TryCreateHeartbeatTemplate(raw, out var template)
        ? new HeartbeatState(template)
        : null;
    }
  }
}
