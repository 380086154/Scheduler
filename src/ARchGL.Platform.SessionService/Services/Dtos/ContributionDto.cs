using System;

namespace ARchGL.Platform.SessionService.Services.Dtos
{
    public class ContributionDto
    {
        /// <summary>时间戳</summary>
        public long Timestamp { get; set; }

        /// <summary>贡献</summary>
        public int Contributions { get; set; }

        /// <summary>获取时间</summary>
        public DateTime GetTime()
        {
            var startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1)); // 当地时区
            return startTime.AddSeconds(Timestamp);
        }
    }
}
