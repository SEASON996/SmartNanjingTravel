using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.UI;

namespace SmartNanjingTravel
{
    /// <summary>
    /// 图层项模型（控制单个图层的显示/隐藏）
    /// </summary>
    public class LayerItem : INotifyPropertyChanged
    {
        private bool _isVisible;
        private Layer _layer;
        private string _name;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public Layer Layer
        {
            get => _layer;
            set { _layer = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                Layer.IsVisible = value; // 同步到ArcGIS图层的可见性
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Provides map data to an application
    /// </summary>
    public class MapViewModel : INotifyPropertyChanged
    {
        private Map _map;
        // 可绑定的图层集合（用于UI控制）
        public ObservableCollection<LayerItem> LayerItems { get; set; } = new ObservableCollection<LayerItem>();

        public MapViewModel()
        {
            _map = new Map(SpatialReferences.WebMercator)
            {
                InitialViewpoint = new Viewpoint(new Envelope(118.5, 31.5, 119.5, 32.5, SpatialReferences.Wgs84))
            };
            SetGaodeBaseMap();
        }

        /// <summary>
        /// Gets or sets the map
        /// </summary>
        public Map Map
        {
            get => _map;
            set { _map = value; OnPropertyChanged(); }
        }

        private void SetGaodeBaseMap()
        {
            // 创建高德底图图层
            WebTiledLayer gaodeLayer = new WebTiledLayer(
                "https://webrd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={col}&y={row}&z={level}",
                new List<string> { "1", "2", "3", "4", "5" })
            {
                Attribution = "Map data ©2015 AutoNavi - GS(2015)2080号",
                Name = "高德底图",
                IsVisible = true // 默认显示
            };

            // 添加到底图
            Basemap basemap = new Basemap();
            basemap.BaseLayers.Add(gaodeLayer);
            _map.Basemap = basemap;

            // 将图层添加到可绑定集合（如果有多个图层，都可以加到这里）
            LayerItems.Add(new LayerItem
            {
                Name = "高德底图",
                Layer = gaodeLayer,
                IsVisible = gaodeLayer.IsVisible
            });

            // 示例：可添加更多自定义图层
            // var customLayer = new FeatureLayer(...) { Name = "景点图层", IsVisible = true };
            // _map.OperationalLayers.Add(customLayer);
            // LayerItems.Add(new LayerItem { Name = "景点图层", Layer = customLayer, IsVisible = true });
        }

        /// <summary>
        /// Raises the <see cref="MapViewModel.PropertyChanged" /> event
        /// </summary>
        /// <param name="propertyName">The name of the property that has changed</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}