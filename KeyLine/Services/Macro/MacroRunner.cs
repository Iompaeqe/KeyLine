using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Input;
using KeyLine.Services.SystemActions;
using KeyLine.Services.Timeline;
using KeyLine.Services.Windows;

namespace KeyLine.Services.Macro;

public sealed class MacroRunner
{
    private const int InfiniteLoopMinimumDelayMs = 10;

    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _pauseGate;
    private Action<string>? _reportFailure;

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
        Action? loopCompleted = null,
        MacroRunContext? runContext = null,
        Action<string>? reportFailure = null)
    {
        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        _pauseGate = null;
        _reportFailure = reportFailure;
        var token = _cts.Token;
        var context = runContext ?? new MacroRunContext(targetHwnd);

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
                    context,
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
            _reportFailure = null;
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
                    DelayMs = standardDelayMs,
                    MinDelayMs = standardDelayMs,
                    MaxDelayMs = standardDelayMs,
                    RandomDelayMinMs = standardDelayMs,
                    RandomDelayMaxMs = standardDelayMs
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
        MacroRunContext context,
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

                var repeatContext = repeatStack.Pop();
                repeatContext.RemainingIterations--;

                if (repeatContext.RemainingIterations > 0)
                {
                    repeatStack.Push(repeatContext);
                    i = repeatContext.BodyStartIndex - 1;
                }

                continue;
            }

            if (step.Type == MacroNodeType.ConditionStart)
            {
                if (!conditionPairs.TryGetValue(i, out var endIndex))
                    continue;

                if (!EvaluateCondition(step, context, currentLoop, loopCount, repeatStack))
                    i = endIndex;

                continue;
            }

            if (step.Type == MacroNodeType.ConditionEnd)
                continue;

            await ExecuteStep(context, step, minimumDelayMs, heldModifierKeys, useTextInputMode, token);

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
        MacroRunContext context,
        int currentLoop,
        int loopCount,
        Stack<RepeatContext> repeatStack)
    {
        var result = node.ConditionType switch
        {
            MacroConditionType.KeyState => IsInputCombinationDown(node),
            MacroConditionType.PixelColor => IsPixelMatch(node, context.CurrentTargetWindowHandle),
            MacroConditionType.RandomChance => IsRandomChanceHit(node.ConditionChancePercent),
            MacroConditionType.LoopContext => IsLoopContextMatch(node, currentLoop, loopCount, repeatStack),
            MacroConditionType.TargetWindowFocused => IsWindowFocused(node, context),
            MacroConditionType.WindowExists => DoesWindowExist(node, context),
            MacroConditionType.MacroRunning => IsMacroRunning(node, context),
            MacroConditionType.TimePassed => context.HasTimePassed(
                node,
                Math.Max(1, node.ConditionTimePassedMs),
                DateTime.UtcNow),
            _ => true
        };

        return MacroConditionDefinitions.ShouldInvert(node)
            ? !result
            : result;
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

    private static bool IsWindowFocused(MacroNode node, MacroRunContext context)
    {
        var reference = GetFocusedConditionReference(node);
        var handle = ResolveWindowReference(reference, context, out _);
        if (!IsValidWindow(handle))
            return false;

        var foreground = NativeMethods.GetForegroundWindow();
        if (!IsValidWindow(foreground))
            return false;

        return GetRootWindow(handle) == GetRootWindow(foreground);
    }

    private static WindowReference GetFocusedConditionReference(MacroNode node)
    {
        var reference = node.GetEffectiveWindowReference();

        // Legacy nodes had no window reference and always meant the selected target window.
        if (reference.Type == WindowReferenceType.CustomTitle &&
            string.IsNullOrWhiteSpace(reference.CustomTitle))
        {
            reference.Type = WindowReferenceType.SelectedTarget;
        }

        return reference;
    }

    private static bool DoesWindowExist(MacroNode node, MacroRunContext context)
    {
        var handle = ResolveWindowReference(node, context, out _);
        if (!IsValidWindow(handle))
            return false;

        context.LastFoundWindowHandle = handle;
        return true;
    }

    private static bool IsMacroRunning(MacroNode node, MacroRunContext context)
    {
        var macroId = node.ConditionMacroId?.Trim() ?? "";
        return !string.IsNullOrWhiteSpace(macroId) &&
               context.RunHost?.IsMacroRunning(macroId, context) == true;
    }

    private async Task ExecuteStep(
        MacroRunContext context,
        MacroNode node,
        int minimumDelayMs,
        ISet<int> heldModifierKeys,
        bool useTextInputMode,
        CancellationToken token)
    {
        if (context.FollowForegroundWindow)
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (IsValidWindow(foreground))
                context.CurrentTargetWindowHandle = foreground;
        }

        var hwnd = context.CurrentTargetWindowHandle;

        switch (node.Type)
        {
            case MacroNodeType.KeyDown:
                if (TryExecuteToggleKeyMode(node))
                    break;

                var isModifierKey = InputMessageSender.IsModifierKey(node.VirtualKey);
                if (useTextInputMode && !isModifierKey && heldModifierKeys.Count == 0 &&
                    InputMessageSender.TrySendCharacter(hwnd, node.VirtualKey))
                    break;

                InputMessageSender.SendKeyDown(hwnd, node.VirtualKey, !isModifierKey && heldModifierKeys.Count == 0);
                if (isModifierKey)
                    heldModifierKeys.Add(node.VirtualKey);
                break;

            case MacroNodeType.KeyUp:
                if (TryExecuteToggleKeyMode(node))
                    break;

                if (useTextInputMode && !InputMessageSender.IsModifierKey(node.VirtualKey) && heldModifierKeys.Count == 0)
                    break;

                InputMessageSender.SendKeyUp(hwnd, node.VirtualKey);
                if (InputMessageSender.IsModifierKey(node.VirtualKey))
                    heldModifierKeys.Remove(node.VirtualKey);
                break;

            case MacroNodeType.Delay:
            case MacroNodeType.RandomDelay:
                var delayMs = GetPlaybackDelayMs(node);
                await DelayWithPause(Math.Max(delayMs, minimumDelayMs), token);
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
                SendMouseWheelScroll(node);
                break;

            case MacroNodeType.MouseScrollLeft:
            case MacroNodeType.MouseScrollRight:
                SendMouseHorizontalWheelScroll(node);
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
                CaptureLaunchResult(context, SystemLaunchService.TryOpen(node));
                break;

            case MacroNodeType.SystemVolumeControl:
                SystemVolumeService.TryExecute(node);
                break;

            case MacroNodeType.SystemWaitUntilWindowOpens:
                await WaitUntilWindowOpens(node, context, token);
                break;

            case MacroNodeType.SystemFocusWindow:
                FocusWindow(node, context, token);
                break;

            case MacroNodeType.SystemSelectTargetWindow:
                SetTargetWindow(node, context, token);
                break;

            case MacroNodeType.RunMacro:
                await RunMacroNode(node, context, token);
                break;
        }
    }

    private static bool TryExecuteToggleKeyMode(MacroNode node)
    {
        if (node.ToggleKeyMode == ToggleKeyMode.Normal ||
            !ToggleKeyService.IsToggleKey(node.VirtualKey))
        {
            return false;
        }

        ToggleKeyService.Execute(node.VirtualKey, node.ToggleKeyMode);
        return true;
    }

    private async Task RunMacroNode(MacroNode node, MacroRunContext context, CancellationToken token)
    {
        var macroId = node.RunMacroId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(macroId))
        {
            _reportFailure?.Invoke("Run Macro node has no macro selected.");
            return;
        }

        if (context.RunHost == null)
        {
            _reportFailure?.Invoke("Run Macro is not available in the current playback context.");
            return;
        }

        await context.RunHost.RunMacroAsync(macroId, context, _reportFailure, token);
    }

    private static void CaptureLaunchResult(MacroRunContext context, SystemLaunchResult result)
    {
        context.LastLaunchedProcessId = result.Succeeded ? result.ProcessId : null;
        context.LastLaunchedWindowHandle = result.Succeeded ? result.WindowHandle : 0;
    }

    private async Task WaitUntilWindowOpens(MacroNode node, MacroRunContext context, CancellationToken token)
    {
        var reference = node.GetEffectiveWindowReference();
        var pollIntervalMs = Math.Clamp(node.SystemWaitPollIntervalMs <= 0 ? 250 : node.SystemWaitPollIntervalMs, 50, 10_000);

        while (!token.IsCancellationRequested)
        {
            await WaitIfPaused(token);

            var handle = ResolveWindowReferenceForWait(reference, context, out var failureMessage, out var shouldKeepWaiting);
            if (IsValidWindow(handle))
            {
                context.LastFoundWindowHandle = handle;
                return;
            }

            if (!shouldKeepWaiting)
                FailPlayback(failureMessage, token);

            await DelayWithPause(pollIntervalMs, token);
        }
    }

    private void FocusWindow(MacroNode node, MacroRunContext context, CancellationToken token)
    {
        var handle = ResolveWindowReference(node, context, out var failureMessage);
        if (!IsValidWindow(handle))
            FailPlayback(failureMessage, token);

        NativeMethods.SetForegroundWindow(handle);
        context.LastFoundWindowHandle = handle;
        context.CurrentTargetWindowHandle = handle;
    }

    private void SetTargetWindow(MacroNode node, MacroRunContext context, CancellationToken token)
    {
        var handle = ResolveWindowReference(node, context, out var failureMessage);
        if (!IsValidWindow(handle))
            FailPlayback(failureMessage, token);

        context.LastFoundWindowHandle = handle;
        context.CurrentTargetWindowHandle = handle;
    }

    private nint ResolveWindowReferenceForWait(
        WindowReference reference,
        MacroRunContext context,
        out string failureMessage,
        out bool shouldKeepWaiting)
    {
        shouldKeepWaiting = false;
        failureMessage = GetWindowReferenceFailureMessage(reference);

        switch (reference.Type)
        {
            case WindowReferenceType.CustomTitle:
                var title = reference.CustomTitle?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(title))
                {
                    failureMessage = "No custom window title is configured.";
                    return 0;
                }

                shouldKeepWaiting = true;
                failureMessage = $"No window found matching title: {title}.";
                return FindWindowByTitleContains(title);

            case WindowReferenceType.LastLaunchedWindow:
                var launchedHandle = ResolveLastLaunchedWindow(context);
                if (IsValidWindow(launchedHandle))
                    return launchedHandle;

                if (context.LastLaunchedProcessId is > 0)
                {
                    shouldKeepWaiting = true;
                    return 0;
                }

                failureMessage = "Could not resolve Last Launched Window.";
                return 0;

            case WindowReferenceType.SelectedTarget:
            case WindowReferenceType.FocusedWindow:
            case WindowReferenceType.LastFoundWindow:
                return ResolveWindowReference(reference, context, out failureMessage);

            default:
                failureMessage = "Could not resolve window reference.";
                return 0;
        }
    }

    private static nint ResolveWindowReference(
        MacroNode node,
        MacroRunContext context,
        out string failureMessage)
    {
        return ResolveWindowReference(node.GetEffectiveWindowReference(), context, out failureMessage);
    }

    private static nint ResolveWindowReference(
        WindowReference reference,
        MacroRunContext context,
        out string failureMessage)
    {
        failureMessage = GetWindowReferenceFailureMessage(reference);

        switch (reference.Type)
        {
            case WindowReferenceType.SelectedTarget:
                if (context.HasValidSelectedTargetWindow)
                    return context.SelectedTargetWindowHandle;

                failureMessage = "Selected Target Window is no longer available.";
                return 0;

            case WindowReferenceType.FocusedWindow:
                var focused = NativeMethods.GetForegroundWindow();
                if (IsValidWindow(focused))
                    return focused;

                failureMessage = "Could not resolve Focused Window.";
                return 0;

            case WindowReferenceType.LastLaunchedWindow:
                var launched = ResolveLastLaunchedWindow(context);
                if (IsValidWindow(launched))
                    return launched;

                failureMessage = "Could not resolve Last Launched Window.";
                return 0;

            case WindowReferenceType.LastFoundWindow:
                if (IsValidWindow(context.LastFoundWindowHandle))
                    return context.LastFoundWindowHandle;

                failureMessage = "Could not resolve Last Found Window.";
                return 0;

            case WindowReferenceType.CustomTitle:
                var title = reference.CustomTitle?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(title))
                {
                    failureMessage = "No custom window title is configured.";
                    return 0;
                }

                var match = FindWindowByTitleContains(title);
                if (IsValidWindow(match))
                    return match;

                failureMessage = $"No window found matching title: {title}.";
                return 0;

            default:
                failureMessage = "Could not resolve window reference.";
                return 0;
        }
    }

    private static nint ResolveLastLaunchedWindow(MacroRunContext context)
    {
        if (IsValidWindow(context.LastLaunchedWindowHandle))
            return context.LastLaunchedWindowHandle;

        if (context.LastLaunchedProcessId is not > 0)
            return 0;

        var handle = WindowEnumerator.GetVisibleWindowsForProcess(context.LastLaunchedProcessId.Value)
            .FirstOrDefault()
            ?.Handle ?? 0;

        if (IsValidWindow(handle))
            context.LastLaunchedWindowHandle = handle;

        return handle;
    }

    private static nint GetRootWindow(nint window)
    {
        var root = NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT);
        return root == 0 ? window : root;
    }

    private static nint FindWindowByTitleContains(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return 0;

        return WindowEnumerator.GetVisibleWindows()
            .FirstOrDefault(window => window.Title.Contains(title.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.Handle ?? 0;
    }

    private static string GetWindowReferenceFailureMessage(WindowReference reference)
    {
        return reference.Type switch
        {
            WindowReferenceType.SelectedTarget => "Selected Target Window is no longer available.",
            WindowReferenceType.FocusedWindow => "Could not resolve Focused Window.",
            WindowReferenceType.LastLaunchedWindow => "Could not resolve Last Launched Window.",
            WindowReferenceType.LastFoundWindow => "Could not resolve Last Found Window.",
            WindowReferenceType.CustomTitle => string.IsNullOrWhiteSpace(reference.CustomTitle)
                ? "No custom window title is configured."
                : $"No window found matching title: {reference.CustomTitle.Trim()}.",
            _ => "Could not resolve window reference."
        };
    }

    private void FailPlayback(string message, CancellationToken token)
    {
        _reportFailure?.Invoke(message);
        _cts?.Cancel();
        throw new OperationCanceledException(message, token);
    }

    private static bool IsValidWindow(nint handle) =>
        handle != 0 && NativeMethods.IsWindow(handle);

    private static int GetPlaybackDelayMs(MacroNode node)
    {
        var (min, max) = node.GetEffectiveDelayRange();
        min = DelayFormatter.ClampMilliseconds(min);
        max = DelayFormatter.ClampMilliseconds(max);

        if (max < min)
            (min, max) = (max, min);

        return min == max
            ? min
            : Random.Shared.Next(min, max + 1);
    }

    private static int GetMouseWheelDelta(MacroNode node)
    {
        if (node.MouseWheelDelta != 0)
            return node.MouseWheelDelta;

        return node.Type == MacroNodeType.MouseScrollDown
            ? -NativeMethods.WHEEL_DELTA
            : NativeMethods.WHEEL_DELTA;
    }

    private static void SendMouseWheelScroll(MacroNode node)
    {
        var delta = Math.Sign(GetMouseWheelDelta(node)) * NativeMethods.WHEEL_DELTA;
        var amount = GetMouseScrollAmount(node);

        for (var i = 0; i < amount; i++)
            InputMessageSender.SendForegroundMouseWheel(delta);
    }

    private static void SendMouseHorizontalWheelScroll(MacroNode node)
    {
        var delta = node.Type == MacroNodeType.MouseScrollLeft
            ? -NativeMethods.WHEEL_DELTA
            : NativeMethods.WHEEL_DELTA;
        var amount = GetMouseScrollAmount(node);

        for (var i = 0; i < amount; i++)
            InputMessageSender.SendForegroundMouseHorizontalWheel(delta);
    }

    private static int GetMouseScrollAmount(MacroNode node)
    {
        if (node.MouseScrollAmount > 0)
            return Math.Clamp(node.MouseScrollAmount, 1, 100);

        return Math.Clamp(Math.Abs(node.MouseWheelDelta) / NativeMethods.WHEEL_DELTA, 1, 100);
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
