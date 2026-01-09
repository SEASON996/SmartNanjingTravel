// GeodatabaseService.cs
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartNanjingTravel.Services
{
    public class GeodatabaseService
    {
        private readonly string? _geodatabasePath;

        public string? GeodatabasePath 
        {
            get => _geodatabasePath;
        }

        private Geodatabase _geodatabase;

        public GeodatabaseService()
        {
            // 获取当前程序集所在目录
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(baseDirectory, "Data");

            // 移动地理数据库文件路径
            _geodatabasePath = System.IO.Path.Combine(dataDirectory, "Smart Traveling.geodatabase");
        }

        // 获取所有可用的路线名称

        public async Task<List<string>> GetAvailableRouteNames(string gdbPath)
        {
            var routeNames = new List<string>();

            try
            {
                if (!File.Exists(gdbPath))
                {
                    return routeNames;
                }

                _geodatabase = await Geodatabase.OpenAsync(gdbPath);

                if (_geodatabase == null)
                {
                    return routeNames;
                }

                // 找出所有以_points结尾的表，提取路线名称
                foreach (var table in _geodatabase.GeodatabaseFeatureTables)
                {
                    string tableName = table.TableName ?? "";
                    if (tableName.EndsWith("_points", StringComparison.OrdinalIgnoreCase))
                    {
                        string routeName = tableName.Substring(0, tableName.Length - "_points".Length);
/*                        // 移除可能的"main."前缀
                        if (routeName.StartsWith("main."))
                            routeName = routeName.Substring(5);*/
                        routeNames.Add(routeName);
                    }
                }

                return routeNames.Distinct().ToList();
            }

            catch (Exception ex)
            {
                Console.WriteLine($"获取路线名称失败: {ex.Message}");
                return routeNames;
            }

            finally
            {
                Close();
            }
        }

        /// 加载指定路线的图层
        /// 
        public async Task<RouteLayers> LoadRouteLayers(string routeName, string gdbPath)
        {
            var result = new RouteLayers { RouteName = routeName };

            try
            {
                if (!File.Exists(gdbPath))
                {
                    result.ErrorMessage = $"数据库文件不存在: {gdbPath}";
                    return result;
                }

                _geodatabase = await Geodatabase.OpenAsync(gdbPath);

                if (_geodatabase == null)
                {
                    result.ErrorMessage = "无法打开地理数据库";
                    return result;
                }

                // 查找点图层（尝试多种命名方式）
                string[] pointTableNames = {
                    $"{routeName}_points",
                    $"main.{routeName}_points",
                    $"{routeName}_point",
                    $"main.{routeName}_point"
                };

                // 查找线图层（尝试多种命名方式）
                string[] lineTableNames = {
                    $"{routeName}_lines",
                    $"main.{routeName}_lines",
                    $"{routeName}_line",
                    $"main.{routeName}_line",
                    $"{routeName}_route",
                    $"main.{routeName}_route"
                };

                // 加载点图层
                var pointTable = FindGeodatabaseTable(pointTableNames);
                if (pointTable != null)
                {
                    result.PointLayer = new FeatureLayer(pointTable)
                    {
                        Name = $"{routeName} - 景点",
                        IsVisible = true
                    };
                }

                // 加载线图层
                var lineTable = FindGeodatabaseTable(lineTableNames);
                if (lineTable != null)
                {
                    result.RouteLayer = new FeatureLayer(lineTable)
                    {
                        Name = $"{routeName} - 路线",
                        IsVisible = true
                    };
                }

                result.IsLoaded = result.PointLayer != null || result.RouteLayer != null;

                if (!result.IsLoaded)
                {
                    result.ErrorMessage = $"未找到路线'{routeName}'的图层";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"加载失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 查找表
        /// </summary>
        private GeodatabaseFeatureTable FindGeodatabaseTable(string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                var table = _geodatabase.GeodatabaseFeatureTables
                    .FirstOrDefault(t => t.TableName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
                if (table != null)
                    return table;
            }
            return null;
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void Close()
        {
            _geodatabase?.Close();
        }
    }

    /// <summary>
    /// 路线图层结果类
    /// </summary>
    public class RouteLayers
    {
        public string RouteName { get; set; }
        public bool IsLoaded { get; set; }
        public string ErrorMessage { get; set; }

        public FeatureLayer PointLayer { get; set; }
        public FeatureLayer RouteLayer { get; set; }

        public List<Layer> GetAllLayers()
        {
            var layers = new List<Layer>();
            if (PointLayer != null) layers.Add(PointLayer);
            if (RouteLayer != null) layers.Add(RouteLayer);
            return layers;
        }
    }
}