using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;
using SmartNanjingTravel.Models;
using SmartNanjingTravel.Services;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using Windows.Devices.Geolocation;

namespace SmartNanjingTravel
{
    public partial class App : Application
    {
        public static string CurrentUserId { get; set; } = "default_user"; // 默认用户ID
        public static FavoriteService FavoriteService { get; private set; }
        public static string GeodatabasePath { get; private set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            FavoriteService = new FavoriteService();
            FavoriteService.InitializeDatabase();
            SetGeodatabasePath();

            // 初始化ArcGIS Maps SDK
            try
            {
                ArcGISRuntimeEnvironment.Initialize(config => config
                  .ConfigureAuthentication(auth => auth
                     .UseDefaultChallengeHandler()
                   )
                );
                ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ArcGIS Maps SDK runtime initialization failed.");
                this.Shutdown();
            }
        }

        private void SetGeodatabasePath()
        {
            // 应用程序运行目录下的Data文件夹
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(baseDirectory, "Data");
            GeodatabasePath = Path.Combine(dataDirectory, "Smart Traveling.geodatabase");
            if (!File.Exists(GeodatabasePath))
            {
                MessageBox.Show($"文件不存在：{GeodatabasePath}", "错误");
                return;
            }
        }
    }
}
