using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace ARchGL.Platform.SessionService
{
    public class HttpRequestHelper
    {
        public static string RequestUrl(string url, Dictionary<string, object> dict)
        {
            // 设置参数
            var request = WebRequest.Create(AppConsts.host + url);
            request.Credentials = CredentialCache.DefaultCredentials;
            //var cookieContainer = new CookieContainer();
            //request.CookieContainer = cookieContainer;
            //request.AllowAutoRedirect = false;
            request.Method = "POST";
            request.ContentType = "application/json";

            if (dict != null)
            {
                var json = JsonConvert.SerializeObject(dict);
                byte[] bs = Encoding.UTF8.GetBytes(json);
                request.ContentLength = bs.Length;
                using (var stream = request.GetRequestStream())
                    stream.Write(bs, 0, bs.Length);
            }

            using (var response = request.GetResponse() as HttpWebResponse) // 发送请求并获取相应回应数据
            using (var responseStream = response.GetResponseStream()) // 直到request.GetResponse()程序才开始向目标网页发送Post请求
            using (var sr = new StreamReader(responseStream, Encoding.UTF8))
            {
                return sr.ReadToEnd(); // 返回结果网页（html）代码
            }
        }

        /// <summary>
        /// 登录并获取 Cookie 信息
        /// </summary>
        /// <param name="url"></param>
        /// <param name="dict"></param>
        /// <param name="method"></param>
        /// <param name="cookieName"></param>
        /// <returns></returns>
        public static HttpWebResponse RequestUrl(string url, Dictionary<string, object> dict, string method = "GET", string cookies = null)
        {
            //1.获取登录Cookie
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.AllowAutoRedirect = false;
            if (cookies != null)
            {
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.SetCookies(request.RequestUri, cookies);
            }
            request.ContentType = "application/x-www-form-urlencoded";

            if (dict != null)
            {
                var json = JsonConvert.SerializeObject(dict);
                byte[] bs = Encoding.UTF8.GetBytes(json);
                request.ContentLength = bs.Length;
                using (var stream = request.GetRequestStream())
                    stream.Write(bs, 0, bs.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();
            return response;
        }

        /// <summary>
        /// 根据完整 Url 发送 Post 请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string RequestFullUrl(string url, string json)
        {
            // 设置参数
            var request = WebRequest.Create(url);
            request.Credentials = CredentialCache.DefaultCredentials;
            request.Method = "POST";
            request.ContentType = "application/json";

            if (string.IsNullOrWhiteSpace(json)) return null;

            byte[] bs = Encoding.UTF8.GetBytes(json);
            request.ContentLength = bs.Length;
            using (var stream = request.GetRequestStream())
                stream.Write(bs, 0, bs.Length);

            using (var response = request.GetResponse() as HttpWebResponse) // 发送请求并获取相应回应数据
            using (var responseStream = response.GetResponseStream()) // 直到request.GetResponse()程序才开始向目标网页发送Post请求
            using (var sr = new StreamReader(responseStream, Encoding.UTF8))
            {
                return sr.ReadToEnd(); //返回结果网页（html）代码
            }
        }

        public static string Request(string url)
        {
            // Create a request for the URL. 		
            var request = WebRequest.Create(AppConsts.host + url);
            // If required by the server, set the credentials.
            request.Credentials = CredentialCache.DefaultCredentials;
            // Get the response.
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream dataStream = response.GetResponseStream()) // Get the stream containing content returned by the server.
            using (var reader = new StreamReader(dataStream)) // Open the stream using a StreamReader for easy access.
            {
                // Display the status.
                //Console.WriteLine(response.StatusDescription);                

                // Read the content.
                var responseFromServer = reader.ReadToEnd();
                // Display the content.
                //Console.WriteLine(responseFromServer);

                return responseFromServer;
            }
        }

        public static string GetJsonValue(string jsonStr, string key)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(jsonStr))
            {
                key = "\"" + key.Trim('"') + "\"";
                int index = jsonStr.IndexOf(key) + key.Length + 1;
                if (index > key.Length + 1)
                {
                    //先截逗号，若是最后一个，截“｝”号，取最小值
                    int end = jsonStr.IndexOf(',', index);
                    if (end == -1)
                    {
                        end = jsonStr.IndexOf('}', index);
                    }

                    result = jsonStr.Substring(index, end - index);
                    result = result.Trim(new char[] { '"', ' ', '\'' }); //过滤引号或空格
                }
            }
            return result;
        }
    }
}
