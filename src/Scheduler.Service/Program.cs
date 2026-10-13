using System;
using System.IO;
using System.ServiceProcess;

namespace ARchGL.Platform.SessionService
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main()
        {
#if DEBUG
            var service = new SessionService();
            service.SendGogsCommitRecord();
#endif
            try
            {
                var servicesToRun = new ServiceBase[]
                {
                new SessionService()
                };
                ServiceBase.Run(servicesToRun);
            }
            catch (Exception ex)
            {
                var path = AppDomain.CurrentDomain.BaseDirectory;

                File.WriteAllText("C:\\service_crash.log", path + "\r\n" + ex.ToString());
            }
        }
    }
}
