using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Reduction;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SmartNanjingTravel.Services;
using SmartNanjingTravel.Models;
using SmartNanjingTravel.ViewModels;
using System.Collections.ObjectModel;

namespace SmartNanjingTravel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isInitialLoading = true;
        private AmapRouteService _amapService = new AmapRouteService();
        private AmapPoiViewModel _amapPoiViewModel;
        private ObservableCollection<SmartNanjingTravel.Models.FavoriteItem> _favoriteItems = new ObservableCollection<SmartNanjingTravel.Models.FavoriteItem>();
        private List<SmartNanjingTravel.Models.FavoriteItem> _allFavorites = new List<SmartNanjingTravel.Models.FavoriteItem>();
        private bool _isScenicLayerVisible = false;
        private FeatureCollectionLayer _scenicSpotsLayer = null;
        private GraphicsOverlay _scenicSpotsOverlay = null;
        private PlotModel _ratingDistributionModel;
        private PlotModel _districtRatingModel;

        public class ViaPointItem
        {
            public string Address { get; set; } = "";
            public override string ToString() => Address;
        }

        public ObservableCollection<ViaPointItem> ViaPoints { get; set; } = new ObservableCollection<ViaPointItem>();
        private string _geodatabasePath;

        public MainWindow()
        {
            InitializeComponent();

            // 设置ItemsControl的数据源
            ViaPointsItemsControl.ItemsSource = ViaPoints;
            _amapPoiViewModel = new AmapPoiViewModel();
            this.DataContext = _amapPoiViewModel;


            // 设置收藏列表的数据源
            FavoritesItemsControl.ItemsSource = _favoriteItems;

            // 初始化时加载收藏数据
            LoadFavoritesData();

            this.Loaded += MainWindow_Loaded;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _isInitialLoading = true;

                // 设置查询关键词为南京的主要景点类型
                _amapPoiViewModel.InputAddress = "景点";

                // 调用查询方法
                await _amapPoiViewModel.QueryPoiAsync(MyMapView, 40);

                // 定位到南京区域
                await MyMapView.SetViewpointAsync(new Viewpoint(
                    new MapPoint(118.8, 32.05, SpatialReferences.Wgs84),
                    50000));

                // 更新状态
                _isScenicLayerVisible = true;
/*                HomeButtonText.Text = "关闭总览"; // 更新按钮文本提示*/

                // 存储图层引用以便后续移除
                StoreLayerReferences();

                // 设置初始加载完成
                _isInitialLoading = false;
            }
            catch (Exception ex)
            {
                _isInitialLoading = false;
                MessageBox.Show($"初始化加载景点失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 放大
        private async void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            await MyMapView.SetViewpointScaleAsync(MyMapView.MapScale * 0.5);
        }

        // 缩小
        private async void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            await MyMapView.SetViewpointScaleAsync(MyMapView.MapScale * 2.0);
        }

        // 全览南京
        private async void ViewNanjing_Click(object sender, RoutedEventArgs e)
        {
            // 南京中心点 (WGS84)
            Esri.ArcGISRuntime.Geometry.MapPoint nanjingCenter =
                new Esri.ArcGISRuntime.Geometry.MapPoint(118.796, 32.058, Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84);

            // 移动视角并重置正北
            await MyMapView.SetViewpointCenterAsync(nanjingCenter, 200000);
            await MyMapView.SetViewpointRotationAsync(0);
        }

        private void MyMapView_ViewpointChanged(object sender, EventArgs e)
        {
            // 更新图形比例尺
            UpdateGraphicScale();
        }

        // 1. 处理经纬度显示
        private void MyMapView_MouseMove(object sender, MouseEventArgs e)
        {
            // 将屏幕坐标转换为地理坐标
            Point screenPoint = e.GetPosition(MyMapView);
            MapPoint mapPoint = MyMapView.ScreenToLocation(screenPoint);

            if (mapPoint != null)
            {
                // WebMercator转成WGS84
                MapPoint wgs84Point = (MapPoint)GeometryEngine.Project(mapPoint, SpatialReferences.Wgs84);
                CoordsTextBlock.Text = $"经度: {wgs84Point.X:F3}  纬度: {wgs84Point.Y:F3}";
            }
        }

        private void UpdateGraphicScale()
        {
            if (MyMapView.VisibleArea == null) return;

            // 获取地图当前 1 像素代表的实际距离（米）
            double mPerPixel = MyMapView.UnitsPerPixel;
            double targetLengthInPixels = 100;
            double actualDistance = targetLengthInPixels * mPerPixel;

            double roundedDistance = RoundToSignificant(actualDistance);

            // 重新计算对齐后的像素宽度
            double finalBarWidth = roundedDistance / mPerPixel;

            // 更新 UI
            ScaleDistanceText.Text = roundedDistance >= 1000 ? $"{roundedDistance / 1000:F1} km" : $"{roundedDistance:F0} m";
            ScaleBarPath.Data = System.Windows.Media.Geometry.Parse($"M 0,0 L 0,8 L {finalBarWidth},8 L {finalBarWidth},0");
            ScaleBarCanvas.Width = finalBarWidth;
        }

        // 辅助函数：让比例尺数字看起来更自然
        private double RoundToSignificant(double distance)
        {
            double factor = Math.Pow(10, Math.Floor(Math.Log10(distance)));
            double normalized = distance / factor;

            if (normalized < 1.5) normalized = 1;
            else if (normalized < 3.5) normalized = 2;
            else if (normalized < 7.5) normalized = 5;
            else normalized = 10;

            return normalized * factor;
        }

        // 3. 指北针点击重置北向
        private async void Compass_Click(object sender, MouseButtonEventArgs e)
        {
            // 点击指北针，地图恢复正北
            await MyMapView.SetViewpointRotationAsync(0);
        }


        // 面板切换方法
        private void SwitchPanel(FrameworkElement panelToShow)
        {
            // 获取当前的 ViewModel 实例
            var viewModel = this.Resources["MapViewModel"] as MapViewModel;
            if (viewModel != null)
            {
                // 调用MapViewModel.cs 中已经写好的清除方法
                viewModel.ClearRouteLayers();
            }
            // 如果显示其他面板，则隐藏景区图层
            if (panelToShow != null &&
                panelToShow != FavoritesPanel &&
                panelToShow != RoutePlanningPanel &&
                panelToShow != RecommendationPanel &&
                !_isInitialLoading) 
            {
                if (_isScenicLayerVisible)
                {
                    RemoveScenicLayer();
                }
            }

            var panels = new List<FrameworkElement>
            {
                RoutePlanningPanel,
                FavoritesPanel,
                LayerControlPanel,
                RecommendationPanel,
                RatingStatisticsPanel
            };

            // 如果要显示的面板就是当前点击的，则显示；其他全部隐藏
            foreach (var panel in panels)
            {
                if (panel == panelToShow)
                {
                    panel.Visibility = Visibility.Visible;
                }
                else
                {
                    panel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 如果图层已经显示，则移除它
                if (_isScenicLayerVisible)
                {
                    RemoveScenicLayer();
/*                    HomeButtonText.Text = "打开总览"; // 更新按钮文本提示*/
                    return;
                }

                _isScenicLayerVisible = true;

                // 设置查询关键词为南京的主要景点类型
                _amapPoiViewModel.InputAddress = "景点";

                // 调用查询方法
                await _amapPoiViewModel.QueryPoiAsync(MyMapView, 40);

                // 定位到南京区域
                await MyMapView.SetViewpointAsync(new Viewpoint(
                    new MapPoint(118.8, 32.05, SpatialReferences.Wgs84),
                    50000));

                // 更新状态
                _isScenicLayerVisible = true;
/*                HomeButtonText.Text = "关闭总览"; // 更新按钮文本提示*/

                // 存储图层引用以便后续移除
                StoreLayerReferences();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载景点失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 新增：存储图层引用
        private void StoreLayerReferences()
        {
            // 存储聚合图层
            _scenicSpotsLayer = MyMapView.Map.OperationalLayers
                .FirstOrDefault(l => l.Id == "ScenicSpotsLayer") as FeatureCollectionLayer;

            // 存储图形叠加层
            _scenicSpotsOverlay = MyMapView.GraphicsOverlays
                .FirstOrDefault(o => o.Id == "ScenicSpotsOverlay");
        }

        // 移除景点图层
        private void RemoveScenicLayer()
        {
            try
            {
                // 移除聚合图层
                if (_scenicSpotsLayer != null)
                {
                    MyMapView.Map.OperationalLayers.Remove(_scenicSpotsLayer);
                    _scenicSpotsLayer = null;
                }

                // 移除图形叠加层
                if (_scenicSpotsOverlay != null)
                {
                    MyMapView.GraphicsOverlays.Remove(_scenicSpotsOverlay);
                    _scenicSpotsOverlay = null;
                }

                // 更新状态
                _isScenicLayerVisible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移除图层失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            try
            {
                bool handled = await HandleGeodatabasePointClick(e);
                if (handled) return;
                // 1. 先找外层的图层壳（聚合图层）
                var collectionLayer = MyMapView.Map.OperationalLayers
                                        .FirstOrDefault(l => l.Id == "ScenicSpotsLayer")
                                        as Esri.ArcGISRuntime.Mapping.FeatureCollectionLayer;

                if (collectionLayer != null)
                {
                    // 2. 直接获取里面真正存点的子图层（一般就只有一层，取第0个）
                    var subLayer = collectionLayer.Layers.FirstOrDefault();

                    if (subLayer != null)
                    {
                        // 3. 精准识别这个子图层
                        var result = await MyMapView.IdentifyLayerAsync(subLayer, e.Position, 15, false);

                        if (result.GeoElements.Count > 0)
                        {
                            var element = result.GeoElements.First();

                            // 情况 A：点到了聚合圈圈 (AggregateGeoElement)
                            if (element is AggregateGeoElement)
                            {
                                return;
                            }

                            // 情况 B：点到了具体的景点 (Feature)
                            if (element is Feature feature)
                            {
                                // 准备数据
                                var attrs = feature.Attributes;

                                // 辅助取值方法
                                string GetVal(string k1, string k2 = null)
                                {
                                    if (attrs.ContainsKey(k1)) return attrs[k1]?.ToString();
                                    if (k2 != null && attrs.ContainsKey(k2)) return attrs[k2]?.ToString();
                                    return "暂无";
                                }

                                string name = GetVal("Name", "名称");
                                if (name == "暂无") name = "未知景点";

                                string rating = GetVal("Rating", "评分");
                                string district = GetVal("Adname", "行政区");
                                string openTime = GetVal("Opentime", "开门时间");
                                string address = GetVal("Address", "地址");

                                // 获取POI_ID
                                string poiIdStr = GetVal("POI_ID", "POI_ID");
                                int poiId = 0;
                                if (!string.IsNullOrEmpty(poiIdStr) && int.TryParse(poiIdStr, out int parsedId))
                                {
                                    poiId = parsedId;
                                }

                                // 获取坐标
                                string longitudeStr = GetVal("Longitude", "经度");
                                string latitudeStr = GetVal("Latitude", "纬度");
                                double longitude = 0, latitude = 0;
                                double.TryParse(longitudeStr, out longitude);
                                double.TryParse(latitudeStr, out latitude);

                                // 图片处理
                                string imageUrl = "";
                                if (attrs.ContainsKey("ImageUrl"))
                                {
                                    imageUrl = attrs["ImageUrl"]?.ToString();
                                }
                                else if (attrs.ContainsKey("图片"))
                                {
                                    imageUrl = attrs["图片"]?.ToString();
                                }
                                else if (attrs.ContainsKey("Photos"))
                                {
                                    imageUrl = attrs["Photos"]?.ToString();
                                }

                                // 传递更多参数给ScenicInfoWindow
                                var win = new ScenicInfoWindow(name, rating, district, openTime, imageUrl,
                                                               poiId, longitude, latitude);
                                win.Owner = this;
                                win.ShowDialog();
                                return;
                            }
                        }
                    }
                }

                // 4. 如果上面的聚合图层没找到，再去查旧的 GraphicsOverlay
                var overlay = MyMapView.GraphicsOverlays.FirstOrDefault(o => o.Id == "ScenicSpotsOverlay");
                if (overlay != null)
                {
                    var overlayResult = await MyMapView.IdentifyGraphicsOverlayAsync(overlay, e.Position, 10, false);
                    if (overlayResult.Graphics.Count > 0)
                    {
                        var graphic = overlayResult.Graphics[0];

                        // 旧逻辑简单处理
                        string name = graphic.Attributes["名称"]?.ToString() ?? "未知";
                        string rating = graphic.Attributes["评分"]?.ToString() ?? "暂无";
                        string district = graphic.Attributes["行政区"]?.ToString() ?? "未知";
                        string openTime = graphic.Attributes["开门时间"]?.ToString() ?? "暂无";

                        string imageUrl = "";
                        if (graphic.Attributes.ContainsKey("图片")) imageUrl = graphic.Attributes["图片"]?.ToString();

                        var win = new ScenicInfoWindow(name, rating, district, openTime, imageUrl);
                        win.Owner = this;
                        win.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"点击处理异常: {ex.Message}");
            }
        }
        private async Task<bool> HandleGeodatabasePointClick(GeoViewInputEventArgs e)
        {
            try
            {
                // 查找所有可能的地理数据库点图层（名称包含"景点"的图层）
                var geodatabaseLayers = MyMapView.Map.OperationalLayers
                    .Where(l => l is FeatureLayer &&
                           l.Name != null &&
                           l.Name.Contains("景点"))
                    .Cast<FeatureLayer>()
                    .ToList();

                foreach (var layer in geodatabaseLayers)
                {
                    var result = await MyMapView.IdentifyLayerAsync(layer, e.Position, 15, false);

                    if (result.GeoElements.Count > 0)
                    {
                        var element = result.GeoElements.First();
                        if (element is Feature feature)
                        {
                            // 获取属性
                            var attrs = feature.Attributes;

                            // 辅助方法获取字段值
                            string GetAttributeValue(string key, string alternativeKey = null)
                            {
                                if (attrs.ContainsKey(key))
                                    return attrs[key]?.ToString();
                                if (alternativeKey != null && attrs.ContainsKey(alternativeKey))
                                    return attrs[alternativeKey]?.ToString();
                                return "暂无";
                            }

                            // 获取景点信息
                            string name = GetAttributeValue("景区名称", "Name");
                            string district = GetAttributeValue("所属区县", "District");

                            // 生成一个临时的POI_ID（基于名称和位置）
                            string poiIdStr = GetAttributeValue("POI_ID", "ID");
                            int poiId = 0;
                            if (!string.IsNullOrEmpty(poiIdStr) && int.TryParse(poiIdStr, out int parsedId))
                            {
                                poiId = parsedId;
                            }
                            else
                            {
                                // 如果没有POI_ID，生成一个临时的
                                var geometry = feature.Geometry as MapPoint;
                                if (geometry != null)
                                {
                                    poiId = GenerateTempPoiId(name, geometry.X, geometry.Y);
                                }
                            }

                            // 获取坐标
                            double longitude = 0, latitude = 0;
                            if (feature.Geometry is MapPoint point)
                            {
                                var wgs84Point = (MapPoint)GeometryEngine.Project(point, SpatialReferences.Wgs84);
                                longitude = wgs84Point.X;
                                latitude = wgs84Point.Y;
                            }

                            // 打开ScenicInfoWindow
                            var win = new ScenicInfoWindow(
                                name: name,
                                rating: "暂无", // 地理数据库没有评分信息
                                district: district,
                                openTime: "暂无开放时间", // 地理数据库没有开放时间
                                imageUrl: "", // 地理数据库没有图片
                                poiId: poiId,
                                longitude: longitude,
                                latitude: latitude
                            );
                            win.Owner = this;
                            win.ShowDialog();

                            return true; // 已处理
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理地理数据库点击失败: {ex.Message}");
            }

            return false;
        }

        // 生成临时POI_ID
        private int GenerateTempPoiId(string name, double longitude, double latitude)
        {
            string combinedString = $"{name}_{longitude:F6}_{latitude:F6}";
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

        private void CloseRoutePlanningPanel_Click(object sender, RoutedEventArgs e)
        {
            RouteHistoryList.Items.Clear();   // <--- 新增这一行：清空之前的记录
            RoutePlanningPanel.Visibility = Visibility.Collapsed;
            // 1. 获取或创建用于显示路线的图层
            var routeOverlay = MyMapView.GraphicsOverlays.FirstOrDefault(o => o.Id == "RouteOverlay");
            if (routeOverlay == null)
            {
                routeOverlay = new GraphicsOverlay { Id = "RouteOverlay" };
                MyMapView.GraphicsOverlays.Add(routeOverlay);
            }

            routeOverlay.Graphics.Clear();
            // --- 0. 准备工作：清理旧图层 ---
            var oldLayer = MyMapView.Map.OperationalLayers.FirstOrDefault(l => l.Id == "ScenicSpotsLayer");
            if (oldLayer != null)
            {
                MyMapView.Map.OperationalLayers.Remove(oldLayer);
            }

            // 如果你之前还用了 GraphicsOverlay，也要清理掉，防止重影
            var oldOverlay = MyMapView.GraphicsOverlays.FirstOrDefault(o => o.Id == "ScenicSpotsOverlay");
            if (oldOverlay != null)
            {
                MyMapView.GraphicsOverlays.Remove(oldOverlay);
            }
        }

        // --- 修改添加按钮事件 ---
        private void AddViaPointButton_Click(object sender, RoutedEventArgs e)
        {
            // 以前是: ViaPoints.Add("");
            // 改为: 添加一个新的对象
            ViaPoints.Add(new ViaPointItem { Address = "" });
        }

        // --- 修改删除按钮事件 ---
        private void DeleteViaPointButton_Click(object sender, RoutedEventArgs e)
        {
            // 修改类型判断：string -> ViaPointItem
            if (sender is Button button && button.DataContext is ViaPointItem viaPoint)
            {
                ViaPoints.Remove(viaPoint);
            }
        }
        // 清空所有输入框
        private void ClearRouteButton_Click(object sender, RoutedEventArgs e)
        {
            StartPointTextBox.Text = "";
            DestinationTextBox.Text = "";
            ViaPoints.Clear();
        }
       
        // 加载收藏数据
        private void LoadFavoritesData()
        {
            try
            {
                var favorites = App.FavoriteService.GetFavorites(App.CurrentUserId);
                _allFavorites = favorites;
                _favoriteItems.Clear();

                foreach (var item in favorites)
                {
                    _favoriteItems.Add(item);
                }

                UpdateFavoritesCount();
                ShowEmptyStateIfNeeded();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载收藏数据失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 更新收藏数量
        private void UpdateFavoritesCount()
        {
            if (_favoriteItems.Count == 0)
            {
                FavoritesCountText.Text = "暂无收藏";
            }
            else
            {
                FavoritesCountText.Text = $"共 {_favoriteItems.Count} 个收藏";
            }
        }

        // 显示空状态
        private void ShowEmptyStateIfNeeded()
        {
            if (_favoriteItems.Count == 0)
            {
                EmptyStateBorder.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyStateBorder.Visibility = Visibility.Collapsed;
            }
        }

        // 在地图上查看
        private void ViewOnMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SmartNanjingTravel.Models.FavoriteItem favorite)
            {
                try
                {
                    // 定位到收藏的景点
                    MapPoint location = new MapPoint((double)favorite.Longitude,
                                                    (double)favorite.Latitude,
                                                    SpatialReferences.Wgs84);

                    MyMapView.SetViewpointCenterAsync(location, 10000);

                    MessageBox.Show($"已定位到: {favorite.Name}", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"定位失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }



        private void DeleteFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SmartNanjingTravel.Models.FavoriteItem favorite)
            {
                var result = MessageBox.Show($"确定要删除收藏的'{favorite.Name}'吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (App.FavoriteService.RemoveFavorite(App.CurrentUserId, favorite.PoiId))
                        {
                            // 从本地集合中移除
                            _allFavorites.RemoveAll(f => f.FavoriteId == favorite.FavoriteId);
                            _favoriteItems.Remove(favorite);

                            UpdateFavoritesCount();
                            ShowEmptyStateIfNeeded();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除失败: {ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ClearAllFavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要清空所有收藏吗？此操作不可恢复！", "确认清空",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (App.FavoriteService.ClearAllFavorites(App.CurrentUserId))
                    {
                        _allFavorites.Clear();
                        _favoriteItems.Clear();

                        MessageBox.Show("已清空所有收藏", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        UpdateFavoritesCount();
                        ShowEmptyStateIfNeeded();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 导出收藏到CSV文件
        private void ExportFavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_allFavorites.Count == 0)
                {
                    MessageBox.Show("没有收藏可以导出", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    FileName = $"南京旅游收藏_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    DefaultExt = ".csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    StringBuilder csvContent = new StringBuilder();

                    // 添加标题行
                    csvContent.AppendLine("收藏ID,景点名称,行政区,地址,经度,纬度,评分,价格,开放时间,分类,收藏时间,备注");

                    // 添加数据行
                    foreach (var item in _allFavorites)
                    {
                        csvContent.AppendLine($"\"{item.FavoriteId}\"," +
                                            $"\"{item.Name}\"," +
                                            $"\"{item.District}\"," +
                                            $"\"{item.Address}\"," +
                                            $"\"{item.Longitude}\"," +
                                            $"\"{item.Latitude}\"," +
                                            $"\"{item.Rating}\"," +
                                            $"\"{item.OpenTime}\"," +
                                            $"\"{item.FavoriteTime:yyyy-MM-dd HH:mm:ss}\"," +
                                            $"\"{item.Notes}\"");
                    }

                    File.WriteAllText(saveFileDialog.FileName, csvContent.ToString(), Encoding.UTF8);

                    MessageBox.Show($"成功导出 {_allFavorites.Count} 个收藏到文件:\n{saveFileDialog.FileName}",
                        "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 关闭图层控制面板
        private void CloseLayerControlPanel_Click(object sender, RoutedEventArgs e)
        {
            LayerControlPanel.Visibility = Visibility.Collapsed;

            // 找到并清除景点叠加层的所有 Graphic
            var scenicOverlay = MyMapView.GraphicsOverlays
                .FirstOrDefault(o => o.Id == "ScenicSpotsOverlay");

            if (scenicOverlay != null)
            {
                scenicOverlay.Graphics.Clear();
            }
        }

        // 点击行程规划
        private void RoutePlanningButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(RoutePlanningPanel);
        }

        // 点击我的收藏
        private void MyFavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(FavoritesPanel);
            LoadFavoritesData(); // 每次打开都刷新数据
        }

        // 点击图层控制
        private void LayerControlButton_Click(object sender, RoutedEventArgs e)
        {
            // 逻辑：如果已经显示，点击就关闭；如果没显示，点击就切换过去
            if (LayerControlPanel.Visibility == Visibility.Visible)
            {
                LayerControlPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SwitchPanel(LayerControlPanel);
            }
        }

        // 游玩推荐按钮点击事件
        private void RecommendButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(RecommendationPanel);
        }

        // 关闭游玩推荐面板
        private void CloseRecommendationPanel_Click(object sender, RoutedEventArgs e)
        {
            RecommendationPanel.Visibility = Visibility.Collapsed;
            var mapViewModel = Resources["MapViewModel"] as MapViewModel;
            mapViewModel?.ClearRouteLayers();
        }


        // 主题按钮点击事件
        private async void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string routeName = "";

                // 根据按钮名称确定路线名称
                if (button == SixDynastiesButton) routeName = "六朝古都";
                else if (button == RepublicanButton) routeName = "民国风情";
                else if (button == RedMemoryButton) routeName = "红色记忆寻访";
                else if (button == CityWallButton) routeName = "明城墙";
                else if (button == GeologyDiscoveryButton) routeName = "地质";

                if (string.IsNullOrEmpty(routeName))
                    return;

                try
                {
                    // 获取MapViewModel
                    var mapViewModel = Resources["MapViewModel"] as MapViewModel;
                    if (mapViewModel == null)
                    {
                        MessageBox.Show("地图视图模型未初始化", "错误");
                        return;
                    }

                    // 调用MapViewModel的LoadRouteToMap方法
                    bool success = await mapViewModel.LoadRouteToMap(routeName);

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载路线异常: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 季节按钮点击事件
        private async void SeasonButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string routeName = "";
                
                // 根据按钮名称确定主题
                if (button == SpringButton) routeName = "春";
                else if (button == SummerButton) routeName = "夏";
                else if (button == AutumnButton) routeName = "秋";
                else if (button == WinterButton) routeName = "冬";

                try
                {
                    // 获取MapViewModel
                    var mapViewModel = Resources["MapViewModel"] as MapViewModel;
                    if (mapViewModel == null)
                    {
                        MessageBox.Show("地图视图模型未初始化", "错误");
                        return;
                    }

                    // 调用MapViewModel的LoadRouteToMap方法
                    bool success = await mapViewModel.LoadRouteToMap(routeName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载路线异常: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        //开始导航
        private async void SearchRouteButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveScenicLayer();
            string start = StartPointTextBox.Text.Trim();
            string destination = DestinationTextBox.Text.Trim();
            if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(destination))
            {
                MessageBox.Show("请输入起点和终点");
                return;
            }

            SearchRouteButton.IsEnabled = false;
            SearchRouteButton.Content = "规划中...";

            List<string> midPoints = new List<string>();
            foreach (var viaPoint in ViaPoints)
            {
                if (!string.IsNullOrWhiteSpace(viaPoint.Address)) midPoints.Add(viaPoint.Address);
            }

            var result = await _amapService.GetDrivingRoutePathAsync(start, destination, midPoints);

            if (result != null && result.Points.Count > 0)
            {
                // === 1. 构建新记录 ===
                var allPoints = new List<string> { start };
                allPoints.AddRange(midPoints);
                allPoints.Add(destination);

                var segmentList = new List<RouteSegment>();

                // 循环生成分段
                for (int i = 0; i < allPoints.Count - 1; i++)
                {
                    string detailStr = "计算中";

                    // 尝试从结果中取对应的段详情
                    if (result.SegmentDetails != null && i < result.SegmentDetails.Count)
                    {
                        // 格式是 "时间|距离"，我们把它换成 "时间  距离"
                        detailStr = result.SegmentDetails[i].Replace("|", "  ");
                    }

                    segmentList.Add(new RouteSegment
                    {
                        From = allPoints[i],
                        To = allPoints[i + 1],
                        Detail = detailStr // 这里就是真实的每段详情了！
                    });
                }

                var item = new RouteRecordItem
                {
                    SummaryText = $"{result.DurationText}  |  {result.DistanceText}",
                    Segments = segmentList
                };

                // 后面的插入代码不变
                RouteHistoryList.Items.Clear();   // <--- 新增这一行：清空之前的记录
                RouteHistoryList.Items.Add(item); // <--- 改为 Add 即可

                // 3. 地图显示
                ShowRouteOnMap(result.Points);

                // POI 查询 (保留您原有逻辑)
                midPoints.Add(start);
                midPoints.Add(destination);
                await _amapPoiViewModel.QueryPoiAsync(MyMapView, 1,false,midPoints);
            }
            else
            {
                MessageBox.Show("规划失败");
            }

            SearchRouteButton.IsEnabled = true;
            SearchRouteButton.Content = "开始导航";
        }
        private void ShowRouteOnMap(List<GeoPoint> points)
        {
            // 1. 获取或创建用于显示路线的图层
            var routeOverlay = MyMapView.GraphicsOverlays.FirstOrDefault(o => o.Id == "RouteOverlay");
            if (routeOverlay == null)
            {
                routeOverlay = new GraphicsOverlay { Id = "RouteOverlay" };
                MyMapView.GraphicsOverlays.Add(routeOverlay);
            }

            routeOverlay.Graphics.Clear();

            // 2. 构建点集合 (使用全名 Esri.ArcGISRuntime.Geometry.PointCollection)
            var stopPoints = new Esri.ArcGISRuntime.Geometry.PointCollection(SpatialReferences.Wgs84);

            foreach (var p in points)
            {
                // 确保 MapPoint 也是 Esri 的
                stopPoints.Add(new MapPoint(p.Longitude, p.Latitude));
            }

            // 3. 创建线几何体 (使用全名 Esri.ArcGISRuntime.Geometry.Polyline)
            var routeLine = new Esri.ArcGISRuntime.Geometry.Polyline(stopPoints);

            // 4. 定义线的样式
            // 注意：如果有报错提示 Color，请确保引用了 System.Drawing，或者改成 Color.FromRgb(...)
            var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.FromArgb(103, 58, 183), 3);

            // 5. 创建图形并显示
            var routeGraphic = new Graphic(routeLine, lineSymbol);
            routeOverlay.Graphics.Add(routeGraphic);

            // 6. 自动缩放视角
            if (routeLine.Parts.Count > 0 && routeLine.Parts[0].PointCount > 0)
            {
                MyMapView.SetViewpointGeometryAsync(routeLine.Extent, 50);
            }
        }
        // 关闭收藏面板
        private void CloseFavoritesPanel_Click(object sender, RoutedEventArgs e)
        {
            FavoritesPanel.Visibility = Visibility.Collapsed;
        }

        private void RatingStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(RatingStatisticsPanel);
            LoadRatingStatistics();
        }

        // 关闭评分统计面板
        private void CloseRatingStatisticsPanel_Click(object sender, RoutedEventArgs e)
        {
            RatingStatisticsPanel.Visibility = Visibility.Collapsed;
        }

        // 刷新统计数据
        private void RefreshStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRatingStatistics();
        }

        // 加载评分统计数据
        private void LoadRatingStatistics()
        {
            try
            {
                var allRatings = new List<double>();
                var districtRatings = new Dictionary<string, List<double>>();

                // 方法1：从 AmapPoiViewModel 的 AddressInfoList 获取数据
                if (_amapPoiViewModel?.AddressInfoList != null && _amapPoiViewModel.AddressInfoList.Count > 0)
                {
                    foreach (var addressInfo in _amapPoiViewModel.AddressInfoList)
                    {
                        if (!string.IsNullOrEmpty(addressInfo.Rating))
                        {
                            // 处理评分字符串（可能需要清理非数字字符）
                            string ratingStr = addressInfo.Rating.Replace("暂无评分", "0")
                                                                 .Replace("暂无", "0");

                            if (double.TryParse(ratingStr, out double rating))
                            {
                                allRatings.Add(rating);

                                // 按行政区统计
                                string district = string.IsNullOrEmpty(addressInfo.Adname)
                                    ? "未知"
                                    : addressInfo.Adname;

                                if (!districtRatings.ContainsKey(district))
                                {
                                    districtRatings[district] = new List<double>();
                                }
                                districtRatings[district].Add(rating);
                            }
                        }
                    }
                }

                // 方法2：如果上面没数据，尝试从地图图层获取
                if (allRatings.Count == 0)
                {
                    // 从 FeatureCollectionLayer 获取数据
                    var scenicLayer = MyMapView.Map.OperationalLayers
                        .FirstOrDefault(l => l.Id == "ScenicSpotsLayer") as FeatureCollectionLayer;

                    if (scenicLayer != null)
                    {
                        // 这里需要异步查询，所以可能需要修改方法为 async
                        // 简化处理：直接从 _amapPoiViewModel 获取数据
                    }
                
                }

                // 更新UI
                UpdateStatisticsUI(allRatings, districtRatings, DateTime.Now);

                // 创建图表（如果数据为空，显示空图表）
                if (allRatings.Count > 0)
                {
                    CreateRatingDistributionChart(allRatings);
                    CreateDistrictRatingChart(districtRatings);
                }
                else
                {
                    // 显示无数据提示
                    ShowNoDataMessage();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载评分统计失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 显示无数据提示的方法
        private void ShowNoDataMessage()
        {
            // 清空图表
            RatingDistributionChart.Model = null;
            DistrictRatingChart.Model = null;

            // 更新统计文本
            AverageRatingText.Text = "暂无数据";
            TotalPoiText.Text = "0";
            MaxRatingText.Text = "暂无";
            MinRatingText.Text = "暂无";
        }

        private void UpdateStatisticsUI(List<double> allRatings, Dictionary<string, List<double>> districtRatings, DateTime lastUpdate)
        {
            if (allRatings.Count == 0) return;

            double average = allRatings.Average();
            double max = allRatings.Max();
            double min = allRatings.Min();

            AverageRatingText.Text = average.ToString("F1");
            TotalPoiText.Text = allRatings.Count.ToString();
            MaxRatingText.Text = max.ToString("F1");
            MinRatingText.Text = min.ToString("F1");
        }
        private void CreateRatingDistributionChart(List<double> ratings)
        {
            if (ratings == null || ratings.Count == 0)
            {

                // 创建空图表显示提示
                var emptyModel = new PlotModel
                {
                    Title = "暂无评分数据",
                    TitleColor = OxyColors.Gray,
                    TitleFontSize = 14,
                    Background = OxyColors.White
                };

                RatingDistributionChart.Model = emptyModel;
                return;
            }

            // 创建 PlotModel
            var model = new PlotModel
            {
                TitleFontSize = 16,
                TitleColor = OxyColors.Black,
                DefaultFont = "Microsoft YaHei",
                DefaultFontSize = 12,
                Background = OxyColors.White,
                PlotAreaBorderThickness = new OxyThickness(1.5, 1.5, 1.5, 1.5),
                PlotAreaBorderColor = OxyColor.FromArgb(255, 47, 47, 47)
            };

            var ratingGroups = new Dictionary<string, int>
            {
                {"3.8以下", 0},
                {"3.8-4.0", 0},
                {"4.0-4.2", 0},
                {"4.2-4.4", 0},
                {"4.4-4.6", 0},
                {"4.6-4.8", 0},
                {"4.8-5.0", 0}
            };
            // 统计评分分布
            foreach(var rating in ratings)
            {
                if (rating < 3.8)
                    ratingGroups["3.8以下"]++;
                else if (rating >= 3.8 && rating < 4.0)
                    ratingGroups["3.8-4.0"]++;
                else if (rating >= 4.0 && rating < 4.2)
                    ratingGroups["4.0-4.2"]++;
                else if (rating >= 4.2 && rating < 4.4)
                    ratingGroups["4.2-4.4"]++;
                else if (rating >= 4.4 && rating < 4.6)
                    ratingGroups["4.4-4.6"]++;
                else if (rating >= 4.6 && rating < 4.8)
                    ratingGroups["4.6-4.8"]++;
                else if (rating >= 4.8 && rating <= 5.0)
                    ratingGroups["4.8-5.0"]++;
            }

            // 创建条形图系列
            var series = new BarSeries
            {
                Title = "数量",
                FillColor = OxyColor.FromRgb(103, 58, 183), // 紫色
                LabelPlacement = LabelPlacement.Outside,
                LabelFormatString = "{0}",
                Font = "Microsoft YaHei",
                BarWidth = 0.8
            };

            // 添加数据点
            var categories = new List<string>();
            int categoryIndex = 0;

            foreach (var kvp in ratingGroups)
            {
                categories.Add(kvp.Key);
                series.Items.Add(new BarItem(kvp.Value, categoryIndex));
                categoryIndex++;
            }

            // 添加 Y 轴（类别轴）
            model.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                ItemsSource = categories,
                Title = "评分区间",
                TitleFontSize = 13,
                AxisTitleDistance = 8,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
                GapWidth = 0.4,
            });

            // 添加 X 轴（数值轴）
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "数量",
                TitleFontSize = 13,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
                AxisTitleDistance = 8,
                FontSize = 11,
                Minimum = 0,
                MinimumPadding = 0,
                MaximumPadding = 0.1
            };

            // 自动设置主要刻度，基于最大数量
            int maxCount = ratingGroups.Values.Max();
            if (maxCount <= 10)
            {
                xAxis.MajorStep = 1; // 数量少时，每1个单位一个刻度
                xAxis.MinorStep = 0.5;
            }
            else if (maxCount <= 50)
            {
                xAxis.MajorStep = 5; // 数量中等时，每5个单位一个刻度
                xAxis.MinorStep = 1;
            }
            else
            {
                xAxis.MajorStep = Math.Ceiling(maxCount / 10.0); // 自动计算合适的刻度间隔
                xAxis.MinorStep = xAxis.MajorStep / 5;
            }

            model.Axes.Add(xAxis);

            model.Series.Add(series);

            // 设置图表
            RatingDistributionChart.Model = model;
            RatingDistributionChart.InvalidatePlot(); // 强制刷新显示
        }

        // 创建行政区评分饼图
        private void CreateDistrictRatingChart(Dictionary<string, List<double>> districtRatings)
        {
            if (districtRatings.Count == 0) return;

            _districtRatingModel = new PlotModel
            {
                TitleColor = OxyColors.Black,
                TitleFontSize = 14,
                PlotAreaBorderThickness = new OxyThickness(0),
                DefaultFont = "Microsoft YaHei",

            };

            var series = new PieSeries
            {
                Font = "Microsoft YaHei",
                StrokeThickness = 2,
                InsideLabelPosition = 0.8,
                AngleSpan = 360,
                StartAngle = 0,
                OutsideLabelFormat = "{1}: {0}个",
                InsideLabelFormat = "",
                Diameter = 0.8
            };

            // 定义颜色方案
            var colors = new[]
            {
                OxyColor.FromRgb(103, 58, 183),   // 紫色
                OxyColor.FromRgb(33, 150, 243),   // 蓝色
                OxyColor.FromRgb(0, 150, 136),    // 青色
                OxyColor.FromRgb(255, 193, 7),    // 黄色
                OxyColor.FromRgb(255, 87, 34),    // 橙色
                OxyColor.FromRgb(156, 39, 176),   // 深紫色
                OxyColor.FromRgb(76, 175, 80)     // 绿色
            };

            int colorIndex = 0;

            // 按POI数量排序
            foreach (var kvp in districtRatings.OrderByDescending(d => d.Value.Count))
            {
                if (kvp.Value.Count == 0) continue;

                double averageRating = kvp.Value.Average();
                string label = kvp.Key;

                series.Slices.Add(new PieSlice(label, kvp.Value.Count)
                {
                    Fill = colors[colorIndex % colors.Length]
                });

                colorIndex++;
            }

            _districtRatingModel.Series.Add(series);
            DistrictRatingChart.Model = _districtRatingModel;
        }
    }
}