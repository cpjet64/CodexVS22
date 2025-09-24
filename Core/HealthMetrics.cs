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
      // Simple thresholds for status bar indicator at logic-layer.
      // Red: frequent reconnects or high error rate.
      // Yellow: moderate reconnects or sustained high output rate.
      // Green: otherwise.
      HealthLevel level;
      if (reconnects >= 2 || errors >= 5)
        level = HealthLevel.Red;
      else if (reconnects == 1 || errors >= 2 || ratePerSec >= 50)
        level = HealthLevel.Yellow;
      else
        level = HealthLevel.Green;

      return new HealthStatus(level, uptimeMinutes, reconnects, errors, ratePerSec);
    }
  }
}

