using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Input;
using KeyLine.Services.SystemActions;
using KeyLine.Services.Timeline;

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
                var didDelayThisLoop = await RunExecutionPassAsync(
                    targetHwnd,
                    executionSteps,
                    safeBaseDelayMs,
                    minimumDelayMs,
                    currentLoop,
                    loopCount,
                    heldModifierKeys,
                    useTextInputMode,
                    token);

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

        for (var i = 0; i < sourceSteps.Count; i++)
        {
            var step = sourceSteps[i];
            if (IsDelayNode(step))
                continue;

            result.Add(step);

            if (!TimelineBlockService.IsControlNode(step) &&
                HasLaterExecutableStep(sourceSteps, i + 1))
            {
                result.Add(new MacroNode
                {
                    Type = MacroNodeType.Delay,
                    DelayMs = standardDelayMs
                });
            }
        }

        return result;
    }

    private static bool HasLaterExecutableStep(IReadOnlyList<MacroNode> sourceSteps, int startIndex)
    {
        for (var i = startIndex; i < sourceSteps.Count; i++)
        {
            var step = sourceSteps[i];
            if (!IsDelayNode(step) && !TimelineBlockService.IsControlNode(step))
                return true;
        }

        return false;
    }

    private async Task<bool> RunExecutionPassAsync(
        nint targetHwnd,
        IReadOnlyList<MacroNode> executionSteps,
        int safeBaseDelayMs,
        int minimumDelayMs,
        int currentLoop,
        int loopCount,
        ISet<int> heldModifierKeys,
        bool useTextInputMode,
        CancellationToken token)
    {
        var didDelayThisPass = false;
        var repeatPairs = TimelineBlockService.BuildRepeatPairMap(executionSteps);
        var conditionPairs = TimelineBlockService.BuildConditionPairMap(executionSteps);
        var repeatStack = new Stack<RepeatContext>();

        for (var i = 0; i < executionSteps.Count; i++)
        {
            var step = executionSteps[i];
            token.ThrowIfCancellationRequested();
            await WaitIfPaused(token);

            if (step.Type == MacroNodeType.RepeatStart)
            {
                if (!repeatPairs.TryGetValue(i, out var endIndex))
                    continue;

                var repeatCount = Math.Max(0, step.RepeatCount);
                if (repeatCount == 0)
                {
                    i = endIndex;
                    continue;
                }

                repeatStack.Push(new RepeatContext(i + 1, endIndex, repeatCount, repeatCount));
                continue;
            }

            if (step.Type == MacroNodeType.RepeatEnd)
            {
                if (repeatStack.Count == 0 || repeatStack.Peek().EndIndex != i)
                    continue;

                var context = repeatStack.Pop();
                context.RemainingIterations--;

                if (context.RemainingIterations > 0)
                {
                    repeatStack.Push(context);
                    i = context.BodyStartIndex - 1;
                }

                continue;
            }

            if (step.Type == MacroNodeType.ConditionStart)
            {
                if (!conditionPairs.TryGetValue(i, out var endIndex))
                    continue;

                if (!EvaluateCondition(step, targetHwnd, currentLoop, loopCount, repeatStack))
                    i = endIndex;

                continue;
            }

            if (step.Type == MacroNodeType.ConditionEnd)
                continue;

            await ExecuteStep(targetHwnd, step, minimumDelayMs, heldModifierKeys, useTextInputMode, token);

            didDelayThisPass |= IsDelayNode(step);

            if (ShouldApplyBaseDelay(executionSteps, i) && safeBaseDelayMs > 0)
            {
                await DelayWithPause(safeBaseDelayMs, token);
                didDelayThisPass = true;
            }
        }

        return didDelayThisPass;
    }

    private static bool ShouldApplyBaseDelay(IReadOnlyList<MacroNode> executionSteps, int index)
    {
        var step = executionSteps[index];
        if (IsDelayNode(step) || TimelineBlockService.IsControlNode(step))
            return false;

        var nextIndex = GetNextNonControlNodeIndex(executionSteps, index + 1);
        return nextIndex >= executionSteps.Count || !IsDelayNode(executionSteps[nextIndex]);
    }

    private static int GetNextNonControlNodeIndex(IReadOnlyList<MacroNode> executionSteps, int startIndex)
    {
        for (var i = startIndex; i < executionSteps.Count; i++)
        {
            if (!TimelineBlockService.IsControlNode(executionSteps[i]))
                return i;
        }

        return executionSteps.Count;
    }

    private static bool IsDelayNode(MacroNode node) =>
        node.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay;

    private static bool EvaluateCondition(
        MacroNode node,
        nint targetHwnd,
        int currentLoop,
        int loopCount,
        Stack<RepeatContext> repeatStack)
    {
        return node.ConditionType switch
        {
            MacroConditionType.KeyState => IsInputCombinationDown(node),
            MacroConditionType.PixelColor => IsPixelMatch(node, targetHwnd),
            MacroConditionType.RandomChance => IsRandomChanceHit(node.ConditionChancePercent),
            MacroConditionType.LoopContext => IsLoopContextMatch(node, currentLoop, loopCount, repeatStack),
            _ => true
        };
    }

    private static bool IsInputCombinationDown(MacroNode node)
    {
        var keys = ConditionInputGesture.GetVirtualKeys(node);
        return keys.Length > 0 && keys.All(IsVirtualKeyDown);
    }

    private static bool IsVirtualKeyDown(int virtualKey)
    {
        if (virtualKey <= 0)
            return false;

        return (NativeMethods.GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }

    private static bool IsPixelMatch(MacroNode node, nint targetHwnd)
    {
        if (!ScreenPixelReader.TryReadClientPixel(
                targetHwnd,
                node.ConditionPixelX,
                node.ConditionPixelY,
                out var actualColor))
        {
            return false;
        }

        var expectedRed = Math.Clamp(node.ConditionPixelRed, 0, 255);
        var expectedGreen = Math.Clamp(node.ConditionPixelGreen, 0, 255);
        var expectedBlue = Math.Clamp(node.ConditionPixelBlue, 0, 255);
        var tolerance = Math.Clamp(node.ConditionPixelTolerance, 0, 255);

        return Math.Abs(actualColor.Red - expectedRed) <= tolerance &&
               Math.Abs(actualColor.Green - expectedGreen) <= tolerance &&
               Math.Abs(actualColor.Blue - expectedBlue) <= tolerance;
    }

    private static bool IsRandomChanceHit(int chancePercent)
    {
        var chance = Math.Clamp(chancePercent, 0, 100);
        if (chance <= 0)
            return false;

        if (chance >= 100)
            return true;

        return Random.Shared.Next(100) < chance;
    }

    private static bool IsLoopContextMatch(
        MacroNode node,
        int currentLoop,
        int loopCount,
        Stack<RepeatContext> repeatStack)
    {
        var loopNumber = currentLoop + 1;
        var interval = Math.Max(1, node.ConditionLoopInterval);

        return node.ConditionLoopMode switch
        {
            MacroConditionLoopMode.FirstLoop => currentLoop == 0,
            MacroConditionLoopMode.LastLoop => loopCount > 0 && currentLoop == loopCount - 1,
            MacroConditionLoopMode.EveryNLoops => loopNumber % interval == 0,
            MacroConditionLoopMode.FirstRepeat => repeatStack.Count > 0 && repeatStack.Peek().CurrentIteration == 1,
            MacroConditionLoopMode.LastRepeat => repeatStack.Count > 0 && repeatStack.Peek().RemainingIterations == 1,
            MacroConditionLoopMode.EveryNRepeats => repeatStack.Count > 0 && repeatStack.Peek().CurrentIteration % interval == 0,
            _ => false
        };
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

            case MacroNodeType.MouseScrollUp:
            case MacroNodeType.MouseScrollDown:
                InputMessageSender.SendForegroundMouseWheel(GetMouseWheelDelta(node));
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

            case MacroNodeType.SystemOpenLaunch:
                SystemLaunchService.TryOpen(node);
                break;

            case MacroNodeType.SystemVolumeControl:
                SystemVolumeService.TryExecute(node);
                break;
        }
    }

    private static int GetMouseWheelDelta(MacroNode node)
    {
        if (node.MouseWheelDelta != 0)
            return node.MouseWheelDelta;

        return node.Type == MacroNodeType.MouseScrollDown
            ? -NativeMethods.WHEEL_DELTA
            : NativeMethods.WHEEL_DELTA;
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

    private sealed class RepeatContext
    {
        public RepeatContext(int bodyStartIndex, int endIndex, int remainingIterations, int totalIterations)
        {
            BodyStartIndex = bodyStartIndex;
            EndIndex = endIndex;
            RemainingIterations = remainingIterations;
            TotalIterations = totalIterations;
        }

        public int BodyStartIndex { get; }
        public int EndIndex { get; }
        public int TotalIterations { get; }
        public int RemainingIterations { get; set; }
        public int CurrentIteration => TotalIterations - RemainingIterations + 1;
    }
}
