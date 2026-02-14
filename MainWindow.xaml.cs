using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using FancyStart.Services;
using FancyStart.ViewModels;

namespace FancyStart;

#nullable enable

public class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();
        DataContext = viewModel;
        viewModel.LoadCommand.Execute(null);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // WPF uses OLE drag-drop which is completely blocked by UIPI when elevated.
        // Fix: revoke OLE drop target, switch to Win32 WM_DROPFILES, and allow through UIPI.
        RevokeDragDrop(hwnd);
        ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);
        DragAcceptFiles(hwnd, true);

        // Hook WndProc to handle WM_DROPFILES
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)WM_DROPFILES)
        {
            HandleDropFiles(wParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void HandleDropFiles(IntPtr hDrop)
    {
        try
        {
            var count = DragQueryFile(hDrop, 0xFFFFFFFF, null!, 0);
            for (uint i = 0; i < count; i++)
            {
                var size = DragQueryFile(hDrop, i, null!, 0) + 1;
                var sb = new StringBuilder((int)size);
                DragQueryFile(hDrop, i, sb, size);

                var filePath = sb.ToString();
                if (IsValidFileType(filePath))
                {
                    // Resolve .lnk shortcut to its target exe
                    if (Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        var (target, _) = FolderStartupProvider.ResolveShortcut(filePath);
                        if (!string.IsNullOrEmpty(target))
                            filePath = target;
                    }

                    ViewModel.AddItemCommand.Execute(filePath);
                }
            }
        }
        finally
        {
            DragFinish(hDrop);
        }
    }

    private void ToggleButton_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: { } item })
        {
            ViewModel.ToggleItemCommand.Execute(item);
        }
    }

    private static bool IsValidFileType(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".exe" or ".lnk" or ".bat" or ".cmd";
    }

    #region P/Invoke

    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_COPYGLOBALDATA = 0x0049;
    private const uint MSGFLT_ALLOW = 1;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(
        IntPtr hwnd, uint message, uint action, IntPtr changeFilterStruct);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(
        IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    [DllImport("ole32.dll")]
    private static extern int RevokeDragDrop(IntPtr hwnd);

    #endregion
}

public class SourceTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value?.ToString() ?? string.Empty;
        return name switch
        {
            "Registry" => "注册表",
            "StartupFolder" => "启动文件夹",
            "TaskScheduler" => "计划任务",
            _ => name
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
