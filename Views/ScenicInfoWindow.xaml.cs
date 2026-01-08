using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SmartNanjingTravel
{
    public partial class ScenicInfoWindow : Window
    {
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

        // 带参数的构造函数（业务调用）
        public ScenicInfoWindow(string name, string rating, string district, string openTime,string imageUrl) : this()
        {
            TxtName.Text = name ?? "未知景点";
            TxtRating.Text = $"评分：{rating ?? "暂无"}";
            TxtDistrict.Text = $"行政区：{district ?? "未知"}";
            TxtOpenTime.Text = openTime ?? "暂无营业时间信息";
            // 【新增】加载图片逻辑
            if (!string.IsNullOrEmpty(imageUrl))
            {
                try
                {
                    // 创建 BitmapImage 加载网络图片
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
                    bitmap.EndInit(); // 触发加载
                    ImgScenic.Source = bitmap;
                    ImgScenic.Visibility = Visibility.Visible;
                }
                catch
                {
                    // 加载失败时隐藏图片或显示默认图
                    ImgScenic.Visibility = Visibility.Collapsed;
                }
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

        // 去这里按钮事件（预留函数）
        private void BtnGoHere_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}