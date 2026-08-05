using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;

namespace ARchGL.Platform.SessionService
{
    /// <summary>
    /// 计划任务帮助类
    /// </summary>
    public class SchtasksHelper
    {
        private static readonly string _name = "LockScreen";

        /// <summary>
        /// 创建任务：在指定时间执行一次锁屏，如果任务存在则覆盖
        /// </summary>
        public static void Create()
        {
            using (var ts = new TaskService())
            {
                // 创建新任务
                var td = ts.NewTask();
                td.RegistrationInfo.Description = "定时锁屏";

                // 设置触发器，或其他触发器，如WeeklyTrigger、MonthlyTrigger等
                td.Triggers.Add(new TimeTrigger { StartBoundary = DateTime.Now.AddMinutes(1) });
                td.Actions.Add(new ExecAction("rundll32.exe", "user32.dll,LockWorkStation", null)); // 设置操作
                //td.Actions.Add(new ExecAction($"{Environment.CurrentDirectory}/LockScreen.exe", "", null));
                //td.Principal.LogonType = TaskLogonType.InteractiveTokenOrPassword;
                //td.Principal.UserId = "administrator";

                // 注册任务，并且需要指定登录的系统账号密码才能在成功
                ts.RootFolder.RegisterTaskDefinition(_name, td, TaskCreation.CreateOrUpdate, "administrator", "0", TaskLogonType.InteractiveTokenOrPassword);
            }

            //Exec("schtasks", $"/create /tn {_name} /tr \"rundll32.exe user32.dll,LockWorkStation\" /sc once /st {DateTime.Now.AddMinutes(1):HH:mm} /f");
        }

        /// <summary>
        /// 手动执行任务
        /// </summary>
        public static void Run()
        {
            Exec("schtasks", $"/run /tn {_name}");
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        public static void Delete()
        {
            Exec("schtasks", $"/delete /tn {_name} /f");
        }

        /// <summary>
        /// 通过 CMD 执行命令
        /// </summary>
        /// <param name="applocaltion"></param>
        /// <param name="argument"></param>
        private static void Exec(string applocaltion, string argument)
        {
            using (var process = new Process())
            {
                process.StartInfo.Arguments = argument;
                process.StartInfo.FileName = applocaltion;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                process.StandardInput.AutoFlush = true;
                process.StandardInput.WriteLine("exit");

                //获取cmd窗口的输出信息  
                //string output = process.StandardOutput.ReadToEnd();

                process.WaitForExit();
                process.Close();
            }
        }
    }
}
