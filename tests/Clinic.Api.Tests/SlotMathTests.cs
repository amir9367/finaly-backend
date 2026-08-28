using Clinic.Api.Common;
using Xunit;

namespace Clinic.Api.Tests;

public class SlotMathTests
{
    private static TimeOnly T(string hhmm)
    {
        var parts = hhmm.Split(':');
        return new TimeOnly(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    [Fact]
    public void Adjacent_bookings_do_not_overlap()
    {
        // 09:00–09:20 followed by 09:20–09:40 is legal.
        Assert.False(SlotMath.Overlaps(T("09:00"), T("09:20"), T("09:20"), T("09:40")));
        Assert.False(SlotMath.Overlaps(T("09:20"), T("09:40"), T("09:00"), T("09:20")));
    }

    [Fact]
    public void Partial_overlaps_are_detected_in_both_directions()
    {
        Assert.True(SlotMath.Overlaps(T("09:00"), T("09:30"), T("09:20"), T("09:50")));
        Assert.True(SlotMath.Overlaps(T("09:20"), T("09:50"), T("09:00"), T("09:30")));
    }

    [Fact]
    public void Containment_and_identical_ranges_overlap()
    {
        Assert.True(SlotMath.Overlaps(T("09:00"), T("10:00"), T("09:10"), T("09:15")));
        Assert.True(SlotMath.Overlaps(T("09:00"), T("10:00"), T("09:00"), T("10:00")));
    }

    [Fact]
    public void Disjoint_ranges_do_not_overlap()
    {
        Assert.False(SlotMath.Overlaps(T("08:00"), T("08:59"), T("09:00"), T("10:00")));
    }

    [Fact]
    public void FitsInWindow_requires_full_containment()
    {
        Assert.True(SlotMath.FitsInWindow(T("09:00"), T("09:20"), T("09:00"), T("13:00")));
        Assert.False(SlotMath.FitsInWindow(T("12:50"), T("13:10"), T("09:00"), T("13:00"))); // spills past end
        Assert.False(SlotMath.FitsInWindow(T("08:50"), T("09:10"), T("09:00"), T("13:00"))); // starts early
    }
}
