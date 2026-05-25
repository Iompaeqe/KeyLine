using MacroSpammer.Domain;
using MacroSpammer.Services.Input;

namespace MacroSpammer.Services.Macro;

public sealed class MacroRunner
{
    private const int InfiniteLoopMinimumDelayMs = 10;

    private CancellationTokenSource? _cts;

    public bool IsRunning => _cts != null;

    public async Task StartAsync(
        nint targetHwnd,
        IReadOnlyList<MacroStep> sourceSteps,
        int loopCount,
        int baseDelayMs,
        bool useStandardDelay,
        int standardDelayMs)
    {
        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var executionSteps = BuildExecutionSteps(sourceSteps, useStandardDelay, standardDelayMs);
        var minimumDelayMs = loopCount == 0 ? InfiniteLoopMinimumDelayMs : 0;
        var safeBaseDelayMs = Math.Max(Math.Max(0, baseDelayMs), minimumDelayMs);

        try
        {
            var currentLoop = 0;

            while (!token.IsCancellationRequested)
            {
                for (var i = 0; i < executionSteps.Count; i++)
                {
                    var step = executionSteps[i];
                    token.ThrowIfCancellationRequested();
                    await ExecuteStep(targetHwnd, step, minimumDelayMs, token);

                    if (ShouldApplyBaseDelay(executionSteps, i) && safeBaseDelayMs > 0)
                        await Task.Delay(safeBaseDelayMs, token);
                }

                currentLoop++;

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
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
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
            if (step.Type == MacroStepType.Delay)
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

    private static bool ShouldApplyBaseDelay(IReadOnlyList<MacroStep> executionSteps, int index)
    {
        var step = executionSteps[index];
        if (step.Type == MacroStepType.Delay)
            return false;

        var nextIndex = index + 1;
        return nextIndex >= executionSteps.Count ||
               executionSteps[nextIndex].Type != MacroStepType.Delay;
    }

    private static async Task ExecuteStep(nint hwnd, MacroStep step, int minimumDelayMs, CancellationToken token)
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
                await Task.Delay(Math.Max(step.DelayMs, minimumDelayMs), token);
                break;

            case MacroStepType.Text:
                InputMessageSender.SendText(hwnd, step.Text);
                break;

            case MacroStepType.MouseDown:
                InputMessageSender.SendMouseDown(hwnd, step.MouseX, step.MouseY);
                break;

            case MacroStepType.MouseUp:
                InputMessageSender.SendMouseUp(hwnd, step.MouseX, step.MouseY);
                break;

            case MacroStepType.MouseClick:
                InputMessageSender.SendMouseClick(hwnd, step.MouseX, step.MouseY);
                break;
        }
    }
}
