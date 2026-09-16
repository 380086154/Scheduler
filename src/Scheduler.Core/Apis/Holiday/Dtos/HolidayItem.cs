using System;
using System.Collections.Generic;
using System.Text;

namespace Scheduler.Core.Apis.Holiday.Dtos;

public class HolidayItem
{
    /// <summary>true=节假日休息；false=调休补班</summary>
    public bool Holiday { get; set; }

    public string Name { get; set; } = "";

    /// <summary>工资倍数：1 补班 / 2 周末 / 3 节假日</summary>
    public int Wage { get; set; }

    public string Date { get; set; } = "";

    public int Rest { get; set; }

    public bool? After { get; set; }

    public string? Target { get; set; }
}
