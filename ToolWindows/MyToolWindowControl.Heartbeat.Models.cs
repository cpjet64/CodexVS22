using System;
using CodexVS22.Shared.Cli;
using Newtonsoft.Json.Linq;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private sealed class HeartbeatState
    {
      public HeartbeatState(CliHeartbeatTemplate template)
      {
        Template = template;
      }

      public CliHeartbeatTemplate Template { get; }

      public TimeSpan Interval => Template?.Interval ?? TimeSpan.Zero;

      public JObject OpTemplate => Template?.OpTemplate;

      public string OpType => Template?.OperationType ?? string.Empty;
    }
  }
}
