using System.Collections.Generic;

namespace SSSPen;

/// <summary>Простейшая локализация: English (по умолчанию) и русский.</summary>
public static class Loc
{
    public static string Lang = "en";

    public static string T(string key)
    {
        if (Table.TryGetValue(key, out var v))
            return Lang == "ru" ? v.Ru : v.En;
        return key;
    }

    private static readonly Dictionary<string, (string En, string Ru)> Table = new()
    {
        // инструменты
        ["tool.pen"] = ("Pencil", "Карандаш"),
        ["tool.line"] = ("Line", "Линия"),
        ["tool.arrow"] = ("Arrow", "Стрелка"),
        ["tool.rect"] = ("Rectangle", "Прямоугольник"),
        ["tool.ellipse"] = ("Ellipse", "Эллипс"),
        ["tool.eraser"] = ("Eraser", "Ластик"),

        // панель
        ["tb.drag"] = ("Drag", "Перетащить"),
        ["tb.cursor"] = ("Cursor (mouse)", "Курсор (мышь)"),
        ["tb.thickness"] = ("Thickness", "Толщина"),
        ["tb.undo"] = ("Undo", "Отменить"),
        ["tb.clear"] = ("Clear all", "Очистить всё"),
        ["tb.shotFull"] = ("Full-screen screenshot", "Скриншот всего экрана"),
        ["tb.shotRegion"] = ("Region screenshot", "Скриншот области"),
        ["tb.settings"] = ("Settings", "Настройки"),
        ["tb.hide"] = ("Hide toolbar", "Скрыть панель"),

        // редактор области
        ["ed.undo"] = ("Undo (Ctrl+Z)", "Отменить (Ctrl+Z)"),
        ["ed.save"] = ("Save (Ctrl+S)", "Сохранить (Ctrl+S)"),
        ["ed.copy"] = ("Copy (Ctrl+C)", "Копировать (Ctrl+C)"),
        ["ed.close"] = ("Close (Esc)", "Закрыть (Esc)"),

        // уведомления и ошибки
        ["n.saved"] = ("Screenshot saved", "Скриншот сохранён"),
        ["n.copied"] = ("Copied", "Скопировано"),
        ["n.copiedText"] = ("Selection copied to clipboard", "Область скопирована в буфер обмена"),
        ["err.save"] = ("Could not save:", "Не удалось сохранить:"),
        ["err.shot"] = ("Could not take screenshot:", "Не удалось сделать скриншот:"),
        ["hk.fail"] = ("Could not register these hotkeys (taken by another app or invalid):",
                       "Не удалось занять эти сочетания (заняты другой программой или записаны неверно):"),
        ["hk.failEnd"] = ("Change them in Settings.", "Измени их в настройках."),

        // трей
        ["tray.toggle"] = ("Show / hide toolbar", "Показать / скрыть панель"),
        ["tray.full"] = ("Full-screen screenshot", "Скриншот всего экрана"),
        ["tray.region"] = ("Region screenshot", "Скриншот области"),
        ["tray.clear"] = ("Clear drawings", "Очистить рисунки"),
        ["tray.settings"] = ("Settings", "Настройки"),
        ["tray.exit"] = ("Exit", "Выход"),

        // настройки
        ["s.title"] = ("SSS Pen — Settings", "SSS Pen — настройки"),
        ["s.hotkeys"] = ("Hotkeys (click a field and press a combination; Del clears it)",
                         "Горячие клавиши (кликни в поле и нажми комбинацию; Del — очистить)"),
        ["s.folders"] = ("Where to save screenshots", "Куда сохранять скриншоты"),
        ["s.fullFolder"] = ("Full screen:", "Весь экран:"),
        ["s.regionFolder"] = ("Region (Lightshot-style):", "Область (как в Lightshot):"),
        ["s.browse"] = ("Browse…", "Обзор…"),
        ["s.colors"] = ("Colors (click a color to change it)", "Цвета (клик по цвету — изменить)"),
        ["s.thickness"] = ("Line thickness", "Толщина линии"),
        ["s.scale"] = ("Toolbar size (0.6 – 2.0)", "Размер панели (0.6 – 2.0)"),
        ["s.delay"] = ("Screenshot delay, ms (only while the toolbar is open; with the toolbar hidden screenshots are instant)",
                       "Задержка скриншота, мс (только при открытой панели; со скрытой панелью скриншот мгновенный)"),
        ["s.startup"] = ("Start with Windows", "Запускать вместе с Windows"),
        ["s.language"] = ("Language", "Язык"),
        ["s.save"] = ("Save", "Сохранить"),
        ["s.cancel"] = ("Cancel", "Отмена"),

        // названия биндов
        ["hk.ToggleDraw"] = ("Pen on/off", "Рисовать вкл/выкл (перо)"),
        ["hk.ToggleToolbar"] = ("Show / hide toolbar", "Показать/скрыть панель"),
        ["hk.ScreenshotFull"] = ("Full-screen screenshot", "Скриншот всего экрана"),
        ["hk.ScreenshotRegion"] = ("Region screenshot", "Скриншот области"),
        ["hk.Line"] = ("Quick line", "Быстрая линия"),
        ["hk.Rect"] = ("Quick rectangle", "Быстрый прямоугольник"),
        ["hk.Ellipse"] = ("Quick ellipse", "Быстрый эллипс"),
        ["hk.Arrow"] = ("Quick arrow", "Быстрая стрелка"),
        ["hk.Eraser"] = ("Eraser (on)", "Ластик (вкл)"),
        ["hk.Undo"] = ("Undo last", "Отменить последнее"),
        ["hk.Clear"] = ("Clear all", "Очистить всё"),
        ["hk.Color1"] = ("Color 1", "Цвет 1"),
        ["hk.Color2"] = ("Color 2", "Цвет 2"),
        ["hk.Color3"] = ("Color 3", "Цвет 3"),
        ["hk.Color4"] = ("Color 4", "Цвет 4"),
        ["hk.Color5"] = ("Color 5", "Цвет 5"),
        ["hk.Color6"] = ("Color 6", "Цвет 6"),
        ["hk.OpenSettings"] = ("Open settings", "Открыть настройки"),
    };
}
