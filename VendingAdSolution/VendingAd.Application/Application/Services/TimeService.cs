namespace VendingAdSystem.Application.Services;

public interface ITimeService
{
    DateTime UtcNow { get; }
    DateTime ToVietnamTime(DateTime utc);
    DateTime ToUtc(DateTime local);
}

public class TimeService : ITimeService
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime ToVietnamTime(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Unspecified)
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
    }

    public DateTime ToUtc(DateTime local)
    {
        if (local.Kind == DateTimeKind.Utc)
            return local;

        if (local.Kind == DateTimeKind.Unspecified)
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(local, VietnamTimeZone);
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
