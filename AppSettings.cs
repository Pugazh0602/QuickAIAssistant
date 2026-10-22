using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace QuickAIAssistant
{
    internal static class AppSettings
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickAIAssistant");
        private static readonly string KeyFile = Path.Combine(Folder, "groqkey.bin");
        private const string StartupName = "QuickAIAssistant";

        public static string GetApiKey()
        {
            try
            {
                if (!File.Exists(KeyFile)) return "";
                byte[] encrypted = File.ReadAllBytes(KeyFile);
                byte[] clear = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch { return ""; }
        }

        public static void SaveApiKey(string key)
        {
            Directory.CreateDirectory(Folder);
            byte[] clear = Encoding.UTF8.GetBytes(key ?? "");
            byte[] encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFile, encrypted);
        }

        public static bool StartupEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                        return k != null && k.GetValue(StartupName) != null;
                }
                catch { return false; }
            }
        }

        public static void SetStartup(bool enabled)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)
                ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (enabled)
                    k.SetValue(StartupName, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                else
                    k.DeleteValue(StartupName, false);
            }
        }
    }
}
