using System;
using Newtonsoft.Json.Linq;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private sealed class HeartbeatState
    {
      public HeartbeatState(TimeSpan interval, JObject opTemplate, string opType)
      {
        Interval = interval;
        OpTemplate = opTemplate;
        OpType = opType ?? string.Empty;
      }

      public TimeSpan Interval { get; }

      public JObject OpTemplate { get; }

      public string OpType { get; }
    }
  }
}
