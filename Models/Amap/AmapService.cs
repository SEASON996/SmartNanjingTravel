using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SmartNanjingTravel.Models.Amap
{
    public class AmapService
    {
        private const string ApiKey = "88eb4252bbd3f175d95e1fe5501da57c";
        private const string GeocodeApiUrl = "https://restapi.amap.com/v3/place/text";

        private readonly HttpClient _httpClient;

        public AmapService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task<List<AddressInfo>> QueryPoiAsync(string keywords, string city = "南京")
        {
            if (string.IsNullOrEmpty(keywords))
                throw new ArgumentException("查询关键词不能为空");

            var requestUrl = $"{GeocodeApiUrl}?keywords={Uri.EscapeDataString(keywords)}&city={city}&key={ApiKey}";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            string jsonResult = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PoiSearchResponse>(jsonResult);

            if (result.Status != "1" || result.Pois == null || result.Pois.Count == 0)
                MessageBox.Show($"暂无此景点");
            return ConvertPoisToAddressInfo(result.Pois, keywords);
        }

        private List<AddressInfo> ConvertPoisToAddressInfo(List<PoiItem> pois, string originalQuery)
        {
            var addressInfoList = new List<AddressInfo>();

            foreach (var item in pois)
            {
                var location = string.IsNullOrEmpty(item.Location) ?
                    new string[] { "无", "无" } : item.Location.Split(',');

                var longitude = location.Length >= 1 ? location[0] : "无";
                var latitude = location.Length >= 2 ? location[1] : "无";
                var adname = string.IsNullOrEmpty(item.Adname) ? "无" : item.Adname;

                var rating = "暂无评分";
                if (item.BizExt != null && !string.IsNullOrEmpty(item.BizExt.Rating))
                {
                    rating = item.BizExt.Rating;
                }

                var opentime = "暂无";
                if (item.BizExt != null && item.BizExt.OpenTime != null)
                {
                    opentime = item.BizExt.OpenTime.ToString() ?? "暂无";
                }

                var photos = "暂无照片";
                if (item.Photos != null && item.Photos.Count > 0)
                {
                    photos = item.Photos[0].Url ?? "暂无照片";
                }

                addressInfoList.Add(new AddressInfo
                {
                    Address = originalQuery,
                    Longitude = longitude,
                    Latitude = latitude,
                    Adname = adname,
                    Rating = rating,
                    Name = item.Name ?? "无",
                    Opentime = opentime,
                    Photos = photos
                });
            }

            return addressInfoList;
        }
    }
}
