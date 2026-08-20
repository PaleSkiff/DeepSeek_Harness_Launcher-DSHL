# DeepSeek Harness Launcher

一个 Windows 桌面启动器，用于**一键启动、停止、重启和监控 DeepSeek Harness 本地服务**，并提供依赖安装、配置管理、实时日志与系统托盘等配套能力。

> 技术栈：C# / WPF / .NET 8（仅 Windows 10/11 x64）· 纯黑主题 · 支持中英文界面

---

## 1. 项目解决什么问题

DeepSeek Harness（`@deepseek-ai/dsh`）是一个在本地命令行运行的 AI 智能体框架，日常使用需要手动执行：

```text
npx --verbose @deepseek-ai/dsh web
```

这对非命令行用户很不友好：需要记忆指令、自己处理 node.js 安装、观察启动状态、排查端口占用/超时/依赖缺失等。本项目把这些全部收进一个图形界面：

- **不懂命令行也能用**：点击「启动」即可运行本地 DSH 服务，就绪后自动打开网页；
- **自动装环境**：node.js 缺失时可一键安装（优先本地离线包），DSH 缺失时可一键安装；
- **看得见状态**：未运行 / 启动中 / 运行中 / 停止中 / 异常 五种状态实时呈现，托盘图标同步变色；
- **方便排障**：服务输出实时滚动展示、按日落盘，配置与日志都在程序目录（便携）。

## 2. 主要功能

| 模块 | 功能 |
|---|---|
| **服务控制**（首页） | 一键启动 / 停止 / 重启 DSH 服务；点击启动**立即显示「启动中」**（先转状态再做环境准备）；就绪后自动在浏览器打开 `http://127.0.0.1:3080` |
| **状态监控** | 五态状态机（未运行/启动中/运行中/停止中/异常），端口探测 + HTTP 健康检查判定就绪；启动/停止超时检测；进程意外退出自动转异常 |
| **日志** | 实时捕获服务进程 stdout/stderr，自动滚动、级别过滤（全部/INFO/WARN/ERROR/DEBUG）、清空、复制、导出 `.log/.txt`；按日落盘到 `logs\`，按保留天数自动清理 |
| **配置** | 表单 + JSON 文本双模式编辑启动命令/参数/工作目录/环境变量/端口/超时/健康检查，保存到程序目录 `config.json`（便携模式） |
| **环境 / 安装** | 自动并行检测 node.js 与 DSH；**node.js 一键安装**（本地离线 MSI 优先 → winget → 官网下载，UAC 提权 + 进度提示）；**DSH 一键安装**（隐藏控制台运行 `npx --yes --verbose @deepseek-ai/dsh --help`，实时详细日志 + 包可用性验证，npm 全局安装提权兜底）；已安装的组件自动禁用安装按钮 |
| **系统托盘** | 状态色图标 + 右键菜单（启动/停止/重启/打开主界面/退出）、双击恢复窗口；最小化到托盘时右上角弹出圆角提示卡片 |
| **设置** | 开机自启（到托盘）、启动后自动启动服务、关闭行为（最小化到托盘/退出 + 首次关闭询问）、日志保留天数、退出时停止服务、**中英文界面切换** |
| **引导与提示** | 首次启动弹出使用引导（功能介绍 + 使用步骤 + 当前环境状态）；端口占用、安装结果等均有明确提示 |

## 3. 系统要求

- Windows 10 / 11（64 位）
- 无需预装 .NET 运行时（安装程序为自包含版本）
- **node.js**：建议缺失时直接在应用内「环境 / 安装」页一键安装（安装程序已内置离线 MSI，不依赖网络）；也可先自行安装
- DeepSeek Harness 由应用自动安装/按需下载（安装 DSH 需要联网访问 npm registry）

## 4. 安装方法

### 方式一：安装程序（推荐）

运行 `installer\DeepSeekHarnessLauncher_Setup_v1.0.0.exe`（Inno Setup 安装包，用户级安装，无需管理员）：

- 安装到 `%LOCALAPPDATA%\DeepSeek Harness Launcher`
- 自动附带离线 node.js MSI（`nodejs\`），断网也能装 node.js
- 可选创建桌面快捷方式；卸载时会一并删除程序目录下的 `logs\` 与 `config.json`

### 方式二：绿色发布（完整文件夹）

在项目根目录执行（需本机装有 .NET 8 SDK）：

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

产物输出到 `publish\`，将该文件夹整体拷贝到目标 Windows 10/11 x64 机器即可双击运行（自包含，无需安装 .NET 运行时）。

## 5. 使用方法

1. **首次启动**：出现引导对话框（应用介绍、使用步骤、当前环境状态），点击「开始使用」。
2. **检查环境**：进入「环境 / 安装」页（页面会自动检测）。若 node.js 或 DSH 缺失，点击对应「安装」按钮一键安装（node.js 需在 UAC 弹窗中点击「是」；DSH 安装时日志会实时滚动，出现 `[成功]` 即完成）。
3. **启动服务**：回到「服务控制」页，点击「▶ 启动」——状态立即变为「启动中」，就绪后变为「运行中」，并自动打开浏览器访问 `http://127.0.0.1:3080`。
4. **日常操作**：
   - 停止 / 重启：点击「■ 停止」「↻ 重启」；
   - 后台常驻：点标题栏「—」最小化到托盘（右上角出现提示卡片），双击托盘图标恢复；
   - 托盘右键：启动 / 停止 / 重启 / 打开主界面 / 退出；
   - 查看输出：进入「日志」页（实时滚动，可过滤/导出）；
   - 改端口等参数：进入「配置」页修改并保存，下次启动生效；
   - 开机自启、关闭行为、语言等：进入「设置」页。
