# TaskbarLyrics Light

原生 WPF 轻量版。与原版共享 `TaskbarLyrics.Core` 业务逻辑，歌词与设置页均用纯 WPF 渲染，去除 WebView2、WPF-UI 依赖，显著降低运行时进程数与内存占用。

## 与原版对比

### 架构

| 项目 | 原版 (`TaskbarLyrics.App`) | 轻量版 (`TaskbarLyrics.Light`) |
|------|---------------------------|-------------------------------|
| 歌词渲染 | WebView2 + HTML/CSS/JS | 原生 WPF `LyricsDisplayControl` |
| 设置页 | WebView2 加载 `settings.html` | 原生 WPF + `SettingsTheme.xaml` |
| UI 框架 | WPF + WPF-UI | 纯 WPF |
| 默认字体 | 内置 Source Han Sans SC（约 33 MB） | 内置 Source Han Sans SC（约 33 MB，默认） |
| WebView2 Runtime | 必须 | 不需要 |
| 业务逻辑 | `TaskbarLyrics.Core` | 共享 `TaskbarLyrics.Core` |
| 歌词缓存 / 歌曲映射 | `%APPDATA%\TaskbarLyrics\` | 与原版共享 |

### 性能（实测）

测试环境：Windows 11 x64，`Release` 发布，`win-x64` 框架依赖（`--self-contained false`），启动后静置 20 秒（含完整子进程树）。

| 指标 | 原版 | 轻量版（含思源黑体） | 变化 |
|------|------|---------------------|------|
| 发布包体积 | 75.7 MB（56 文件） | **67.6 MB**（39 文件） | **约 −11%** |
| 进程数 | 7（含 WebView2 子进程） | 1 | **单进程** |
| 工作集内存 | ~633 MB | ~364 MB | **约 −43%** |
| 专用内存 (Private) | ~386 MB | ~252 MB | **约 −35%** |

> 轻量版在包含思源黑体后，磁盘体积与原版接近，但**无 Chromium 多进程栈**、无 `ExecuteScriptAsync` 跨进程开销，空闲内存仍显著更低。若去掉内置字体改用系统字体，发布包可降至约 **35 MB**。

轻量版去掉的主要运行时开销：**Chromium 多进程栈**、**每帧 `ExecuteScriptAsync` 跨进程调用**、以及 WPF-UI 依赖。

### 性能优化（轻量版内置）

- **延迟初始化**：SQLite / EF Core 与歌词同步服务在首次需要检索歌词时才加载
- **Provider 懒加载**：各在线歌词源在首次被调用时才实例化
- **合并帧定时器**：16 ms 统一调度歌词（约 64 ms）与频谱（约 32 ms）刷新
- **频谱按需采集**：仅在纯音乐播放时启动 WASAPI 环回采集线程
- **WPF 渲染缓存**：行高 `FormattedText` 测量缓存、画刷 `Freeze`
- **设置页**：字体列表延迟枚举、下拉列表虚拟化
- **Release 发布**：框架依赖发布时剥离 PDB（`DebugType=none`）；因托盘使用 WinForms，`PublishTrimmed` 与当前 SDK 不兼容

### 运行时路径差异

- **原版**：C# 定时器 → JSON 序列化 → `ExecuteScriptAsync` → Chromium 渲染（歌词约 60 ms/次，频谱约 33 ms/次）
- **轻量版**：C# 定时器 → 直接更新 WPF 控件属性；频谱在控件内以 16 ms `DispatcherTimer` 插值，切歌动画走 `CompositionTarget.Rendering`

## 功能

与原版对齐的核心能力：

- 双行歌词滚动与 560 ms 平滑切换动画
- 专辑封面交叉淡入 / 播放器品牌色回退
- 纯音乐 24 条实时频谱（可调参）
- SMTC 多播放器识别与歌词多源检索
- 系统托盘、设置页、SMTC 时间轴调试窗口、频谱调参窗口

轻量版独有：

- **开机自启动**（写入当前用户注册表 `Run` 项，默认开启）
- **播放器联动**：打开音乐软件时自动显示歌词、全部播放器退出后自动隐藏（默认开启；基于 SMTC 播放/暂停/停止状态，非进程窗口标题）
- **默认字体**：`Source Han Sans SC`（旧配置缺字段时自动补全）

轻量版设置页（原生 WPF）额外支持：

- 窗口宽度 / 高度随歌词内容自动适配（可关）
- 行距自动适配字号（可关）

与原版的功能差异：

| 能力 | 原版 | 轻量版 |
|------|------|--------|
| 显示歌词翻译 | 支持（设置项可开关） | **不支持**（仅显示原文，与 Core 默认行为一致） |
| 开机自启动 | 无 | 支持 |
| 播放器打开/关闭联动 | 无 | 支持 |

## 系统要求

- Windows 10/11 x64
- 从源码运行或框架依赖发布时，需安装 [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **不需要** Microsoft Edge WebView2 Runtime

## 运行

```bash
dotnet run --project TaskbarLyrics.Light/TaskbarLyrics.Light.App
```

## 发布

框架依赖（需目标机器已装 .NET 8，含内置字体约 **68 MB**）：

```bash
dotnet publish TaskbarLyrics.Light/TaskbarLyrics.Light.App -c Release -r win-x64 --self-contained false -o publish/light
```

独立发布（无需预装运行时）：

```bash
dotnet publish TaskbarLyrics.Light/TaskbarLyrics.Light.App -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o publish/light-standalone
```

## 配置目录

| 用途 | 路径 |
|------|------|
| 轻量版设置 | `%APPDATA%\TaskbarLyrics.Light\settings.json` |
| 歌词缓存 | `%APPDATA%\TaskbarLyrics\cache`（与原版共享） |
| 歌曲映射数据库 | `%APPDATA%\TaskbarLyrics\database\song_maps.db`（与原版共享） |

两版可同时安装；设置文件独立，缓存与数据库共用。

## 选用建议

- **优先轻量版**：日常挂任务栏、在意内存与进程数、不想装 WebView2、需要开机自启与播放器联动、只需原文歌词
- **优先原版**：需要歌词翻译、已有原版配置习惯，或偏好 WebView2 版设置页
