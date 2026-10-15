# 构建与验证

## 环境

- Windows x64；界面与桌面集成检查需要可交互的 Windows 桌面。
- .NET SDK **10.0.302**，版本选择见 `global.json`。允许同一 SDK 功能版本段内的更新补丁。
- PowerShell。以下示例使用 PowerShell 7。
- 首次恢复依赖需要访问 NuGet。无需安装 Visual Studio。

在任意有足够空间的可写目录克隆仓库。以下使用 E 盘举例，程序和测试没有必须使用 E 盘的要求：

```powershell
git clone https://github.com/Caylicia3/Tools.git E:\Project\Tools
Set-Location E:\Project\Tools\desktop-todo
.\build.ps1 -Action build
.\build.ps1 -Action test
.\build.ps1 -Action format-check
```

脚本将 NuGet 缓存、CLI 缓存及临时文件放在当前项目的 `.cache` 和 `.tmp` 下。首次构建不要使用 `-Offline`；该选项仅适用于已经自行准备了完整 `.cache/package-feed` 离线包源的环境。

如依赖恢复报 TLS 或网络错误，检查对 NuGet 的连接。不要将恢复失败视为测试通过，也不应为了绕过网络问题随意修改依赖版本。

## 运行与演示

正常开发运行：

```powershell
.\build.ps1 -Action run
```

该命令使用已有 Release 构建。首次运行时选择数据目录。

独立演示数据：

```powershell
.\build.ps1 -Action build
dotnet run --project src/DesktopTodo.App/DesktopTodo.App.csproj -c Release --no-build -- --demo --data-dir (Join-Path $PWD '.tmp/demo-data')
```

演示模式只在指定目录没有日程和任务时填充虚构数据。测试和演示都必须显式提供独立数据目录，不要使用日常日程目录。

## 核心检查与格式

当前测试项目是自定义控制台检查程序，不是 xUnit 或 MSTest 项目；请运行 `build.ps1 -Action test`，不要把空的 `dotnet test` 输出当作验收结果。

每项检查输出 PASS／FAIL，末尾输出总数；有失败时返回非零退出码，并将摘要写入 `.tmp/tests/results.txt`。覆盖日期展开、重复事件修改、提醒与去重、SQLite 持久化、备份、内容历史及视图计算。

`format-check` 使用 SDK 自带的 `dotnet format whitespace --verify-no-changes`，检查 App、Core 和 Tests。请先完成 build 和 test 以恢复全部项目依赖。需要修复格式时，在项目目录运行：

```powershell
dotnet format whitespace src/DesktopTodo.App/DesktopTodo.App.csproj --no-restore
dotnet format whitespace src/DesktopTodo.Core/DesktopTodo.Core.csproj --no-restore
dotnet format whitespace tests/DesktopTodo.Tests/DesktopTodo.Tests.csproj --no-restore
```

格式整理与功能修改应分开提交，便于审阅。

## 界面实测

已有 `--smoke --demo --data-dir <独立目录>` 检查入口，可结合 `--interactions --update-checks` 运行交互回归。它会创建窗口、操作鼠标并保存结果；只在空闲的交互桌面运行，测试期间不要同时操作电脑。

结果位于指定目录内的 `smoke-result.json`。必须检查 `passed` 字段和应用错误日志，不能只看进程退出码。截图可能包含桌面或任务栏，不应自动上传全部测试输出。

GitHub Actions 运行编译、核心检查和格式检查，不运行这类桌面操作，也不代表完成 Windows 10／11 的全部实机验收。

## 发布目录与安装包

```powershell
.\build.ps1 -Action publish
.\build.ps1 -Action publish -Compatibility
```

分别生成 `artifacts/DesktopTodo` 和 `artifacts/DesktopTodo-Compatibility`。每次发布先重建，避免增量构建复用另一变体的启动文件；README、使用说明、验证说明、MIT 许可和第三方声明随目录复制。

编译安装包还需要 Inno Setup 6.7，以及放在 `.tools/InnoSetup/Languages/ChineseSimplified.isl` 的[简体中文语言文件](https://jrsoftware.org/files/istrans/)。使用安装好的 ISCC 编译：

```powershell
ISCC.exe installer/DesktopTodo.iss
ISCC.exe /DPayload=..\artifacts\DesktopTodo-Compatibility /DOutputName=DesktopTodo-0.1.2-win-x64-Compatibility-Setup installer/DesktopTodo.iss
```

发行前需独立验证安装、升级、卸载保留数据及两种构建的差异。不要将个人数据库、`data-location.txt`、测试截图或缓存打入发行包。

现有 0.1.2 二进制对应原发布标签；主分支后续文档与格式整理不代表重新发布了安装包。将来发布新版本时再更新版本号、安装器名称、发布说明与校验清单。
