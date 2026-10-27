using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SSSPen;

public sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyManager(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
    }

    public bool Register(string combo, Action action)
    {
        if (!TryParse(combo, out uint mods, out uint vk))
            return false;

        int id = _nextId++;
        if (!Native.RegisterHotKey(_hwnd, id, mods | Native.MOD_NOREPEAT, vk))
            return false;

        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (int id in _actions.Keys)
            Native.UnregisterHotKey(_hwnd, id);
        _actions.Clear();
    }

    public static bool TryParse(string combo, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(combo))
            return false;

        foreach (string raw in combo.Split('+'))
        {
            string t = raw.Trim();
            switch (t.ToLowerInvariant())
            {
                case "ctrl": mods |= Native.MOD_CONTROL; break;
                case "alt": mods |= Native.MOD_ALT; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "win": mods |= Native.MOD_WIN; break;
                default:
                {
                    if (!Enum.TryParse<Key>(t, true, out var key))
                        return false;
                    int v = KeyInterop.VirtualKeyFromKey(key);
                    if (v == 0)
                        return false;
                    vk = (uint)v;
                    break;
                }
            }
        }
        return vk != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_actions.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }
}
