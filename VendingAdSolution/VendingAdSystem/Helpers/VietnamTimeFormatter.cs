namespace VendingAdSystem.Helpers;

public static class VietnamTimeFormatter
{
    private static readonly Lazy<TimeZoneInfo> VietnamTimeZone = new(ResolveVietnamTimeZone);

    public static DateTime ToVietnamTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Local)
            utcDateTime = utcDateTime.ToUniversalTime();
        else if (utcDateTime.Kind == DateTimeKind.Unspecified)
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone.Value);
    }

    public static string FormatUtc(DateTime utcDateTime, string format)
        => ToVietnamTime(utcDateTime).ToString(format);

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        string[] timeZoneIds =
        [
            "SE Asia Standard Time",
            "Asia/Ho_Chi_Minh"
        ];

        foreach (var id in timeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "UTC+07",
            TimeSpan.FromHours(7),
            "UTC+07",
            "UTC+07");
    }
}
