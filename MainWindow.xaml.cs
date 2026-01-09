using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Reduction;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Microsoft.Win32;
using SmartNanjingTravel.Models;
using SmartNanjingTravel.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
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
using System.Net.Http;
using System.Text.Json; // 推荐使用 System.Text.Json 或 Newtonsoft.Json
namespace SmartNanjingTravel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AmapRouteService _routeService = new AmapRouteService();
        private AmapPoiViewModel _amapPoiViewModel;
        private ObservableCollection<FavoriteItem> _favoriteItems = new ObservableCollection<FavoriteItem>();
        private List<FavoriteItem> _allFavorites = new List<FavoriteItem>();
        public class ViaPointItem
        {
            public string Address { get; set; } = "";

            // 为了方便调试，您可以重写 ToString
            public override string ToString() => Address;
        }
        // 新增：追踪景点图层状态的字段
        private bool _isScenicLayerVisible = false;
        private FeatureCollectionLayer _scenicSpotsLayer = null;
        private GraphicsOverlay _scenicSpotsOverlay = null;
        public ObservableCollection<ViaPointItem> ViaPoints { get; set; } = new ObservableCollection<ViaPointItem>();


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
                // 如果地图是 WebMercator，转成 WGS84 (经纬度)
                MapPoint wgs84Point = (MapPoint)GeometryEngine.Project(mapPoint, SpatialReferences.Wgs84);
                CoordsTextBlock.Text = $"经度: {wgs84Point.X:F5}  纬度: {wgs84Point.Y:F5}";
            }
        }

        private void UpdateGraphicScale()
        {
            if (MyMapView.VisibleArea == null) return;

            // 获取地图当前 1 像素代表的实际距离（米）
            double mPerPixel = MyMapView.UnitsPerPixel;

            // 目标：我们希望比例尺长度在屏幕上大概是 100 像素左右
            double targetLengthInPixels = 100;
            double actualDistance = targetLengthInPixels * mPerPixel;

            // 对距离进行取整显示
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
                HomeButtonText.Text = "景区总览";
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

        // 新增：移除景点图层
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
                // 调试时解开这行，看看是不是报了什么错
                // MessageBox.Show("点击报错：" + ex.Message);
            }
        }


        private void CloseRoutePlanningPanel_Click(object sender, RoutedEventArgs e)
        {
            RoutePlanningPanel.Visibility = Visibility.Collapsed;
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
        private async void SearchRouteButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. 从界面获取输入
            string start = StartPointTextBox.Text;
            string destination = DestinationTextBox.Text;

            // 获取途经点列表
            List<string> midPoints = new List<string>();
            foreach (var viaPoint in ViaPoints)
            {
                if (!string.IsNullOrWhiteSpace(viaPoint.Address)) // 注意：这里用到 ViaPointItem.Address
                {
                    midPoints.Add(viaPoint.Address);
                }
            }

            if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(destination))
            {
                MessageBox.Show("请输入起点和终点");
                return;
            }

            // 2. 界面交互
            SearchRouteButton.IsEnabled = false;
            SearchRouteButton.Content = "规划中..."; // 提示一下

            // 3. 调用服务
            List<GeoPoint> routePoints = await _routeService.GetDrivingRoutePathAsync(start, destination, midPoints);

            // 4. 处理结果
            if (routePoints != null && routePoints.Count > 0)
            {

                ShowRouteOnMap(routePoints);
                midPoints.Add(start);
                midPoints.Add( destination);
                await _amapPoiViewModel.QueryPoiAsync(MyMapView, 0, false , midPoints);

            }
            else
            {
                MessageBox.Show("未找到路径，请检查地址是否正确。");
            }

            SearchRouteButton.IsEnabled = true;
            SearchRouteButton.Content = "开始导航"; // 恢复按钮文字
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

/*        // 筛选按钮点击事件
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                // 重置所有按钮状态（除了当前点击的）
                if (toggleButton == AllFilterButton)
                {
                    AttractionFilterButton.IsChecked = false;
                    RouteFilterButton.IsChecked = false;
                }
                else if (toggleButton == AttractionFilterButton)
                {
                    AllFilterButton.IsChecked = false;
                    RouteFilterButton.IsChecked = false;
                }
                else if (toggleButton == RouteFilterButton)
                {
                    AllFilterButton.IsChecked = false;
                    AttractionFilterButton.IsChecked = false;
                }

                ApplyFilter(toggleButton.Content.ToString());
            }
        }*/

        // 应用筛选
