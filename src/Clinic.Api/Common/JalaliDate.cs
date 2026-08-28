using System.Globalization;
using System.Security.Cryptography;

namespace Clinic.Api.Common;

/// <summary>
/// Jalali (Persian) calendar + Iran timezone helpers.
/// All storage is UTC; conversion happens at the API edge.
/// Iran has been on fixed UTC+03:30 since DST was abolished in 2022 — a custom
/// time zone is used so containers without tzdata behave identically.
/// Jalali text format: "1405/06/04" and "1405/06/04 14:30".
/// </summary>
public static class JalaliDate
{
    public const int IranOffsetMinutes = 210; // UTC+03:30

    private static readonly TimeZoneInfo TehranZone =
        TimeZoneInfo.CreateCustomTimeZone("IranStandard", TimeSpan.FromMinutes(IranOffsetMinutes), "Iran Standard Time", "Iran Standard Time");

    private static readonly PersianCalendar Calendar = new();

    /// <summary>Current wall-clock time in Tehran.</summary>
    public static DateTime NowTehran => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TehranZone);

    public static DateTime UtcToTehran(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TehranZone);

    public static DateTime UtcFromTehran(DateTime tehranLocal) =>
        tehranLocal.Kind == DateTimeKind.Local
            ? throw new ArgumentException("Expected Kind=Unspecified Tehran local time", nameof(tehranLocal))
            : TimeZoneInfo.ConvertTimeToUtc(tehranLocal, TehranZone);

    /// <summary>Formats a UTC instant as a Jalali date in Tehran time, e.g. "1405/06/04".</summary>
    public static string ToJalaliDate(DateTime utc)
    {
        var local = UtcToTehran(utc);
        return $"{Calendar.GetYear(local):0000}/{Calendar.GetMonth(local):00}/{Calendar.GetDayOfMonth(local):00}";
    }

    /// <summary>Formats a UTC instant as "1405/06/04 14:30" in Tehran time.</summary>
    public static string ToJalaliDateTime(DateTime utc)
    {
        var local = UtcToTehran(utc);
        return $"{ToJalaliDate(utc)} {local.Hour:00}:{local.Minute:00}";
    }

    /// <summary>
    /// Parses Jalali text ("1405/06/04" optionally with "14:30") into Tehran-local time.
    /// Accepts both "/" and "-" separators.
    /// </summary>
    public static bool TryParse(string? text, out DateTime tehranLocal)
    {
        tehranLocal = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2) return false;

        var dateSegments = parts[0].Replace('-', '/').Split('/');
        if (dateSegments.Length != 3) return false;
        if (!int.TryParse(dateSegments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) return false;
        if (!int.TryParse(dateSegments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)) return false;
        if (!int.TryParse(dateSegments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)) return false;

        var hour = 0;
        var minute = 0;
        if (parts.Length == 2)
        {
            var timeSegments = parts[1].Split(':');
            if (timeSegments.Length != 2) return false;
            if (!int.TryParse(timeSegments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)) return false;
            if (!int.TryParse(timeSegments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minute)) return false;
        }

        try
        {
            tehranLocal = Calendar.ToDateTime(year, month, day, hour, minute, 0, 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses strict "HH:mm" into hours/minutes. Both parts must be exactly
    /// two digits — "9:5" and "7:30" are rejected, matching the documented
    /// template format and avoiding ambiguous imports.
    /// </summary>
    public static bool TryParseTime(string? text, out int hour, out int minute)
    {
        hour = minute = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var segments = text.Trim().Split(':');
        if (segments.Length != 2) return false;
        if (segments[0].Length != 2 || segments[1].Length != 2) return false;
        if (!int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)) return false;
        if (!int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minute)) return false;
        return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    /// <summary>Iranian weekday index: Saturday=0 … Friday=6.</summary>
    public static int IranianWeekday(DayOfWeek day) => ((int)day + 1) % 7;

    public static readonly string[] WeekdayNamesFa =
    [
        "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه",
    ];

    public static readonly string[] WeekdayNamesEn =
    [
        "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday",
    ];
}

/// <summary>Short human tracking codes (confusable characters excluded).</summary>
public static class Codes
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string NewShortCode(int length = 8)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(b => Alphabet[b % Alphabet.Length]).ToArray());
    }

    public static string NewSixDigitCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

public static class PhoneNormalizer
{
    /// <summary>
    /// Canonicalizes Iranian numbers to E.164-style "+98…" so 0912…, 98912… and
    /// 0098912… all become the same identity (per-phone caps can't be evaded by
    /// reformatting, lookups match regardless of spelling). Other formats keep
    /// their digits, plus a leading "+" when one was supplied.
    /// </summary>
    public static string Normalize(string phone)
    {
        var trimmed = phone?.Trim() ?? string.Empty;
        var hasPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        if (!hasPlus)
        {
            if (digits.StartsWith("00"))
                return "+" + digits[2..];
            if (digits.Length == 12 && digits.StartsWith("98")) // 989123456789
                return "+" + digits;
            if (digits.Length == 11 && digits.StartsWith('0'))  // 09123456789
                return "+98" + digits[1..];
        }

        return hasPlus ? "+" + digits : digits;
    }
}
