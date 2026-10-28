using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SSSPen;

/// <summary>Прозрачное окно на весь экран поверх всего.</summary>
public sealed class OverlayWindow : Window
{
    private readonly AppSettings _settings;
    // почти прозрачный фон (alpha=1), чтобы окно ловило мышь в режиме рисования
    private readonly Canvas _surface = new() { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
    private readonly List<UIElement> _history = new();

    private bool _drawMode;
    private bool _oneShot;
    private bool _erasing;
    private Tool _tool = Tool.Pen;
    private Point _start;
    private Shape? _current;

    public bool IsDrawing => _drawMode;
    public Tool CurrentTool => _tool;
    public event Action? DrawStateChanged;

    public OverlayWindow(AppSettings settings)
    {
        _settings = settings;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Content = _surface;

        _surface.MouseLeftButtonDown += OnDown;
        _surface.MouseMove += OnMove;
        _surface.MouseLeftButtonUp += OnUp;
        _surface.MouseRightButtonDown += (_, _) => { if (_drawMode) Undo(); };

        SourceInitialized += (_, _) => ApplyStyle();
    }

    // ---------- управление (хоткеи и панель) ----------

    /// <summary>Хоткей: перо вкл/выкл.</summary>
    public void ToggleDraw()
    {
        if (_drawMode && !_oneShot)
        {
            SetDrawMode(false);
        }
        else
        {
            _tool = Tool.Pen;
            _oneShot = false;
            SetDrawMode(true);
        }
    }

    /// <summary>Быстрая фигура: одна фигура и обратно на курсор.</summary>
    public void StartShape(Tool tool)
    {
        _tool = tool;
        _oneShot = true;
        SetDrawMode(true);
    }

    /// <summary>Постоянный инструмент (с панели): рисуем, пока сам не переключишься на мышь.</summary>
    public void SelectTool(Tool tool)
    {
        _tool = tool;
        _oneShot = false;
        SetDrawMode(true);
    }

    public void SetDrawMode(bool on)
    {
        _drawMode = on;
        if (!on)
        {
            _oneShot = false;
            _tool = Tool.Pen;
        }
        _surface.Cursor = on ? (_tool == Tool.Eraser ? Cursors.Hand : Cursors.Cross) : null;
        ApplyStyle();
        DrawStateChanged?.Invoke();
    }

    public void Undo()
    {
        if (_history.Count == 0) return;
        var last = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        _surface.Children.Remove(last);
    }

    public void Clear()
    {
        _surface.Children.Clear();
        _history.Clear();
    }

    // ---------- режим "сквозь окно" / "рисование" ----------

    private void ApplyStyle()
    {
        IntPtr h = new WindowInteropHelper(this).Handle;
        if (h == IntPtr.Zero) return;

        int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        ex = _drawMode ? (ex & ~Native.WS_EX_TRANSPARENT) : (ex | Native.WS_EX_TRANSPARENT);
        Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex);
    }

    // ---------- рисование ----------

    private Color CurrentColor()
    {
        try { return (Color)ColorConverter.ConvertFromString(_settings.CurrentColor); }
        catch { return Colors.Red; }
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (!_drawMode) return;

        if (_tool == Tool.Eraser)
        {
            _erasing = true;
            _surface.CaptureMouse();
            EraseAt(e.GetPosition(_surface));
            return;
        }

        _start = e.GetPosition(_surface);
        var brush = new SolidColorBrush(CurrentColor());
        brush.Freeze();

        _current = ShapeFactory.Create(_tool, _start, brush, _settings.Thickness);
        _surface.Children.Add(_current);
        _surface.CaptureMouse();
        ShapeFactory.Update(_current, _tool, _start, _start, _settings.Thickness);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_erasing)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                EraseAt(e.GetPosition(_surface));
            return;
        }

        if (_current == null || e.LeftButton != MouseButtonState.Pressed) return;
        ShapeFactory.Update(_current, _tool, _start, e.GetPosition(_surface), _settings.Thickness);
    }

    private void EraseAt(Point p)
    {
        for (int i = _history.Count - 1; i >= 0; i--)
        {
            if (_history[i] is Shape s && ShapeFactory.Hits(s, _surface, p))
            {
                _surface.Children.Remove(s);
                _history.RemoveAt(i);
            }
        }
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_erasing)
        {
            _erasing = false;
            _surface.ReleaseMouseCapture();
            return;
        }

        if (_current == null) return;

        ShapeFactory.Update(_current, _tool, _start, e.GetPosition(_surface), _settings.Thickness);
        _history.Add(_current);
        _current = null;
        _surface.ReleaseMouseCapture();

        if (_oneShot)
            SetDrawMode(false);   // быстрая фигура нарисована -> обратно на курсор
    }
}