/*        private void ApplyFilter(string filterType)
        {
            _favoriteItems.Clear();

            if (filterType == "全部")
            {
                foreach (var item in _allFavorites)
                {
                    _favoriteItems.Add(item);
                }
            }
            else if (filterType == "景点")
            {
                foreach (var item in _allFavorites.Where(f => f.Type == "景点"))
                {
                    _favoriteItems.Add(item);
                }
            }
            else if (filterType == "路线")
            {
                // 这里可以筛选路线类型的收藏
                foreach (var item in _allFavorites.Where(f => f.Type == "路线"))
                {
                    _favoriteItems.Add(item);
                }
            }

            UpdateFavoritesCount();
            ShowEmptyStateIfNeeded();
        }*/

        // 在地图上查看
        private void ViewOnMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is FavoriteItem favorite)
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
            if (sender is Button button && button.Tag is FavoriteItem favorite)
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

        // 新增 SwitchPanel 方法（修复 CS0103 错误）
        private void SwitchPanel(FrameworkElement panelToShow)
        {
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

            // 1. 列出所有需要互相排斥的面板
            var panels = new List<FrameworkElement>
            {
                RoutePlanningPanel,
                FavoritesPanel,
                LayerControlPanel,
                RecommendationPanel
            };

            // 2. 遍历处理：如果要显示的面板就是当前点击的，则显示；其他全部隐藏
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
        }

        // 主题按钮点击事件
        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string themeName = "";

                // 根据按钮名称确定主题
                if (button == SixDynastiesButton) themeName = "六朝古都探秘";
                else if (button == RepublicanButton) themeName = "民国风情之旅";
                else if (button == RedMemoryButton) themeName = "红色记忆寻访";
                else if (button == CityWallButton) themeName = "明城墙徒步";

                MessageBox.Show($"正在为您加载【{themeName}】主题路线...\n\n" +
                               "功能说明：\n" +
                               "1. 地图上将高亮显示相关景点位置\n" +
                               "2. 显示推荐的游览路线\n" +
                               "3. 提供详细的景点介绍\n\n" +
                               "（数据库功能待完善）",
                               "主题路线推荐",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
        }

        // 季节按钮点击事件
        private void SeasonButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string seasonName = "";

                // 根据按钮名称确定季节
                if (button == SpringButton) seasonName = "春游南京";
                else if (button == SummerButton) seasonName = "夏游南京";
                else if (button == AutumnButton) seasonName = "秋游南京";
                else if (button == WinterButton) seasonName = "冬游南京";

                MessageBox.Show($"正在为您加载【{seasonName}】季节路线...\n\n" +
                               "功能说明：\n" +
                               "1. 显示该季节最佳观赏景点\n" +
                               "2. 推荐适合该季节的户外活动\n" +
                               "3. 提供天气和穿着建议\n\n" +
                               "（数据库功能待完善）",
                               "季节路线推荐",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
        }
        // 路径规划类
        public class AmapRouteService
        {
            // 您的 Key
            private const string ApiKey = "88eb4252bbd3f175d95e1fe5501da57c";
            private readonly HttpClient _httpClient = new HttpClient();

            public async Task<List<GeoPoint>> GetDrivingRoutePathAsync(string startAddr, string endAddr, List<string> viaAddrs)
            {
                try
                {
                    // 1. 查起点
                    var startLoc = await GetLocationByAddressAsync(startAddr);
                    if (startLoc == null) { MessageBox.Show($"无法识别起点：{startAddr}"); return new List<GeoPoint>(); }

                    // 2. 查终点
                    var endLoc = await GetLocationByAddressAsync(endAddr);
                    if (endLoc == null) { MessageBox.Show($"无法识别终点：{endAddr}"); return new List<GeoPoint>(); }

                    // 3. 查途经点 (循环处理)
                    string waypointsStr = "";
                    if (viaAddrs != null && viaAddrs.Count > 0)
                    {
                        var locList = new List<string>();
                        foreach (var addr in viaAddrs)
                        {
                            if (string.IsNullOrWhiteSpace(addr)) continue;
                            var p = await GetLocationByAddressAsync(addr);
                            if (p != null)
                            {
                                locList.Add($"{p.Longitude:F6},{p.Latitude:F6}");
                            }
                        }
                        // 高德要求用分号 ; 隔开
                        waypointsStr = string.Join(";", locList);
                    }

                    // 4. 请求最终路径
                    return await RequestDrivingRouteAsync(startLoc, endLoc, waypointsStr);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"出错: {ex.Message}");
                    return new List<GeoPoint>();
                }
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
            private async Task<List<GeoPoint>> RequestDrivingRouteAsync(GeoPoint start, GeoPoint end, string waypoints)
            {
                var list = new List<GeoPoint>();
                try
                {
                    string url = $"https://restapi.amap.com/v3/direction/driving?origin={start.Longitude:F6},{start.Latitude:F6}&destination={end.Longitude:F6},{end.Latitude:F6}&extensions=base&strategy=0&key={ApiKey}";

                    // 如果有途经点，拼接到 URL 后面
                    if (!string.IsNullOrEmpty(waypoints))
                    {
                        url += $"&waypoints={waypoints}";
                    }

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
                                    // 解析第一个方案的所有步骤
                                    if (paths[0].TryGetProperty("steps", out var steps))
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
                                                    {
                                                        list.Add(new GeoPoint(
                                                            double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
                                                            double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)
                                                        ));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"解析路径失败: {ex.Message}");
                }
                return list;
            }
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