using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using SmartNanjingTravel.Models.Amap;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace SmartNanjingTravel.ViewModels
{
    public class AmapPoiViewModel : INotifyPropertyChanged
    {
        private readonly AmapService _amapService;
        private string _inputAddress;
        private string _queryResult;

        public string InputAddress
        {
            get => _inputAddress;
            set
            {
                _inputAddress = value;
                OnPropertyChanged();
            }
        }

        public string QueryResult
        {
            get => _queryResult;
            set
            {
                _queryResult = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<AddressInfo> AddressInfoList { get; set; } =
            new ObservableCollection<AddressInfo>();

        public AmapPoiViewModel()
        {
            _amapService = new AmapService();
        }

        public async Task QueryPoiAsync(MapView mapView)
        {
            if (string.IsNullOrEmpty(InputAddress))
            {
                QueryResult = "请输入要查询的地址！";
                MessageBox.Show("错误：未输入查询关键词", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var results = await _amapService.QueryPoiAsync(InputAddress, "南京");

                AddressInfoList.Clear();
                foreach (var item in results)
                {
                    AddressInfoList.Add(item);
                }

                AddScenicSpotsToMap(mapView);
                QueryResult = $"共加载{AddressInfoList.Count}条景点数据！";
            }
            catch (HttpRequestException ex)
            {
                QueryResult = $"网络请求异常：{ex.Message}";
                MessageBox.Show($"网络请求失败：{ex.Message}", "异常",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                QueryResult = $"程序异常：{ex.Message}";
                MessageBox.Show($"程序执行异常：\n{ex}", "异常详情",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AddScenicSpotsToMap(MapView mapView)
        {
            var spotsOverlay = mapView.GraphicsOverlays
                .FirstOrDefault(o => o.Id == "ScenicSpotsOverlay");

            if (spotsOverlay == null)
            {
                spotsOverlay = new GraphicsOverlay { Id = "ScenicSpotsOverlay" };
                mapView.GraphicsOverlays.Add(spotsOverlay);
            }

            spotsOverlay.Graphics.Clear();

            foreach (var info in AddressInfoList)
            {
                if (!double.TryParse(info.Longitude, out double lon) ||
                    !double.TryParse(info.Latitude, out double lat))
                    continue;

                var point = new MapPoint(lon, lat, SpatialReferences.Wgs84);
                var iconUri = new Uri("pack://application:,,,/SmartNanjingTravel;component/Image/icons1.png");
                /*                var defaultSymbol = new SimpleMarkerSymbol(
                                    SimpleMarkerSymbolStyle.Circle,
                                    System.Drawing.Color.Red,
                                    20); // 大小20*/

                /*                // 添加白色边框
                                defaultSymbol.Outline = new SimpleLineSymbol(
                                    SimpleLineSymbolStyle.Solid,
                                    System.Drawing.Color.White,
                                    2); // 边框宽度2*/

                var picSymbol = new PictureMarkerSymbol(iconUri)
                {
                    Width = 20,
                    Height = 35,
                    OffsetY = 12
                };

                var textSymbol = new TextSymbol
                {
                    Text = info.Name ?? "未知景点",
                    Color = System.Drawing.Color.Black,
                    FontWeight = Esri.ArcGISRuntime.Symbology.FontWeight.Bold,
                    Size = 12,
                    HorizontalAlignment = Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                    VerticalAlignment = Esri.ArcGISRuntime.Symbology.VerticalAlignment.Top,
                    OffsetY = 50
                };

                var compositeSymbol = new CompositeSymbol();
                compositeSymbol.Symbols.Add(picSymbol);
                compositeSymbol.Symbols.Add(textSymbol);

                var attrs = new Dictionary<string, object>
                {
                    { "名称", info.Name },
                    { "评分", info.Rating },
                    { "行政区", info.Adname },
                    { "开门时间", info.Opentime }
                };

                var graphic = new Graphic(point, attrs, compositeSymbol);
                spotsOverlay.Graphics.Add(graphic);
            }
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
