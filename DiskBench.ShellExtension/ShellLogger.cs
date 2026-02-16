namespace DiskBench.ShellExtension
{
    using System;
    using System.Globalization;
    using System.IO;
    using Microsoft.Win32;

    internal static class ShellLogger
    {
        private const string RegistryPath = @"SOFTWARE\DiskBench\ShellExtension";
        private static readonly object Gate = new();
        private static readonly bool DiagnosticsEnabled = ReadDiagnosticsEnabled();
        private static readonly string LogPath = ReadLogPath();

        public static void Log(string message)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            #pragma warning disable CA1031 // Logging must never crash Explorer.
            try
            {
                var timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                var line = $"{timestamp} [DiskBench.ShellExtension] {message}";

                lock (Gate)
                {
                    var directory = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Best-effort logging only.
            }
            #pragma warning restore CA1031
        }

        public static string? ReadExePath()
        {
            var value = ReadStringValue(RegistryHive.CurrentUser, "ExePath")
                ?? ReadStringValue(RegistryHive.LocalMachine, "ExePath");

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value;
        }

        private static bool ReadDiagnosticsEnabled()
        {
            var value = ReadDwordValue(RegistryHive.CurrentUser, "Diagnostics")
                ?? ReadDwordValue(RegistryHive.LocalMachine, "Diagnostics");

            if (value.HasValue)
            {
                return value.Value != 0;
            }

            return true;
        }

        private static string ReadLogPath()
        {
            var value = ReadStringValue(RegistryHive.CurrentUser, "LogPath")
                ?? ReadStringValue(RegistryHive.LocalMachine, "LogPath");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value!;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "DiskBench", "ShellExtension.log");
        }

        private static string? ReadStringValue(RegistryHive hive, string name)
        {
#pragma warning disable CA1031 // Registry access is best-effort and should not throw.
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(RegistryPath);
                return key?.GetValue(name) as string;
            }
            catch
            {
                return null;
            }
#pragma warning restore CA1031
        }

        private static int? ReadDwordValue(RegistryHive hive, string name)
        {
#pragma warning disable CA1031 // Registry access is best-effort and should not throw.
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(RegistryPath);
                if (key?.GetValue(name) is int value)
                {
                    return value;
                }
            }
            catch
            {
                return null;
            }
#pragma warning restore CA1031

            return null;
        }
    }
}
