using KeyLine.Domain;
using KeyLine.Services.Recording;

namespace KeyLine.Tests;

public sealed class MouseScrollRecordingTests
{
    [Fact]
    public void RecordMouseScroll_CreatesScrollUpNodeForPositiveDelta()
    {
        var recorder = new MacroRecorder();
        recorder.Start();

        var nodes = recorder.RecordMouseScroll(240, includeDelay: false).ToList();

        Assert.Single(nodes);
        Assert.Equal(MacroNodeType.MouseScrollUp, nodes[0].Type);
        Assert.Equal("Wheel Up", nodes[0].KeyName);
        Assert.Equal(120, nodes[0].MouseWheelDelta);
        Assert.Equal(2, nodes[0].MouseScrollAmount);
    }

    [Fact]
    public void RecordMouseScroll_CreatesScrollDownNodeForNegativeDelta()
    {
        var recorder = new MacroRecorder();
        recorder.Start();

        var nodes = recorder.RecordMouseScroll(-120, includeDelay: false).ToList();

        Assert.Single(nodes);
        Assert.Equal(MacroNodeType.MouseScrollDown, nodes[0].Type);
        Assert.Equal("Wheel Down", nodes[0].KeyName);
        Assert.Equal(-120, nodes[0].MouseWheelDelta);
        Assert.Equal(1, nodes[0].MouseScrollAmount);
    }
}
