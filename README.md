# Scheduler

Windows desktop scheduler for workday reminders, office-presence tracking, and daily input **counts** (keyboard / mouse / wheel). It does **not** record key contents.

面向国内办公场景的 Windows 后台调度程序：按工作日发送提醒、统计今日在岗时长，以及键鼠操作次数。代码根命名空间为 `Scheduler.*`（`Scheduler.App` / `Scheduler.Core` / `Scheduler.Common`）。

## 它做什么

- 工作日 / 本周最后工作日 / 本月最后工作日提醒（节假日数据来自公开接口，并缓存到本地）
- 根据锁屏与远程桌面状态估算今日在岗时长，用于「人在公司才提醒」
- 统计今日键盘按下次数、鼠标点击、滚轮次数，可在下班时发一条摘要
- 通知渠道：Microsoft Teams、企业微信；钉钉为占位（尚未发送）

## 隐私

- 输入统计只保存**次数**，写入本地 `data/input/stats.txt`
- **不记录按键字符、不写按键日志、不上传输入内容**
- Webhook 等密钥不要放进仓库，使用本地覆盖文件（见下文）

## 运行要求

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)（从源码运行）
- 无需管理员权限；适合没有 Windows Service 安装权限的电脑

## 从源码运行

```powershell
git clone <your-fork-or-clone-url>
cd DailyCheck
copy src\Scheduler.App\appsettings.Local.json.example src\Scheduler.App\appsettings.Local.json
dotnet run --project src\Scheduler.App
```

未配置 Webhook 时进程仍会启动：在岗采样和输入统计可用，通知会跳过并在日志里给出警告。

### 配置通知

1. 复制 `src/Scheduler.App/appsettings.Local.json.example` 为同目录下的 `appsettings.Local.json`（该文件已被 git 忽略）。
2. 填写其中一个渠道：

```json
{
  "Notifications": {
    "Provider": "Teams",
    "TeamsWebhookUrl": "https://your-tenant.environment.api.powerplatform.com/..."
  }
}
```

企业微信示例：

```json
{
  "Notifications": {
    "Provider": "WeCom",
    "WeComWebhookUrl": "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=YOUR_KEY"
  }
}
```

3. 提醒规则在 `src/Scheduler.App/appsettings.json` 的 `Notifications:Rules` 中，可按需改时间、文案和是否启用。

`appsettings.json` 只放默认值和示例规则，不要提交真实 Webhook。

## 仓库结构

| 路径 | 说明 |
| --- | --- |
| `src/Scheduler.sln` | 解决方案 |
| `src/Scheduler.App` | 可执行主程序（后续托盘与设置窗口也在这里） |
| `src/Scheduler.Core` | 通知、节假日、在岗、输入统计 |
| `src/Scheduler.Common` | 公共工具 |
| `src/ARchGL.Platform.LockScreen` | **Legacy**，不再维护 |
| `src/ARchGL.Platform.SessionService` | **Legacy**，不再维护 |

解决方案默认只编译上面三套现行项目。旧项目仍在仓库里便于对照历史实现，不进入 CI。

## 构建

```powershell
dotnet build src/Scheduler.App/Scheduler.App.csproj -c Release
```

## 许可证

[MIT](LICENSE)
