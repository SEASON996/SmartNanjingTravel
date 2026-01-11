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

        // 保存各层实例
        private WebTiledLayer _originalGaodeLayer;     // 原高德底图（style=8）
        private WebTiledLayer _satelliteImageLayer;    // 卫星影像（style=6）
        private WebTiledLayer _satelliteLabelLayer;    // 卫星模式专用注记（style=8，独立实例）

        public MapViewModel()
        {
            InitializeMap();
            _geodatabaseService = new Services.GeodatabaseService();
            InitializeMap();                 // 原有方法
            AddSatelliteBasemapOption();     // 新增卫星选项
        }

        private void InitializeMap()
        {
            _map = new Map(SpatialReferences.WebMercator)
            {
                InitialViewpoint = new Viewpoint(new Envelope(118.5, 31.5, 119.5, 32.5,
            Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84))
            };

            SetAmapBaseMap();
        }

        // 初始加载高德底图
        private void SetAmapBaseMap()
        {
            // 你原来的高德底图（style=8）
            _originalGaodeLayer = new Esri.ArcGISRuntime.Mapping.WebTiledLayer(
            "https://webrd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={col}&y={row}&z={level}",
            new System.Collections.Generic.List<string> { "1", "2", "3", "4", "5" })
            {
                Attribution = "Map data ©2015 AutoNavi - GS(2015)2080号",
                Name = "标准地图",
                IsVisible = true
            };

            var basemap = new Basemap();
            basemap.BaseLayers.Add(_originalGaodeLayer);
            _map.Basemap = basemap;

            LayerItems.Add(new LayerItem
            {
                Name = "标准地图",
                Layer = _originalGaodeLayer,
                IsVisible = true
            });

            // 订阅互斥切换事件
            LayerItems[0].PropertyChanged += BasemapItem_PropertyChanged;
        }

        // 新增：添加卫星地图选项
        private void AddSatelliteBasemapOption()
        {
            // 卫星影像层（style=6，使用当前稳定域名模板）
            _satelliteImageLayer = new WebTiledLayer(
                "https://webrd0{s}.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=6&x={col}&y={row}&z={level}",
                new List<string> { "1", "2", "3", "4" })
            {
                Name = "高德卫星影像",
                Attribution = "© AutoNavi"
            };

            // 卫星模式专用注记层（style=8，独立实例，与原底图参数一致）
            _satelliteLabelLayer = new WebTiledLayer(
                "https://webrd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={col}&y={row}&z={level}",
                new List<string> { "1", "2", "3", "4", "5" })
            {
                Name = "高德卫星注记",
                Attribution = "© AutoNavi"
            };

            var satelliteItem = new LayerItem
            {
                Name = "卫星地图",
                Layer = _satelliteImageLayer,  // 用影像层代表整个卫星选项
                IsVisible = false
            };

            LayerItems.Add(satelliteItem);

            // 订阅事件
            satelliteItem.PropertyChanged += BasemapItem_PropertyChanged;
        }

        // 处理底图互斥切换
        private void BasemapItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LayerItem.IsVisible)) return;

            var selectedItem = sender as LayerItem;
            // 只有当 IsVisible 变为 true 时才触发切换逻辑
            if (selectedItem == null || !selectedItem.IsVisible) return;

            // 1. 互斥逻辑：取消选中其他底图标注
            foreach (var item in LayerItems)
            {
                if (item != selectedItem)
                {
                    // 暂时移除事件监听防止死循环
                    item.PropertyChanged -= BasemapItem_PropertyChanged;
                    item.IsVisible = false;
                    item.PropertyChanged += BasemapItem_PropertyChanged;
                }
            }

            // 2. 创建新的底图对象
            Basemap newBasemap = new Basemap();

            if (selectedItem.Name == "标准地图")
            {
                // 确保使用街道样式的 URL (style=7 或 8)
                var streetLayer = new WebTiledLayer("https://webst01.is.autonavi.com/appmaptile?style=7&x={col}&y={row}&z={level}");
                newBasemap.BaseLayers.Add(streetLayer);
            }
            else if (selectedItem.Name == "卫星地图")
            {
                // 影像图层 (style=6)
                var satelliteImage = new WebTiledLayer("https://webst01.is.autonavi.com/appmaptile?style=6&x={col}&y={row}&z={level}");
                // 路网注记层 (style=8)
                var satelliteLabel = new WebTiledLayer("https://webst01.is.autonavi.com/appmaptile?style=8&x={col}&y={row}&z={level}");

                newBasemap.BaseLayers.Add(satelliteImage);
                newBasemap.BaseLayers.Add(satelliteLabel);
            }

            if (_map != null)
            {
                _map.Basemap = newBasemap;
            }
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
                // 加载路线图层
                var routeLayers = await _geodatabaseService.LoadRouteLayers(routeName);
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
                // 为点图层创建符号
                if (routeLayers.PointLayer != null)
                {
                    Uri iconUri = new Uri("pack://application:,,,/SmartNanjingTravel;component/image/icons1.png");

                    // 创建图片符号
                    Esri.ArcGISRuntime.Symbology.PictureMarkerSymbol picSymbol = new Esri.ArcGISRuntime.Symbology.PictureMarkerSymbol(iconUri)
                    {
                        Width = 20,
                        Height = 35,
                        OffsetY = 12
                    };

                    routeLayers.PointLayer.Renderer = new SimpleRenderer(picSymbol);
                }

                // 为线图层创建符号
                if (routeLayers.RouteLayer != null)
                {
                    var lineSymbol = new SimpleLineSymbol()
                    {
                        Style = SimpleLineSymbolStyle.Solid,
                        Color = System.Drawing.Color.FromArgb(255, 103, 58, 183), 
                        Width = 6
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

/*        /// 创建点渲染器
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
        }*/


        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}