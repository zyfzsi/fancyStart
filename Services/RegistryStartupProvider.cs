#nullable enable

using System.IO;
using FancyStart.Models;
using Microsoft.Win32;

namespace FancyStart.Services;

public class RegistryStartupProvider : IStartupProvider
{
    private static readonly (RegistryKey Root, string Path, string Label)[] RunKeys =
    {
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU"),
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
    };

    public List<StartupItem> GetStartupItems()
    {
        var items = new List<StartupItem>();

        foreach (var (root, path, label) in RunKeys)
        {
            // Enabled items from the Run key
            try
            {
                using var key = root.OpenSubKey(path, writable: false);
                if (key is not null)
                {
                    foreach (var valueName in key.GetValueNames())
                    {
                        try
                        {
                            var rawValue = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                            if (rawValue is null) continue;

                            var (command, arguments) = ParseCommand(rawValue);

                            items.Add(new StartupItem
                            {
                                Name = valueName,
                                Command = command,
                                Arguments = arguments,
                                IsEnabled = true,
                                Source = StartupSourceType.Registry,
                                SourceDetail = $"{label}\\...\\Run",
                                RegistryKeyPath = $"{label}\\{path}",
                                RegistryValueName = valueName,
                                IconPath = command,
                            });
                        }
                        catch
                        {
                            // Skip individual values that fail to read
                        }
                    }
                }
            }
            catch
            {
                // Skip this key entirely if it can't be opened
            }

            // Disabled items from the AutorunsDisabled subkey
            var disabledPath = $@"{path}\AutorunsDisabled";
            try
            {
                using var disabledKey = root.OpenSubKey(disabledPath, writable: false);
                if (disabledKey is not null)
                {
                    foreach (var valueName in disabledKey.GetValueNames())
                    {
                        try
                        {
                            var rawValue = disabledKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                            if (rawValue is null) continue;

                            var (command, arguments) = ParseCommand(rawValue);

                            items.Add(new StartupItem
                            {
                                Name = valueName,
                                Command = command,
                                Arguments = arguments,
                                IsEnabled = false,
                                Source = StartupSourceType.Registry,
                                SourceDetail = $"{label}\\...\\Run (Disabled)",
                                RegistryKeyPath = $"{label}\\{path}",
                                RegistryValueName = valueName,
                                IconPath = command,
                            });
                        }
                        catch
                        {
                            // Skip individual values that fail to read
                        }
                    }
                }
            }
            catch
            {
                // Skip this disabled key if it can't be opened
            }
        }

        return items;
    }

    public void Disable(StartupItem item)
    {
        var (root, runPath) = ResolveRootAndPath(item);
        var disabledPath = $@"{runPath}\AutorunsDisabled";

        try
        {
            using var runKey = root.OpenSubKey(runPath, writable: true);
            var rawValue = runKey?.GetValue(item.RegistryValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            if (rawValue is null) return;

            // Create AutorunsDisabled subkey if it doesn't exist, then write the value there
            using var disabledKey = root.CreateSubKey(disabledPath, writable: true);
            disabledKey.SetValue(item.RegistryValueName, rawValue, RegistryValueKind.String);

            // Remove from Run key
            runKey!.DeleteValue(item.RegistryValueName, throwOnMissingValue: false);

            item.IsEnabled = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to disable registry startup item '{item.Name}'.", ex);
        }
    }

    public void Enable(StartupItem item)
    {
        var (root, runPath) = ResolveRootAndPath(item);
        var disabledPath = $@"{runPath}\AutorunsDisabled";

        try
        {
            using var disabledKey = root.OpenSubKey(disabledPath, writable: true);
            var rawValue = disabledKey?.GetValue(item.RegistryValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            if (rawValue is null) return;

            // Write back to Run key
            using var runKey = root.OpenSubKey(runPath, writable: true);
            runKey?.SetValue(item.RegistryValueName, rawValue, RegistryValueKind.String);

            // Remove from AutorunsDisabled
            disabledKey!.DeleteValue(item.RegistryValueName, throwOnMissingValue: false);

            item.IsEnabled = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to enable registry startup item '{item.Name}'.", ex);
        }
    }

    public void Delete(StartupItem item)
    {
        var (root, runPath) = ResolveRootAndPath(item);
        var disabledPath = $@"{runPath}\AutorunsDisabled";

        try
        {
            // Try deleting from Run key first
            using (var runKey = root.OpenSubKey(runPath, writable: true))
            {
                runKey?.DeleteValue(item.RegistryValueName, throwOnMissingValue: false);
            }

            // Also try deleting from AutorunsDisabled
            using (var disabledKey = root.OpenSubKey(disabledPath, writable: true))
            {
                disabledKey?.DeleteValue(item.RegistryValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete registry startup item '{item.Name}'.", ex);
        }
    }

    public void Add(string filePath)
    {
        var runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runPath, writable: true);
            if (key is null)
                throw new InvalidOperationException("Could not open HKCU Run key for writing.");

            var valueName = Path.GetFileNameWithoutExtension(filePath);
            key.SetValue(valueName, $"\"{filePath}\"", RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to add registry startup item for '{filePath}'.", ex);
        }
    }

    private static (RegistryKey Root, string Path) ResolveRootAndPath(StartupItem item)
    {
        var root = item.RegistryKeyPath.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase)
            ? Registry.LocalMachine
            : Registry.CurrentUser;

        // Strip the HKCU\ or HKLM\ prefix to get the actual registry path
        var idx = item.RegistryKeyPath.IndexOf('\\');
        var path = idx >= 0 ? item.RegistryKeyPath[(idx + 1)..] : item.RegistryKeyPath;

        return (root, path);
    }

    /// <summary>
    /// Splits a raw registry value like <c>"C:\path\app.exe" -arg1 --arg2</c>
    /// into the executable command and its arguments.
    /// </summary>
    internal static (string Command, string Arguments) ParseCommand(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return (string.Empty, string.Empty);

        rawValue = rawValue.Trim();

        if (rawValue.StartsWith('"'))
        {
            var closingQuote = rawValue.IndexOf('"', 1);
            if (closingQuote > 0)
            {
                var command = rawValue[1..closingQuote];
                var arguments = closingQuote + 1 < rawValue.Length
                    ? rawValue[(closingQuote + 1)..].TrimStart()
                    : string.Empty;
                return (command, arguments);
            }
        }

        // No quotes — split on first space
        var spaceIndex = rawValue.IndexOf(' ');
        if (spaceIndex > 0)
        {
            return (rawValue[..spaceIndex], rawValue[(spaceIndex + 1)..].TrimStart());
        }

        return (rawValue, string.Empty);
    }
}
