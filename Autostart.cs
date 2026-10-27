using System;
using System.IO;
using Microsoft.Win32;

namespace SSSPen;

/// <summary>Автозапуск через HKCU\...\Run (права администратора не нужны).</summary>
internal static class Autostart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SSSPen";

    public static void Apply(bool enable)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, true)
                                    ?? Registry.CurrentUser.CreateSubKey(KeyPath);

            if (!enable)
            {
                key.DeleteValue(ValueName, false);
                return;
            }

            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            // при запуске через "dotnet run" путь указывает на dotnet.exe — такой не регистрируем
            if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
                return;

            key.SetValue(ValueName, "\"" + exe + "\"");
        }
        catch
        {
            // не критично
        }
    }
}
