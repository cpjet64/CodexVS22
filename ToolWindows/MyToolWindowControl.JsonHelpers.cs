using Newtonsoft.Json.Linq;
using CodexVS22.Shared.Cli;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private static string TryGetString(JObject obj, params string[] names)
    {
      return CliJsonUtilities.TryGetString(obj, names);
    }

    private static int? TryGetInt(JObject obj, params string[] names)
    {
      return CliJsonUtilities.TryGetInt(obj, names);
    }

    private static bool? TryGetBoolean(JObject obj, params string[] names)
    {
      return CliJsonUtilities.TryGetBoolean(obj, names);
    }

    private static JToken SafeSelectToken(JObject obj, string path)
    {
      return CliJsonUtilities.SafeSelectToken(obj, path);
    }
  }
}
