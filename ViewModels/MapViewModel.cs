// MapViewModel.cs
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI.Controls;
using SmartNanjingTravel.Models;
using SmartNanjingTravel.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace SmartNanjingTravel.ViewModels
{
    public class MapViewModel : INotifyPropertyChanged
    {
        private Map _map;
        private MapView _mapView;

        public Map Map
        {
            get => _map;
            set
            {
                _map = value;
                OnPropertyChanged();
            }
        }

        private readonly GeodatabaseService _geodatabaseService;

        public ObservableCollection<LayerItem> LayerItems { get; set; } = new ObservableCollection<LayerItem>();

        public MapViewModel()
        {
            InitializeMap();
            _geodatabaseService = new Services.GeodatabaseService();
        }

        private void InitializeMap()
        {
            _map = new Map(SpatialReferences.WebMercator)
            {
                InitialViewpoint = new Viewpoint(new Envelope(118.5, 31.5, 119.5, 32.5,
                    SpatialReferences.Wgs84))
            };

            SetAmapBaseMap();
        }

        // 初始加载高德底图
        private void SetAmapBaseMap()
        {
            var gaodeLayer = new WebTiledLayer(
                "https://webrd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={col}&y={row}&z={level}",
                new List<string> { "1", "2", "3", "4", "5" })
            {
                Attribution = "Map data ©2015 AutoNavi - GS(2015)2080号",
                Name = "高德底图",
                IsVisible = true
            };

            var basemap = new Basemap();
            basemap.BaseLayers.Add(gaodeLayer);
            _map.Basemap = basemap;

            LayerItems.Add(new LayerItem
            {
                Name = "高德底图",
                Layer = gaodeLayer,
                IsVisible = gaodeLayer.IsVisible
            });
        }

        public void SetMapView(MapView mapView)
        {
            _mapView = mapView;
        }

        /// 加载游玩推荐路线到地图
        public async Task<bool> LoadRouteToMap(string routeName)
        {
            try
            {
                // 清除现有的路线图层
                ClearRouteLayers();

                // 从App配置获取数据库路径
                string gdbPath = App.GeodatabasePath;
                if (string.IsNullOrEmpty(gdbPath) || !System.IO.File.Exists(gdbPath))
                {
                    MessageBox.Show($"数据库文件不存在: {gdbPath}", "错误");
                    return false;
                }

                // 加载路线图层
                var routeLayers = await _geodatabaseService.LoadRouteLayers(routeName, gdbPath);



                if (!routeLayers.IsLoaded)
                {
                    MessageBox.Show($"加载路线失败: {routeLayers.ErrorMessage}", "错误");
                    return false;
                }

                // 应用符号
                ApplyRouteSymbology(routeLayers);

                // 添加到地图
                var layers = routeLayers.GetAllLayers();
                if (layers.Count == 0)
                {
                    MessageBox.Show("未找到有效的图层", "提示");
                    return false;
                }

                foreach (var layer in layers)
                {
                    Map.OperationalLayers.Add(layer);

                    // 添加到图层控制列表
                    LayerItems.Add(new LayerItem
                    {
                        Name = layer.Name,
                        Layer = layer,
                        IsVisible = layer.IsVisible
                    });
                }

                // 缩放到路线范围
                if (routeLayers.PointLayer != null && routeLayers.PointLayer.FullExtent != null)
                {
                    await _mapView?.SetViewpointAsync(new Viewpoint(routeLayers.PointLayer.FullExtent));
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载路线异常: {ex.Message}", "错误");
                return false;
            }
        }

        // 应用路线符号
        private void ApplyRouteSymbology(Services.RouteLayers routeLayers)
        {
            try
            {
                // 为点图层创建红色圆形符号
                if (routeLayers.PointLayer != null)
                {
                    var pointSymbol = new SimpleMarkerSymbol()
                    {
                        Style = SimpleMarkerSymbolStyle.Circle,
                        Color = System.Drawing.Color.Red,
                        Size = 12,
                        Outline = new SimpleLineSymbol()
                        {
                            Color = System.Drawing.Color.White,
                            Width = 2
                        }
                    };

                    routeLayers.PointLayer.Renderer = new SimpleRenderer(pointSymbol);
                }

                // 为线图层创建蓝色线符号
                if (routeLayers.RouteLayer != null)
                {
                    var lineSymbol = new SimpleLineSymbol()
                    {
                        Style = SimpleLineSymbolStyle.Solid,
                        Color = System.Drawing.Color.Blue,
                        Width = 3
                    };

                    routeLayers.RouteLayer.Renderer = new SimpleRenderer(lineSymbol);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"符号设置失败: {ex.Message}");
            }
        }

        /// 清除所有路线图层
        public void ClearRouteLayers()
        {
            try
            {
                // 从地图中移除
                var layersToRemove = Map.OperationalLayers
                    .Where(l => l.Name != null && (l.Name.Contains("路线") || l.Name.Contains("景点")))
                    .ToList();

                foreach (var layer in layersToRemove)
                {
                    Map.OperationalLayers.Remove(layer);
                }

                // 从LayerItems中移除
                var itemsToRemove = LayerItems
                    .Where(item => item.Name != null && (item.Name.Contains("路线") || item.Name.Contains("景点")))
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    LayerItems.Remove(item);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清除图层失败: {ex.Message}");
            }
        }

        /// 获取可用的路线列表
        public async Task<List<string>> GetAvailableRoutes()
        {
            try
            {
                string gdbPath = _geodatabaseService.GeodatabasePath;
                if (string.IsNullOrEmpty(gdbPath) || !System.IO.File.Exists(gdbPath))
                {
                    MessageBox.Show($"数据库文件不存在: {gdbPath}", "错误");
                    return new List<string>();
                }
                return await _geodatabaseService.GetAvailableRouteNames(gdbPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取路线列表失败: {ex.Message}", "错误");
                return new List<string>();
            }
        }

        /// 创建点渲染器
        private SimpleRenderer CreatePointRenderer()
        {
            var pointSymbol = new SimpleMarkerSymbol()
            {
                Style = SimpleMarkerSymbolStyle.Circle,
                Color = System.Drawing.Color.Red,
                Size = 10,
                Outline = new SimpleLineSymbol()
                {
                    Color = System.Drawing.Color.White,
                    Width = 1.5
                }
            };

            return new SimpleRenderer(pointSymbol);
        }

        /// <summary>
        /// 创建线渲染器（原有的方法，保留）
        /// </summary>
        private SimpleRenderer CreateLineRenderer()
        {
            var lineSymbol = new SimpleLineSymbol()
            {
                Style = SimpleLineSymbolStyle.Solid,
                Color = System.Drawing.Color.Blue,
                Width = 3
            };

            return new SimpleRenderer(lineSymbol);
        }


        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}