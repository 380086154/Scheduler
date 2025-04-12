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
            
            var servicesToRun = new ServiceBase[]
            {
                new SessionService()
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
