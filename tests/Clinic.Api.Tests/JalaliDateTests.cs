using System.Globalization;
using Clinic.Api.Common;
using Xunit;

namespace Clinic.Api.Tests;

public class JalaliDateTests
{
    private static readonly PersianCalendar Calendar = new();

    // Externally-known anchors (Nowruz 1405 = Saturday 2026-03-21; the second
    // date is exactly 158 days later = 1405/06/04, the requirements day).
    public static TheoryData<DateTime, string> AnchorDates => new()
    {
        { new DateTime(2026, 3, 21, 0, 0, 0), "1405/01/01" },
        { new DateTime(2026, 8, 26, 0, 0, 0), "1405/06/04" },
    };

    [Theory]
    [MemberData(nameof(AnchorDates))]
    public void ToJalaliDate_matches_known_anchor_dates(DateTime tehranLocal, string expected)
    {
        var utc = JalaliDate.UtcFromTehran(tehranLocal);
        Assert.Equal(expected, JalaliDate.ToJalaliDate(utc));
    }

    [Theory]
    [MemberData(nameof(AnchorDates))]
    public void TryParse_round_trips_anchor_dates(DateTime tehranLocal, string jalaliText)
    {
        Assert.True(JalaliDate.TryParse(jalaliText, out var parsed));

        Assert.Equal(tehranLocal.Year, parsed.Year);
        Assert.Equal(tehranLocal.Month, parsed.Month);
        Assert.Equal(tehranLocal.Day, parsed.Day);
        Assert.Equal(0, parsed.Hour);
        Assert.Equal(0, parsed.Minute);
    }

    /// <summary>
    /// Sweeps every day of two years: whatever the parser accepts must match
    /// what the PersianCalendar says, in both directions.
    /// </summary>
    [Fact]
    public void Parser_agrees_with_PersianCalendar_across_two_years()
    {
        var start = new DateTime(2025, 1, 1);
        for (var offset = 0; offset < 730; offset++)
        {
            var gregorian = start.AddDays(offset);
            var expected =
                $"{Calendar.GetYear(gregorian):0000}/{Calendar.GetMonth(gregorian):00}/{Calendar.GetDayOfMonth(gregorian):00}";

            Assert.True(JalaliDate.TryParse(expected, out var parsed), $"Failed to parse {expected}");
            Assert.Equal(gregorian.Date, parsed.Date);
            Assert.Equal(
                $"{expected}",
                $"{Calendar.GetYear(parsed):0000}/{Calendar.GetMonth(parsed):00}/{Calendar.GetDayOfMonth(parsed):00}");
        }
    }

    [Fact]
    public void TryParse_accepts_dash_separator_and_time()
    {
        Assert.True(JalaliDate.TryParse("1405-06-04 14:30", out var parsed));
        var utc = JalaliDate.UtcFromTehran(parsed);

        Assert.Equal("1405/06/04 14:30", JalaliDate.ToJalaliDateTime(utc));
    }

    [Fact]
    public void PersianCalendar_confirms_1403_is_leap()
    {
        // Guards the leap-day expectations below against calendar surprises.
        Assert.True(Calendar.IsLeapYear(1403));
        Assert.False(Calendar.IsLeapYear(1404));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not a date")]
    [InlineData("1405/13/01")]   // month out of range
    [InlineData("1405/07/31")]   // Mehr (month 7) has 30 days; months 1-6 have 31
    [InlineData("1405/01/32")]   // day out of range even for 31-day months
    public void TryParse_rejects_invalid_input(string? text)
    {
        Assert.False(JalaliDate.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_accepts_leap_day_of_leap_year_1403()
    {
        Assert.True(JalaliDate.TryParse("1403/12/30", out _));
    }

    [Fact]
    public void TryParse_rejects_leap_day_of_common_year()
    {
        Assert.False(JalaliDate.TryParse("1404/12/30", out _));
    }

    [Theory]
    [InlineData(DayOfWeek.Saturday, 0)]
    [InlineData(DayOfWeek.Sunday, 1)]
    [InlineData(DayOfWeek.Monday, 2)]
    [InlineData(DayOfWeek.Tuesday, 3)]
    [InlineData(DayOfWeek.Wednesday, 4)]
    [InlineData(DayOfWeek.Thursday, 5)]
    [InlineData(DayOfWeek.Friday, 6)]
    public void IranianWeekday_maps_saturday_first(DayOfWeek day, int expected) =>
        Assert.Equal(expected, JalaliDate.IranianWeekday(day));

    [Fact]
    public void Requirements_day_is_Wednesday()
    {
        // 2026-08-26 was described as "Wednesday" during requirements gathering.
        Assert.Equal(DayOfWeek.Wednesday, new DateTime(2026, 8, 26).DayOfWeek);
        Assert.Equal(4, JalaliDate.IranianWeekday(DayOfWeek.Wednesday)); // چهارشنبه
    }

    [Theory]
    [InlineData("09:00")]
    [InlineData("23:59")]
    [InlineData("00:00")]
    public void TryParseTime_accepts_valid_times(string text)
    {
        Assert.True(JalaliDate.TryParseTime(text, out _, out _));
    }

    [Theory]
    [InlineData("24:00")]   // hour out of range
    [InlineData("7:30")]    // not zero-padded HH
    [InlineData("9:5")]
    [InlineData("abc")]
    [InlineData("10")]
    [InlineData("")]
    public void TryParseTime_rejects_invalid_times(string text)
    {
        Assert.False(JalaliDate.TryParseTime(text, out _, out _));
    }
}

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("0912 345 6789", "+989123456789")]
    [InlineData("(+98) 912-345-6789", "+989123456789")]
    [InlineData(" 09121234567 ", "+989121234567")]
    [InlineData("00989121234567", "+989121234567")]
    [InlineData("989121234567", "+989121234567")]
    [InlineData("+1 (555) 010-2030", "+15550102030")]
    [InlineData("5551234", "5551234")] // short foreign numbers pass through
    public void Normalize_canonicalizes_iranian_numbers_to_e164(string input, string expected) =>
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));

    [Fact]
    public void Normalize_collapses_all_local_spellings_to_one_identity()
    {
        var canonical = PhoneNormalizer.Normalize("+989121234567");
        Assert.Equal(canonical, PhoneNormalizer.Normalize("0912 123 4567"));
        Assert.Equal(canonical, PhoneNormalizer.Normalize("989121234567"));
        Assert.Equal(canonical, PhoneNormalizer.Normalize("00989121234567"));
    }

    [Fact]
    public void ShortCodes_exclude_confusable_characters()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = Codes.NewShortCode();
            Assert.Equal(8, code.Length);
            Assert.All(code, c => Assert.DoesNotContain(c, "OIL10"));
        }
    }

    [Fact]
    public void SixDigitCodes_are_zero_padded()
    {
        for (var i = 0; i < 100; i++)
        {
            var code = Codes.NewSixDigitCode();
            Assert.Equal(6, code.Length);
            Assert.All(code, c => Assert.InRange(c, '0', '9'));
        }
    }
}
