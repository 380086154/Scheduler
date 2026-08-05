using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace ARchGL.Platform.SessionService.Services
{
    /// <summary>
    /// 通过检查 ToDesk 日志是否存在并且，并且最近5分钟内是否有更新，来判断是否正在远程
    /// </summary>
    public class ToDeskService
    {
        public static bool IsRemote = false;

        public static void Init()
        {
            var thread = new Thread(() =>
            {
                var path = GetToDeskInstallPathFromRegistry();
                path += "/Logs";

                if (!Directory.Exists(path)) return;

                while (true)
                {
                    try
                    {
                        var date = DateTime.Now;
                        var di = new DirectoryInfo(path);

                        // sessionphqghume_2025_04_12.log
                        // zrtcsessionfircvscx_2025_04_12.log
                        // sdksessionaylnlfdx_2025_04_11.log

                        var latestFiles = di.GetFiles($"session*{date:_yyyy_MM_dd}.log")
                            .OrderByDescending(x => x.LastWriteTime)
                            .Take(10);

                        if (latestFiles == null || latestFiles.Count() == 0)
                            IsRemote = false;

                        foreach (var item in latestFiles)
                        {
                            if (item.LastWriteTime >= date.AddMinutes(-5))
                            {
                                IsRemote = true;
                                break;
                            }
                            else IsRemote = false;
                        }
                    }
                    catch { }
                    
                    Thread.Sleep(30000); // 每隔 30 秒检查一次
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        #region 安装路径检查

        /// <summary>查找 ToDesk 程序安装路径</summary>
        public static string GetToDeskInstallPathFromRegistry()
        {
            var path = GetInstPath(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\ToDesk"); // 尝试从64位系统的注册表查找
            if (!string.IsNullOrWhiteSpace(path)) return path;

            path = GetInstPath(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\ToDesk"); // 尝试从32位系统的注册表查找
            if (!string.IsNullOrWhiteSpace(path)) return path;

            return GetInstPath(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\ToDesk"); // 尝试从当前用户的注册表查找
        }

        public static string GetInstPath(RegistryKey registryKey, string path)
        {
            using (RegistryKey key = registryKey.OpenSubKey(path))
            {
                if (key == null) return null;

                var value = key.GetValue("InstPath");
                return value?.ToString();
            }
        }

        #endregion
    }
}
