using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartNanjingTravel.Models
{
    /// <summary>
    /// 收藏项数据模型
    /// </summary>
    public class FavoriteItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }  // "景点", "路线"
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public DateTime AddedDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Location { get; set; } // 详细地址
    }
}
