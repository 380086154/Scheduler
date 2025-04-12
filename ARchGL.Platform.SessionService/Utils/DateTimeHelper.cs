using System;
using System.Globalization;

namespace ARchGL.Platform.SessionService.Utils
{
    public static class DateTimeHelper
    {
        public static DateTime asdfasdf(string str)
        {
            //string dateString = "Fri, 11 Apr 2025 11:26:52 CST";
            var format = "ddd, dd MMM yyyy HH:mm:ss 'CST'";
            var provider = CultureInfo.InvariantCulture;

            DateTime dateTime = DateTime.MinValue;

            try
            {
                dateTime = DateTime.ParseExact(str, format, provider);
                //Console.WriteLine("转换后的 DateTime 对象:");
                //Console.WriteLine(dateTime);

                //// 如果需要考虑时区，可以转换为 UTC
                //DateTime utcDateTime = DateTime.ParseExact(dateString, format, provider, DateTimeStyles.AssumeUniversal);
                //Console.WriteLine("\n转换为 UTC 的 DateTime 对象:");
                //Console.WriteLine(utcDateTime);
            }
            catch (FormatException)
            {
                Console.WriteLine("日期格式不正确。");
            }

            return dateTime;
        }
    }
}
