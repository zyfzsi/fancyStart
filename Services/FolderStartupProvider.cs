#nullable enable

using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using FancyStart.Models;

namespace FancyStart.Services;

public class FolderStartupProvider : IStartupProvider
{
    private static readonly string UserStartupFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static readonly string AllUsersStartupFolder =
        @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup";

    private static readonly string[] StartupExtensions = { ".lnk", ".exe", ".bat", ".cmd" };

    public List<StartupItem> GetStartupItems()
    {
        var items = new List<StartupItem>();

        CollectFromFolder(UserStartupFolder, "User Startup Folder", items);
        CollectFromFolder(AllUsersStartupFolder, "All Users Startup Folder", items);

        return items;
    }

    public void Disable(StartupItem item)
    {
        try
        {
            var path = item.FilePath;
            if (!File.Exists(path))
                throw new FileNotFoundException($"Startup file not found: {path}");

            var disabledPath = path + ".disabled";
            File.Move(path, disabledPath);

            item.FilePath = disabledPath;
            item.IsEnabled = false;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidOperationException($"Failed to disable startup folder item '{item.Name}'.", ex);
        }
    }

    public void Enable(StartupItem item)
    {
        try
        {
            var path = item.FilePath;
            if (!File.Exists(path))
                throw new FileNotFoundException($"Startup file not found: {path}");

            if (!path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                return;

            var enabledPath = path[..^".disabled".Length];
            File.Move(path, enabledPath);

            item.FilePath = enabledPath;
            item.IsEnabled = true;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidOperationException($"Failed to enable startup folder item '{item.Name}'.", ex);
        }
    }

    public void Delete(StartupItem item)
    {
        try
        {
            if (File.Exists(item.FilePath))
                File.Delete(item.FilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete startup folder item '{item.Name}'.", ex);
        }
    }

    public void Add(string filePath)
    {
        try
        {
            if (!Directory.Exists(UserStartupFolder))
                Directory.CreateDirectory(UserStartupFolder);

            var linkName = Path.GetFileNameWithoutExtension(filePath) + ".lnk";
            var linkPath = Path.Combine(UserStartupFolder, linkName);

            CreateShortcut(linkPath, filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add startup folder item for '{filePath}'.", ex);
        }
    }

    private static void CollectFromFolder(string folderPath, string sourceDetail, List<StartupItem> items)
    {
        if (!Directory.Exists(folderPath))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                try
                {
                    var fileName = Path.GetFileName(file);
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var isDisabled = false;

                    // Check for .disabled suffix
                    if (ext.Equals(".disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        isDisabled = true;
                        // Get the real extension before .disabled
                        var nameWithoutDisabled = file[..^".disabled".Length];
                        ext = Path.GetExtension(nameWithoutDisabled).ToLowerInvariant();
                    }

                    if (Array.IndexOf(StartupExtensions, ext) < 0)
                        continue;

                    var command = file;
                    var arguments = string.Empty;
                    var name = Path.GetFileNameWithoutExtension(
                        isDisabled ? file[..^".disabled".Length] : file);

                    // Resolve .lnk targets
                    if (ext == ".lnk")
                    {
                        var (target, args) = ResolveShortcut(file);
                        if (!string.IsNullOrEmpty(target))
                        {
                            command = target;
                            arguments = args;
                        }
                    }

                    items.Add(new StartupItem
                    {
                        Name = name,
                        Command = command,
                        Arguments = arguments,
                        IsEnabled = !isDisabled,
                        Source = StartupSourceType.StartupFolder,
                        SourceDetail = sourceDetail,
                        FilePath = file,
                        IconPath = command,
                    });
                }
                catch
                {
                    // Skip individual files that fail
                }
            }
        }
        catch
        {
            // Skip entire folder if enumeration fails
        }
    }

    private static (string Target, string Arguments) ResolveShortcut(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            var persistFile = (IPersistFile)link;
            persistFile.Load(lnkPath, 0); // STGM_READ = 0

            var targetBuffer = new StringBuilder(260);
            link.GetPath(targetBuffer, targetBuffer.Capacity, IntPtr.Zero, 0);

            var argsBuffer = new StringBuilder(1024);
            link.GetArguments(argsBuffer, argsBuffer.Capacity);

            return (targetBuffer.ToString(), argsBuffer.ToString());
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static void CreateShortcut(string linkPath, string targetPath)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetPath);
        link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);

        var persistFile = (IPersistFile)link;
        persistFile.Save(linkPath, true);
    }

    #region COM Interop

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch,
            IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch,
            out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);          // IPersist
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName,
            [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    #endregion
}
