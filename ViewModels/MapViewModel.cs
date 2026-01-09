using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        public ObservableCollection<LayerItem> LayerItems { get; set; } = new ObservableCollection<LayerItem>();

        // 保存各层实例
        private WebTiledLayer _originalGaodeLayer;     // 原高德底图（style=8）
        private WebTiledLayer _satelliteImageLayer;    // 卫星影像（style=6）
        private WebTiledLayer _satelliteLabelLayer;    // 卫星模式专用注记（style=8，独立实例）

        public MapViewModel()
        {
            InitializeMap();                 // 原有方法
            AddSatelliteBasemapOption();     // 新增卫星选项
        }

        private void InitializeMap()
        {
            _map = new Map(Esri.ArcGISRuntime.Geometry.SpatialReferences.WebMercator)
            {
                InitialViewpoint = new Viewpoint(new Envelope(118.5, 31.5, 119.5, 32.5,
            Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84))
            };
            SetGaodeBaseMap();
        }

        private void SetGaodeBaseMap()
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

            // 3. 【最关键的一步】将新底图赋值给当前的 Map 对象
            if (_map != null)
            {
                _map.Basemap = newBasemap;
            }
        }

        public void SetMapView(MapView mapView)
        {
            _mapView = mapView;
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