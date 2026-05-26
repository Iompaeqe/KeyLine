using MacroSpammer.Domain;
using MacroSpammer.Services.Input;

namespace MacroSpammer.Services.Macro;

public sealed class MacroRunner
{
    private const int InfiniteLoopMinimumDelayMs = 10;

    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _pauseGate;

    public bool IsRunning => _cts != null;
    public bool IsPaused => _pauseGate != null;

    public async Task StartAsync(
        nint targetHwnd,
        IReadOnlyList<MacroStep> sourceSteps,
        int loopCount,
        bool useStandardDelay,
        int standardDelayMs,
        Action? loopCompleted = null)
    {
        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        _pauseGate = null;
        var token = _cts.Token;

        var executionSteps = BuildExecutionSteps(sourceSteps, useStandardDelay, standardDelayMs);
        var minimumDelayMs = loopCount == 0 ? InfiniteLoopMinimumDelayMs : 0;

        try
        {
            var currentLoop = 0;

            while (!token.IsCancellationRequested)
            {
                var didDelayThisLoop = false;

                foreach (var step in executionSteps)
                {
                    token.ThrowIfCancellationRequested();
                    await WaitIfPaused(token);
                    await ExecuteStep(targetHwnd, step, minimumDelayMs, token);

                    didDelayThisLoop |= step.Type is MacroStepType.Delay or MacroStepType.RandomDelay;
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

    private static List<MacroStep> BuildExecutionSteps(
        IReadOnlyList<MacroStep> sourceSteps,
        bool useStandardDelay,
        int standardDelayMs)
    {
        if (!useStandardDelay)
            return sourceSteps.ToList();

        var result = new List<MacroStep>();

        foreach (var step in sourceSteps)
        {
            if (step.Type is MacroStepType.Delay or MacroStepType.RandomDelay)
                continue;

            result.Add(step);

            result.Add(new MacroStep
            {
                Type = MacroStepType.Delay,
                DelayMs = standardDelayMs
            });
        }

        return result;
    }

    private async Task ExecuteStep(nint hwnd, MacroStep step, int minimumDelayMs, CancellationToken token)
    {
        switch (step.Type)
        {
            case MacroStepType.KeyDown:
                InputMessageSender.SendKeyDown(hwnd, step.VirtualKey);
                break;

            case MacroStepType.KeyUp:
                InputMessageSender.SendKeyUp(hwnd, step.VirtualKey);
                break;

            case MacroStepType.Delay:
                await DelayWithPause(Math.Max(step.DelayMs, minimumDelayMs), token);
                break;

            case MacroStepType.RandomDelay:
                var min = Math.Min(step.RandomDelayMinMs, step.RandomDelayMaxMs);
                var max = Math.Max(step.RandomDelayMinMs, step.RandomDelayMaxMs);
                await DelayWithPause(Math.Max(Random.Shared.Next(min, max + 1), minimumDelayMs), token);
                break;

            case MacroStepType.Text:
                InputMessageSender.SendText(hwnd, step.Text);
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
