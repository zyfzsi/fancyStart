# FancyStart

Windows 开机启动项管理工具，基于 WPF (.NET 8) 构建。

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows](https://img.shields.io/badge/platform-Windows-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)

## 功能

- **多源管理** — 统一管理注册表 (HKCU/HKLM Run)、启动文件夹、计划任务中的启动项
- **一键开关** — 拨动开关启用/禁用，注册表项使用 `AutorunsDisabled` 子键（兼容 Sysinternals Autoruns）
- **拖拽添加** — 拖入 `.exe` / `.lnk` / `.bat` / `.cmd` 文件即可添加为启动项
- **右键删除** — 右键菜单一键删除，带确认对话框
- **图标提取** — 自动提取并显示程序图标
- **暗色主题** — 现代扁平风格，按来源分组显示

## 截图

启动后自动以管理员权限运行，列出所有启动项：

```
┌─ FancyStart - 启动项管理 ──────────────────────┐
│  FancyStart                                      │
│  启动项管理工具                                    │
│──────────────────────────────────────────────────│
│  ▸ 注册表                                        │
│    [icon] SecurityHealth  C:\Windows\...  [●━━]  │
│    [icon] Discord         C:\Users\...   [━━○]  │
│  ▸ 启动文件夹                                     │
│    [icon] Startup App     C:\Program...  [●━━]  │
│  ▸ 计划任务                                       │
│    [icon] GoogleUpdate    C:\Program...  [●━━]  │
│──────────────────────────────────────────────────│
│  共 4 个启动项，已启用 3 个                         │
└──────────────────────────────────────────────────┘
```

## 下载

前往 [Releases](https://github.com/zyfzsi/fancyStart/releases) 下载最新版本。

`FancyStart-v1.0-win-x64.zip` 为自包含单文件，解压即用，无需安装 .NET 运行时。

## 从源码构建

```bash
# 需要 .NET 8 SDK
dotnet build
dotnet run

# 发布自包含单文件
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 项目结构

```
FancyStart/
├── Models/          # 数据模型 (StartupItem, StartupSourceType)
├── Services/        # 启动项提供者 (注册表/启动文件夹/计划任务)
├── ViewModels/      # MVVM (MainViewModel, RelayCommand)
├── Themes/          # 暗色主题样式
├── MainWindow.xaml  # 主界面
└── app.manifest     # UAC 管理员提权
```

## 技术特点

- 零 NuGet 依赖，快捷方式和计划任务均通过 COM 互操作实现
- 图标提取使用 `SHGetFileInfo` P/Invoke
- 拖拽使用 Win32 `WM_DROPFILES` 绕过 UIPI 限制（管理员窗口接收非提权 Explorer 拖放）
- 每个 Provider 独立 try/catch，单个来源失败不影响整体

## License

[MIT](LICENSE)
