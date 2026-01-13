using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Reduction;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SmartNanjingTravel.Data;
using SmartNanjingTravel.Models;
using SmartNanjingTravel.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
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

namespace SmartNanjingTravel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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

            // 为了方便调试，您可以重写 ToString
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

            RatingDistributionChart = new OxyPlot.Wpf.PlotView();
            DistrictRatingChart = new OxyPlot.Wpf.PlotView();

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
            // --- 新增：清除地图上的路径图层 ---
            // 获取当前的 ViewModel 实例（通常在 Resources 或 DataContext 中）
            var viewModel = this.Resources["MapViewModel"] as MapViewModel;
            if (viewModel != null)
            {
                // 调用您在 MapViewModel.cs 中已经写好的清除方法
                viewModel.ClearRouteLayers();
            }
            // 如果显示其他面板，则隐藏景区图层
            if (panelToShow != null &&
                panelToShow != FavoritesPanel &&
                panelToShow != RoutePlanningPanel &&
                panelToShow != RecommendationPanel)
            {
                if (_isScenicLayerVisible)
                {
                    RemoveScenicLayer();
                    if (HomeButtonText != null)
                        HomeButtonText.Text = "景区总览";
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
                    HomeButtonText.Text = "景区总览"; // 更新按钮文本提示
                    return;
                }

                _isScenicLayerVisible = true;

                // 设置查询关键词为南京的主要景点类型
                _amapPoiViewModel.InputAddress = "景点";

                // 调用查询方法
                await _amapPoiViewModel.QueryPoiAsync(MyMapView, 30);

                // 定位到南京区域
                await MyMapView.SetViewpointAsync(new Viewpoint(
                    new MapPoint(118.8, 32.05, SpatialReferences.Wgs84),
                    50000));

                // 更新状态
                _isScenicLayerVisible = true;
                HomeButtonText.Text = "景区总览"; // 更新按钮文本提示

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

                            MessageBox.Show("删除成功", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Information);

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

                    // 显示加载提示
                    var loadingDialog = new Window
                    {
                        Title = "正在加载",
                        Width = 300,
                        Height = 120,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        WindowStyle = WindowStyle.ToolWindow,
                        ResizeMode = ResizeMode.NoResize,
                        ShowInTaskbar = false
                    };

                    var stackPanel = new StackPanel
                    {
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        Margin = new Thickness(20)
                    };

                    var progressBar = new ProgressBar
                    {
                        IsIndeterminate = true,
                        Width = 200,
                        Height = 20,
                        Margin = new Thickness(0, 0, 0, 15)
                    };

                    var textBlock = new TextBlock
                    {
                        Text = $"正在加载【{routeName}】路线...",
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center
                    };

                    stackPanel.Children.Add(progressBar);
                    stackPanel.Children.Add(textBlock);
                    loadingDialog.Content = stackPanel;
                    loadingDialog.Show();

                    // 调用MapViewModel的LoadRouteToMap方法
                    bool success = await mapViewModel.LoadRouteToMap(routeName);

                    loadingDialog.Close();

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

                    // 显示加载提示
                    var loadingDialog = new Window
                    {
                        Title = "正在加载",
                        Width = 300,
                        Height = 120,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        WindowStyle = WindowStyle.ToolWindow,
                        ResizeMode = ResizeMode.NoResize,
                        ShowInTaskbar = false
                    };

                    var stackPanel = new StackPanel
                    {
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        Margin = new Thickness(20)
                    };

                    var progressBar = new ProgressBar
                    {
                        IsIndeterminate = true,
                        Width = 200,
                        Height = 20,
                        Margin = new Thickness(0, 0, 0, 15)
                    };

                    var textBlock = new TextBlock
                    {
                        Text = $"正在加载【{routeName}】路线...",
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center
                    };

                    stackPanel.Children.Add(progressBar);
                    stackPanel.Children.Add(textBlock);
                    loadingDialog.Content = stackPanel;
                    loadingDialog.Show();

                    // 调用MapViewModel的LoadRouteToMap方法
                    bool success = await mapViewModel.LoadRouteToMap(routeName);

                    loadingDialog.Close();

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
            //midPoints.Add(start);
            //midPoints.Add(destination);

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
               Debug.WriteLine("开始加载评分统计...");
                Debug.WriteLine($"AddressInfoList 数量: {_amapPoiViewModel?.AddressInfoList?.Count ?? 0}");
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

                // 方法3：从 POI_INFO 数据库表获取历史数据
                if (allRatings.Count == 0)
                {
                    try
                    {
                        using (var connection = new SqliteConnection($"Data Source={DatabaseHelper.DatabasePath}"))
                        {
                            connection.Open();

                            using (var command = connection.CreateCommand())
                            {
                                command.CommandText = @"
                            SELECT RATING, ADNAME 
                            FROM POI_INFO 
                            WHERE RATING IS NOT NULL AND RATING != ''";

                                using (var reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string ratingStr = reader["RATING"]?.ToString();
                                        string district = reader["ADNAME"]?.ToString() ?? "未知";

                                        if (double.TryParse(ratingStr, out double rating))
                                        {
                                            allRatings.Add(rating);

                                            if (!districtRatings.ContainsKey(district))
                                            {
                                                districtRatings[district] = new List<double>();
                                            }
                                            districtRatings[district].Add(rating);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"从数据库获取评分数据失败: {ex.Message}");
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

        // 创建评分分布柱状图
        private void CreateRatingDistributionChart(List<double> ratings)
        {
            if (ratings.Count == 0) return;

            // 创建数据模型
            _ratingDistributionModel = new PlotModel
            {
                Title = "评分分布",
                TitleColor = OxyColors.Black,
                TitleFontSize = 14,
                PlotAreaBorderThickness = new OxyThickness(0),
                Background = OxyColors.White,
                PlotAreaBackground = OxyColors.White
            };

            // 创建柱状图系列（使用BarSeries代替ColumnSeries）
            var series = new BarSeries
            {
                Title = "数量",
                FillColor = OxyColor.FromRgb(103, 58, 183), // 紫色
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                LabelPlacement = LabelPlacement.Inside,
                LabelFormatString = "{0}"
            };

            // 定义评分区间（更精细的划分）
            var ratingBins = new Dictionary<string, int>
    {
        {"1-1.5星", 0},
        {"1.5-2星", 0},
        {"2-2.5星", 0},
        {"2.5-3星", 0},
        {"3-3.5星", 0},
        {"3.5-4星", 0},
        {"4-4.5星", 0},
        {"4.5-5星", 0}
    };

            // 统计各区间数量
            foreach (var rating in ratings)
            {
                if (rating >= 1.0 && rating < 1.5)
                    ratingBins["1-1.5星"]++;
                else if (rating >= 1.5 && rating < 2.0)
                    ratingBins["1.5-2星"]++;
                else if (rating >= 2.0 && rating < 2.5)
                    ratingBins["2-2.5星"]++;
                else if (rating >= 2.5 && rating < 3.0)
                    ratingBins["2.5-3星"]++;
                else if (rating >= 3.0 && rating < 3.5)
                    ratingBins["3-3.5星"]++;
                else if (rating >= 3.5 && rating < 4.0)
                    ratingBins["3.5-4星"]++;
                else if (rating >= 4.0 && rating < 4.5)
                    ratingBins["4-4.5星"]++;
                else if (rating >= 4.5 && rating <= 5.0)
                    ratingBins["4.5-5星"]++;
            }

            // 创建数据点
            var categories = new List<string>();
            var values = new List<double>();

            foreach (var kvp in ratingBins)
            {
                categories.Add(kvp.Key);
                values.Add(kvp.Value);
                series.Items.Add(new BarItem(kvp.Value));
            }

            // 添加X轴（数值轴）
            _ratingDistributionModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "数量",
                Minimum = 0,
                AbsoluteMinimum = 0,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(40, 0, 0, 0),
                MinorGridlineColor = OxyColor.FromArgb(20, 0, 0, 0)
            });

            // 添加Y轴（类别轴）
            _ratingDistributionModel.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "评分区间",
                ItemsSource = categories,
                GapWidth = 0.2
            });

            _ratingDistributionModel.Series.Add(series);
            RatingDistributionChart.Model = _ratingDistributionModel;
        }

        // 创建行政区评分饼图
        private void CreateDistrictRatingChart(Dictionary<string, List<double>> districtRatings)
        {
            if (districtRatings.Count == 0) return;

            _districtRatingModel = new PlotModel
            {
                Title = "行政区POI数量分布",
                TitleColor = OxyColors.Black,
                TitleFontSize = 14,
                PlotAreaBorderThickness = new OxyThickness(0)
            };

            var series = new PieSeries
            {
                StrokeThickness = 2,
                InsideLabelPosition = 0.8,
                AngleSpan = 360,
                StartAngle = 0,
                OutsideLabelFormat = "{1}: {0:F0}",
                TrackerFormatString = "{0}: {1}个POI，平均评分{3:F1}"
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
                string label = $"{kvp.Key} ({kvp.Value.Count}个)";

                series.Slices.Add(new PieSlice(label, kvp.Value.Count)
                {
                    Fill = colors[colorIndex % colors.Length]
                    // 在Tooltip中显示平均评分
                });

                colorIndex++;
            }

            _districtRatingModel.Series.Add(series);
            DistrictRatingChart.Model = _districtRatingModel;
        }


        // 路径规划类
        public class AmapRouteService
        {
            // 您的 Key
            private const string ApiKey = "88eb4252bbd3f175d95e1fe5501da57c";
            private readonly HttpClient _httpClient = new HttpClient();

            // 改了返回值：Task<RouteResult>
            public async Task<RouteResult> GetDrivingRoutePathAsync(string startAddr, string endAddr, List<string> viaAddrs)
            {
                try
                {
                    // 1. 解析所有地址为坐标
                    var startLoc = await GetLocationByAddressAsync(startAddr);
                    if (startLoc == null) { MessageBox.Show($"无法识别起点：{startAddr}"); return null; }

                    var endLoc = await GetLocationByAddressAsync(endAddr);
                    if (endLoc == null) { MessageBox.Show($"无法识别终点：{endAddr}"); return null; }

                    var viaLocs = new List<GeoPoint>();
                    foreach (var addr in viaAddrs)
                    {
                        if (!string.IsNullOrWhiteSpace(addr))
                        {
                            var p = await GetLocationByAddressAsync(addr);
                            if (p != null) viaLocs.Add(p);
                        }
                    }

                    // 2. 将所有点连成一个序列: Start -> Via1 -> Via2 -> End
                    var sequence = new List<GeoPoint> { startLoc };
                    sequence.AddRange(viaLocs);
                    sequence.Add(endLoc);

                    // 3. 结果容器
                    var finalResult = new RouteResult();

                    // 累计总时间和距离
                    double totalSeconds = 0;
                    double totalMeters = 0;

                    // 4. 分段请求 (Step-by-Step Request)
                    // 例如 A->B->C，我们先请求 A->B，再请求 B->C
                    for (int i = 0; i < sequence.Count - 1; i++)
                    {
                        var p1 = sequence[i];
                        var p2 = sequence[i + 1];

                        // 这里的 RequestDrivingRouteAsync 只请求两个点，不带 waypoints
                        var segmentResult = await RequestDrivingRouteAsync(p1, p2, null);

                        if (segmentResult != null)
                        {
                            // 累加所有的坐标点
                            finalResult.Points.AddRange(segmentResult.Points);

                            // 解析这一段的时间和距离，存入 SegmentDetails
                            // 格式： "20 分钟|5.5 公里" (用 | 分隔方便后续拆分)
                            string segDuration = segmentResult.DurationText; // 例如 "15 分钟"
                            string segDistance = segmentResult.DistanceText; // 例如 "3.2 公里"

                            // 我们在 RouteResult 里加一个列表来存每段的详情
                            finalResult.SegmentDetails.Add($"{segDuration}|{segDistance}");

                            // 累加总数值 (为了重新计算总和)
                            totalSeconds += ParseDurationToSeconds(segmentResult.DurationText);
                            totalMeters += ParseDistanceToMeters(segmentResult.DistanceText);
                        }
                    }

                    // 5. 重新格式化总时间和总距离
                    finalResult.DurationText = totalSeconds < 60 ? "< 1分钟" : $"{totalSeconds / 60:F0} 分钟";
                    finalResult.DistanceText = totalMeters >= 1000 ? $"{totalMeters / 1000:F1} 公里" : $"{totalMeters:F0} 米";

                    return finalResult;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"流程出错: {ex.Message}");
                    return null;
                }
            }

            // 辅助：解析 "15 分钟" -> 900
            private double ParseDurationToSeconds(string text)
            {
                try
                {
                    string num = System.Text.RegularExpressions.Regex.Replace(text, "[^0-9]", "");
                    if (int.TryParse(num, out int val)) return val * 60;
                }
                catch { }
                return 0;
            }

            // 辅助：解析 "3.5 公里" -> 3500
            private double ParseDistanceToMeters(string text)
            {
                try
                {
                    if (text.Contains("公里"))
                    {
                        string num = text.Replace(" 公里", "").Replace("公里", "");
                        if (double.TryParse(num, out double val)) return val * 1000;
                    }
                    else // 米
                    {
                        string num = text.Replace(" 米", "").Replace("米", "");
                        if (double.TryParse(num, out double val)) return val;
                    }
                }
                catch { }
                return 0;
            }

            // 地址解析 (保留之前的稳健逻辑)
            private async Task<GeoPoint> GetLocationByAddressAsync(string keyword)
            {
                try
                {
                    // 注意 URL 变了：从 geocode/geo 变成了 place/text
                    // 强制指定 city=南京，搜索更精准
                    string url = $"https://restapi.amap.com/v3/place/text?keywords={keyword}&city=南京&output=JSON&key={ApiKey}";

                    var json = await _httpClient.GetStringAsync(url);

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("status", out var status) && status.GetString() == "1")
                        {
                            // 这里变了：检查 "pois" 数组，而不是 "geocodes"
                            if (root.TryGetProperty("pois", out var pois) && pois.GetArrayLength() > 0)
                            {
                                var firstPoi = pois[0]; // 取第一个结果

                                // 获取 location 字段
                                if (firstPoi.TryGetProperty("location", out var locProp))
                                {
                                    // 格式依然是 "经度,纬度"
                                    string locStr = locProp.GetString();
                                    if (!string.IsNullOrEmpty(locStr)) // 有时候可能是空的数组，加个判断
                                    {
                                        var parts = locStr.Split(',');
                                        if (parts.Length == 2)
                                        {
                                            double lon = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                                            double lat = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                                            return new GeoPoint(lon, lat);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
                return null;
            }

            // 路径规划 (增加 waypoints 参数)
            // 求路径（核心修改：获取 duration 和 distance）
            private async Task<RouteResult> RequestDrivingRouteAsync(GeoPoint start, GeoPoint end, string waypoints)
            {
                var result = new RouteResult();
                try
                {
                    // ... 原来的 URL 构建 ...
                    string url = $"https://restapi.amap.com/v3/direction/driving?origin={start.Longitude:F6},{start.Latitude:F6}&destination={end.Longitude:F6},{end.Latitude:F6}&extensions=base&strategy=0&key={ApiKey}";
                    if (!string.IsNullOrEmpty(waypoints)) url += $"&waypoints={waypoints}";

                    var json = await _httpClient.GetStringAsync(url);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("status", out var status) && status.GetString() == "1")
                        {
                            if (root.TryGetProperty("route", out var route) && route.TryGetProperty("paths", out var paths))
                            {
                                if (paths.GetArrayLength() > 0)
                                {
                                    var path = paths[0];

                                    // 1. 获取时间
                                    if (path.TryGetProperty("duration", out var durProp))
                                    {
                                        int seconds = int.Parse(durProp.GetString());
                                        result.DurationText = seconds < 60 ? "< 1分钟" : $"{seconds / 60} 分钟";
                                    }
                                    // 2. 获取距离
                                    if (path.TryGetProperty("distance", out var distProp))
                                    {
                                        double meters = double.Parse(distProp.GetString());
                                        result.DistanceText = meters >= 1000 ? $"{meters / 1000.0:F1} 公里" : $"{meters} 米";
                                    }
                                    // 3. 获取路线点
                                    if (path.TryGetProperty("steps", out var steps))
                                    {
                                        foreach (var step in steps.EnumerateArray())
                                        {
                                            if (step.TryGetProperty("polyline", out var polyline))
                                            {
                                                var pointArr = polyline.GetString().Split(';');
                                                foreach (var p in pointArr)
                                                {
                                                    var xy = p.Split(',');
                                                    if (xy.Length == 2)
                                                        result.Points.Add(new GeoPoint(
                                                            double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
                                                            double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
                return result;
            }

        }
        // 用于显示历史记录的数据模型
        // 放在 MainWindow.xaml.cs 底部或单独文件
        public class RouteRecordItem
        {
            // 总览信息
            public string SummaryText { get; set; } // 例如 "总共 45分钟 | 20公里"

            // 路段集合：每一段都是一个 Start -> End 的小结构
            public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();
        }

        public class RouteSegment
        {
            public string From { get; set; }  // 本段起点
            public string To { get; set; }    // 本段终点
            public string Detail { get; set; } // 本段详情 (因为API可能只给总时间，这里暂时显示为"下一站")

            // 用于界面显示的图标颜色逻辑
            public string IconColor => "Green";
        }
        public class RouteResult
        {
            public List<GeoPoint> Points { get; set; } = new List<GeoPoint>();
            public string DurationText { get; set; } = "";
            public string DistanceText { get; set; } = "";

            // 新增：存每段的原始数据
            public List<string> SegmentDetails { get; set; } = new List<string>();
        }
        // 数据模型类
        public class GeoPoint
        {
            public double Longitude { get; set; }
            public double Latitude { get; set; }
            public GeoPoint(double lon, double lat) { Longitude = lon; Latitude = lat; }
        }
        // 查看所有推荐
        private void ViewAllRecommendations_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("查看所有推荐功能待开发\n\n" +
                           "将显示所有主题和季节的推荐汇总，\n" +
                           "包含景点分布图和详细路线规划。",
                           "所有推荐",
                           MessageBoxButton.OK,
                           MessageBoxImage.Information);
        }
    }
}