using Microsoft.Win32;

namespace IpadScreen.Services;

public class AutoStartService
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "IpadScreen";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                var value = key?.GetValue(AppName)?.ToString();
                return value?.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) == true;
            }
            catch { return false; }
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
                if (key == null) return;
                if (value)
                    key.SetValue(AppName, Environment.ProcessPath ?? "");
                else
                    key.DeleteValue(AppName, false);
            }
            catch { }
        }
    }
}
