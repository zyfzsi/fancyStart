#nullable enable

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FancyStart.Models;
using FancyStart.Services;

namespace FancyStart.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly StartupService _service = new();
    private int _totalCount;
    private int _enabledCount;
    private string _searchText = string.Empty;

    public ObservableCollection<StartupItem> StartupItems { get; } = new();

    public ICollectionView ItemsView { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ItemsView.Refresh();
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public int EnabledCount
    {
        get => _enabledCount;
        private set => SetProperty(ref _enabledCount, value);
    }

    public RelayCommand LoadCommand { get; }
    public RelayCommand ToggleItemCommand { get; }
    public RelayCommand DeleteItemCommand { get; }
    public RelayCommand AddItemCommand { get; }

    public MainViewModel()
    {
        LoadCommand = new RelayCommand(_ => Load());
        ToggleItemCommand = new RelayCommand(p => ToggleItem(p as StartupItem));
        DeleteItemCommand = new RelayCommand(p => DeleteItem(p as StartupItem));
        AddItemCommand = new RelayCommand(p => AddItem(p as string));

        ItemsView = CollectionViewSource.GetDefaultView(StartupItems);
        ItemsView.GroupDescriptions.Add(
            new PropertyGroupDescription("Source", new SourceTypeToStringConverter()));
        ItemsView.Filter = FilterItem;
    }

    private bool FilterItem(object obj)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (obj is not StartupItem item) return false;

        var query = _searchText;
        return item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Command.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.SourceDetail.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void Load()
    {
        StartupItems.Clear();

        foreach (var item in _service.GetAllStartupItems())
        {
            LoadIcon(item);
            StartupItems.Add(item);
        }

        UpdateCounts();
    }

    private void ToggleItem(StartupItem? item)
    {
        if (item is null) return;

        try
        {
            if (item.IsEnabled)
                _service.Enable(item);
            else
                _service.Disable(item);
        }
        catch
        {
            // Revert the UI toggle on failure
            item.IsEnabled = !item.IsEnabled;
        }

        UpdateCounts();
    }

    private void DeleteItem(StartupItem? item)
    {
        if (item is null) return;

        var result = MessageBox.Show(
            $"确定要删除启动项 \"{item.Name}\" 吗？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _service.Delete(item);
            StartupItems.Remove(item);
            UpdateCounts();
        }
        catch
        {
            MessageBox.Show($"删除 \"{item.Name}\" 失败。", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddItem(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            _service.Add(filePath);
            Load(); // Reload to pick up the new item
        }
        catch
        {
            MessageBox.Show($"添加 \"{Path.GetFileName(filePath)}\" 失败。", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateCounts()
    {
        TotalCount = StartupItems.Count;
        EnabledCount = StartupItems.Count(i => i.IsEnabled);
    }

    private static void LoadIcon(StartupItem item)
    {
        try
        {
            var path = item.IconPath;
            if (string.IsNullOrEmpty(path)) path = item.Command;
            if (string.IsNullOrEmpty(path)) return;

            path = path.Trim('"');
            path = Environment.ExpandEnvironmentVariables(path);

            if (!File.Exists(path)) return;

            // Use SHGetFileInfo to extract the icon via pure P/Invoke
            var shfi = new SHFILEINFO();
            var result = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi),
                SHGFI_ICON | SHGFI_LARGEICON);

            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero) return;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    shfi.hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                item.Icon = source;
            }
            finally
            {
                DestroyIcon(shfi.hIcon);
            }
        }
        catch
        {
            // Icon extraction is best-effort
        }
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