5. **退出**：若服务仍在运行，退出前会提示并停止服务，避免遗留后台进程。

## 6. 输入输出示例

### 6.1 环境检测（「环境 / 安装」页检测输出框）

输入：点击「重新检测」（或页面自动检测）

输出（示例）：

```text
> node -v
v24.19.0

> npx --no-install @deepseek-ai/dsh --help
Usage: dsh [options] [command] ...

> npm view @deepseek-ai/dsh version
0.1.0
```

对应状态：`node.js ✓ v24.19.0`、`DeepSeek Harness ✓ 0.1.0`（缺失时显示 ✗ 未安装/不可用，安装按钮可用）。

### 6.2 安装 node.js（本地离线包路径）

输入：点击「自动安装 (winget)」→ UAC 弹窗点「是」

输出（安装日志，含提权提示与静默安装心跳）：

```text
[正在安装 Node.js]
查找本地离线安装包…
使用本地离线安装包：node-v24.19.0-x64.msi
执行命令：msiexec /i "…\node-v24.19.0-x64.msi" /qn /norestart
正在请求管理员权限（UAC），请在弹窗中点击「是」…
正在静默安装，请稍候（已等待 10 秒）…
[成功] Node.js 安装完成
```

安装成功后自动重新检测，状态立即更新为「已安装」。

### 6.3 安装 DeepSeek Harness（隐藏控制台，轻量验证）

输入：点击「安装 DeepSeek Harness」

输出（安装日志，实时滚动，按批合并）：

```text
[正在安装 DeepSeek Harness]
通过 npx --yes --verbose @deepseek-ai/dsh --help 安装并验证（详细日志）
执行命令：npx --yes --verbose @deepseek-ai/dsh --help
npm verbose cli C:\Program Files\nodejs\node.exe C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js
npm http fetch GET 200 https://registry.npmjs.org/@deepseek-ai%2fdsh 855ms (cache revalidated)
Usage: dsh [options] [command] ...
[成功] DeepSeek Harness 安装完成
```

> 说明：安装阶段采用轻量 `--help` 验证（不启动完整 web 服务，低配机器不会因内存耗尽崩溃）；实际服务由「启动」按钮按需启动。

### 6.4 启动 / 停止服务（「服务控制」页）

输入：点击「▶ 启动」

输出（状态流转）：

```text
未运行 → 启动中 → 运行中（PID 12345 · 127.0.0.1:3080）
```

随后浏览器自动打开 `http://127.0.0.1:3080`；底部状态栏与托盘图标同步变为绿色「运行中」。

输入：点击「■ 停止」

输出：

```text
运行中 → 停止中 → 未运行
```

### 6.5 配置文件 `config.json`（程序目录，便携）

首次运行使用默认值，保存配置后生成；完整字段示例：

```json
{
  "service": {
    "command": "npx",
    "arguments": "--verbose @deepseek-ai/dsh web",
    "workingDirectory": "",
    "environmentVariables": {}
  },
  "network": {
    "port": 3080,
    "healthCheckUrl": "",
    "healthCheckIntervalSeconds": 5
  },
  "timeout": {
    "startSeconds": 60,
    "stopSeconds": 15
  },
  "behavior": {
    "autoStartServiceOnLaunch": false,
    "closeToTray": true,
    "askOnFirstClose": true,
    "stopServiceOnExit": true
  },
  "startup": {
    "autoStartOnBoot": false
  },
  "logging": {
    "retentionDays": 7
  },
  "language": "zh-CN"
}
```

损坏时自动备份为 `config.json.bad` 并以默认值继续运行；删除后下次启动恢复默认。

## 7. 数据文件（均在程序目录，便携）

- `config.json`：启动器全部配置（如上）。
- `logs\`：按日滚动的服务日志（`yyyy-MM-dd.log`），按「日志保留天数」自动清理。

## 8. 构建与测试

```powershell
# 构建
dotnet build src/DeepSeekHarnessLauncher/DeepSeekHarnessLauncher.csproj -c Debug

# 运行全部测试（xUnit，200+ 项）
dotnet test DeepSeekHarnessLauncher.sln

# 发布（自包含完整文件夹 → publish\）
powershell -ExecutionPolicy Bypass -File publish.ps1

# 打包安装程序（需 Inno Setup 6；ISCC.exe 路径按实际安装位置填写）
ISCC.exe installer\dshl-setup.iss
```

## 9. 目录结构

```text
DeepSeek Harness Launcher/
├── README.md / PRD.md
├── docs/                        # 页面结构与交互设计、开发计划
├── publish.ps1                  # 自包含发布脚本
├── nodejs/                      # 离线 node.js 安装包（随安装程序分发）
├── installer/                   # Inno Setup 脚本 + 输出安装包
├── logo/                        # 应用图标源图
└── src/
    ├── DeepSeekHarnessLauncher/          # WPF 主项目
    └── DeepSeekHarnessLauncher.Tests/    # xUnit 测试项目
```

## 许可证

本项目以 **MIT License** 开源，详见根目录 [LICENSE](LICENSE)。
