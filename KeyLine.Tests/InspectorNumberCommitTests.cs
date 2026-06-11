using KeyLine.Services.Timeline;
using KeyLine.UI.Inspector.Batch;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.Tests;

/// <summary>
/// Covers the shared commit decision every inspector numeric/delay field uses, so the "write only when the
/// value actually changed" and "never normalize a mixed field" rules are guaranteed across all fields.
/// </summary>
public sealed class InspectorNumberCommitTests
{
    [Fact]
    public void Decide_WritesParsedValue_WhenChanged()
    {
        var result = InspectorNumberCommit.Decide("50", BatchValue<int>.Common(20), min: 0, max: null);

        Assert.True(result.ShouldWrite);
        Assert.Equal(50, result.Value);
    }

    [Fact]
    public void Decide_DoesNotWrite_WhenValueUnchanged()
    {
        var result = InspectorNumberCommit.Decide("20", BatchValue<int>.Common(20), min: 0, max: null);

        Assert.False(result.ShouldWrite);
    }

    [Fact]
    public void Decide_ClampsToMinAndMax()
    {
        var high = InspectorNumberCommit.Decide("500", BatchValue<int>.Common(10), min: 0, max: 100);
        var low = InspectorNumberCommit.Decide("-5", BatchValue<int>.Common(10), min: 1, max: 100);

        Assert.True(high.ShouldWrite);
        Assert.Equal(100, high.Value);
        Assert.True(low.ShouldWrite);
        Assert.Equal(1, low.Value);
    }

    [Fact]
    public void Decide_NonNumericOnNormalField_FallsBackToMin()
    {
        var result = InspectorNumberCommit.Decide("abc", BatchValue<int>.Common(50), min: 7, max: null);

        Assert.True(result.ShouldWrite);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void Decide_MixedAndUnparseable_DoesNotWrite()
    {
        var empty = InspectorNumberCommit.Decide("", BatchValue<int>.Mixed(), min: 0, max: null);
        var junk = InspectorNumberCommit.Decide("--", BatchValue<int>.Mixed(), min: 0, max: null);

        Assert.False(empty.ShouldWrite);
        Assert.False(junk.ShouldWrite);
    }

    [Fact]
    public void Decide_MixedWithValue_Writes()
    {
        var result = InspectorNumberCommit.Decide("42", BatchValue<int>.Mixed(), min: 0, max: 100);

        Assert.True(result.ShouldWrite);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void DecideDelay_EmptyOnNormalField_WritesZero()
    {
        var result = InspectorNumberCommit.DecideDelay("", BatchValue<int>.Common(500));

        Assert.True(result.ShouldWrite);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void DecideDelay_MixedAndEmpty_DoesNotWrite()
    {
        var result = InspectorNumberCommit.DecideDelay("", BatchValue<int>.Mixed());

        Assert.False(result.ShouldWrite);
    }

    [Fact]
    public void DecideDelay_ClampsToMaxMilliseconds()
    {
        var huge = (long)DelayFormatter.MaxMilliseconds + 10_000;
        var result = InspectorNumberCommit.DecideDelay(huge.ToString(), BatchValue<int>.Common(0));

        Assert.True(result.ShouldWrite);
        Assert.Equal(DelayFormatter.MaxMilliseconds, result.Value);
    }

    [Fact]
    public void DecideDelay_DoesNotWrite_WhenUnchanged()
    {
        var result = InspectorNumberCommit.DecideDelay("250", BatchValue<int>.Common(250));

        Assert.False(result.ShouldWrite);
    }
}
