using System.Windows.Controls;
using System.Windows.Controls.Primitives; // 添加这行
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Reduction;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using SmartNanjingTravel.Data;
using SmartNanjingTravel.ViewModels;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;

namespace SmartNanjingTravel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AmapPoiViewModel _amapPoiViewModel;
        private ObservableCollection<FavoriteItem> _favoriteItems = new ObservableCollection<FavoriteItem>();
        private List<FavoriteItem> _allFavorites = new List<FavoriteItem>();

        public ObservableCollection<string> ViaPoints { get; set; } = new ObservableCollection<string>();

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
            // 更新指北针
            CompassRotation.Angle = -MyMapView.MapRotation;

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
                // 设置查询关键词为南京的主要景点类型
                _amapPoiViewModel.InputAddress = "景点";

                // 调用查询方法
                await _amapPoiViewModel.QueryPoiAsync(MyMapView, 30);

                // 定位到南京区域
                await MyMapView.SetViewpointAsync(new Viewpoint(
                    new MapPoint(118.8, 32.05, SpatialReferences.Wgs84),
                    50000));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载景点失败：{ex.Message}", "错误",
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

        // 添加途径点按钮点击事件
        private void AddViaPointButton_Click(object sender, RoutedEventArgs e)
        {
            ViaPoints.Add("");
        }

        // 清空所有输入框
        private void ClearRouteButton_Click(object sender, RoutedEventArgs e)
        {
            StartPointTextBox.Text = "";
            DestinationTextBox.Text = "";
            ViaPoints.Clear();
        }

        // 开始导航
        private void SearchRouteButton_Click(object sender, RoutedEventArgs e)
        {
            string start = StartPointTextBox.Text;
            string destination = DestinationTextBox.Text;

            foreach (var viaPoint in ViaPoints)
            {
                // 处理每个途径点
            }

            MessageBox.Show("路径规划功能待实现");
        }

        // 删除单个途径点
        private void DeleteViaPointButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is string viaPoint)
            {
                ViaPoints.Remove(viaPoint);
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
                _allFavorites = DatabaseHelper.GetFavorites(App.CurrentUserId);
                _favoriteItems.Clear();

                foreach (var item in _allFavorites)
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

        // 筛选按钮点击事件
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
        }

        // 应用筛选
        private void ApplyFilter(string filterType)
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
        }

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

        // 删除收藏
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
                        if (DatabaseHelper.RemoveFavorite(App.CurrentUserId, favorite.PoiId))
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

        // 清空所有收藏
        private void ClearAllFavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要清空所有收藏吗？此操作不可恢复！", "确认清空",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (DatabaseHelper.ClearAllFavorites(App.CurrentUserId))
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
                                            $"\"{item.Price}\"," +
                                            $"\"{item.OpenTime}\"," +
                                            $"\"{item.CategoryName}\"," +
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