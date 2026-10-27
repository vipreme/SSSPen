using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SSSPen;

/// <summary>SVG-path данные иконок (формат тот же, что в svg: d="...").</summary>
public static class Icons
{
    // залитые
    public const string Pencil = "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1 1 0 0 0 0-1.41l-2.34-2.34a1 1 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z";
    public const string Cursor = "M5 3 L5 19 L9.5 14.8 L12.3 21 L14.8 19.9 L12.1 13.8 L18 13.8 Z";

    // контурные
    public const string Line = "M5 19 L19 5";
    public const string Rect = "M4 6 H20 V18 H4 Z";
    public const string Ellipse = "M3 12 A9 6 0 1 0 21 12 A9 6 0 1 0 3 12 Z";
    public const string Arrow = "M5 19 L19 5 M10 5 H19 V14";
    public const string Eraser = "M4.5 14.5 L13 6 L19 12 L11 20 H8 L4.5 16.5 Z M8 11 L14 17 M12 20 H21";
    public const string Undo = "M9 5 L4 10 L9 15 M4 10 H14 A6 6 0 0 1 14 22 H10";
    public const string Trash = "M5 7 H19 M10 7 V4 H14 V7 M7 7 L8 21 H16 L17 7";
    public const string ScreenFull = "M3 4 H21 V16 H3 Z M8 20 H16 M12 16 V20";
    public const string ScreenRegion = "M4 9 V4 H9 M15 4 H20 V9 M20 15 V20 H15 M9 20 H4 V15";
    public const string Settings = "M4 7 H20 M4 12 H20 M4 17 H20 M8 5 V9 M15 10 V14 M9 15 V19";
    public const string Close = "M6 6 L18 18 M18 6 L6 18";
    public const string Save = "M5 3 H16 L20 7 V21 H5 Z M8 3 V8 H15 V3 M8 21 V14 H17 V21";
    public const string Copy = "M9 9 H20 V20 H9 Z M5 15 V4 H15";

    public static FrameworkElement Make(string data, bool filled, double size = 20)
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xED));
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(data) };
        if (filled)
        {
            path.Fill = brush;
        }
        else
        {
            path.Stroke = brush;
            path.StrokeThickness = 2;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
        }

        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    /// <summary>Иконка в трей: чёрный карандаш с белой обводкой (чтобы было видно на тёмной панели).</summary>
    public static System.Drawing.Icon CreateTrayIcon()
    {
        const int size = 32;
        var geo = Geometry.Parse(Pencil);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 24.0, size / 24.0));
            dc.DrawGeometry(Brushes.Black, new System.Windows.Media.Pen(Brushes.White, 0.9), geo);
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        ms.Position = 0;
        using var bmp = new System.Drawing.Bitmap(ms);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }
}

/// <summary>Мини-кнопка с иконкой (без стандартного стиля Button).</summary>
public static class UiKit
{
    private static readonly Brush Hover = Frozen(Color.FromRgb(0x3A, 0x3C, 0x40));
    private static readonly Brush Active = Frozen(Color.FromRgb(0x2F, 0x6F, 0xEB));

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static Border Button(string data, bool filled, string tip, Action click, double iconSize = 20, double box = 36)
    {
        var b = new Border
        {
            Width = box,
            Height = box,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            ToolTip = tip,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 1, 0, 1),
            Child = Icons.Make(data, filled, iconSize),
        };
        b.MouseEnter += (_, _) => { if (b.Tag is not true) b.Background = Hover; };
        b.MouseLeave += (_, _) => b.Background = b.Tag is true ? Active : Brushes.Transparent;
        b.MouseLeftButtonUp += (_, _) => click();
        return b;
    }

    public static void SetActive(Border b, bool on)
    {
        b.Tag = on;
        b.Background = on ? Active : Brushes.Transparent;
    }
}
