using System.Management.Instrumentation;
using System.Text.RegularExpressions;

namespace ARchGL.Platform.SessionService
{
    /// <summary>
    /// 正则工具类
    /// </summary>
    public class RegexHelper
    {
        public static RegexHelper Instance = new RegexHelper();

        //根据正则返回集合
        public MatchCollection GetListByRegex(string regex, string html)
        {
            var reg = new Regex(regex, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return reg.Matches(html);
        }

        /// <summary>根据正则返回值</summary>
        /// <param name="regex"></param>
        /// <param name="html"></param>
        /// <returns></returns>
        public string GetValueByRegex(string regex, string html)
        {
            var reg = new Regex(regex, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var value = reg.Match(html).Groups[1].Value;

            return value.Trim();
        }
    }
}
