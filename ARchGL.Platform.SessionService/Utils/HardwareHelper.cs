using System;
using System.Collections.Specialized;
using System.Management;
using System.Net;
using System.Net.Sockets;

namespace ARchGL.Platform.SessionService
{
    public class HardwareHelper
    {
        public string CpuID; //1.cpu序列号
        public string MacAddress; //2.mac序列号
        public string DiskID; //3.硬盘id
        public string IpAddress; //4.ip地址
        public string LoginUserName; //5.登录用户名
        public string ComputerName; //6.计算机名
        public string SystemType; //7.系统类型
        public string TotalPhysicalMemory; //8.内存量 单位：M

        public HardwareHelper()
        {
            CpuID = GetCpuID(); // 1.获取CPU序列号代码
            MacAddress = GetMacAddress(); // 2.获取网卡硬件地址
            DiskID = GetDiskID(); // 3.获取硬盘ID 
            //IpAddress = GetIPAddress(); //4.获取IP地址
            //LoginUserName = Environment.UserName; // 5.操作系统的登录用户名 
            //ComputerName = Environment.MachineName; //6.获取计算机名
            //SystemType = GetInfo("Win32_ComputerSystem", "SystemType"); //7 PC类型 
            //TotalPhysicalMemory = GetInfo("Win32_ComputerSystem", "TotalPhysicalMemory"); //8.物理内存
        }

        /// <summary>
        /// 获取 {CpuID}|{MacAddress}|{DiskID} 信息
        /// <para>返回格式及内容不能随意修改，在接收端会根据格式验证当前客户端是不是规则内的客户端，以避免来路不明的客户端接入</para>
        /// </summary>
        public string GetInfo()
        {
            return $"{CpuID}|{MacAddress}|{DiskID}";
        }

        #region Get Hardware info

        //1.获取CPU序列号代码
        string GetCpuID()
        {
            try
            {
                using (var moc = GetManagementObjectCollection("Win32_Processor"))
                {
                    foreach (ManagementObject mo in moc)
                        return mo.Properties["ProcessorId"].Value.ToString();
                    return "unknow";
                }
            }
            catch
            {
                return "unknow";
            }
        }

        //2.获取网卡硬件地址
        string GetMacAddress()
        {
            try
            {
                using (var moc = GetManagementObjectCollection("Win32_NetworkAdapterConfiguration"))
                {
                    foreach (ManagementObject mo in moc)
                    {
                        if ((bool)mo["IPEnabled"] == true)
                        {
                            return mo["MacAddress"].ToString();
                        }
                    }
                }
                return "unknow";
            }
            catch
            {
                return "unknow";
            }
        }

        //3.获取硬盘ID 
        string GetDiskID()
        {
            try
            {
                using (var moc = GetManagementObjectCollection("Win32_DiskDrive"))
                {
                    foreach (ManagementObject mo in moc)
                        //return mo.Properties["Model"].Value.ToString();
                        return mo.Properties["SerialNumber"].Value.ToString().Trim(); 
                    return "unknow";
                }
            }
            catch
            {
                return "unknow";
            }
        }

        //4.获取IP地址
        string GetIPAddress()
        {
            try
            {
                //事先不知道ip的个数，数组长度未知，因此用StringCollection储存
                var localIPs = Dns.GetHostAddresses(Dns.GetHostName());
                var ipCollection = new StringCollection();
                foreach (IPAddress ip in localIPs)
                {
                    //根据AddressFamily判断是否为ipv4,如果是InterNetWork则为ipv6
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        ipCollection.Add(ip.ToString());
                }
                var ipArray = new string[ipCollection.Count];
                ipCollection.CopyTo(ipArray, 0);

                return string.Join(",", ipArray);//返回字IPv4符串
            }
            catch
            {
                return "unknow";
            }
        }

        #endregion

        #region Private method

        private string GetInfo(string path, string name)
        {
            try
            {
                using (var moc = GetManagementObjectCollection(path))
                {
                    foreach (ManagementObject mo in moc)
                        return mo[name].ToString();
                    return "unknow";
                }
            }
            catch
            {
                return "unknow";
            }
        }

        private ManagementObjectCollection GetManagementObjectCollection(string path)
        {
            using (var mc = new ManagementClass(path))
            {
                return mc.GetInstances();
            }
        }

        #endregion
    }
}
