using System;

namespace SmartNanjingTravel.Models
{
    public class FavoriteItem
    {
        public int FavoriteId { get; set; }
        public string UserId { get; set; }
        public int PoiId { get; set; }
        public string Name { get; set; }          // 景点名称
        public string Description { get; set; }   

        public string District { get; set; }
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Rating { get; set; }
        public string OpenTime { get; set; }
        public string Photos { get; set; }
        public string Notes { get; set; }
        public DateTime FavoriteTime { get; set; }

        // 显示属性
        public string AddedDate => FavoriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        public string DisplayRating
        {
            get
            {
                if (string.IsNullOrEmpty(Rating) || Rating == "暂无评分" || Rating == "0")
                    return "暂无评分";
                return $"{Rating}分";
            }
        }

        public string ShortDescription
        {
            get
            {
                if (!string.IsNullOrEmpty(Description))
                    return Description.Length > 30 ? Description.Substring(0, 30) + "..." : Description;
                return $"位于{District}的景点";
            }
        }

    }
}
