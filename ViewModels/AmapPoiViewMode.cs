using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Reduction;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Microsoft.Data.Sqlite;
using SmartNanjingTravel.Data;
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
using System.Windows.Media;
using SmartNanjingTravel.Services;
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

        public async Task QueryPoiAsync(MapView mapView,int i,Boolean isguihua = true, List<string> viaPointList = null)
        {
            if (mapView == null)
            {
                MessageBox.Show("错误：地图控件未正确加载 (mapView is null)。\n请确保 XAML 中的 CommandParameter 绑定正确。", "绑定错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (isguihua)
            {
                if (string.IsNullOrEmpty(InputAddress))
                {
                    QueryResult = "请输入要查询的地址！";
                    MessageBox.Show("错误：未输入查询关键词", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
      

            try
            {
                if (isguihua)
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
                }
                else
                {
                    AddressInfoList.Clear();
                    foreach (var item in viaPointList) {
                        var results = await _amapService.QueryPoiAsync(item, "南京");
                        foreach (var gtti in results)
                        {                     
                            AddressInfoList.Add(gtti);
                            break;
                        }
                    }                              
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

        // 修改后的完整方法
        public async Task AddScenicSpotsToMap(Esri.ArcGISRuntime.UI.Controls.MapView mapView)
        {
            // --- 0. 准备工作：清理旧图层 ---
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
            List<Field> fields = new List<Field>
    {
        new Field(FieldType.Text, "POI_ID", "景点ID", 100),
        new Field(FieldType.Text, "Name", "名称", 100),
        new Field(FieldType.Text, "Rating", "评分", 50),
        new Field(FieldType.Text, "Adname", "行政区", 100),
        new Field(FieldType.Text, "Opentime", "开门时间", 200),
        new Field(FieldType.Text, "Address", "地址", 500),
        new Field(FieldType.Text, "Photos", "图片", 255),
        new Field(FieldType.Text, "Longitude", "经度", 50),
        new Field(FieldType.Text, "Latitude", "纬度", 50)
    };

            // 创建表，指定几何类型为点，坐标系为 WGS84
            var featureTable = new FeatureCollectionTable(fields, GeometryType.Point, SpatialReferences.Wgs84);

            Uri iconUri = new Uri("pack://application:,,,/SmartNanjingTravel;component/image/icons1.png");

            // 创建图片符号
            Esri.ArcGISRuntime.Symbology.PictureMarkerSymbol picSymbol = new Esri.ArcGISRuntime.Symbology.PictureMarkerSymbol(iconUri)
            {
                Width = 20,
                Height = 35,
                OffsetY = 12
            };
            featureTable.Renderer = new Esri.ArcGISRuntime.Symbology.SimpleRenderer(picSymbol);

            // --- 2. 填充数据 ---
            List<Feature> featuresToAdd = new List<Feature>();
            List<Esri.ArcGISRuntime.Geometry.MapPoint> validPoints = new List<Esri.ArcGISRuntime.Geometry.MapPoint>();

            await featureTable.LoadAsync();

            // 准备一个集合，用于存储POI ID到AddressInfo的映射
            Dictionary<int, AddressInfo> poiIdMap = new Dictionary<int, AddressInfo>();

            foreach (var info in AddressInfoList)
            {
                if (!double.TryParse(info.Longitude, out double lon) || !double.TryParse(info.Latitude, out double lat))
                    continue;

                var point = new Esri.ArcGISRuntime.Geometry.MapPoint(lon, lat, SpatialReferences.Wgs84);
                validPoints.Add(point);

                // 【关键修改】生成一个稳定的POI_ID
                // 使用景点名称和坐标的哈希值作为POI_ID，确保同一景点在不同查询中具有相同的ID
                int poiId = GenerateStablePoiId(info.Name, lon, lat);

                // 将POI_ID和AddressInfo存储到映射中，供后续使用
                if (!poiIdMap.ContainsKey(poiId))
                {
                    poiIdMap[poiId] = info;
                }

                // 创建 Feature 并填入属性
                var feature = featureTable.CreateFeature();
                feature.Geometry = point;

                // 【关键修改】添加POI_ID到Feature属性中
                feature.SetAttributeValue("POI_ID", poiId.ToString());
                feature.SetAttributeValue("Name", info.Name);
                feature.SetAttributeValue("Rating", info.Rating);
                feature.SetAttributeValue("Adname", info.Adname);
                feature.SetAttributeValue("Opentime", info.Opentime);
                feature.SetAttributeValue("Address", info.Address ?? "暂无地址");
                feature.SetAttributeValue("Photos", info.Photos);
                feature.SetAttributeValue("Longitude", info.Longitude);
                feature.SetAttributeValue("Latitude", info.Latitude);

                featuresToAdd.Add(feature);
            }

            // 批量添加 Feature
            if (featuresToAdd.Any())
            {
                await featureTable.AddFeaturesAsync(featuresToAdd);
            }

            // --- 3. 将POI数据保存到本地数据库（如果需要）---
            // 这可以确保收藏功能有可用的POI_ID
            SavePoiDataToLocalDatabase(poiIdMap);

            // --- 4. 创建并显示地图图层 ---
            var featureCollection = new FeatureCollection();
            featureCollection.Tables.Add(featureTable);

            var featureCollectionLayer = new FeatureCollectionLayer(featureCollection)
            {
                Id = "ScenicSpotsLayer",
                Name = "景点图层组"
            };

            if (mapView.Map == null)
            {
                mapView.Map = new Map(BasemapStyle.ArcGISStreets);
            }
            mapView.Map.OperationalLayers.Add(featureCollectionLayer);
            await featureCollectionLayer.LoadAsync();

            // --- 5. 配置聚合和标签 ---
            var subLayer = featureCollectionLayer.Layers.FirstOrDefault(l => l.FeatureTable == featureTable);
            if (subLayer != null)
            {
                // 配置聚合
                var fixedSizeSymbol = new Esri.ArcGISRuntime.Symbology.SimpleMarkerSymbol
                {
                    Style = SimpleMarkerSymbolStyle.Circle,
                    Color =   System.Drawing.Color.FromArgb(200, 255, 128, 1),
                    Size = 32,
                    OffsetX = 0,
                    OffsetY = 0,

                };

                var clusterRenderer = new Esri.ArcGISRuntime.Symbology.SimpleRenderer(fixedSizeSymbol);
                var clusterReduction = new Esri.ArcGISRuntime.Reduction.ClusteringFeatureReduction(clusterRenderer);

                // 配置聚合标签
                var countTextSymbol = new Esri.ArcGISRuntime.Symbology.TextSymbol
                {
                    Color = System.Drawing.Color.FromArgb(255, 255, 128, 1),
                    Size = 22,
                    FontWeight = Esri.ArcGISRuntime.Symbology.FontWeight.Bold,
                    HaloColor = System.Drawing.Color.White,
                    HaloWidth = 2,
                    HorizontalAlignment = Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                };
                var countLabelExpression = new Esri.ArcGISRuntime.Mapping.Labeling.SimpleLabelExpression("[cluster_count]");
                var countLabelDef = new Esri.ArcGISRuntime.Mapping.LabelDefinition(countLabelExpression, countTextSymbol);
                clusterReduction.LabelDefinitions.Add(countLabelDef);
                subLayer.FeatureReduction = clusterReduction;

                // 配置景点名称标签
                var labelTextSymbol = new Esri.ArcGISRuntime.Symbology.TextSymbol
                {
                    Color = System.Drawing.Color.FromArgb(255, 103, 53, 183),
                    FontWeight = Esri.ArcGISRuntime.Symbology.FontWeight.Bold,
                    Size = 14,
                    HaloColor = System.Drawing.Color.White,
                    HaloWidth = 2,
                    OffsetY = 12,
                    HorizontalAlignment = Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center,
                    OffsetX = -10,
                };
                var labelExpression = new Esri.ArcGISRuntime.Mapping.Labeling.SimpleLabelExpression("[Name]");
                var labelDef = new Esri.ArcGISRuntime.Mapping.LabelDefinition(labelExpression, labelTextSymbol)
                {
                    MinScale = 0
                };
                subLayer.LabelDefinitions.Clear();
                subLayer.LabelDefinitions.Add(labelDef);
                subLayer.LabelsEnabled = true;
            }

            // --- 6. 缩放逻辑 ---
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

        // 新增方法：生成稳定的POI_ID
        private int GenerateStablePoiId(string name, double longitude, double latitude)
        {
            // 使用景点名称和坐标生成一个稳定的哈希值
            // 这样同一景点在不同查询中会有相同的POI_ID
            string combinedString = $"{name}_{longitude:F6}_{latitude:F6}";

            // 使用简单的哈希算法
            unchecked
            {
                int hash = 17;
                foreach (char c in combinedString)
                {
                    hash = hash * 31 + c;
                }
                return Math.Abs(hash);
            }
        }

        // 新增方法：将POI数据保存到本地数据库
        private void SavePoiDataToLocalDatabase(Dictionary<int, AddressInfo> poiIdMap)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseHelper.DatabasePath}"))
                {
                    connection.Open();

                    foreach (var kvp in poiIdMap)
                    {
                        var poiId = kvp.Key;
                        var info = kvp.Value;

                        // 检查是否已存在该POI
                        using (var checkCommand = connection.CreateCommand())
                        {
                            checkCommand.CommandText = "SELECT COUNT(*) FROM POI_INFO WHERE POI_ID = @poiId";
                            checkCommand.Parameters.AddWithValue("@poiId", poiId);
                            var exists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;

                            if (!exists)
                            {
                                // 插入新的POI数据
                                using (var insertCommand = connection.CreateCommand())
                                {
                                    insertCommand.CommandText = @"
                                INSERT OR IGNORE INTO POI_INFO 
                                (POI_ID, POI_NAME, CAT_ID, DISTRICT, ADDR, LAT, LNG, 
                                 DESC, RATING, OPEN_TIME, CREATE_TIME) 
                                VALUES (@poiId, @name, @catId, @district, @address, 
                                        @latitude, @longitude, @desc, @rating, 
                                        @openTime, @createTime)";

                                    insertCommand.Parameters.AddWithValue("@poiId", poiId);
                                    insertCommand.Parameters.AddWithValue("@name", info.Name ?? "未知景点");
                                    insertCommand.Parameters.AddWithValue("@catId", GetCategoryId(info.Name)); // 根据名称确定分类
                                    insertCommand.Parameters.AddWithValue("@district", info.Adname ?? "");
                                    insertCommand.Parameters.AddWithValue("@address", info.Address ?? "");
                                    insertCommand.Parameters.AddWithValue("@latitude", info.Latitude ?? "0");
                                    insertCommand.Parameters.AddWithValue("@longitude", info.Longitude ?? "0");
                                    insertCommand.Parameters.AddWithValue("@desc", info.Name ?? "");
                                    insertCommand.Parameters.AddWithValue("@rating", info.Rating ?? "0");
                                    insertCommand.Parameters.AddWithValue("@openTime", info.Opentime ?? "");
                                    insertCommand.Parameters.AddWithValue("@createTime", DateTime.Now);

                                    insertCommand.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果数据库保存失败，只是记录日志，不影响主要功能
                Console.WriteLine($"保存POI数据到数据库失败: {ex.Message}");
            }
        }

        // 辅助方法：根据景点名称确定分类ID
        private int GetCategoryId(string poiName)
        {
            // 简单的分类逻辑，可以根据需要扩展
            if (poiName.Contains("博物馆") || poiName.Contains("纪念馆"))
                return 2; // 博物馆
            else if (poiName.Contains("公园") || poiName.Contains("山") || poiName.Contains("湖"))
                return 3; // 自然风光
            else if (poiName.Contains("美食") || poiName.Contains("小吃"))
                return 4; // 美食购物
            else
                return 1; // 默认历史遗迹
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
