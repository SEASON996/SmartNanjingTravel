using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Reduction;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
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

            // 设置ItemsControl的数据源
            ViaPointsItemsControl.ItemsSource = ViaPoints;
            _amapPoiViewModel = new AmapPoiViewModel();
            this.DataContext = _amapPoiViewModel;

        }

        private async void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 设置查询关键词为南京的主要景点类型
                _amapPoiViewModel.InputAddress = "景点";

                // 调用查询方法
                await _amapPoiViewModel.QueryPoiAsync(MyMapView,30);

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
                        // 3. 【核心修改】精准识别这个子图层
                        // 容差设为 15，returnPopupsOnly = false
                        var result = await MyMapView.IdentifyLayerAsync(subLayer, e.Position, 15, false);

                        if (result.GeoElements.Count > 0)
                        {
                            var element = result.GeoElements.First();

                            // 情况 A：点到了聚合圈圈 (AggregateGeoElement)
                            if (element is AggregateGeoElement)
                            {
                                // 这就是那个蓝色的数字圈，不像 GraphicsOverlay，这个是点不开详情的
                                // 我们直接返回，让用户去放大地图
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
                                if (name == "暂无") name = "未知景点"; // 特殊处理名字

                                string rating = GetVal("Rating", "评分");
                                string district = GetVal("Adname", "行政区");
                                string openTime = GetVal("Opentime", "开门时间");

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

                                // 弹窗
                                var win = new ScenicInfoWindow(name, rating, district, openTime, imageUrl);
                                win.Owner = this;
                                win.ShowDialog();
                                return; // 找到了就结束
                            }
                        }
                    }
                }

                // ==========================================
                // 4. (保底逻辑) 如果上面的聚合图层没找到，再去查旧的 GraphicsOverlay
                // ==========================================
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