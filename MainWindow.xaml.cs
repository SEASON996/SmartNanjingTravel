using Esri.ArcGISRuntime.Mapping;
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


        public ObservableCollection<string> ViaPoints { get; set; } = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();

            // 设置ItemsControl的数据源
            ViaPointsItemsControl.ItemsSource = ViaPoints;
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