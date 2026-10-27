using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace SSSPen;

public sealed class AppController
{
    private AppSettings _settings = null!;
    private OverlayWindow _overlay = null!;
    private ToolbarWindow _toolbar = null!;
    private HotkeyManager _hotkeys = null!;
    private System.Windows.Forms.NotifyIcon _tray = null!;
    private SettingsWindow? _settingsWindow;
    private RegionCaptureWindow? _region;

    public void Start()
    {
        _settings = AppSettings.Load();
        Loc.Lang = _settings.Language;
        if (_settings.RunAtStartup) Autostart.Apply(true);   // обновить путь к exe, если файл переместили

        _overlay = new OverlayWindow(_settings);
        _overlay.Show();

        _toolbar = new ToolbarWindow(
            _settings, _overlay,
            fullShot: () => _ = CaptureFullAsync(),
            regionShot: () => _ = CaptureRegionAsync(),
            openSettings: OpenSettings,
            hide: ToggleToolbar);
        _toolbar.Show();

        _overlay.StateChanged += () =>
        {
            _toolbar.Refresh();
            _toolbar.Raise();
        };

        _hotkeys = new HotkeyManager(_overlay);
        RegisterHotkeys(showErrors: true);

        CreateTray();
    }

    // ---------- хоткеи ----------

    private void RegisterHotkeys(bool showErrors)
    {
        _hotkeys.UnregisterAll();

        var actions = new Dictionary<string, Action>
        {
            ["ToggleDraw"] = _overlay.ToggleDraw,
            ["ToggleToolbar"] = ToggleToolbar,
            ["ScreenshotFull"] = () => _ = CaptureFullAsync(),
            ["ScreenshotRegion"] = () => _ = CaptureRegionAsync(),
            ["Line"] = () => _overlay.StartShape(Tool.Line),
            ["Rect"] = () => _overlay.StartShape(Tool.Rect),
            ["Ellipse"] = () => _overlay.StartShape(Tool.Ellipse),
            ["Arrow"] = () => _overlay.StartShape(Tool.Arrow),
            ["Eraser"] = () => _overlay.SelectTool(Tool.Eraser),
            ["Undo"] = _overlay.Undo,
            ["Clear"] = _overlay.Clear,
            ["OpenSettings"] = OpenSettings,
        };

        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            actions["Color" + (i + 1)] = () => SetColor(idx);
        }

        var failed = new List<string>();
        foreach (var kv in actions)
        {
            if (_settings.Hotkeys.TryGetValue(kv.Key, out string? combo) && !string.IsNullOrWhiteSpace(combo))
            {
                if (!_hotkeys.Register(combo, kv.Value))
                    failed.Add($"{kv.Key}: {combo}");
            }
        }

        if (showErrors && failed.Count > 0)
        {
            MessageBox.Show(
                Loc.T("hk.fail") + "\n\n" +
                string.Join("\n", failed) +
                "\n\n" + Loc.T("hk.failEnd"),
                "SSS Pen", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetColor(int idx)
    {
        if (idx < _settings.Palette.Count)
        {
            _settings.CurrentColor = _settings.Palette[idx];
            _settings.Save();
            _toolbar.Refresh();
        }
    }

    // ---------- панель / настройки ----------

    private void ToggleToolbar()
    {
        if (_toolbar.IsVisible)
        {
            _toolbar.Hide();
        }
        else
        {
            _toolbar.Show();
            _toolbar.Raise();
        }
    }

    private void OpenSettings()
    {
        _overlay.SetDrawMode(false);

        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, OnSettingsSaved);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsSaved()
    {
        Loc.Lang = _settings.Language;
        Autostart.Apply(_settings.RunAtStartup);

        var oldMenu = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildTrayMenu();
        oldMenu?.Dispose();

        RegisterHotkeys(showErrors: true);
        _toolbar.ApplyScale();
        _toolbar.Rebuild();
    }

    // ---------- скриншоты ----------

    private void Balloon(string title, string text) =>
        _tray.ShowBalloonTip(2500, title, text, System.Windows.Forms.ToolTipIcon.Info);

    private async Task CaptureFullAsync()
    {
        if (_region != null) return;

        bool tb = _toolbar.IsVisible;   // menuOn
        if (tb)
        {
            _toolbar.Hide();
            if (_settings.ScreenshotDelayMs > 0)
                await Task.Delay(_settings.ScreenshotDelayMs);   // дать панели исчезнуть с экрана
        }
        // панель скрыта -> ждать нечего, снимаем мгновенно

        try
        {
            using var bmp = ScreenCapture.CaptureCurrentScreen();
            string folder = string.IsNullOrWhiteSpace(_settings.FullFolder)
                ? AppSettings.DefaultFullFolder() : _settings.FullFolder;
            string path = ScreenCapture.SavePng(bmp, folder, "Screen");
            Balloon(Loc.T("n.saved"), path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.T("err.shot") + " " + ex.Message, "SSS Pen");
        }
        finally
        {
            if (tb)
            {
                _toolbar.Show();
                _toolbar.Raise();
            }
        }
    }

    private async Task CaptureRegionAsync()
    {
        if (_region != null) return;

        bool tb = _toolbar.IsVisible;   // menuOn
        if (tb)
        {
            _toolbar.Hide();
            if (_settings.ScreenshotDelayMs > 0)
                await Task.Delay(_settings.ScreenshotDelayMs);
        }

        System.Drawing.Bitmap bmp;
        try
        {
            bmp = ScreenCapture.CaptureVirtualScreen();
        }
        catch (Exception ex)
        {
            if (tb) { _toolbar.Show(); _toolbar.Raise(); }
            MessageBox.Show(Loc.T("err.shot") + " " + ex.Message, "SSS Pen");
            return;
        }

        _region = new RegionCaptureWindow(bmp, _settings, Balloon);
        _region.Closed += (_, _) =>
        {
            _region = null;
            if (tb)
            {
                _toolbar.Show();
                _toolbar.Raise();
            }
        };
        _region.Show();
    }

    // ---------- трей ----------

    private System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.T("tray.toggle"), null, (_, _) => ToggleToolbar());
        menu.Items.Add(Loc.T("tray.full"), null, (_, _) => _ = CaptureFullAsync());
        menu.Items.Add(Loc.T("tray.region"), null, (_, _) => _ = CaptureRegionAsync());
        menu.Items.Add(Loc.T("tray.clear"), null, (_, _) => _overlay.Clear());
        menu.Items.Add(Loc.T("tray.settings"), null, (_, _) => OpenSettings());
        menu.Items.Add(Loc.T("tray.exit"), null, (_, _) => Exit());
        return menu;
    }

    private void CreateTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = Icons.CreateTrayIcon(),
            Text = "SSS Pen",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };

        // левый клик по иконке — открыть/скрыть панель
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                ToggleToolbar();
        };
    }

    private void Exit()
    {
        _settings.Save();
        _tray.Visible = false;
        _tray.Dispose();
        _hotkeys.Dispose();
        Application.Current.Shutdown();
    }
}
