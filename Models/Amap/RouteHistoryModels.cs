using System.Collections.Generic;

namespace SmartNanjingTravel.Models
{
    /// <summary>
    /// 路线记录项
    /// </summary>
    public class RouteRecordItem
    {
        public string SummaryText { get; set; } // 例如 "总共 45分钟 | 20公里"
        public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();
    }

    /// <summary>
    /// 路线段
    /// </summary>
    public class RouteSegment
    {
        public string From { get; set; }  // 本段起点
        public string To { get; set; }    // 本段终点
        public string Detail { get; set; } // 本段详情

        public string IconColor => "Green"; // 用于界面显示的图标颜色逻辑
    }
}