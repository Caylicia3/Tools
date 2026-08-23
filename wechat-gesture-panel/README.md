# 微信手势面板

在微信网页版（wx.qq.com / web.wechat.com）中，通过摄像头识别简单手势，实现无接触滚动、选中和打开操作。

## 功能简介

- **手势识别**：基于摄像头捕捉手部动作，实时识别上下挥动、握拳、张开手等手势。
- **无接触操作**：
  - 上下挥动手掌：页面向上 / 向下滚动
  - 握拳：选中当前焦点元素
  - 张开手掌：触发点击 / 打开操作
- **可拖拽面板**：面板可折叠 / 展开，支持按住把手拖拽到屏幕任意位置。
- **新手引导**：首次使用会展示引导步骤，可在展开面板中点击“重新引导”再次查看。
- **隐私优先**：摄像头画面仅在本地处理，不会上传或存储任何视频数据。

## 安装方式

### 1. 下载扩展包

- 方式一：从本仓库的 [Releases](../../releases) 页面下载 `wechat-gesture-panel-extension.zip`。
- 方式二：在本地项目目录运行 `node scripts/build-zip.js` 自行打包（需安装 7z 或 7z.exe）。

### 2. 解压扩展包

将下载的 `wechat-gesture-panel-extension.zip` 解压到本地文件夹，例如 `wechat-gesture-panel-extension/`。解压后，该文件夹内应直接包含 `manifest.json` 文件。

### 3. 加载到浏览器

1. 打开 Chrome / Edge 浏览器，访问 `chrome://extensions/`。
2. 开启右上角 **“开发者模式”**。
3. 点击左上角 **“加载已解压的扩展程序”**。
4. 在弹出的文件选择器中，**选中解压后的文件夹**（即包含 `manifest.json` 的那一级，不要选择 `.zip` 文件本身，也不要选择其父目录）。
5. 确认扩展图标出现在浏览器工具栏。

### 4. 开始使用

进入 [微信网页版](https://wx.qq.com) 或 [web.wechat.com](https://web.wechat.com)，首次使用时会提示授权摄像头，允许后即可通过手势控制页面。

## 使用说明

### 手势列表

| 手势 | 动作 | 说明 |
|------|------|------|
| 上下挥动手掌 | 滚动 | 向上挥动 = 页面上滚，向下挥动 = 页面下滚 |
| 握拳 | 选中 | 将焦点移动到可交互元素 |
| 张开手掌 | 打开 | 触发屏幕中心的点击事件 |

### 授权

首次启用时，浏览器会请求摄像头权限。手势识别完全在本地运行，请允许访问摄像头以使用本功能。

### 拖拽与收起

- 按住面板左侧的把手即可拖动。
- 点击“展开 / 收起”按钮切换完整面板。
- 点击“×”可临时退出面板，5 秒内可通过提示恢复。

## 隐私说明

- 本扩展仅在微信网页版域名（`wx.qq.com`、`web.wechat.com`）注入内容脚本。
- 摄像头视频流仅在本地通过 MediaPipe 模型进行手势识别，不会上传到任何服务器。
- 不收集、不存储、不分享任何用户数据。

## 添加到已有 GitHub 仓库

本扩展可以单独作为一个仓库，也可以放入你已有的仓库中。以下示例假设你已有一个仓库 `https://github.com/Caylicia3/Tools.git`，并希望把扩展作为 `wechat-gesture-panel/` 子目录放入其中。

### 1. 克隆已有仓库到 D 盘

```bash
git clone https://github.com/Caylicia3/Tools.git D:\GitHub\Tools
cd D:\GitHub\Tools
```

### 2. 复制扩展文件

将 `D:\TraeProjects\wechat-gesture-panel` 下的全部内容复制到 `D:\GitHub\Tools\wechat-gesture-panel\` 目录下。

复制后的结构示例：

```
D:\GitHub\Tools
├── wechat-gesture-panel
│   ├── extension
│   ├── scripts
│   ├── .github
│   │   └── workflows
│   │       └── release.yml
│   ├── README.md
│   └── ...
└── ... 其他工具
```

### 3. 提交并推送

```bash
git add wechat-gesture-panel/
git commit -m "feat: add wechat gesture panel extension"
git push origin main
```

> 说明：`wechat-gesture-panel-extension.zip` 已被 `.gitignore` 排除，不会进入 Git 仓库。Release 附件由 GitHub Actions 在推送标签时自动生成。

## 发布 Release

本仓库的 `.github/workflows/release.yml` 会在推送以 `wechat-gesture-panel-v` 开头的标签时自动打包并创建 Release。

### 1. 更新版本号

编辑 `wechat-gesture-panel/extension/manifest.json`，修改 `"version"` 字段：

```json
{
  "version": "1.0.1"
}
```

### 2. 提交并推送

```bash
git add wechat-gesture-panel/extension/manifest.json
git commit -m "chore: bump version to 1.0.1"
git push origin main
```

### 3. 推送标签触发 Release

```bash
git tag wechat-gesture-panel-v1.0.1
git push origin wechat-gesture-panel-v1.0.1
```

推送标签后，GitHub Actions 会自动：

1. 检出代码。
2. 安装 `p7zip-full`。
3. 在 `wechat-gesture-panel/` 目录下运行打包脚本，生成 `wechat-gesture-panel-extension.zip`。
4. 创建 Release，并将 ZIP 作为附件上传，同时自动生成 Release Notes。

你可以在仓库的 **Actions** 标签页查看构建进度，在 **Releases** 页面下载最终安装包。

## 本地构建

如果你想在本地生成安装包，可以在项目目录运行：

```bash
cd wechat-gesture-panel
node scripts/build-zip.js
```

脚本会自动检测系统是否安装 `7z` / `7z.exe` 并打包 `extension/` 目录。若未检测到，请手动将 `extension/` 压缩为 `wechat-gesture-panel-extension.zip`。

## 故障排查

| 问题 | 可能原因 | 解决方法 |
|------|----------|----------|
| Chrome 提示“无法加载扩展” | 选择了 `.zip` 文件或项目根目录 | 先解压 ZIP，然后选择包含 `manifest.json` 的文件夹 |
| 扩展加载后没有反应 | 当前页面不是微信网页版 | 访问 `https://wx.qq.com` 或 `https://web.wechat.com` |
| 摄像头未开启 | 未授权摄像头权限 | 点击地址栏左侧摄像头图标，选择“允许” |
| 本地打包失败 | 未安装 7z | 安装 7-Zip，或手动压缩 `extension/` 目录 |
| 推送标签后没有 Release | 标签格式不正确 | 使用 `wechat-gesture-panel-v*` 格式，例如 `wechat-gesture-panel-v1.0.1` |

## 许可证

本项目采用 [MIT](./LICENSE) 许可证。
