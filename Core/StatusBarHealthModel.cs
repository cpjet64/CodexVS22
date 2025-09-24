using System;

namespace CodexVS22.Core
{
  internal sealed class StatusBarHealthModel
  {
    public HealthStatus Last { get; private set; }
    public string Label { get; private set; } = "Healthy";
    public string Glyph { get; private set; } = "●";
    public string Beacon { get; private set; } = "OK";

    public void Update(double uptimeMinutes, int reconnects, int errors, int ratePerSec)
    {
      Last = HealthMetrics.Compute(uptimeMinutes, reconnects, errors, ratePerSec);
      switch (Last.Level)
      {
        case HealthLevel.Green:
          Label = "Healthy"; Glyph = "●"; Beacon = "OK"; break;
        case HealthLevel.Yellow:
          Label = "Degraded"; Glyph = "▲"; Beacon = $"WARN e={Last.Errors} r={Last.Reconnects}"; break;
        case HealthLevel.Red:
          Label = "Unstable"; Glyph = "■"; Beacon = $"ERR e={Last.Errors} r={Last.Reconnects}"; break;
        default:
          Label = "Unknown"; Glyph = "?"; Beacon = "?"; break;
      }
    }
  }
}
