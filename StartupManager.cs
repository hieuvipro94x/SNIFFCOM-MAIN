using Microsoft.Win32;
using System.Diagnostics;

namespace SniffCom
{
    public static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SniffCom";

        public static bool SetEnabled(bool enabled, out string? error)
        {
            error = null;
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key == null)
                {
                    error = "Không mở được khóa khởi động của Windows.";
                    return false;
                }

                if (!enabled)
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                    return true;
                }

                string? executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                    executablePath = Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    error = "Không xác định được đường dẫn chương trình.";
                    return false;
                }

                key.SetValue(ValueName, $"\"{executablePath}\" --startup", RegistryValueKind.String);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message.Split('\r', '\n')[0];
                return false;
            }
        }
    }
}
