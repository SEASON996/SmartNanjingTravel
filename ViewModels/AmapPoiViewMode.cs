using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Reduction;
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
using System.Windows.Input;
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
        public ICommand SearchCommand { get; private set; }
        public ObservableCollection<AddressInfo> AddressInfoList { get; set; } =
            new ObservableCollection<AddressInfo>();

        public AmapPoiViewModel()
        {
            _amapService = new AmapService();

            // 初始化命令：
            // 当命令触发时，调用 QueryPoiAsync，并传入参数 (MapView)
            // 注意：因为 QueryPoiAsync 是异步的，这里使用了 async/await 包装
            SearchCommand = new RelayCommand<MapView>(async (mapView) =>
            {
                await QueryPoiAsync(mapView,3);
            });
        }
        // 这是一个标准的命令实现辅助类
        public class RelayCommand<T> : ICommand
        {
            private readonly Action<T> _execute;
            private readonly Predicate<T> _canExecute;

            public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute == null || _canExecute((T)parameter);
            }

            public void Execute(object parameter)
            {
                _execute((T)parameter);
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }
        public async Task QueryPoiAsync(MapView mapView,int i)
        {
            if (mapView == null)
            {
                MessageBox.Show("错误：地图控件未正确加载 (mapView is null)。\n请确保 XAML 中的 CommandParameter 绑定正确。", "绑定错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
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
                var j = 0;
                foreach (var item in results)
                {
                    j++;
                    AddressInfoList.Add(item);
                    if (i <= j) break;
                    
                }
                await AddScenicSpotsToMap(mapView);
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

        public async Task AddScenicSpotsToMap(Esri.ArcGISRuntime.UI.Controls.MapView mapView)
        {
            // --- 0. 准备工作：清理旧图层 ---
            // 我们的目标是创建一个 FeatuerLayer，所以要检查并清理旧的 FeatureLayer
            var oldLayer = mapView.Map.OperationalLayers.FirstOrDefault(l => l.Id == "ScenicSpotsLayer");
            if (oldLayer != null)
            {
                mapView.Map.OperationalLayers.Remove(oldLayer);
            }

            // 如果你之前还用了 GraphicsOverlay，也要清理掉，防止重影
            var oldOverlay = mapView.GraphicsOverlays.FirstOrDefault(o => o.Id == "ScenicSpotsOverlay");
            if (oldOverlay != null)
            {
                mapView.GraphicsOverlays.Remove(oldOverlay);
            }

            if (AddressInfoList == null || AddressInfoList.Count == 0) return;

            // --- 1. 定义 FeatureCollectionTable 的结构 (Schema) ---
            // 我们需要定义字段，这就像是在定义数据库表的列
            List<Field> fields = new List<Field>
    {
        new Field(FieldType.Text, "Name", "名称", 100),
        new Field(FieldType.Text, "Rating", "评分", 50),
        new Field(FieldType.Text, "Adname", "行政区", 100),
        new Field(FieldType.Text, "Opentime", "开门时间", 200),
        new Field(FieldType.Text, "ImageUrl", "图片", 255)
    };

            // 创建表，指定几何类型为点，坐标系为 WGS84
            var featureTable = new FeatureCollectionTable(fields, GeometryType.Point, SpatialReferences.Wgs84);

            Uri iconUri = new Uri("pack://application:,,,/SmartNanjingTravel;component/image/icons1.png");

            // 创建图片符号（仅用URI，无任何流/字节数组）
            Esri.ArcGISRuntime.Symbology.PictureMarkerSymbol picSymbol = new Esri.ArcGISRuntime.Symbology.PictureMarkerSymbol(iconUri)
            {
                Width = 20,
                Height = 35,
                OffsetY = 12 // 图片中心对准点位
            };
            featureTable.Renderer = new Esri.ArcGISRuntime.Symbology.SimpleRenderer(picSymbol);

            // --- 3. 填充数据 ---
            List<Feature> featuresToAdd = new List<Feature>();
            List<Esri.ArcGISRuntime.Geometry.MapPoint> validPoints = new List<Esri.ArcGISRuntime.Geometry.MapPoint>();

            await featureTable.LoadAsync(); // 加载表以便写入

            foreach (var info in AddressInfoList)
            {
                if (!double.TryParse(info.Longitude, out double lon) || !double.TryParse(info.Latitude, out double lat))
                    continue;

                var point = new Esri.ArcGISRuntime.Geometry.MapPoint(lon, lat, SpatialReferences.Wgs84);
                validPoints.Add(point);

                // 创建 Feature 并填入属性
                var feature = featureTable.CreateFeature();
                feature.Geometry = point;
                feature.SetAttributeValue("Name", info.Name);
                feature.SetAttributeValue("Rating", info.Rating);
                feature.SetAttributeValue("Adname", info.Adname);
                feature.SetAttributeValue("Opentime", info.Opentime);
                feature.SetAttributeValue("ImageUrl",info.Photos);

                featuresToAdd.Add(feature);
            }

            // 批量添加 Feature，性能更好
            if (featuresToAdd.Any())
            {
                await featureTable.AddFeaturesAsync(featuresToAdd);
            }

            // 1. 创建一个 FeatureCollection
            var featureCollection = new FeatureCollection();

            // 2. 将你的 table 添加到 collection 中
            featureCollection.Tables.Add(featureTable);

            // 3. 使用 FeatureCollectionLayer 来显示
            var featureCollectionLayer = new FeatureCollectionLayer(featureCollection)
            {
                Id = "ScenicSpotsLayer", // 给整个图层组起个名字
                Name = "景点图层组"
            };

            if (mapView.Map == null)
            {
                mapView.Map = new Map(BasemapStyle.ArcGISStreets);
            }
            mapView.Map.OperationalLayers.Add(featureCollectionLayer);
            await featureCollectionLayer.LoadAsync();
            var subLayer = featureCollectionLayer.Layers.FirstOrDefault(l => l.FeatureTable == featureTable);
            if (subLayer != null)
            {
               var fixedSizeSymbol = new Esri.ArcGISRuntime.Symbology.SimpleMarkerSymbol
               {
                   Style = SimpleMarkerSymbolStyle.Circle,
                   Color = System.Drawing.Color.Orange,
                   // 修复1：根据系统DPI缩放反向计算，确保视觉上是30像素
                   Size = 30,
                   // 修复2：设置锚点居中，避免渲染偏移
                   OffsetX = 0,
                   OffsetY = 0,

               };

                var clusterRenderer = new Esri.ArcGISRuntime.Symbology.SimpleRenderer(fixedSizeSymbol);

                // 2. 创建聚合对象，传入上面的固定样式渲染器
                var clusterReduction = new Esri.ArcGISRuntime.Reduction.ClusteringFeatureReduction(clusterRenderer)
                {
                };

                // 3. 关键步骤：添加显示数量的标签
                // 3.1 定义文字样式 (白色粗体，居中)
                var countTextSymbol = new Esri.ArcGISRuntime.Symbology.TextSymbol
                {
                    Color = System.Drawing.Color.Orange,
                    Size = 20,
                    HaloColor = System.Drawing.Color.White,
                    HaloWidth = 2,
                    HorizontalAlignment = Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                };

                // 3.2 定义标签表达式，使用特殊字段 "[cluster_count]" 表示聚合数量 
                var countLabelExpression = new Esri.ArcGISRuntime.Mapping.Labeling.SimpleLabelExpression("[cluster_count]");

                var countLabelDef = new Esri.ArcGISRuntime.Mapping.LabelDefinition(countLabelExpression, countTextSymbol);


                // 3.4 将标签定义添加到聚合对象中
                clusterReduction.LabelDefinitions.Add(countLabelDef);

                // 4. 将配置好的聚合对象赋值给图层
                subLayer.FeatureReduction = clusterReduction;


                // 2. 创建文字符号 (TextSymbol)
                var labelTextSymbol = new Esri.ArcGISRuntime.Symbology.TextSymbol
                {
                    Color = System.Drawing.Color.Black,
                    Size = 12,
                    HaloColor = System.Drawing.Color.White,
                    HaloWidth = 2,
                    OffsetY = 12,
                    HorizontalAlignment = Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                    OffsetX = -10,
                };

                // 3. 创建标签表达式 (注意：SimpleLabelExpression 需要中间的 .Labeling)
                var labelExpression = new Esri.ArcGISRuntime.Mapping.Labeling.SimpleLabelExpression("[Name]");
                var labelDef = new Esri.ArcGISRuntime.Mapping.LabelDefinition(labelExpression, labelTextSymbol)
                {
                    MinScale = 0
                };

                // 5. 添加并开启
                subLayer.LabelDefinitions.Clear(); //以此防重复添加
                subLayer.LabelDefinitions.Add(labelDef);
                subLayer.LabelsEnabled = true;
            }
            // --- 5. 缩放逻辑 (和以前一样) ---
            if (validPoints.Count > 0)
            {
                if (validPoints.Count == 1)
                    await mapView.SetViewpointCenterAsync(validPoints[0], 10000);
                else
                {
                    var multipoint = new Esri.ArcGISRuntime.Geometry.Multipoint(validPoints);
                    await mapView.SetViewpointGeometryAsync(multipoint.Extent, 50);
                }
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
