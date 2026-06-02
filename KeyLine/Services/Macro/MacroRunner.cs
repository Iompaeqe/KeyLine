using KeyLine.Domain;
using KeyLine.Services.Input;

namespace KeyLine.Services.Macro;

public sealed class MacroRunner
{
    private const int InfiniteLoopMinimumDelayMs = 10;

    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _pauseGate;

    public bool IsRunning => _cts != null;
    public bool IsPaused => _pauseGate != null;
    public CancellationToken CancellationToken => _cts?.Token ?? CancellationToken.None;

    public async Task StartAsync(
        nint targetHwnd,
        IReadOnlyList<MacroNode> sourceSteps,
        int loopCount,
        int baseDelayMs,
        bool useStandardDelay,
        int standardDelayMs,
        bool useTextInputMode,
        Action? loopCompleted = null)
    {
        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        _pauseGate = null;
        var token = _cts.Token;

        var executionSteps = BuildExecutionSteps(sourceSteps, useStandardDelay, standardDelayMs);
        var minimumDelayMs = loopCount == 0 ? InfiniteLoopMinimumDelayMs : 0;
        var safeBaseDelayMs = Math.Max(Math.Max(0, baseDelayMs), minimumDelayMs);
        var heldModifierKeys = new HashSet<int>();

        try
        {
            var currentLoop = 0;

            while (!token.IsCancellationRequested)
            {
                var didDelayThisLoop = false;

                for (var i = 0; i < executionSteps.Count; i++)
                {
                    var step = executionSteps[i];
                    token.ThrowIfCancellationRequested();
                    await WaitIfPaused(token);
                    await ExecuteStep(targetHwnd, step, minimumDelayMs, heldModifierKeys, useTextInputMode, token);

                    didDelayThisLoop |= step.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay;

                    if (ShouldApplyBaseDelay(executionSteps, i) && safeBaseDelayMs > 0)
                    {
                        await DelayWithPause(safeBaseDelayMs, token);
                        didDelayThisLoop = true;
                    }
                }

                if (!didDelayThisLoop && minimumDelayMs > 0)
                    await DelayWithPause(minimumDelayMs, token);

                currentLoop++;
                loopCompleted?.Invoke();

                if (loopCount > 0 && currentLoop >= loopCount)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts = null;
            _pauseGate = null;
        }
    }

    public void Stop()
    {
        Resume();
        _cts?.Cancel();
    }

    public async Task DelayAsync(int milliseconds)
    {
        if (_cts != null)
            return;

        var delayMs = Math.Max(0, milliseconds);
        if (delayMs <= 0)
            return;

        _cts = new CancellationTokenSource();
        _pauseGate = null;
        var token = _cts.Token;

        try
        {
            await DelayWithPause(delayMs, token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts = null;
            _pauseGate = null;
        }
    }

    public void Pause()
    {
        if (_cts == null || _pauseGate != null)
            return;

        _pauseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Resume()
    {
        var gate = _pauseGate;
        if (gate == null)
            return;

        _pauseGate = null;
        gate.TrySetResult();
    }

    private static List<MacroNode> BuildExecutionSteps(
        IReadOnlyList<MacroNode> sourceSteps,
        bool useStandardDelay,
        int standardDelayMs)
    {
        if (!useStandardDelay)
            return sourceSteps.ToList();

        var result = new List<MacroNode>();

        foreach (var step in sourceSteps)
        {
            if (step.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay)
                continue;

            result.Add(step);

            result.Add(new MacroNode
            {
                Type = MacroNodeType.Delay,
                DelayMs = standardDelayMs
            });
        }

        return result;
    }

    private static bool ShouldApplyBaseDelay(IReadOnlyList<MacroNode> executionSteps, int index)
    {
        var step = executionSteps[index];
        if (step.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay)
            return false;

        var nextIndex = index + 1;
        return nextIndex >= executionSteps.Count ||
               executionSteps[nextIndex].Type is not (MacroNodeType.Delay or MacroNodeType.RandomDelay);
    }

    private async Task ExecuteStep(
        nint hwnd,
        MacroNode node,
        int minimumDelayMs,
        ISet<int> heldModifierKeys,
        bool useTextInputMode,
        CancellationToken token)
    {
        switch (node.Type)
        {
            case MacroNodeType.KeyDown:
                var isModifierKey = InputMessageSender.IsModifierKey(node.VirtualKey);
                if (useTextInputMode && !isModifierKey && heldModifierKeys.Count == 0 &&
                    InputMessageSender.TrySendCharacter(hwnd, node.VirtualKey))
                    break;

                InputMessageSender.SendKeyDown(hwnd, node.VirtualKey, !isModifierKey && heldModifierKeys.Count == 0);
                if (isModifierKey)
                    heldModifierKeys.Add(node.VirtualKey);
                break;

            case MacroNodeType.KeyUp:
                if (useTextInputMode && !InputMessageSender.IsModifierKey(node.VirtualKey) && heldModifierKeys.Count == 0)
                    break;

                InputMessageSender.SendKeyUp(hwnd, node.VirtualKey);
                if (InputMessageSender.IsModifierKey(node.VirtualKey))
                    heldModifierKeys.Remove(node.VirtualKey);
                break;

            case MacroNodeType.Delay:
                await DelayWithPause(Math.Max(node.DelayMs, minimumDelayMs), token);
                break;

            case MacroNodeType.RandomDelay:
                var min = Math.Min(node.RandomDelayMinMs, node.RandomDelayMaxMs);
                var max = Math.Max(node.RandomDelayMinMs, node.RandomDelayMaxMs);
                await DelayWithPause(Math.Max(Random.Shared.Next(min, max + 1), minimumDelayMs), token);
                break;

            case MacroNodeType.Text:
                InputMessageSender.SendText(hwnd, node.Text);
                break;

            case MacroNodeType.MouseClick:
                InputMessageSender.SendForegroundMouseClick();
                break;

            case MacroNodeType.MouseDown:
                InputMessageSender.SendForegroundMouseDown(node.MouseButton);
                break;

            case MacroNodeType.MouseUp:
                InputMessageSender.SendForegroundMouseUp(node.MouseButton);
                break;

            case MacroNodeType.CursorMove:
                InputMessageSender.MoveCursorToClientPoint(hwnd, node.MouseX, node.MouseY);
                break;

            case MacroNodeType.BackgroundMouseDown:
                InputMessageSender.SendMouseDown(hwnd, node.MouseX, node.MouseY);
                break;

            case MacroNodeType.BackgroundMouseUp:
                InputMessageSender.SendMouseUp(hwnd, node.MouseX, node.MouseY);
                break;

            case MacroNodeType.BackgroundMouseClick:
                InputMessageSender.SendMouseClick(hwnd, node.MouseX, node.MouseY);
                break;
        }
    }

    private async Task DelayWithPause(int milliseconds, CancellationToken token)
    {
        var remaining = Math.Max(0, milliseconds);

        while (remaining > 0)
        {
            token.ThrowIfCancellationRequested();
            await WaitIfPaused(token);

            var slice = Math.Min(remaining, 50);
            await Task.Delay(slice, token);
            remaining -= slice;
        }
    }

    private async Task WaitIfPaused(CancellationToken token)
    {
        while (_pauseGate != null)
        {
            token.ThrowIfCancellationRequested();
            await _pauseGate.Task.WaitAsync(token);
        }
    }
}
