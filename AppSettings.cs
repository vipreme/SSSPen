using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SSSPen;

public sealed class AppSettings
{
    public Dictionary<string, string> Hotkeys { get; set; } = DefaultHotkeys();
    public List<string> Palette { get; set; } = DefaultPalette();
    public string CurrentColor { get; set; } = "#FF0000";
    public double Thickness { get; set; } = 4;

    // панель
    public double ToolbarScale { get; set; } = 1.0;

    // язык интерфейса: "en" (по умолчанию) или "ru"
    public string Language { get; set; } = "en";

    // автозапуск вместе с Windows
    public bool RunAtStartup { get; set; } = false;

    // задержка скриншота (мс), только когда панель открыта (ей нужно время скрыться); при скрытой панели задержка = 0
    public int ScreenshotDelayMs { get; set; } = 150;
    public double? ToolbarLeft { get; set; }
    public double? ToolbarTop { get; set; }

    // папки для скриншотов
    public string FullFolder { get; set; } = DefaultFullFolder();
    public string RegionFolder { get; set; } = DefaultRegionFolder();

    private static string PicturesRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SSSPen");

    /// <summary>Обычные скриншоты: Pictures\SSSPen</summary>
    public static string DefaultFullFolder() => PicturesRoot;

    /// <summary>Скриншоты областей: Pictures\SSSPen\captures</summary>
    public static string DefaultRegionFolder() => Path.Combine(PicturesRoot, "captures");

    // старые дефолтные пути из прошлых версий -> заменяем на новые
    private static string FixLegacy(string path, string newDefault, string legacySub)
    {
        if (string.IsNullOrWhiteSpace(path)) return newDefault;
        string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string p = path.Trim().TrimEnd('\\', '/');
        foreach (string oldRoot in new[] { "EpicPenClone", "SPen" })
        {
            string legacy = Path.Combine(pics, oldRoot, legacySub);
            if (string.Equals(p, legacy, StringComparison.OrdinalIgnoreCase))
                return newDefault;
        }
        return path;
    }

    public static Dictionary<string, string> DefaultHotkeys() => new()
    {
        ["ToggleDraw"] = "Ctrl+Alt+E",
        ["ToggleToolbar"] = "Ctrl+Alt+Q",
        ["ScreenshotFull"] = "Ctrl+Alt+Home",
        ["ScreenshotRegion"] = "Ctrl+Home",
        ["Line"] = "Ctrl+Alt+W",
        ["Rect"] = "Ctrl+Alt+R",
        ["Ellipse"] = "Ctrl+Alt+D",
        ["Arrow"] = "Ctrl+Alt+A",
        ["Eraser"] = "Ctrl+Alt+X",
        ["Undo"] = "Ctrl+Alt+Z",
        ["Clear"] = "Ctrl+Alt+C",
        ["Color1"] = "Ctrl+Alt+D1",
        ["Color2"] = "Ctrl+Alt+D2",
        ["Color3"] = "Ctrl+Alt+D3",
        ["Color4"] = "Ctrl+Alt+D4",
        ["Color5"] = "Ctrl+Alt+D5",
        ["Color6"] = "Ctrl+Alt+D6",
        ["OpenSettings"] = "Ctrl+Alt+S",
    };

    public static List<string> DefaultPalette() => new()
    {
        "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FFFFFF", "#000000"
    };

    private static string FilePath
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "SSSPen");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "settings.json");

            // переносим настройки из прошлых версий (папки SPen / EpicPenClone)
            if (!File.Exists(file))
            {
                foreach (string oldDir in new[] { "SPen", "EpicPenClone" })
                {
                    string old = Path.Combine(appData, oldDir, "settings.json");
                    if (File.Exists(old))
                    {
                        try { File.Copy(old, file); } catch { }
                        break;
                    }
                }
            }
            return file;
        }
    }

    // если в настройках лежит старая палитра по умолчанию — заменяем на новую (красный, зелёный, синий, жёлтый, белый, чёрный)
    private static void MigrateOldPalette(AppSettings s)
    {
        string[] oldDefault = { "#FF3B30", "#FFCC00", "#34C759", "#0A84FF", "#FFFFFF", "#000000" };
        string[] mapped = { "#FF0000", "#FFFF00", "#00FF00", "#0000FF", "#FFFFFF", "#000000" };

        if (s.Palette.Count != oldDefault.Length) return;
        for (int i = 0; i < oldDefault.Length; i++)
            if (!string.Equals(s.Palette[i], oldDefault[i], StringComparison.OrdinalIgnoreCase))
                return;   // палитру меняли руками — не трогаем

        for (int i = 0; i < oldDefault.Length; i++)
            if (string.Equals(s.CurrentColor, oldDefault[i], StringComparison.OrdinalIgnoreCase))
            {
                s.CurrentColor = mapped[i];
                break;
            }

        s.Palette = DefaultPalette();
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null)
                {
                    foreach (var kv in DefaultHotkeys())
                        if (!s.Hotkeys.ContainsKey(kv.Key))
                            s.Hotkeys[kv.Key] = kv.Value;

                    MigrateOldPalette(s);
                    if (s.Language != "en" && s.Language != "ru") s.Language = "en";

                    var def = DefaultPalette();
                    while (s.Palette.Count < 6)
                        s.Palette.Add(def[s.Palette.Count]);

                    s.FullFolder = FixLegacy(s.FullFolder, DefaultFullFolder(), "Full");
                    s.RegionFolder = FixLegacy(s.RegionFolder, DefaultRegionFolder(), "Region");
                    if (s.ToolbarScale < 0.5 || s.ToolbarScale > 3) s.ToolbarScale = 1.0;
                    return s;
                }
            }
        }
        catch
        {
            // битый файл настроек -> значения по умолчанию
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // не критично
        }
    }
}
