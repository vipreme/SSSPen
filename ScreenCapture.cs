using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace SSSPen;

internal static class ScreenCapture
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int x1, int y1, int rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

    private const int SRCCOPY = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;   // захватывать и прозрачные (layered) окна — наш оверлей с рисунками

    public static System.Drawing.Bitmap CaptureRect(int x, int y, int w, int h)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr mem = CreateCompatibleDC(screen);
        IntPtr hbm = CreateCompatibleBitmap(screen, w, h);
        IntPtr old = SelectObject(mem, hbm);
        try
        {
            if (!BitBlt(mem, 0, 0, w, h, screen, x, y, SRCCOPY | CAPTUREBLT))
                throw new InvalidOperationException("BitBlt не удался");

            SelectObject(mem, old);
            old = IntPtr.Zero;

            using var tmp = System.Drawing.Bitmap.FromHbitmap(hbm);
            var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.DrawImageUnscaled(tmp, 0, 0);
            return bmp;
        }
        finally
        {
            if (old != IntPtr.Zero) SelectObject(mem, old);
            DeleteObject(hbm);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>Все мониторы целиком.</summary>
    public static System.Drawing.Bitmap CaptureVirtualScreen() =>
        CaptureRect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));

    /// <summary>Монитор, на котором сейчас курсор.</summary>
    public static System.Drawing.Bitmap CaptureCurrentScreen()
    {
        var b = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds;
        return CaptureRect(b.X, b.Y, b.Width, b.Height);
    }

    public static BitmapSource ToBitmapSource(System.Drawing.Bitmap bmp, double dpiX, double dpiY)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var src = BitmapSource.Create(bmp.Width, bmp.Height, dpiX, dpiY,
                System.Windows.Media.PixelFormats.Bgr32, null,
                data.Scan0, data.Stride * data.Height, data.Stride);
            src.Freeze();
            return src;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    public static string UniquePath(string folder, string prefix)
    {
        Directory.CreateDirectory(folder);
        string name = $"{prefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        string path = System.IO.Path.Combine(folder, name + ".png");
        int i = 1;
        while (File.Exists(path))
            path = System.IO.Path.Combine(folder, $"{name}_{i++}.png");
        return path;
    }

    public static string SavePng(BitmapSource image, string folder, string prefix)
    {
        string path = UniquePath(folder, prefix);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(image));
        using var fs = new FileStream(path, FileMode.Create);
        enc.Save(fs);
        return path;
    }

    public static string SavePng(System.Drawing.Bitmap bmp, string folder, string prefix)
    {
        string path = UniquePath(folder, prefix);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }
}
