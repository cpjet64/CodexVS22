namespace CodexVS22.Core
{
  internal enum HealthLevel
  {
    Green,
    Yellow,
    Red
  }

  internal readonly struct HealthStatus
  {
    public HealthStatus(HealthLevel level, double uptimeMinutes, int reconnects, int errors, int ratePerSec)
    {
      Level = level; UptimeMinutes = uptimeMinutes; Reconnects = reconnects; Errors = errors; RatePerSec = ratePerSec;
    }
    public HealthLevel Level { get; }
    public double UptimeMinutes { get; }
    public int Reconnects { get; }
    public int Errors { get; }
    public int RatePerSec { get; }
  }

  internal static class HealthMetrics
  {
    public static HealthStatus Compute(double uptimeMinutes, int reconnects, int errors, int ratePerSec)
    {
      // Refined thresholds (UX-oriented):
      // - Red: reconnects >= 3 OR errors >= 4 OR rate >= 120 lines/sec
      // - Yellow: reconnects in [1..2] OR errors in [2..3] OR rate in [60..119]
      // - Green: otherwise
      HealthLevel level;
      if (reconnects >= 3 || errors >= 4 || ratePerSec >= 120)
        level = HealthLevel.Red;
      else if ((reconnects >= 1 && reconnects <= 2) || (errors >= 2 && errors <= 3) || (ratePerSec >= 60))
        level = HealthLevel.Yellow;
      else
        level = HealthLevel.Green;

      return new HealthStatus(level, uptimeMinutes, reconnects, errors, ratePerSec);
    }
  }
}
