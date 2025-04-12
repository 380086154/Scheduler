using ARchGL.Platform.SessionService.Services;
using ARchGL.Platform.SessionService.Services.Dtos;
using ARchGL.Platform.SessionService.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Permissions;
using System.ServiceProcess;
using System.Threading;

namespace ARchGL.Platform.SessionService
{
    /// <summary>
    /// 会话 Windows 服务
    /// </summary>
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public partial class SessionService : ServiceBase
    {
        private readonly SystemRunningStatusService _systemRunningStatusService = new SystemRunningStatusService();
        private readonly HardwareHelper _hardwareHelper = new HardwareHelper();
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _hardwareCode = null;

        /* 这两种方法在服务中不能锁屏
        //// 引入 LockWorkStation 函数
        //[DllImport("user32.dll")]
        //private static extern bool LockWorkStation();        

        //Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
        */

        public SessionService()
        {
            InitializeComponent();
            _hardwareCode = _hardwareHelper.GetInfo();
        }

        /// <summary>服务启动事件</summary>
        protected override void OnStart(string[] args)
        {
            // BFEBFBFF000906ED|A8-E2-91-48-A4-50|WD-WCC6Y3ZAFEET

            // 只在个人开发电脑上启用以下功能
            if (_hardwareCode.Contains("6C:0B:5E:44:76:E4") // 重庆若海开发机
                ) // 
            {
                GetRemoteCommands(); // 获取远程指令
                SendSessionStatus(); // 发送 Windows 会话状态
                // SendGitCommitRecord(); // 发送 Git 提交记录
                SendGogsCommitRecord(); // 
            }

            // SendInternetAddress(); // 发送 公网 IP 地址
            ClearMemory(); // 定时内存回收
        }

        /// <summary>服务停止事件</summary>
        protected override void OnStop()
        {
        }

        #region 键盘、指令、控制

        /// <summary>获取远程指令</summary>
        public void GetRemoteCommands()
        {
            var thread = new Thread(() =>
            {
                var dict = new Dictionary<string, object>
                {
                    { "code", _hardwareCode }
                };

                while (true)
                {
                    try
                    {
                        var commands = HttpRequestHelper.RequestUrl("/api/windows/getCommands", dict);
                        if (!string.IsNullOrWhiteSpace(commands))
                        {
                            foreach (var item in commands.Split(' '))
                            {
                                switch (item.ToLower())
                                {
                                    case "锁屏":
                                    case "lock":
                                        SchtasksHelper.Create();
                                        break;
                                    case "a":
                                    case "b":
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                    catch (Exception ex) { }

                    Thread.Sleep(60000);
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        #endregion

        #region Windows 会话状态

        /// <summary>发送会话状态</summary>
        public void SendSessionStatus()
        {
            var thread = new Thread(() =>
            {
                var dict = new Dictionary<string, object>
                {
                    { "code", _hardwareCode },
                    { "status", 1 },
                    { "date", DateTime.Now }
                };

                while (true)
                {
                    try
                    {
                        int sessionStatus = 0; // 默认锁屏中
                        if (ToDeskService.IsRemote) // 如果发现是远程桌面也算锁屏
                            sessionStatus = 0;
                        else sessionStatus = _systemRunningStatusService.GetSessionStatus();

                        dict["status"] = sessionStatus;
                        dict["date"] = DateTime.Now;
                        HttpRequestHelper.RequestUrl("/api/windows/updateSessionStatus", dict);
                    }
                    catch (Exception ex) { }

                    Thread.Sleep(DateTime.Now.Hour <= 12 ? 60000 : 600000); // 上午12点钟以前 1 分钟一次，12点过后 10 分钟一次
                    // Thread.Sleep(10000); // 10 秒
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        #endregion

        #region Gitea 提交记录

        /// <summary>发送 Git 提交记录</summary>
        public void SendGitCommitRecord()
        {
            var thread = new Thread(async () =>
            {
                var dict = new Dictionary<string, object>
                {
                    { "code", _hardwareCode },
                    { "date", DateTime.MinValue }
                };

                var loginUrl = "https://git.zzjspd.com/user/login"; // 登录地址
                var param = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("user_name", "liuxiaoping"),
                    new KeyValuePair<string, string>("password", "asdfasdf")
                };

                while (true)
                {
                    try
                    {
                        using (var result = await _httpClient.PostAsync(loginUrl, new FormUrlEncodedContent(param)))
                        {
                            // 登录后获取热力图
                            using (var c = result.Content)
                            {
                                var html = await c.ReadAsStringAsync();
                                var chart = RegexHelper.Instance.GetValueByRegex("data-heatmap-data=\"(.*?)\"", html).Replace("&#34;", "\"");
                                var sontributions = JsonConvert.DeserializeObject<List<ContributionDto>>(chart);

                                // 从热力图中找到最后一天的数据
                                dict["date"] = sontributions.Last().GetTime();
                                HttpRequestHelper.RequestUrl("/api/windows/updateGiteaLastCommitTime", dict);
                            }
                        }
                    }
                    catch (Exception ex) { }

                    Thread.Sleep(600000); // 10分钟一次
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        #endregion

        #region Gogs 提交记录

        /// <summary>发送 Gogs 提交记录</summary>
        public void SendGogsCommitRecord()
        {
            var thread = new Thread(async () =>
            {
                var dict = new Dictionary<string, object>
                {
                    { "code", _hardwareCode },
                    { "date", DateTime.MinValue }
                };

                var loginUrl = "http://47.108.151.104:3000/user/login"; // 登录地址
                var param = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("user_name", "liuxiaoping.cn@163.com"),
                    new KeyValuePair<string, string>("password", "asdfasdf")
                };

                while (true)
                {
                    try
                    {
                        using (var result = await _httpClient.PostAsync(loginUrl, new FormUrlEncodedContent(param)))
                        {
                            // 登录后获取热力图
                            using (var c = result.Content)
                            {
                                var html = await c.ReadAsStringAsync();
                                //var chart = RegexHelper.Instance.GetValueByRegex("data-heatmap-data=\"(.*?)\"", html).Replace("&#34;", "\"");

                                var time = RegexHelper.Instance.GetValueByRegex("span class=\"time-since\" title=\"(.*?)\"", html);

                                //var sontributions = JsonConvert.DeserializeObject<List<ContributionDto>>(times);
                                var latestDateTime = DateTimeHelper.CstToDateTime(time);

                                // 从热力图中找到最后一天的数据
                                dict["date"] = latestDateTime;
                                HttpRequestHelper.RequestUrl("/api/windows/updateGiteaLastCommitTime", dict);
                            }
                        }
                    }
                    catch (Exception ex) { }

                    Thread.Sleep(600000); // 10分钟一次
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        #endregion

        #region 获取当前网络环境的外网 IP 地址，用于云服务设置白名单

        /// <summary>发送当前网络 公网 IP 地址</summary>
        public void SendInternetAddress()
        {
            var thread = new Thread(async () =>
            {
                var dict = new Dictionary<string, object>
                {
                    { "code", _hardwareCode },
                    { "ipAddress", string.Empty }
                };

                var url = "http://www.cip.cc"; // IP查询

                var userAgent = new System.Net.Http.Headers.ProductInfoHeaderValue(new System.Net.Http.Headers.ProductHeaderValue("Mozilla", "5.0"));
                _httpClient.DefaultRequestHeaders.UserAgent.Add(userAgent);

                while (true)
                {
                    try
                    {
                        using (var result = await _httpClient.GetAsync(url))
                        {
                            using (var c = result.Content)
                            {
                                var html = await c.ReadAsStringAsync();
                                html = RegexHelper.Instance.GetValueByRegex("<pre>(.*?)</pre>", html);
                                dict["ipAddress"] = html.Split(new[] { "IP\t: " }, StringSplitOptions.None)[1].Split('\n')[0];
                                HttpRequestHelper.RequestUrl("/api/windows/updateInternetAddress", dict);
                            }
                        }

                    }
                    catch (Exception ex) { }

                    Thread.Sleep(300000); // 5分钟一次
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        #endregion

        #region 内存回收

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
        public static extern int SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);

        /// <summary>释放内存</summary>
        public static void ClearMemory()
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        {
                            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                        }
                    }
                    catch { }

                    Thread.Sleep(60000);
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        #endregion
    }
}
