# 日程管理 · DesktopTodo

面向 **Windows 11 64 位电脑**的桌面日程工具，把周／月计划和任务截止时间放在同一个面板中。

[下载安装版](https://github.com/Caylicia3/Tools/releases/download/desktop-todo-v0.1.1/DesktopTodo-0.1.1-win-x64-Setup.exe) · [下载免安装版](https://github.com/Caylicia3/Tools/releases/download/desktop-todo-v0.1.1/DesktopTodo-0.1.1-win-x64.zip) · [查看版本说明](https://github.com/Caylicia3/Tools/releases/tag/desktop-todo-v0.1.1)

当前版本：**0.1.1 预览版**。无需账号，日程数据保存在本机，运行组件随包提供。

## 主要功能

- **周／月日程**：按真实日期浏览，支持时间重叠、每周重复、拖选新增和移动日程。
- **任务时间轴**：每项任务独立一行，清晰显示起止日期、截止时间和里程碑节点。
- **可调视图**：Ctrl＋滚轮缩放，时间轴支持 7／30／90 天和自定义 1～3650 天。
- **撤销与重做**：共享最近 100 步内容操作，支持 Ctrl+Z、Ctrl+Y。
- **灵活提醒**：任务和节点可自定义提前天数、提醒时刻，完成后停止提醒。
- **四套主题**：雪景紫、海岸蓝、秋日橙、林间绿；颜色、背景不透明度、任务条粗细和行高均可实时预览。
- **桌面窗口**：支持拆分、合并、置顶、任务栏恢复和一键退出。
- **本地备份**：支持导出与恢复；升级沿用已有数据目录，卸载保留数据。

## 下载与安装

普通用户下载 Release 中的 **DesktopTodo-0.1.1-win-x64-Setup.exe** 即可，无需安装开发环境。

1. 双击安装包，自选程序目录和数据目录。
2. 数据目录需位于程序目录之外；新安装默认建议放在程序目录旁边。
3. 如需桌面图标，勾选「创建桌面快捷方式」。
4. 安装完成后，双击桌面的「日程管理」打开。

也可以下载 ZIP，**完整解压**后运行 DesktopTodo.exe，首次启动时选择独立的数据目录。请保留全部程序文件。

程序仅手动启动，**不添加开机启动项**。更新前可在设置中导出备份。

## 常用操作

| 操作 | 用法 |
|---|---|
| 切换周／月 | 点击上方「周」「月」 |
| 内容缩放 | 鼠标移入图表，按住 Ctrl 滚动滚轮；普通滚轮上下浏览 |
| 编辑任务 | 点击任务名称或彩色任务条，任务条不能拖动或拉伸 |
| 移动节点 | 拖动菱形查看目标日期，松开后确认才保存 |
| 撤销／重做 | Ctrl+Z ／ Ctrl+Y，也支持 Ctrl+Shift+Z 重做 |
| 最小化 | 点击「—」，从任务栏恢复；Win+D 也会暂时最小化 |
| 完全退出 | 点击主面板「×」或按 Alt+F4，全部面板和后台提醒一起退出 |
| 修改外观 | 设置中调整并实时预览；保存保留，取消或 Esc 恢复 |

关闭程序期间不会弹出提醒；重新运行时会汇总错过的提醒。系统通知设置会影响横幅是否显示。

自定义颜色示例：**#B7ABD4**，即 # 后接 6 位数字或 A～F 字母，也可直接点击「选色」。

## 数据与备份

- 日程、任务和设置保存在所选数据目录的 SQLite 数据库中。
- 在设置中可查看数据位置、导出备份和恢复备份。
- 建议通过应用导出备份；不要在运行时仅复制单个数据库文件。
- 免安装版升级时保留 data-location.txt，再覆盖程序文件。
- 默认不提供联网同步，也不会将日程内容上传 GitHub。

更多操作见 [快速使用说明](USAGE.txt)。已完成的验证范围见 [测试说明](TEST-REPORT.md)。

## 从源码构建

需要 Windows、**.NET SDK 10.0.302** 或同功能版本段的更新补丁、PowerShell。

在本目录执行：

~~~powershell
.\build.ps1 -Action build
.\build.ps1 -Action test
.\build.ps1 -Action publish
~~~

首次构建会从 NuGet 下载依赖。构建缓存、临时文件和输出都放在项目目录内；把项目放到空间充足的磁盘即可。-Offline 用于已另行准备好 .cache/package-feed 离线包的环境，首次构建不加该参数。

发布结果位于 artifacts/DesktopTodo。完整文件夹可直接运行；安装包使用 Inno Setup 6.7 编译 installer/DesktopTodo.iss。编译安装包前：

1. 将需要随包提供的 README.md、USAGE.txt、TEST-REPORT.md 和 THIRD-PARTY-NOTICES 复制到 artifacts/DesktopTodo。
2. 从 [Inno Setup 翻译页](https://jrsoftware.org/files/istrans/)取得 ChineseSimplified.isl，放到 .tools/InnoSetup/Languages。
3. 使用 Inno Setup 的 ISCC.exe 编译 installer/DesktopTodo.iss。

框架：WPF / .NET 10；存储：SQLite；系统通知：Windows App SDK。第三方组件的许可说明随发行文件提供。

## 项目结构

- src/DesktopTodo.App：桌面界面、交互与 Windows 集成。
- src/DesktopTodo.Core：日历、提醒、数据校验和保存逻辑。
- tests/DesktopTodo.Tests：核心逻辑检查。
- installer：安装包脚本。
- build.ps1：构建、测试和发布入口。

个人数据库、备份、日志、开发缓存和构建产物不进入源码仓库。
