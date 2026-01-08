using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;

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
using SmartNanjingTravel.ViewModels;

namespace SmartNanjingTravel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AmapPoiViewModel _amapPoiViewModel;

        public ObservableCollection<string> ViaPoints { get; set; } = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            _amapPoiViewModel = new AmapPoiViewModel();

            // 设置ItemsControl的数据源
            ViaPointsItemsControl.ItemsSource = ViaPoints;
            // --- 新增：订阅地图状态变化事件 ---
            // 监听鼠标移动显示经纬度
            MyMapView.MouseMove += MyMapView_MouseMove;
            // 监听地图视图变化（缩放、旋转）
            MyMapView.ViewpointChanged += MyMapView_ViewpointChanged;
        }
        // 在 ViewpointChanged 事件中更新
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
            // --- 1. 更新指北针 ---
            // 地图向右转（正），指北针图标应向左转（负），使其始终垂直向上
            CompassRotation.Angle = -MyMapView.MapRotation;

            // --- 2. 更新图形比例尺 ---
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
            // UnitsPerPixel 是当前缩放级别下，屏幕一个像素对应的地图单位（通常是米）
            double mPerPixel = MyMapView.UnitsPerPixel;

            // 目标：我们希望比例尺长度在屏幕上大概是 100 像素左右
            double targetLengthInPixels = 100;
            double actualDistance = targetLengthInPixels * mPerPixel;

            // 对距离进行取整显示（例如 123米 -> 100米，或者是 1, 2, 5 进制）
            double roundedDistance = RoundToSignificant(actualDistance);

            // 重新计算对齐后的像素宽度
            double finalBarWidth = roundedDistance / mPerPixel;

            // 更新 UI
            ScaleDistanceText.Text = roundedDistance >= 1000 ? $"{roundedDistance / 1000:F1} km" : $"{roundedDistance:F0} m";
            ScaleBarPath.Data = System.Windows.Media.Geometry.Parse($"M 0,0 L 0,8 L {finalBarWidth},8 L {finalBarWidth},0");
            ScaleBarCanvas.Width = finalBarWidth;
        }
        // 1. 放大功能：将当前比例尺缩小一半

        // 辅助函数：让比例尺数字看起来更自然 (如 100, 200, 500)
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
                await _amapPoiViewModel.QueryPoiAsync(MyMapView);

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

        // 地图点击事件处理方法
        private async void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            try
            {
                // 1. 查找ScenicSpotsOverlay叠加层
                var scenicOverlay = MyMapView.GraphicsOverlays
                    .FirstOrDefault(o => o.Id == "ScenicSpotsOverlay");

                // 如果没有景点叠加层或叠加层为空，直接返回
                if (scenicOverlay == null || scenicOverlay.Graphics.Count == 0)
                {
                    return;
                }

                // 2. 获取点击位置的Graphic（景点点位）
                IdentifyGraphicsOverlayResult result = await MyMapView.IdentifyGraphicsOverlayAsync(
                    scenicOverlay,
                    e.Position,
                    10, // 点击容差（像素），避免点击偏差
                    false);

                // 3. 没有点击到景点，直接返回
                if (result.Graphics.Count == 0) return;

                // 4. 获取点击的景点Graphic，提取信息
                Graphic scenicGraphic = result.Graphics[0];
                string name = scenicGraphic.Attributes["名称"]?.ToString() ?? "未知景点";
                string rating = scenicGraphic.Attributes["评分"]?.ToString() ?? "暂无评分";
                string district = scenicGraphic.Attributes["行政区"]?.ToString() ?? "未知行政区";
                string openTime = scenicGraphic.Attributes["开门时间"]?.ToString() ?? "暂无营业时间";

                // 5. 显示景点详情窗口
                ScenicInfoWindow infoWindow = new ScenicInfoWindow(name, rating, district, openTime);
                infoWindow.Owner = this;
                infoWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"点击景点时出错：{ex.Message}");
            }
        }


        private void CloseRoutePlanningPanel_Click(object sender, RoutedEventArgs e)
        {
            RoutePlanningPanel.Visibility = Visibility.Collapsed;
        }

        // 添加途径点按钮点击事件
        private void AddViaPointButton_Click(object sender, RoutedEventArgs e)
        {
            // 向集合添加一个空字符串，会在UI中显示为空白输入框
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
            // 这里实现路径规划逻辑
            string start = StartPointTextBox.Text;
            string destination = DestinationTextBox.Text;

            // 可以获取所有途径点
            foreach (var viaPoint in ViaPoints)
            {
                // 处理每个途径点
            }

            MessageBox.Show("路径规划功能待实现");
        }
        // MainWindow.xaml.cs 中的部分代码
        // 删除单个途径点

        private void DeleteViaPointButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is string viaPoint)
            {
                ViaPoints.Remove(viaPoint);
            }
        }
        // 在我的收藏按钮点击事件中添加


        // 关闭收藏面板
        private void CloseFavoritesPanel_Click(object sender, RoutedEventArgs e)
        {
            FavoritesPanel.Visibility = Visibility.Collapsed;
        }

        // 加载收藏数据
        private void LoadFavoritesData()
        {
            // 这里可以加载实际的收藏数据
            // 示例：更新收藏数量
            UpdateFavoritesCount();

            // 如果没有收藏，显示空状态
            ShowEmptyStateIfNeeded();
        }

        // 更新收藏数量
        private void UpdateFavoritesCount()
        {
            if (FavoritesItemsControl.Items.Count == 0)
            {
                FavoritesCountText.Text = "暂无收藏";
            }
            else
            {
                FavoritesCountText.Text = $"共 {FavoritesItemsControl.Items.Count} 个收藏";
            }
        }

        // 显示空状态
        private void ShowEmptyStateIfNeeded()
        {
            if (FavoritesItemsControl.Items.Count == 0)
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
            if (sender is RadioButton radioButton)
            {
                // RadioButton会自动处理选中状态，我们只需要根据选中状态进行筛选
                string filterType = radioButton.Content.ToString();

                // 根据筛选条件过滤收藏列表
                ApplyFilter(filterType);
            }
        }

        // 应用筛选
        private void ApplyFilter(string filterType)
        {
            // 这里可以实现筛选逻辑
            // 例如：if (filterType == "景点") { ... }
        }

        // 在地图上查看
        private void ViewOnMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                // 这里可以实现在地图上定位的功能
                // 例如：var favoriteItem = button.Tag as FavoriteItem;
                // NavigateToLocation(favoriteItem);
                MessageBox.Show("在地图上查看功能待实现");
            }
        }

        // 删除收藏
        private void DeleteFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                var result = MessageBox.Show("确定要删除这个收藏吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 这里可以实现删除逻辑
                    // 例如：RemoveFavorite(button.Tag as FavoriteItem);
                    MessageBox.Show("删除收藏功能待实现");

                    // 更新UI
                    UpdateFavoritesCount();
                    ShowEmptyStateIfNeeded();
                }
            }
        }

        // 清空所有收藏
        private void ClearAllFavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要清空所有收藏吗？", "确认清空",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 这里可以实现清空逻辑
                // 例如：ClearAllFavorites();
                MessageBox.Show("清空收藏功能待实现");

                // 更新UI
                UpdateFavoritesCount();
                ShowEmptyStateIfNeeded();
            }
        }

        // 导出收藏
        private void ExportFavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            // 这里可以实现导出功能
            MessageBox.Show("导出收藏功能待实现");
        }
        // 新增：显示/隐藏图层控制面板


        // 新增：关闭图层控制面板
        private void CloseLayerControlPanel_Click(object sender, RoutedEventArgs e)
        {
            LayerControlPanel.Visibility = Visibility.Collapsed;
        }
        // 统一管理所有侧边栏/浮动面板的显示与隐藏
        private void SwitchPanel(FrameworkElement panelToShow)
        {
            // 1. 列出所有需要互相排斥的面板
            var panels = new List<FrameworkElement>
    {
        RoutePlanningPanel,
        FavoritesPanel,
        LayerControlPanel // 如果图层面板也要互斥，就加在这里
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
        // 点击行程规划
        private void RoutePlanningButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(RoutePlanningPanel);
        }

        // 点击我的收藏
        private void MyFavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchPanel(FavoritesPanel);
        }

        // 点击图层控制（如果你希望打开图层时也关闭其他的）
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
    }
}