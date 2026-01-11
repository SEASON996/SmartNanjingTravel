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
            _geodatabasePath = System.IO.Path.Combine(dataDirectory, "Travel.geodatabase");
        }

        // 获取所有可用的路线名称

/*        public async Task<List<string>> GetAvailableRouteNames(string gdbPath)
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
*//*                        // 移除可能的"main."前缀
                        if (routeName.StartsWith("main."))
                            routeName = routeName.Substring(5);*//*
                        routeNames.Add(routeName);
                    }
                }

                return routeNames.Distinct().ToList();
            }*/

/*            catch (Exception ex)
            {
                Console.WriteLine($"获取路线名称失败: {ex.Message}");
                return routeNames;
            }

            finally
            {
                Close();
            }
        }*/

        public async Task<RouteLayers> LoadRouteLayers(string routeName)
        {
            var result = new RouteLayers { RouteName = routeName };

            try
            {
                if (!File.Exists(_geodatabasePath))
                {
                    result.ErrorMessage = $"数据库文件不存在: {_geodatabasePath}";
                    return result;
                }

               _geodatabase = await Geodatabase.OpenAsync(_geodatabasePath);

                if (_geodatabase == null)
                {
                    result.ErrorMessage = "无法打开地理数据库";
                    return result;
                }

                // 加载点图层
                string pointTableName = $"{routeName}_points";
                var pointTable = FindGeodatabaseTable(pointTableName);
                if (pointTable != null)
                {
                    result.PointLayer = new FeatureLayer(pointTable)
                    {
                        Name = $"{routeName} - 景点",
                        IsVisible = true
                    };
                }

                // 加载线图层
                string lineTableName = $"{routeName}_line";
                var lineTable = FindGeodatabaseTable(lineTableName);
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
        private GeodatabaseFeatureTable FindGeodatabaseTable(string tableName)
        {
            return _geodatabase.GeodatabaseFeatureTables
                .FirstOrDefault(t => t.TableName?.Equals(tableName, StringComparison.OrdinalIgnoreCase) == true);
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