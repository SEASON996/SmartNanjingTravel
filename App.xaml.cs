using System.Configuration;
using System.Data;
using System.Windows;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;
using SmartNanjingTravel.Models;
using SmartNanjingTravel.Services;

namespace SmartNanjingTravel
{
    public partial class App : Application
    {
        public static string CurrentUserId { get; set; } = "default_user"; // 默认用户ID
        public static FavoriteService FavoriteService { get; private set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            FavoriteService = new FavoriteService();
            FavoriteService.InitializeDatabase();

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
    }
}
