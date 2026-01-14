using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartNanjingTravel.Converters;

namespace SmartNanjingTravel.Models.Amap
{
    public class PoiSearchResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("info")]
        public string Info { get; set; }

        [JsonProperty("infocode")]
        public string InfoCode { get; set; }

        [JsonProperty("count")]
        public string Count { get; set; }

        [JsonProperty("pois")]
        public List<PoiItem> Pois { get; set; }
    }

    public class PoiItem
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("adname")]
        public string Adname { get; set; }

        [JsonProperty("biz_ext")]
        public BizExt BizExt { get; set; }

        [JsonProperty("photos")]
        public List<PhotoItem> Photos { get; set; }
    }

    public class PhotoItem
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class BizExt
    {
        [JsonProperty("opentime2")]
        public object OpenTime { get; set; }

        [JsonProperty("rating")]
        [JsonConverter(typeof(EmptyArrayToNullConverter))]
        public string Rating { get; set; }
    }

    public class AddressInfo
    {
        public string Address { get; set; }
        public string Longitude { get; set; }
        public string Latitude { get; set; }
        public string Adname { get; set; }
        public string Rating { get; set; }
        public string Name { get; set; }
        public string Opentime { get; set; }
        public string Photos { get; set; }
    }
}

