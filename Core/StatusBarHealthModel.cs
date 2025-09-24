using System;

namespace CodexVS22.Core
{
  internal sealed class StatusBarHealthModel
  {
    public HealthStatus Last { get; private set; }
    public string Label { get; private set; } = "Healthy";
    public string Glyph { get; private set; } = "●";

    public void Update(double uptimeMinutes, int reconnects, int errors, int ratePerSec)
    {
      Last = HealthMetrics.Compute(uptimeMinutes, reconnects, errors, ratePerSec);
      switch (Last.Level)
      {
        case HealthLevel.Green:
          Label = "Healthy"; Glyph = "●"; break;
        case HealthLevel.Yellow:
          Label = "Degraded"; Glyph = "▲"; break;
        case HealthLevel.Red:
          Label = "Unstable"; Glyph = "■"; break;
        default:
          Label = "Unknown"; Glyph = "?"; break;
      }
    }
  }
}

