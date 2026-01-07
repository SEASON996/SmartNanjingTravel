using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SmartNanjingTravel.Models;


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

        public MapViewModel()
        {
            InitializeMap();
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
            var gaodeLayer = new Esri.ArcGISRuntime.Mapping.WebTiledLayer(
                "https://webrd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={col}&y={row}&z={level}",
                new System.Collections.Generic.List<string> { "1", "2", "3", "4", "5" })
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

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}