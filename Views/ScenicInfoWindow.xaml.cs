using System.Windows;
using System.Windows.Input;
using System.ComponentModel;

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
        public ScenicInfoWindow(string name, string rating, string district, string openTime) : this()
        {
            TxtName.Text = name ?? "未知景点";
            TxtRating.Text = $"评分：{rating ?? "暂无"}";
            TxtDistrict.Text = $"行政区：{district ?? "未知"}";
            TxtOpenTime.Text = openTime ?? "暂无营业时间信息";
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
            // 这里可以添加导航到该景点的功能
            // 暂时只显示一个消息
            MessageBox.Show("导航功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}