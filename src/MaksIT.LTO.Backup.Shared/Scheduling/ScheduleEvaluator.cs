using System.Globalization;
using MaksIT.LTO.Backup.Shared.Models;


namespace MaksIT.LTO.Backup.Shared.Scheduling;

public static class ScheduleEvaluator {
  public sealed record EvaluationResult(bool ShouldExecute, DateTimeOffset Now, string? SkipReason);

  public static bool MatchesMonth(DateTimeOffset utcNow, IReadOnlyList<string> runMonth) {
    if (runMonth.Count == 0) {
      return true;
    }
    string name = utcNow.UtcDateTime.ToString("MMMM", CultureInfo.InvariantCulture);
    return runMonth.Any((string month) => string.Equals(month, name, StringComparison.OrdinalIgnoreCase));
  }

  public static bool MatchesWeekday(DateTimeOffset utcNow, IReadOnlyList<string> runWeekday) {
    if (runWeekday.Count == 0) {
      return true;
    }
    string name = utcNow.UtcDateTime.DayOfWeek.ToString();
    return runWeekday.Any((string day) => string.Equals(day, name, StringComparison.OrdinalIgnoreCase));
  }

  public static bool MatchesTimeExact(DateTimeOffset utcNow, IReadOnlyList<string> runTime) {
    if (runTime.Count == 0) {
      return true;
    }
    string clock = utcNow.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    return runTime.Any((string time) => string.Equals(time, clock, StringComparison.Ordinal));
  }

  public static bool IntervalElapsed(DateTimeOffset? lastRunUtc, DateTimeOffset utcNow, int minIntervalMinutes) {
    if (!lastRunUtc.HasValue) {
      return true;
    }
    return utcNow >= lastRunUtc.Value.AddMinutes(minIntervalMinutes);
  }

  public static bool HasUnsatisfiedRunTime(DateTimeOffset utcNow, IReadOnlyList<string> runTime, DateTimeOffset? lastRunUtc) {
    if (runTime.Count == 0) {
      return true;
    }
    DateOnly dateOnly = DateOnly.FromDateTime(utcNow.UtcDateTime);
    TimeOnly timeOnly = TimeOnly.FromDateTime(utcNow.UtcDateTime);
    foreach (string item in runTime) {
      if (TimeOnly.TryParseExact(item, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) && !(result > timeOnly)) {
        if (!lastRunUtc.HasValue) {
          return true;
        }
        DateTime utcDateTime = lastRunUtc.Value.UtcDateTime;
        DateOnly dateOnly2 = DateOnly.FromDateTime(utcDateTime);
        if (dateOnly2 < dateOnly) {
          return true;
        }
        if (dateOnly2 == dateOnly && TimeOnly.FromDateTime(utcDateTime) < result) {
          return true;
        }
      }
    }
    return false;
  }

  public static bool MatchesScheduleCatchUp(DateTimeOffset utcNow, BackupSchedule schedule, DateTimeOffset? lastRunUtc) => MatchesMonth(utcNow, schedule.RunMonth) && MatchesWeekday(utcNow, schedule.RunWeekday) && HasUnsatisfiedRunTime(utcNow, schedule.RunTime, lastRunUtc);

  public static EvaluationResult Evaluate(bool automated, BackupSchedule? schedule, DateTimeOffset? lastRunUtc, DateTimeOffset? utcNow = null) {
    DateTimeOffset dateTimeOffset = utcNow ?? DateTimeOffset.UtcNow;
    if (automated) {
      if (schedule == null) {
        return new EvaluationResult(ShouldExecute: false, dateTimeOffset, "No schedule configured.");
      }
      if (!MatchesScheduleCatchUp(dateTimeOffset, schedule, lastRunUtc)) {
        return new EvaluationResult(ShouldExecute: false, dateTimeOffset, "Execution skipped due to schedule.");
      }
    }
    int minIntervalMinutes = schedule?.MinIntervalMinutes ?? 10;
    if (!IntervalElapsed(lastRunUtc, dateTimeOffset, minIntervalMinutes)) {
      return new EvaluationResult(ShouldExecute: false, dateTimeOffset, $"Last run at {lastRunUtc:o}. Interval not reached.");
    }
    return new EvaluationResult(ShouldExecute: true, dateTimeOffset, null);
  }
}
