using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace SmartNanjingTravel.Services
{
    /// <summary>
    /// 移动地理数据库服务 - 负责读取.gdb文件中的要素数据
    /// </summary>
    public class GeodatabaseService
    {
        private Geodatabase _geodatabase;

        /// <summary>
        /// 打开移动地理数据库
        /// </summary>
        public async Task<bool> OpenGeodatabaseAsync(string gdbPath)
        {
            try
            {
                _geodatabase = await Geodatabase.OpenAsync(gdbPath);
                return _geodatabase != null;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"打开地理数据库失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取所有要素表（点、线、面）
        /// </summary>
        public IReadOnlyList<GeodatabaseFeatureTable> GetFeatureTables()
        {
            return _geodatabase?.GeodatabaseFeatureTables;
        }

        /// <summary>
        /// 根据几何类型获取要素表
        /// </summary>
        public List<GeodatabaseFeatureTable> GetFeatureTablesByGeometryType(GeometryType geometryType)
        {
            var tables = new List<GeodatabaseFeatureTable>();

            if (_geodatabase == null) return tables;

            foreach (var table in _geodatabase.GeodatabaseFeatureTables)
            {
                if (table.GeometryType == geometryType)
                {
                    tables.Add(table);
                }
            }

            return tables;
        }

        /// <summary>
        /// 根据名称获取要素表
        /// </summary>
        public GeodatabaseFeatureTable GetFeatureTableByName(string tableName)
        {
            if (_geodatabase == null) return null;

            foreach (var table in _geodatabase.GeodatabaseFeatureTables)
            {
                if (table.TableName == tableName)
                    return table;
            }

            return null;
        }

        /// <summary>
        /// 关闭地理数据库
        /// </summary>
        public void Close()
        {
            _geodatabase?.Close();
            _geodatabase = null;
        }
    }
}
