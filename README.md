<p align="center">
  <img src="pencil.svg" width="96" alt="SSS Pen icon">
</p>

<h1 align="center">SSS Pen</h1>

<p align="center">
  Draw on top of any window and take Lightshot-style screenshots — one small tray app for Windows.
</p>

---

**SSS Pen** combines two tools that usually live in separate programs:

- **Screen annotation** (like Epic Pen): draw lines, arrows, shapes and freehand strokes right over your apps, presentations and video calls, then switch back to your mouse and keep working.
- **Screenshots** (like Lightshot): grab the whole screen instantly, or select a region, annotate it, and save or copy it.

Everything is driven by **global hotkeys** that you can rebind, plus a compact toolbar that you show or hide with one click on the tray icon.

## Features

**Drawing overlay**
- Draw over any window; clicks pass through to your apps when you are not drawing
- Pencil, line, arrow (with a filled, sharp head), rectangle, ellipse
- Eraser that removes whole strokes or shapes you touch
- Six color slots (pure red, green, blue, yellow, white, black by default), editable; your last color is remembered
- Adjustable line thickness
- Undo / clear all
- **Quick shapes:** press a hotkey, draw one shape, and SSS Pen switches back to the mouse automatically
- **Persistent tools** from the toolbar: pick one and keep drawing until you choose the cursor

**Screenshots**
- **Full screen:** one hotkey, saved straight to a folder (monitor under the cursor). When the toolbar is hidden the screenshot is instant; when it is open, a short configurable delay lets it disappear first
- **Region (Lightshot-style):** select an area, drag the edge handles to resize it, move it, see its size in pixels, draw on it (pencil, line, arrow, rectangle, ellipse, eraser), then
  - `Ctrl+S` — save to your "captures" folder
  - `Ctrl+C` — copy to clipboard
  - `Ctrl+Z` — undo, `Esc` — cancel
- Separate, configurable save folders for full-screen and region screenshots

**App**
- Toolbar: vertical strip, draggable, remembers its position, adjustable size, SVG icons
- Left-click the tray icon to show / hide the toolbar
- Rebindable hotkeys (click a field in Settings and press the new combination)
- Start with Windows (optional)
- English (default) and Russian interface
- Settings are stored in `%AppData%\SSSPen\settings.json`

## Default hotkeys

All of these can be changed in **Settings**.

| Action | Hotkey |
|---|---|
| Pen on / off | `Ctrl+Alt+E` |
| Show / hide toolbar | `Ctrl+Alt+Q` |
| Quick line | `Ctrl+Alt+W` |
| Quick rectangle | `Ctrl+Alt+R` |
| Quick ellipse | `Ctrl+Alt+D` |
| Quick arrow | `Ctrl+Alt+A` |
| Eraser (stays on until you switch tools) | `Ctrl+Alt+X` |
| Undo / Clear all | `Ctrl+Alt+Z` / `Ctrl+Alt+C` |
| Color 1 – 6 | `Ctrl+Alt+1` … `Ctrl+Alt+6` |
| Full-screen screenshot | `Ctrl+Alt+Home` |
| Region screenshot | `Ctrl+Home` |
| Open settings | `Ctrl+Alt+S` |

Right mouse button while drawing also undoes the last stroke.

If a hotkey is already taken by another program, SSS Pen tells you at startup which ones failed so you can change them.

## Build from source

Requirements: **Windows 10 / 11** and the **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**.

```bash
git clone <your-repo-url>
cd SSSPen
dotnet run
```

Build a single-file executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The result is in `bin/Release/net8.0-windows/win-x64/publish/SSSPen.exe` (needs the .NET 8 Desktop Runtime on the target machine). Use `--self-contained true` if you want an exe that runs without installing .NET.

## How it works

- **WPF + a bit of WinAPI**, no third-party packages.
- The overlay is a transparent, topmost, full-screen window. In mouse mode it gets `WS_EX_TRANSPARENT`, so clicks go through; in drawing mode that flag is removed. It never takes focus (`WS_EX_NOACTIVATE`), so the app you are annotating stays active.
- Hotkeys use `RegisterHotKey`; screenshots use `BitBlt` with `CAPTUREBLT`, so your drawings end up in the capture too.
- Icons are plain SVG path data rendered by WPF; the executable icon is `spen.ico`.

## Project layout

| File | What it does |
|---|---|
| `Program.cs`, `AppController.cs` | startup, tray, hotkey wiring, screenshot flow |
| `OverlayWindow.cs` | the transparent drawing surface, tools, eraser |
| `ToolbarWindow.cs` | the vertical toolbar |
| `RegionCaptureWindow.cs` | Lightshot-style region selection and annotation |
| `ShapeFactory.cs` | shape creation (shared by overlay and region editor) |
| `SettingsWindow.cs`, `AppSettings.cs` | settings UI and JSON storage |
| `HotkeyManager.cs`, `Native.cs`, `Autostart.cs`, `ScreenCapture.cs` | WinAPI helpers |
| `Loc.cs` | English / Russian strings |

## Known limitations

- Windows only.
- Multi-monitor setups with **different DPI scaling** per monitor can misalign the overlay and region selection.
- The eraser removes whole strokes/shapes, not individual pixels, and erasing cannot be undone.
- No cloud upload or "search similar images" like Lightshot has.
- Screenshots are saved as PNG.

## Contributing

Issues and pull requests are welcome. Ideas that would be nice to have: pixel eraser, highlighter/marker, text tool, blur/pixelate in region mode, screenshot upload.

## License

Add a license of your choice (for example MIT) before publishing.

---

## По-русски

**SSS Pen** — маленькая программа для Windows в трее, которая объединяет рисование поверх экрана (как Epic Pen) и скриншоты (как Lightshot).

- Рисование поверх любых окон: карандаш, линия, стрелка, прямоугольник, эллипс, ластик, 6 настраиваемых цветов, толщина, отмена.
- Быстрые фигуры по хоткею: нажал, нарисовал одну, и программа сама вернулась на мышь. Инструменты на панели — постоянные.
- Скриншот всего экрана (мгновенный, если панель скрыта) и скриншот области как в Lightshot: рамка с маркерами, размер в пикселях, рисование на выделении, `Ctrl+S` — сохранить, `Ctrl+C` — копировать.
- Панель-полоска как в Epic Pen: открывается и скрывается кликом по иконке в трее, размер настраивается.
- Все бинды меняются в настройках, есть автозапуск с Windows и переключение языка (English по умолчанию, Русский).

Сборка: поставь .NET 8 SDK и выполни `dotnet run` в папке проекта. Подробности и таблица хоткеев — выше.
