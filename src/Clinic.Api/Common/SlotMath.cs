namespace Clinic.Api.Common;

/// <summary>Pure interval math shared by booking, availability and tests.</summary>
public static class SlotMath
{
    /// <summary>True when [aStart,aEnd) intersects [bStart,bEnd). Adjacent ranges do not overlap.</summary>
    public static bool Overlaps(TimeOnly aStart, TimeOnly aEnd, TimeOnly bStart, TimeOnly bEnd) =>
        aStart < bEnd && bStart < aEnd;

    /// <summary>True when [start,end) lies fully within the window.</summary>
    public static bool FitsInWindow(TimeOnly start, TimeOnly end, TimeOnly windowStart, TimeOnly windowEnd) =>
        windowStart <= start && start < end && end <= windowEnd;
}
