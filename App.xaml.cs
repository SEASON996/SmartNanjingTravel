using System.Configuration;
using System.Data;
using System.Windows;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;
using SmartNanjingTravel.Data;

namespace SmartNanjingTravel
{
    public partial class App : Application
    {
        public static string CurrentUserId { get; set; } = "default_user"; // 默认用户ID

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 初始化数据库
            DatabaseHelper.InitializeDatabase();

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
