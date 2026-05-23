using System.Windows.Input;

namespace KeySpammer.Core;

public sealed class MacroRecorder
{
    private DateTime _lastInputTimeUtc;

    public bool IsRecording { get; private set; }

    public void Start()
    {
        IsRecording = true;
        _lastInputTimeUtc = DateTime.UtcNow;
    }

    public void Stop()
    {
        IsRecording = false;
    }

    public IEnumerable<MacroStep> RecordKeyDown(KeyEventArgs e, bool includeDelay)
    {
        return RecordKey(e, MacroStepType.KeyDown, includeDelay);
    }

    public IEnumerable<MacroStep> RecordKeyUp(KeyEventArgs e, bool includeDelay)
    {
        return RecordKey(e, MacroStepType.KeyUp, includeDelay);
    }

    private IEnumerable<MacroStep> RecordKey(KeyEventArgs e, MacroStepType type, bool includeDelay)
    {
        if (!VirtualKeyParser.TryFromRecordedKey(e, out var virtualKey, out var keyName))
            yield break;

        if (includeDelay)
        {
            var delayMs = GetDelaySinceLastInput();

            if (delayMs > 0)
            {
                yield return new MacroStep
                {
                    Type = MacroStepType.Delay,
                    DelayMs = delayMs,
                    IsRecordedDelay = true
                };
            }
        }

        yield return new MacroStep
        {
            Type = type,
            KeyName = keyName,
            VirtualKey = virtualKey
        };

        _lastInputTimeUtc = DateTime.UtcNow;
    }

    private int GetDelaySinceLastInput()
    {
        return (int)(DateTime.UtcNow - _lastInputTimeUtc).TotalMilliseconds;
    }
}