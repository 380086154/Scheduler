# Scheduler

Windows desktop scheduler for workday reminders, office-presence tracking, and daily input **counts** (keyboard / mouse / wheel). It does **not** record key contents.

面向国内办公场景的 Windows 托盘程序：按工作日发送提醒、统计今日在岗时长，以及键鼠操作次数。同一进程里包含后台调度和设置窗口。

## 普通用户怎么用

1. 运行 `Scheduler.exe`（从发布包解压后双击即可，不必安装 .NET SDK）。
2. 程序会进入托盘（任务栏右侧）。第一次打开会自动弹出设置页。
3. 在设置里选择 Teams 或企业微信，粘贴 Webhook，按需改提醒规则，勾选「开机时启动」，点 **保存**。
4. 可用 **发送测试通知** 确认通道可用。
5. 关掉设置窗口不会退出；要停止提醒，请右键托盘图标选择「退出」。

用户配置保存在 `%AppData%\Scheduler\settings.json`，不要把这个文件发到网上。

输入统计只记次数，不记录按键内容。

## App 和 Windows Service

两边用同一套通知循环（节假日、在岗采样、到点发送）。同一台电脑上只有一个进程会发送。谁先启动，谁负责发送；另一个进程不再发送。

| | `Scheduler.exe`（App） | `Scheduler.Service`（Windows 服务） |
| --- | --- | --- |
| 做什么 | 托盘、设置窗口，并在自己负责发送时跑通知 | 没有托盘。负责发送时跑同一套通知 |
| 什么时候用 | 没有管理员权限。直接双击、开机启动，或用任务计划在登录时启动 | 需要没人登录也继续发送。安装服务需要管理员 |
| 改配置 | 就是这个程序 | 仍然双击 `Scheduler.exe` 改。服务在跑时，App 只打开设置，不挂托盘，也不再发送 |

不要同时用「开机时启动」和 Windows 服务。服务已经在跑时，登录再启动 App 会弹出设置窗口，而不是托盘。

当前用户、不保存密码的登录任务（不需要管理员）：

```powershell
schtasks /Create /TN "Scheduler" /TR "\"C:\path\Scheduler.exe\"" /SC ONLOGON /RL LIMITED /F
```

安装服务（需要管理员）。服务读取当前登录用户的 `%AppData%\Scheduler\settings.json`；没人登录时，用最近保存过的那份配置。

```powershell
sc.exe create "Scheduler Service" binPath= "\"C:\path\Scheduler.Service.exe\"" start= auto
sc.exe start "Scheduler Service"
```

## 从源码运行

需要 Windows 10/11 和 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```powershell
dotnet run --project src/Scheduler.App
```

开发时仍可用 `appsettings.Local.json` 覆盖配置（已被 git 忽略）。日常用户改设置页即可，不必手改 JSON。

## 发布给普通用户

```powershell
powershell -File scripts/publish.ps1
```

会在 `artifacts/Scheduler` 生成自包含 win-x64 目录，把整个文件夹打成 zip 即可分发。

## 它做什么

- 工作日 / 本周最后工作日 / 本月最后工作日提醒
- 根据锁屏与远程桌面状态估算今日在岗时长
- 统计今日键盘按下、鼠标点击、滚轮次数（可下班发摘要）
- 通知渠道：Microsoft Teams、企业微信（钉钉尚未发送）

## 隐私

- 输入统计只保存次数
- **不记录按键字符、不写按键日志、不上传输入内容**
- Webhook 只存在本机用户配置里

## 仓库结构

| 路径 | 说明 |
| --- | --- |
| `src/Scheduler.sln` | 解决方案 |
| `src/Scheduler.App` | WPF 主程序（托盘 + 设置 + Host） |
| `src/Scheduler.Core` | 通知、节假日、在岗、输入统计 |
| `src/Scheduler.Common` | 公共工具 |
| `src/Scheduler.LockScreen` | **Legacy**，不参与解决方案编译 |
| `src/Scheduler.Service` | Windows 服务。与托盘共用通知循环，同一时间只有一边发送 |

解决方案会编译托盘程序和 `Scheduler.Service`。`Scheduler.LockScreen` 不进入默认编译。

## 构建

```powershell
dotnet build src/Scheduler.sln -c Release
```

## 许可证

[MIT](LICENSE)
