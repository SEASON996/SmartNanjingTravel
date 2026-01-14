using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using JsonEx = System.Text.Json.JsonException;

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

        public async Task<List<AddressInfo>> QueryPoiAsync(string keyword, string city, int maxResults = 40, int offset = 20)
        {
            var allResults = new List<AddressInfo>();
            int page = 1;
            bool firstPage = true;
            int totalCount = 0;

            while (allResults.Count < maxResults)
            {
                try
                {
                    string url = $"https://restapi.amap.com/v3/place/text?" +
                                 $"keywords={Uri.EscapeDataString(keyword)}" +
                                 $"&city={Uri.EscapeDataString(city)}" +
                                 $"&offset={offset}" +
                                 $"&page={page}" +
                                 $"&key={ApiKey}" +
                                 "&extensions=all" +          // 建议加上，获取更完整的信息
                                 "&output=JSON";

                    var response = await _httpClient.GetStringAsync(url);

                    using (JsonDocument doc = JsonDocument.Parse(response))
                    {
                        var root = doc.RootElement;

                        if (!root.TryGetProperty("status", out var status) || status.GetString() != "1")
                        {
                            string info = root.TryGetProperty("info", out var inf) ? inf.GetString() : "未知错误";
                            throw new Exception($"高德API返回错误：{info}");
                        }

                        // 第一页读取总数（可选，用于提前判断是否需要继续翻页）
                        if (firstPage)
                        {
                            if (root.TryGetProperty("count", out var countProp) && int.TryParse(countProp.GetString(), out int cnt))
                            {
                                totalCount = cnt;
                            }
                            firstPage = false;
                        }

                        if (!root.TryGetProperty("pois", out var pois) || pois.ValueKind != JsonValueKind.Array)
                        {
                            break; // 没有更多数据
                        }

                        foreach (var poi in pois.EnumerateArray())
                        {
                            var info = new AddressInfo();

                            // 基本字段
                            info.Name = poi.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            info.Adname = poi.TryGetProperty("adname", out var ad) ? ad.GetString() ?? "" : "";
                            info.Address = poi.TryGetProperty("address", out var addr) ? addr.GetString() ?? "" : "";

                            // rating 和 open_time 通常在 biz_ext 里
                            if (poi.TryGetProperty("biz_ext", out var biz) && biz.ValueKind == JsonValueKind.Object)
                            {
                                info.Rating = biz.TryGetProperty("rating", out var r) ? r.GetString() ?? "" : "";

                                // 尝试多个可能的字段名
                                if (biz.TryGetProperty("open_time", out var ot))
                                {
                                    info.Opentime = ot.GetString() ?? "";
                                }
                                else if (biz.TryGetProperty("opentime", out var ot1))
                                {
                                    info.Opentime = ot1.GetString() ?? "";
                                }
                                else if (biz.TryGetProperty("opentime2", out var ot2))
                                {
                                    info.Opentime = ot2.GetString() ?? "";
                                }
                                else
                                {
                                    info.Opentime = "营业时间未知";
                                }
                            }

                            // 经纬度 - 更安全的写法
                            if (poi.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.String)
                            {
                                var parts = loc.GetString()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
                                if (parts.Length >= 2)
                                {
                                    info.Longitude = parts[0];
                                    info.Latitude = parts[1];
                                }
                            }

                            // Photos - 兼容两种常见格式（字符串数组 或 对象数组）
                            info.Photos = "";
                            if (poi.TryGetProperty("photos", out var photosElement)
                                && photosElement.ValueKind == JsonValueKind.Array
                                && photosElement.GetArrayLength() > 0)
                            {
                                var firstItem = photosElement[0];

                                // 格式1：直接是图片链接字符串
                                if (firstItem.ValueKind == JsonValueKind.String)
                                {
                                    info.Photos = firstItem.GetString() ?? "";
                                }
                                // 格式2：是对象，里面有 url 字段
                                else if (firstItem.ValueKind == JsonValueKind.Object)
                                {
                                    if (firstItem.TryGetProperty("url", out var urlElement)
                                        && urlElement.ValueKind == JsonValueKind.String)
                                    {
                                        info.Photos = urlElement.GetString() ?? "";
                                    }
                                }
                            }

                            allResults.Add(info);

                            // 达到目标数量即可停止
                            if (allResults.Count >= maxResults)
                                break;
                        }

                        // 如果本页返回的数量少于 offset，说明已到最后一页
                        if (pois.GetArrayLength() < offset)
                        {
                            break;
                        }

                        page++;

                        // 高德最大页数限制（通常50页）
                        if (page > 50)
                            break;

                        // 可选：避免请求太频繁，稍作延迟（视你的 Key 限额而定）
                        // await Task.Delay(300);
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"网络请求失败 (page {page}): {ex.Message}");
                    break;
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"查询 POI 异常 (page {page}): {ex.Message}");
                    break;
                }
            }

            // 如果实际获取的超过 maxResults，进行截取（虽然 while 条件已控制，但以防万一）
            if (allResults.Count > maxResults)
            {
                allResults = allResults.Take(maxResults).ToList();
            }

            return allResults;
        }
    }
}
