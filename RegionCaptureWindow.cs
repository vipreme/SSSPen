using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;

namespace SSSPen;

/// <summary>Выделение области как в Lightshot: рамка, ползунки по краям, размер, рисование на скриншоте.</summary>
public sealed class RegionCaptureWindow : Window
{
    private const double HandleHit = 9;

    private static readonly Cursor[] HandleCursors =
    {
        Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE,
        Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE,
    };

    private enum Drag { None, New, Move, Resize, Draw, Erase }

    private readonly AppSettings _s;
    private readonly Action<string, string> _notify;
    private readonly BitmapSource _bg;
    private readonly double _w, _h;            // размер окна в DIP
    private readonly double _scaleX, _scaleY;  // физические пиксели / DIP

    private readonly Canvas _root = new();
    private readonly WPath _dim = new() { Fill = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), IsHitTestVisible = false };
    private readonly Canvas _annot = new() { IsHitTestVisible = false };
    private readonly Rectangle _border = new()
    {
        Stroke = Brushes.White, StrokeThickness = 1, IsHitTestVisible = false, Visibility = Visibility.Hidden,
    };
    private readonly Rectangle[] _handles = new Rectangle[8];
    private readonly Border _sizeLabel;
    private readonly TextBlock _sizeText;
    private readonly Canvas _hit = new() { Background = Brushes.Transparent, Cursor = Cursors.Cross };
    private Border _toolbar = null!;
    private readonly Dictionary<Tool, Border> _toolBtns = new();
    private readonly List<(Border Sw, string Hex)> _swatches = new();

    private Rect _sel;
    private bool _hasSel;
    private Drag _drag = Drag.None;
    private Point _dragStart;
    private Rect _dragSel;
    private int _handle;
    private Tool? _tool;
    private Shape? _cur;
    private readonly List<Shape> _history = new();

    public RegionCaptureWindow(System.Drawing.Bitmap bmp, AppSettings s, Action<string, string> notify)
    {
        _s = s;
        _notify = notify;

        _w = SystemParameters.VirtualScreenWidth;
        _h = SystemParameters.VirtualScreenHeight;
        _scaleX = bmp.Width / _w;
        _scaleY = bmp.Height / _h;
        _bg = ScreenCapture.ToBitmapSource(bmp, 96 * _scaleX, 96 * _scaleY);
        bmp.Dispose();

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        Background = Brushes.Black;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = _w;
        Height = _h;

        _root.Width = _w;
        _root.Height = _h;
        _annot.Width = _w;
        _annot.Height = _h;
        _hit.Width = _w;
        _hit.Height = _h;
        Content = _root;

        _root.Children.Add(new Image
        {
            Source = _bg, Width = _w, Height = _h, Stretch = Stretch.Fill, IsHitTestVisible = false,
        });
        _root.Children.Add(_dim);
        _root.Children.Add(_annot);
        _root.Children.Add(_border);

        for (int i = 0; i < 8; i++)
        {
            _handles[i] = new Rectangle
            {
                Width = 7, Height = 7,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
                StrokeThickness = 1,
                IsHitTestVisible = false,
                Visibility = Visibility.Hidden,
            };
            _root.Children.Add(_handles[i]);
        }

        _sizeText = new TextBlock { Foreground = Brushes.White, FontSize = 12 };
        _sizeLabel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x20, 0x21, 0x24)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = _sizeText,
            IsHitTestVisible = false,
            Visibility = Visibility.Hidden,
        };
        _root.Children.Add(_sizeLabel);

        _root.Children.Add(_hit);
        BuildToolbar();
        _root.Children.Add(_toolbar);

        _hit.MouseLeftButtonDown += OnDown;
        _hit.MouseMove += OnMove;
        _hit.MouseLeftButtonUp += OnUp;

        PreviewKeyDown += OnKey;
        Loaded += (_, _) => { Activate(); Focus(); };

        UpdateChrome();
    }

    // ---------- панель инструментов редактора ----------

    private void BuildToolbar()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        void AddTool(Tool t, string icon, bool filled, string tip)
        {
            var b = UiKit.Button(icon, filled, tip, () => ToggleTool(t), 18, 30);
            _toolBtns[t] = b;
            panel.Children.Add(b);
        }

        AddTool(Tool.Pen, Icons.Pencil, true, "Карандаш");
        AddTool(Tool.Line, Icons.Line, false, "Линия");
        AddTool(Tool.Arrow, Icons.Arrow, false, "Стрелка");
        AddTool(Tool.Rect, Icons.Rect, false, "Прямоугольник");
        AddTool(Tool.Ellipse, Icons.Ellipse, false, "Эллипс");
        AddTool(Tool.Eraser, Icons.Eraser, false, "Ластик");
        panel.Children.Add(VSep());

        foreach (string hex in _s.Palette)
        {
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(hex); }
            catch { c = Colors.Gray; }

            var sw = new Border
            {
                Width = 18, Height = 18,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(c),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
            };
            string h = hex;
            sw.MouseLeftButtonUp += (_, _) =>
            {
                _s.CurrentColor = h;
                _s.Save();
                RefreshToolbar();
            };
            _swatches.Add((sw, hex));
            panel.Children.Add(sw);
        }
        panel.Children.Add(VSep());

        panel.Children.Add(UiKit.Button(Icons.Undo, false, "Отменить (Ctrl+Z)", Undo, 18, 30));
        panel.Children.Add(VSep());
        panel.Children.Add(UiKit.Button(Icons.Save, false, "Сохранить (Ctrl+S)", SaveAndClose, 18, 30));
        panel.Children.Add(UiKit.Button(Icons.Copy, false, "Копировать (Ctrl+C)", CopyAndClose, 18, 30));
        panel.Children.Add(UiKit.Button(Icons.Close, false, "Закрыть (Esc)", Close, 18, 30));

        _toolbar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x20, 0x21, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x46, 0x4B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Child = panel,
            Visibility = Visibility.Hidden,
        };
        RefreshToolbar();
    }

    private static Border VSep() => new()
    {
        Width = 1, Height = 20,
        Margin = new Thickness(5, 0, 5, 0),
        Background = new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x5E)),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void ToggleTool(Tool t)
    {
        _tool = _tool == t ? null : t;
        RefreshToolbar();
    }

    private void RefreshToolbar()
    {
        foreach (var kv in _toolBtns)
            UiKit.SetActive(kv.Value, _tool == kv.Key);

        foreach (var (sw, hex) in _swatches)
            sw.BorderBrush = string.Equals(hex, _s.CurrentColor, StringComparison.OrdinalIgnoreCase)
                ? Brushes.White : Brushes.Transparent;
    }

    private void PlaceToolbar()
    {
        if (!_hasSel) { _toolbar.Visibility = Visibility.Hidden; return; }

        _toolbar.Visibility = Visibility.Visible;
        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size sz = _toolbar.DesiredSize;

        double x = Clamp(_sel.Right - sz.Width, 0, Math.Max(0, _w - sz.Width));
        double y = _sel.Bottom + 8;
        if (y + sz.Height > _h) y = _sel.Top - sz.Height - 8;
        if (y < 0) y = Math.Max(0, _sel.Bottom - sz.Height - 8);

        Canvas.SetLeft(_toolbar, x);
        Canvas.SetTop(_toolbar, y);
    }

    // ---------- мышь ----------

    private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    private Point ClampToWindow(Point p) => new(Clamp(p.X, 0, _w), Clamp(p.Y, 0, _h));

    private Point HandlePoint(int i)
    {
        double l = _sel.Left, t = _sel.Top, r = _sel.Right, b = _sel.Bottom;
        double cx = (l + r) / 2, cy = (t + b) / 2;
        return i switch
        {
            0 => new Point(l, t),
            1 => new Point(cx, t),
            2 => new Point(r, t),
            3 => new Point(r, cy),
            4 => new Point(r, b),
            5 => new Point(cx, b),
            6 => new Point(l, b),
            _ => new Point(l, cy),
        };
    }

    private int HitHandle(Point p)
    {
        for (int i = 0; i < 8; i++)
        {
            Point hp = HandlePoint(i);
            if (Math.Abs(p.X - hp.X) <= HandleHit && Math.Abs(p.Y - hp.Y) <= HandleHit)
                return i;
        }
        return -1;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        Point p = ClampToWindow(e.GetPosition(_hit));
        _dragStart = p;
        _dragSel = _sel;

        if (_hasSel)
        {
            int h = HitHandle(p);
            if (h >= 0)
            {
                _drag = Drag.Resize;
                _handle = h;
                _toolbar.Visibility = Visibility.Hidden;
            }
            else if (_sel.Contains(p))
            {
                if (_tool == Tool.Eraser)
                {
                    _drag = Drag.Erase;
                    EraseAt(p);
                }
                else if (_tool != null)
                {
                    BeginDraw(p);
                }
                else
                {
                    _drag = Drag.Move;
                    _toolbar.Visibility = Visibility.Hidden;
                }
            }
            else
            {
                BeginNew();
            }
        }
        else
        {
            BeginNew();
        }

        _hit.CaptureMouse();
    }

    private void BeginNew()
    {
        _drag = Drag.New;
        _hasSel = false;
        ClearAnnotations();
        _toolbar.Visibility = Visibility.Hidden;
        UpdateChrome();
    }

    private void BeginDraw(Point p)
    {
        var brush = new SolidColorBrush(CurrentColor());
        brush.Freeze();
        _cur = ShapeFactory.Create(_tool!.Value, p, brush, _s.Thickness);
        _annot.Children.Add(_cur);
        ShapeFactory.Update(_cur, _tool.Value, p, p, _s.Thickness);
        _drag = Drag.Draw;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        Point p = ClampToWindow(e.GetPosition(_hit));

        switch (_drag)
        {
            case Drag.None:
                UpdateHoverCursor(p);
                return;

            case Drag.New:
                _sel = new Rect(_dragStart, p);
                _hasSel = _sel.Width > 1 && _sel.Height > 1;
                UpdateChrome();
                break;

            case Drag.Move:
            {
                double x = Clamp(_dragSel.X + (p.X - _dragStart.X), 0, _w - _dragSel.Width);
                double y = Clamp(_dragSel.Y + (p.Y - _dragStart.Y), 0, _h - _dragSel.Height);
                _sel = new Rect(x, y, _dragSel.Width, _dragSel.Height);
                UpdateChrome();
                break;
            }

            case Drag.Resize:
            {
                double l = _dragSel.Left, t = _dragSel.Top, r = _dragSel.Right, b = _dragSel.Bottom;
                switch (_handle)
                {
                    case 0: l = p.X; t = p.Y; break;
                    case 1: t = p.Y; break;
                    case 2: r = p.X; t = p.Y; break;
                    case 3: r = p.X; break;
                    case 4: r = p.X; b = p.Y; break;
                    case 5: b = p.Y; break;
                    case 6: l = p.X; b = p.Y; break;
                    default: l = p.X; break;
                }
                _sel = new Rect(new Point(l, t), new Point(r, b));
                UpdateChrome();
                break;
            }

            case Drag.Erase:
                EraseAt(p);
                break;

            case Drag.Draw:
            {
                if (_cur == null) break;
                var q = new Point(Clamp(p.X, _sel.Left, _sel.Right), Clamp(p.Y, _sel.Top, _sel.Bottom));
                ShapeFactory.Update(_cur, _tool!.Value, ClampToSel(_dragStart), q, _s.Thickness);
                break;
            }
        }
    }

    private Point ClampToSel(Point p) =>
        new(Clamp(p.X, _sel.Left, _sel.Right), Clamp(p.Y, _sel.Top, _sel.Bottom));

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        _hit.ReleaseMouseCapture();

        switch (_drag)
        {
            case Drag.New:
                if (_sel.Width < 4 || _sel.Height < 4)
                    _hasSel = false;
                else
                    _hasSel = true;
                UpdateChrome();
                PlaceToolbar();
                break;

            case Drag.Move:
            case Drag.Resize:
                PlaceToolbar();
                break;

            case Drag.Draw:
                if (_cur != null)
                {
                    _history.Add(_cur);
                    _cur = null;
                }
                break;
        }

        _drag = Drag.None;
    }

    private void UpdateHoverCursor(Point p)
    {
        if (!_hasSel) { _hit.Cursor = Cursors.Cross; return; }

        int h = HitHandle(p);
        if (h >= 0) _hit.Cursor = HandleCursors[h];
        else if (_sel.Contains(p)) _hit.Cursor = _tool != null ? Cursors.Cross : Cursors.SizeAll;
        else _hit.Cursor = Cursors.Cross;
    }

    // ---------- отрисовка рамки ----------

    private void UpdateChrome()
    {
        var full = new RectangleGeometry(new Rect(0, 0, _w, _h));
        if (!_hasSel)
        {
            _dim.Data = full;
            _border.Visibility = Visibility.Hidden;
            _sizeLabel.Visibility = Visibility.Hidden;
            foreach (var h in _handles) h.Visibility = Visibility.Hidden;
            return;
        }

        _dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(_sel));
        _annot.Clip = new RectangleGeometry(_sel);

        _border.Visibility = Visibility.Visible;
        Canvas.SetLeft(_border, _sel.Left);
        Canvas.SetTop(_border, _sel.Top);
        _border.Width = _sel.Width;
        _border.Height = _sel.Height;

        for (int i = 0; i < 8; i++)
        {
            Point hp = HandlePoint(i);
            _handles[i].Visibility = Visibility.Visible;
            Canvas.SetLeft(_handles[i], hp.X - 3.5);
            Canvas.SetTop(_handles[i], hp.Y - 3.5);
        }

        _sizeText.Text = $"{Math.Round(_sel.Width * _scaleX)} × {Math.Round(_sel.Height * _scaleY)}";
        _sizeLabel.Visibility = Visibility.Visible;
        double ly = _sel.Top - 26;
        if (ly < 0) ly = _sel.Top + 4;
        Canvas.SetLeft(_sizeLabel, _sel.Left);
        Canvas.SetTop(_sizeLabel, ly);
    }

    // ---------- рисование на скриншоте ----------

    private Color CurrentColor()
    {
        try { return (Color)ColorConverter.ConvertFromString(_s.CurrentColor); }
        catch { return Colors.Red; }
    }

    private void EraseAt(Point p)
    {
        for (int i = _history.Count - 1; i >= 0; i--)
        {
            if (ShapeFactory.Hits(_history[i], _annot, p))
            {
                _annot.Children.Remove(_history[i]);
                _history.RemoveAt(i);
            }
        }
    }

    private void Undo()
    {
        if (_history.Count == 0) return;
        var last = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        _annot.Children.Remove(last);
    }

    private void ClearAnnotations()
    {
        _annot.Children.Clear();
        _history.Clear();
    }

    // ---------- сохранить / копировать ----------

    private BitmapSource RenderResult()
    {
        int pxW = _bg.PixelWidth, pxH = _bg.PixelHeight;
        var rtb = new RenderTargetBitmap(pxW, pxH, 96.0 * pxW / _w, 96.0 * pxH / _h, PixelFormats.Pbgra32);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawImage(_bg, new Rect(0, 0, _w, _h));
        rtb.Render(dv);
        rtb.Render(_annot);

        int x = (int)Math.Round(_sel.X * _scaleX);
        int y = (int)Math.Round(_sel.Y * _scaleY);
        int w = Math.Max(1, (int)Math.Round(_sel.Width * _scaleX));
        int h = Math.Max(1, (int)Math.Round(_sel.Height * _scaleY));
        x = Math.Max(0, Math.Min(x, pxW - 1));
        y = Math.Max(0, Math.Min(y, pxH - 1));
        w = Math.Min(w, pxW - x);
        h = Math.Min(h, pxH - y);

        var crop = new CroppedBitmap(rtb, new Int32Rect(x, y, w, h));
        var conv = new FormatConvertedBitmap(crop, PixelFormats.Bgr32, null, 0);
        conv.Freeze();
        return conv;
    }

    private void SaveAndClose()
    {
        if (!_hasSel) return;
        try
        {
            string folder = string.IsNullOrWhiteSpace(_s.RegionFolder) ? AppSettings.DefaultRegionFolder() : _s.RegionFolder;
            string path = ScreenCapture.SavePng(RenderResult(), folder, "Region");
            _notify("Скриншот сохранён", path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось сохранить: " + ex.Message, "SSS Pen");
            return;
        }
        Close();
    }

    private void CopyAndClose()
    {
        if (!_hasSel) return;
        var bs = RenderResult();
        try { Clipboard.SetImage(bs); }
        catch
        {
            try
            {
                System.Threading.Thread.Sleep(100);
                Clipboard.SetImage(bs);
            }
            catch { }
        }
        _notify("Скопировано", "Область скопирована в буфер обмена");
        Close();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.S) { e.Handled = true; SaveAndClose(); }
            else if (e.Key == Key.C) { e.Handled = true; CopyAndClose(); }
            else if (e.Key == Key.Z) { e.Handled = true; Undo(); }
        }
    }
}
