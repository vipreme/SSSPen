using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace SSSPen;

/// <summary>Вертикальная панель инструментов в стиле Epic Pen.</summary>
public sealed class ToolbarWindow : Window
{
    private readonly AppSettings _s;
    private readonly OverlayWindow _overlay;
    private readonly Action _fullShot;
    private readonly Action _regionShot;
    private readonly Action _openSettings;
    private readonly Action _hide;

    private readonly Border _root;
    private readonly ScaleTransform _scale = new(1, 1);
    private Border _cursorBtn = null!;
    private readonly Dictionary<Tool, Border> _toolBtns = new();
    private readonly List<(Border Sw, string Hex)> _swatches = new();

    public ToolbarWindow(AppSettings s, OverlayWindow overlay,
        Action fullShot, Action regionShot, Action openSettings, Action hide)
    {
        _s = s;
        _overlay = overlay;
        _fullShot = fullShot;
        _regionShot = regionShot;
        _openSettings = openSettings;
        _hide = hide;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;

        _root = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x20, 0x21, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x46, 0x4B)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5),
            LayoutTransform = _scale,
            Width = 52,                       // 40 (кнопки) + 10 (отступы) + 2 (рамка) — фиксируем, чтобы не растягивалась
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Content = _root;

        SourceInitialized += (_, _) =>
        {
            IntPtr h = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        };
        // первичная раскладка WPF иногда считает окно слишком широким;
        // один раз перестраиваем панель после первой отрисовки (то же, что делает смена размера в настройках)
        ContentRendered += OnFirstRendered;

        ApplyScale();
        Rebuild();
    }

    private void OnFirstRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnFirstRendered;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ApplyScale();
            Rebuild();
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;
            InvalidateMeasure();
            UpdateLayout();
            PlaceInitially();
        }), DispatcherPriority.Loaded);
    }

    public void ApplyScale()
    {
        _scale.ScaleX = _s.ToolbarScale;
        _scale.ScaleY = _s.ToolbarScale;
    }

    /// <summary>Поднять поверх оверлея.</summary>
    public void Raise()
    {
        if (!IsVisible) return;
        Topmost = false;
        Topmost = true;
    }

    public void Rebuild()
    {
        _toolBtns.Clear();
        _swatches.Clear();
        _root.Child = BuildPanel();
        Refresh();
    }

    public void Refresh()
    {
        UiKit.SetActive(_cursorBtn, !_overlay.IsDrawing);
        foreach (var kv in _toolBtns)
            UiKit.SetActive(kv.Value, _overlay.IsDrawing && _overlay.CurrentTool == kv.Key);

        foreach (var (sw, hex) in _swatches)
            sw.BorderBrush = string.Equals(hex, _s.CurrentColor, StringComparison.OrdinalIgnoreCase)
                ? Brushes.White : Brushes.Transparent;
    }

    private UIElement BuildPanel()
    {
        var stack = new StackPanel { Width = 40 };

        // ручка для перетаскивания
        var grip = new Border
        {
            Height = 16,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
            ToolTip = "Перетащить",
            Child = new System.Windows.Shapes.Rectangle
            {
                Width = 22, Height = 3, RadiusX = 1.5, RadiusY = 1.5,
                Fill = new SolidColorBrush(Color.FromRgb(0x80, 0x84, 0x8A)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        grip.MouseLeftButtonDown += (_, _) =>
        {
            try { DragMove(); } catch { }
            _s.ToolbarLeft = Left;
            _s.ToolbarTop = Top;
            _s.Save();
        };
        stack.Children.Add(grip);

        // инструменты (постоянные)
        _cursorBtn = UiKit.Button(Icons.Cursor, true, "Курсор (мышь)", () => _overlay.SetDrawMode(false));
        stack.Children.Add(_cursorBtn);
        AddTool(stack, Tool.Pen, Icons.Pencil, true, "Карандаш");
        AddTool(stack, Tool.Line, Icons.Line, false, "Линия");
        AddTool(stack, Tool.Arrow, Icons.Arrow, false, "Стрелка");
        AddTool(stack, Tool.Rect, Icons.Rect, false, "Прямоугольник");
        AddTool(stack, Tool.Ellipse, Icons.Ellipse, false, "Эллипс");
        AddTool(stack, Tool.Eraser, Icons.Eraser, false, "Ластик");
        stack.Children.Add(Separator());

        // цвета
        var grid = new UniformGrid { Columns = 2, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (string hex in _s.Palette)
        {
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(hex); }
            catch { c = Colors.Gray; }

            var sw = new Border
            {
                Width = 18, Height = 18,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(2),
                Background = new SolidColorBrush(c),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = hex,
            };
            string h = hex;
            sw.MouseLeftButtonUp += (_, _) =>
            {
                _s.CurrentColor = h;
                _s.Save();
                Refresh();
            };
            _swatches.Add((sw, hex));
            grid.Children.Add(sw);
        }
        stack.Children.Add(grid);

        // толщина
        var slider = new Slider
        {
            Minimum = 1, Maximum = 30, Value = _s.Thickness,
            Width = 32, Margin = new Thickness(0, 6, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = "Толщина",
        };
        slider.ValueChanged += (_, e) => _s.Thickness = Math.Round(e.NewValue);
        slider.PreviewMouseLeftButtonUp += (_, _) => _s.Save();
        stack.Children.Add(slider);
        stack.Children.Add(Separator());

        // действия
        stack.Children.Add(UiKit.Button(Icons.Undo, false, "Отменить", _overlay.Undo));
        stack.Children.Add(UiKit.Button(Icons.Trash, false, "Очистить всё", _overlay.Clear));
        stack.Children.Add(Separator());

        // скриншоты
        stack.Children.Add(UiKit.Button(Icons.ScreenFull, false, "Скриншот всего экрана", _fullShot));
        stack.Children.Add(UiKit.Button(Icons.ScreenRegion, false, "Скриншот области", _regionShot));
        stack.Children.Add(Separator());

        stack.Children.Add(UiKit.Button(Icons.Settings, false, "Настройки", _openSettings));
        stack.Children.Add(UiKit.Button(Icons.Close, false, "Скрыть панель", _hide));

        return stack;
    }

    private void AddTool(Panel parent, Tool tool, string icon, bool filled, string tip)
    {
        var b = UiKit.Button(icon, filled, tip, () => _overlay.SelectTool(tool));
        _toolBtns[tool] = b;
        parent.Children.Add(b);
    }

    private static Border Separator() => new()
    {
        Height = 1,
        Margin = new Thickness(6, 5, 6, 5),
        Background = new SolidColorBrush(Color.FromRgb(0x44, 0x46, 0x4B)),
    };

    private void PlaceInitially()
    {
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;

        if (_s.ToolbarLeft is double l && _s.ToolbarTop is double t
            && l >= vl - 10 && l < vr - 20 && t >= vt - 10 && t < vb - 20)
        {
            Left = l;
            Top = t;
            return;
        }

        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth - 20;
        Top = wa.Top + (wa.Height - ActualHeight) / 2;
    }
}
