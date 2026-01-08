using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SmartNanjingTravel.Models;
using SmartNanjingTravel.Services;

namespace SmartNanjingTravel
{
    public partial class ScenicInfoWindow : Window
    {
        private string _currentPoiName;
        private double _longitude;
        private double _latitude;
        private bool _isFavorite;
        private int _poiId;

        // 设计器必需的无参构造函数
        public ScenicInfoWindow()
        {
            InitializeComponent();

            // 设计器模式填充测试数据
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                TxtName.Text = "中山陵";
                TxtRating.Text = "评分：5.0";
                TxtDistrict.Text = "行政区：玄武区";
                TxtOpenTime.Text = "08:30-17:00（周一闭馆）";
            }
        }

        // 带参数的构造函数（修复递归调用）
        public ScenicInfoWindow(string name, string rating, string district, string openTime, string imageUrl)
            : this()
        {
            InitializeWithParameters(name, rating, district, openTime, imageUrl, 0, 0, 0);
        }

        // 带更多参数的构造函数（包括POI_ID和坐标）
        public ScenicInfoWindow(string name, string rating, string district, string openTime, string imageUrl,
                               int poiId, double longitude, double latitude)
            : this()
        {
            InitializeWithParameters(name, rating, district, openTime, imageUrl, poiId, longitude, latitude);
        }

        // 初始化方法，避免代码重复
        private void InitializeWithParameters(string name, string rating, string district, string openTime,
                                            string imageUrl, int poiId, double longitude, double latitude)
        {
            _currentPoiName = name ?? "未知景点";
            _longitude = longitude;
            _latitude = latitude;
            _poiId = poiId;

            TxtName.Text = _currentPoiName;
            TxtRating.Text = $"评分：{rating ?? "暂无"}";
            TxtDistrict.Text = $"行政区：{district ?? "未知"}";
            TxtOpenTime.Text = openTime ?? "暂无营业时间信息";

            // 加载图片
            if (!string.IsNullOrEmpty(imageUrl))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
                    bitmap.EndInit();
                    ImgScenic.Source = bitmap;
                    ImgScenic.Visibility = Visibility.Visible;
                }
                catch
                {
                    ImgScenic.Visibility = Visibility.Collapsed;
                }
            }

            // 检查是否已收藏
            CheckFavoriteStatus();
        }

        private void CheckFavoriteStatus()
        {
            try
            {
                // 根据POI名称或坐标查找POI ID
                if (_poiId == 0)
                {
                    _poiId = FindOrCreatePoiId();
                }

                if (_poiId > 0)
                {
                    _isFavorite = App.FavoriteService.IsFavorite(App.CurrentUserId, _poiId);
                    UpdateFavoriteIcon();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查收藏状态失败: {ex.Message}");
            }
        }

        private int FindOrCreatePoiId()
        {
            // 使用景点名称生成哈希值作为临时ID
            return Math.Abs(_currentPoiName.GetHashCode());
        }

        private void UpdateFavoriteIcon()
        {
            if (_isFavorite)
            {
                FavoriteIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Heart;
                FavoriteButton.ToolTip = "取消收藏";
            }
            else
            {
                FavoriteIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.HeartOutline;
                FavoriteButton.ToolTip = "收藏此景点";
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_poiId <= 0)
                {
                    _poiId = FindOrCreatePoiId();
                }

                if (_poiId > 0)
                {
                    if (_isFavorite)
                    {
                        // 取消收藏
                        if (App.FavoriteService.RemoveFavorite(App.CurrentUserId, _poiId))
                        {
                            _isFavorite = false;
                            MessageBox.Show("已取消收藏", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        // 添加收藏 - 传递完整的景点信息
                        var favorite = new FavoriteItem
                        {
                            UserId = App.CurrentUserId,
                            PoiId = _poiId,
                            Name = _currentPoiName,
                            District = TxtDistrict.Text.Replace("行政区：", ""),
                            Address = "",
                            Latitude = _latitude,
                            Longitude = _longitude,
                            Rating = TxtRating.Text.Replace("评分：", ""),
                            OpenTime = TxtOpenTime.Text,
                            Photos = ImgScenic.Source?.ToString() ?? "",
                            Notes = $"收藏于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                            FavoriteTime = DateTime.Now
                        };

                        if (App.FavoriteService.AddFavorite(favorite))
                        {
                            _isFavorite = true;
                            MessageBox.Show("收藏成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }

                    UpdateFavoriteIcon();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 窗口拖动事件
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // 关闭按钮事件
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // 去这里按钮事件
        private void BtnGoHere_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"导航到 {_currentPoiName}\n经度: {_longitude:F6}, 纬度: {_latitude:F6}",
                "导航", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}