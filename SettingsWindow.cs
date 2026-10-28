using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SSSPen;

public sealed class SettingsWindow : Window
{
    private static readonly string[] HotkeyRows =
    {
        "ToggleDraw", "ToggleToolbar", "ScreenshotFull", "ScreenshotRegion",
        "Line", "Rect", "Ellipse", "Arrow", "Eraser", "Undo", "Clear",
        "Color1", "Color2", "Color3", "Color4", "Color5", "Color6",
        "OpenSettings",
    };

    private readonly AppSettings _settings;
    private readonly Action _onSaved;
    private readonly Dictionary<string, TextBox> _boxes = new();
    private readonly List<string> _palette;
    private readonly List<Button> _paletteButtons = new();
    private readonly Slider _thickness;
    private readonly Slider _toolbarScale;
    private readonly Slider _delay;
    private readonly CheckBox _startup;
    private readonly ComboBox _lang;
    private readonly TextBox _fullFolder;
    private readonly TextBox _regionFolder;

    public SettingsWindow(AppSettings settings, Action onSaved)
    {
        _settings = settings;
        _onSaved = onSaved;
        _palette = new List<string>(settings.Palette);

        Title = Loc.T("s.title");
        Width = 500;
        MaxHeight = 780;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new StackPanel { Margin = new Thickness(16) };

        // --- горячие клавиши ---
        root.Children.Add(Header(Loc.T("s.hotkeys")));
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;
        foreach (string key in HotkeyRows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tb1 = new TextBlock { Text = Loc.T("hk." + key), Margin = new Thickness(0, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(tb1, row);
            Grid.SetColumn(tb1, 0);
            grid.Children.Add(tb1);

            settings.Hotkeys.TryGetValue(key, out string? combo);
            var box = new TextBox { Text = combo ?? "", IsReadOnly = true, Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(4) };
            box.PreviewKeyDown += CaptureKey;
            _boxes[key] = box;
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(box);
            row++;
        }
        root.Children.Add(grid);

        // --- папки скриншотов ---
        root.Children.Add(Header(Loc.T("s.folders")));
        _fullFolder = FolderRow(root, Loc.T("s.fullFolder"), settings.FullFolder);
        _regionFolder = FolderRow(root, Loc.T("s.regionFolder"), settings.RegionFolder);

        // --- палитра ---
        var palHeader = Header(Loc.T("s.colors"));
        palHeader.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(palHeader);
        var wrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 12) };
        for (int i = 0; i < _palette.Count; i++)
        {
            int idx = i;
            var btn = new Button { Width = 56, Height = 32, Margin = new Thickness(0, 0, 8, 0), Content = (i + 1).ToString() };
            btn.Click += (_, _) => PickColor(idx);
            _paletteButtons.Add(btn);
            wrap.Children.Add(btn);
            PaintButton(idx);
        }
        root.Children.Add(wrap);

        // --- толщина ---
        root.Children.Add(Header(Loc.T("s.thickness")));
        _thickness = new Slider
        {
            Minimum = 1, Maximum = 30, Value = settings.Thickness,
            TickFrequency = 1, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 6, 0, 12),
        };
        root.Children.Add(_thickness);

        // --- размер панели ---
        root.Children.Add(Header(Loc.T("s.scale")));
        _toolbarScale = new Slider
        {
            Minimum = 0.6, Maximum = 2.0, Value = settings.ToolbarScale,
            TickFrequency = 0.05, IsSnapToTickEnabled = true,
            AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft,
            AutoToolTipPrecision = 2,
            Margin = new Thickness(0, 6, 0, 12),
        };
        root.Children.Add(_toolbarScale);

        // --- задержка скриншота ---
        root.Children.Add(Header(Loc.T("s.delay")));
        _delay = new Slider
        {
            Minimum = 0, Maximum = 500, Value = settings.ScreenshotDelayMs,
            TickFrequency = 10, IsSnapToTickEnabled = true,
            AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft,
            AutoToolTipPrecision = 0,
            Margin = new Thickness(0, 6, 0, 12),
        };
        root.Children.Add(_delay);

        // --- запуск с Windows ---
        _startup = new CheckBox
        {
            Content = Loc.T("s.startup"),
            IsChecked = settings.RunAtStartup,
            Margin = new Thickness(0, 0, 0, 12),
        };
        root.Children.Add(_startup);

        // --- язык ---
        root.Children.Add(Header(Loc.T("s.language")));
        _lang = new ComboBox { Margin = new Thickness(0, 6, 0, 14) };
        _lang.Items.Add("English");
        _lang.Items.Add("Русский");
        _lang.SelectedIndex = Loc.Lang == "ru" ? 1 : 0;
        root.Children.Add(_lang);

        // --- кнопки ---
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = Loc.T("s.save"), Width = 100, Padding = new Thickness(6), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = Loc.T("s.cancel"), Width = 100, Padding = new Thickness(6), IsCancel = true };
        save.Click += (_, _) => Save();
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    private static TextBlock Header(string text) =>
        new() { Text = text, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };

    private static TextBox FolderRow(StackPanel parent, string label, string value)
    {
        parent.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 2) });

        var dock = new DockPanel();
        var btn = new Button { Content = Loc.T("s.browse"), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(btn, Dock.Right);
        var tb = new TextBox { Text = value, Padding = new Thickness(4) };
        btn.Click += (_, _) =>
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = tb.Text };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                tb.Text = d.SelectedPath;
        };
        dock.Children.Add(btn);
        dock.Children.Add(tb);
        parent.Children.Add(dock);
        return tb;
    }

    private void CaptureKey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var tb = (TextBox)sender;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape || key == Key.Delete || key == Key.Back)
        {
            tb.Text = "";
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.None or Key.DeadCharProcessed or Key.ImeProcessed)
            return;

        var mods = Keyboard.Modifiers;
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        tb.Text = string.Join("+", parts);
    }

    private void PaintButton(int idx)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(_palette[idx]); }
        catch { c = Colors.Gray; }

        var btn = _paletteButtons[idx];
        btn.Background = new SolidColorBrush(c);
        double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        btn.Foreground = lum > 140 ? Brushes.Black : Brushes.White;
    }

    private void PickColor(int idx)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(_palette[idx]); }
        catch { c = Colors.Red; }

        using var dlg = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B),
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var d = dlg.Color;
            _palette[idx] = $"#{d.R:X2}{d.G:X2}{d.B:X2}";
            PaintButton(idx);
        }
    }

    private void Save()
    {
        foreach (var kv in _boxes)
            _settings.Hotkeys[kv.Key] = kv.Value.Text.Trim();

        _settings.FullFolder = string.IsNullOrWhiteSpace(_fullFolder.Text)
            ? AppSettings.DefaultFullFolder() : _fullFolder.Text.Trim();
        _settings.RegionFolder = string.IsNullOrWhiteSpace(_regionFolder.Text)
            ? AppSettings.DefaultRegionFolder() : _regionFolder.Text.Trim();

        _settings.Palette = new List<string>(_palette);
        _settings.Thickness = _thickness.Value;
        _settings.ToolbarScale = Math.Round(_toolbarScale.Value, 2);
        _settings.ScreenshotDelayMs = (int)Math.Round(_delay.Value);
        _settings.RunAtStartup = _startup.IsChecked == true;
        _settings.Language = _lang.SelectedIndex == 1 ? "ru" : "en";
        _settings.Save();
        _onSaved();
        Close();
    }
}
