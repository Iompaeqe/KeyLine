using MacroSpammer.Domain;
using MacroSpammer.Services.Input;

namespace MacroSpammer.Services.Macro;

public sealed class MacroRunner
{
    private CancellationTokenSource? _cts;

    public bool IsRunning => _cts != null;

    public async Task StartAsync(
        nint targetHwnd,
        IReadOnlyList<MacroStep> sourceSteps,
        int loopCount,
        bool useStandardDelay,
        int standardDelayMs)
    {
        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var executionSteps = BuildExecutionSteps(sourceSteps, useStandardDelay, standardDelayMs);

        try
        {
            var currentLoop = 0;

            while (!token.IsCancellationRequested)
            {
                foreach (var step in executionSteps)
                {
                    token.ThrowIfCancellationRequested();
                    await ExecuteStep(targetHwnd, step, token);
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

    private static async Task ExecuteStep(nint hwnd, MacroStep step, CancellationToken token)
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
                await Task.Delay(step.DelayMs, token);
                break;

            case MacroStepType.Text:
                InputMessageSender.SendText(hwnd, step.Text);
                break;
        }
    }
}