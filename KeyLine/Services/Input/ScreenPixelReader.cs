using KeyLine.Interop;

namespace KeyLine.Services.Input;

public readonly record struct ScreenPixelColor(int Red, int Green, int Blue);

public static class ScreenPixelReader
{
    public static bool TryReadClientPixel(nint hwnd, int clientX, int clientY, out ScreenPixelColor color)
    {
        var point = new NativeMethods.POINT
        {
            X = Math.Max(0, clientX),
            Y = Math.Max(0, clientY)
        };

        if (hwnd != 0 && !NativeMethods.ClientToScreen(hwnd, ref point))
        {
            color = default;
            return false;
        }

        return TryReadScreenPixel(point.X, point.Y, out color);
    }

    public static bool TryReadScreenPixel(int screenX, int screenY, out ScreenPixelColor color)
    {
        var hdc = NativeMethods.GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            color = default;
            return false;
        }

        try
        {
            var colorRef = NativeMethods.GetPixel(hdc, screenX, screenY);
            if (colorRef == 0xFFFFFFFF)
            {
                color = default;
                return false;
            }

            color = new ScreenPixelColor(
                (int)(colorRef & 0xFF),
                (int)((colorRef >> 8) & 0xFF),
                (int)((colorRef >> 16) & 0xFF));
            return true;
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, hdc);
        }
    }
}
