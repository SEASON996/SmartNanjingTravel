using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SmartNanjingTravel.Services
{
    public class AmapRouteService
    {
        private const string ApiKey = "88eb4252bbd3f175d95e1fe5501da57c";
        private readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// 获取驾车路线路径
        /// </summary>
        public async Task<RouteResult> GetDrivingRoutePathAsync(string startAddr, string endAddr, List<string> viaAddrs)
        {
            try
            {
                // 1. 解析所有地址为坐标
                var startLoc = await GetLocationByAddressAsync(startAddr);
                if (startLoc == null)
                {
                    MessageBox.Show($"无法识别起点：{startAddr}");
                    return null;
                }

                var endLoc = await GetLocationByAddressAsync(endAddr);
                if (endLoc == null)
                {
                    MessageBox.Show($"无法识别终点：{endAddr}");
                    return null;
                }

                var viaLocs = new List<GeoPoint>();
                foreach (var addr in viaAddrs)
                {
                    if (!string.IsNullOrWhiteSpace(addr))
                    {
                        var p = await GetLocationByAddressAsync(addr);
                        if (p != null) viaLocs.Add(p);
                    }
                }

                // 2. 将所有点连成一个序列: Start -> Via1 -> Via2 -> End
                var sequence = new List<GeoPoint> { startLoc };
                sequence.AddRange(viaLocs);
                sequence.Add(endLoc);

                // 3. 结果容器
                var finalResult = new RouteResult();

                // 累计总时间和距离
                double totalSeconds = 0;
                double totalMeters = 0;

                // 4. 分段请求 (Step-by-Step Request)
                for (int i = 0; i < sequence.Count - 1; i++)
                {
                    var p1 = sequence[i];
                    var p2 = sequence[i + 1];

                    // 这里的 RequestDrivingRouteAsync 只请求两个点，不带 waypoints
                    var segmentResult = await RequestDrivingRouteAsync(p1, p2, null);

                    if (segmentResult != null)
                    {
                        // 累加所有的坐标点
                        finalResult.Points.AddRange(segmentResult.Points);

                        // 解析这一段的时间和距离，存入 SegmentDetails
                        string segDuration = segmentResult.DurationText;
                        string segDistance = segmentResult.DistanceText;

                        finalResult.SegmentDetails.Add($"{segDuration}|{segDistance}");

                        // 累加总数值
                        totalSeconds += ParseDurationToSeconds(segmentResult.DurationText);
                        totalMeters += ParseDistanceToMeters(segmentResult.DistanceText);
                    }
                }

                // 5. 重新格式化总时间和总距离
                finalResult.DurationText = totalSeconds < 60 ? "< 1分钟" : $"{totalSeconds / 60:F0} 分钟";
                finalResult.DistanceText = totalMeters >= 1000 ? $"{totalMeters / 1000:F1} 公里" : $"{totalMeters:F0} 米";

                return finalResult;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"流程出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析持续时间到秒数
        /// </summary>
        private double ParseDurationToSeconds(string text)
        {
            try
            {
                string num = System.Text.RegularExpressions.Regex.Replace(text, "[^0-9]", "");
                if (int.TryParse(num, out int val)) return val * 60;
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 解析距离到米数
        /// </summary>
        private double ParseDistanceToMeters(string text)
        {
            try
            {
                if (text.Contains("公里"))
                {
                    string num = text.Replace(" 公里", "").Replace("公里", "");
                    if (double.TryParse(num, out double val)) return val * 1000;
                }
                else // 米
                {
                    string num = text.Replace(" 米", "").Replace("米", "");
                    if (double.TryParse(num, out double val)) return val;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 通过地址获取地理坐标
        /// </summary>
        private async Task<GeoPoint> GetLocationByAddressAsync(string keyword)
        {
            try
            {
                string url = $"https://restapi.amap.com/v3/place/text?keywords={keyword}&city=南京&output=JSON&key={ApiKey}";

                var json = await _httpClient.GetStringAsync(url);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("status", out var status) && status.GetString() == "1")
                    {
                        if (root.TryGetProperty("pois", out var pois) && pois.GetArrayLength() > 0)
                        {
                            var firstPoi = pois[0];

                            if (firstPoi.TryGetProperty("location", out var locProp))
                            {
                                string locStr = locProp.GetString();
                                if (!string.IsNullOrEmpty(locStr))
                                {
                                    var parts = locStr.Split(',');
                                    if (parts.Length == 2)
                                    {
                                        double lon = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                                        double lat = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                                        return new GeoPoint(lon, lat);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 请求驾车路线
        /// </summary>
        private async Task<RouteResult> RequestDrivingRouteAsync(GeoPoint start, GeoPoint end, string waypoints)
        {
            var result = new RouteResult();
            try
            {
                string url = $"https://restapi.amap.com/v3/direction/driving?origin={start.Longitude:F6},{start.Latitude:F6}&destination={end.Longitude:F6},{end.Latitude:F6}&extensions=base&strategy=0&key={ApiKey}";
                if (!string.IsNullOrEmpty(waypoints)) url += $"&waypoints={waypoints}";

                var json = await _httpClient.GetStringAsync(url);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("status", out var status) && status.GetString() == "1")
                    {
                        if (root.TryGetProperty("route", out var route) && route.TryGetProperty("paths", out var paths))
                        {
                            if (paths.GetArrayLength() > 0)
                            {
                                var path = paths[0];

                                // 1. 获取时间
                                if (path.TryGetProperty("duration", out var durProp))
                                {
                                    int seconds = int.Parse(durProp.GetString());
                                    result.DurationText = seconds < 60 ? "< 1分钟" : $"{seconds / 60} 分钟";
                                }

                                // 2. 获取距离
                                if (path.TryGetProperty("distance", out var distProp))
                                {
                                    double meters = double.Parse(distProp.GetString());
                                    result.DistanceText = meters >= 1000 ? $"{meters / 1000.0:F1} 公里" : $"{meters} 米";
                                }

                                // 3. 获取路线点
                                if (path.TryGetProperty("steps", out var steps))
                                {
                                    foreach (var step in steps.EnumerateArray())
                                    {
                                        if (step.TryGetProperty("polyline", out var polyline))
                                        {
                                            var pointArr = polyline.GetString().Split(';');
                                            foreach (var p in pointArr)
                                            {
                                                var xy = p.Split(',');
                                                if (xy.Length == 2)
                                                    result.Points.Add(new GeoPoint(
                                                        double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
                                                        double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return result;
        }
    }

    /// <summary>
    /// 路线结果
    /// </summary>
    public class RouteResult
    {
        public List<GeoPoint> Points { get; set; } = new List<GeoPoint>();
        public string DurationText { get; set; } = "";
        public string DistanceText { get; set; } = "";
        public List<string> SegmentDetails { get; set; } = new List<string>();
    }

    /// <summary>
    /// 地理坐标点
    /// </summary>
    public class GeoPoint
    {
        public double Longitude { get; set; }
        public double Latitude { get; set; }

        public GeoPoint(double lon, double lat)
        {
            Longitude = lon;
            Latitude = lat;
        }
    }
}
