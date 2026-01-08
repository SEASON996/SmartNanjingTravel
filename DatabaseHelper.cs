using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;

namespace SmartNanjingTravel.Data
{
    public class DatabaseHelper
    {
        private static string _databasePath = "D:\\111.db";

        public static string DatabasePath
        {
            get => _databasePath;
            set
            {
                _databasePath = value;
                InitializeDatabase();
            }
        }

        public static void InitializeDatabase()
        {
            try
            {
                // 确保数据库目录存在
                string directory = Path.GetDirectoryName(DatabasePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 创建数据库连接
                using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
                {
                    connection.Open();

                    // 创建表结构
                    using (var command = connection.CreateCommand())
                    {
                        // 分类表
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS CATEGORY (
                                CAT_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                CAT_NAME VARCHAR(50) NOT NULL UNIQUE,
                                CAT_DESC TEXT,
                                CAT_ICON VARCHAR(50),
                                CAT_COLOR VARCHAR(7) DEFAULT '#2196F3',
                                DISP_ORDER INTEGER DEFAULT 0,
                                CREATE_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                UPDATE_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                            );";
                        command.ExecuteNonQuery();

                        // 景点表
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS POI_INFO (
                                POI_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                POI_NAME VARCHAR(100) NOT NULL,
                                CAT_ID INTEGER NOT NULL,
                                DISTRICT VARCHAR(50),
                                ADDR TEXT,
                                LAT DECIMAL(10, 8) NOT NULL,
                                LNG DECIMAL(11, 8) NOT NULL,
                                MAP_ICON VARCHAR(50) DEFAULT 'default',
                                MARKER_COLOR VARCHAR(20) DEFAULT '#FF5722',
                                MARKER_SIZE VARCHAR(10) DEFAULT 'medium',
                                ZOOM_LEVEL INTEGER DEFAULT 12,
                                DESC TEXT,
                                HISTORY TEXT,
                                OPEN_TIME TEXT,
                                PRICE DECIMAL(10, 2),
                                RATING DECIMAL(3, 2) DEFAULT 0.0,
                                RATE_COUNT INTEGER DEFAULT 0,
                                IS_ACTIVE BOOLEAN DEFAULT 1,
                                CREATE_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                UPDATE_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                UNIQUE (LAT, LNG, POI_NAME),
                                CHECK (LAT BETWEEN -90 AND 90),
                                CHECK (LNG BETWEEN -180 AND 180)
                            );";
                        command.ExecuteNonQuery();

                        // 用户收藏表
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS USER_FAV (
                                FAV_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                USER_ID VARCHAR(50) NOT NULL,
                                POI_ID INTEGER NOT NULL,
                                NOTES TEXT,
                                FAV_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                DISP_ORDER INTEGER DEFAULT 0,
                                UNIQUE (USER_ID, POI_ID)
                            );";
                        command.ExecuteNonQuery();

                        // 主题路线表
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS THEME_ROUTE (
                                ROUTE_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                ROUTE_NAME VARCHAR(100) NOT NULL,
                                ROUTE_DESC TEXT,
                                THEME VARCHAR(50) NOT NULL,
                                EST_HOURS DECIMAL(4, 1),
                                TOTAL_KM DECIMAL(8, 2),
                                DIFF_LEVEL VARCHAR(20),
                                REC_SEASON VARCHAR(50),
                                IS_PUBLISHED BOOLEAN DEFAULT 1,
                                LINE_COLOR VARCHAR(7) DEFAULT '#4CAF50',
                                CREATE_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                UPDATE_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                            );";
                        command.ExecuteNonQuery();

                        // 路线景点关联表
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS ROUTE_POI (
                                RP_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                ROUTE_ID INTEGER NOT NULL,
                                POI_ID INTEGER NOT NULL,
                                STOP_ORDER INTEGER NOT NULL,
                                STAY_MIN INTEGER,
                                TRANS_TYPE VARCHAR(20),
                                NOTES TEXT,
                                UNIQUE (ROUTE_ID, STOP_ORDER)
                            );";
                        command.ExecuteNonQuery();

                        // 创建索引
                        command.CommandText = @"
                            CREATE INDEX IF NOT EXISTS idx_poi_cat ON POI_INFO(CAT_ID);
                            CREATE INDEX IF NOT EXISTS idx_poi_loc ON POI_INFO(LAT, LNG);
                            CREATE INDEX IF NOT EXISTS idx_fav_user ON USER_FAV(USER_ID);
                            CREATE INDEX IF NOT EXISTS idx_fav_poi ON USER_FAV(POI_ID);";
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库初始化失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool AddFavorite(string userId, int poiId, string notes = "")
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT OR IGNORE INTO USER_FAV (USER_ID, POI_ID, NOTES) 
                            VALUES (@userId, @poiId, @notes)";

                        command.Parameters.AddWithValue("@userId", userId);
                        command.Parameters.AddWithValue("@poiId", poiId);
                        command.Parameters.AddWithValue("@notes", notes ?? "");

                        return command.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"收藏失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool RemoveFavorite(string userId, int poiId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM USER_FAV WHERE USER_ID = @userId AND POI_ID = @poiId";
                        command.Parameters.AddWithValue("@userId", userId);
                        command.Parameters.AddWithValue("@poiId", poiId);

                        return command.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消收藏失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static List<FavoriteItem> GetFavorites(string userId)
        {
            var favorites = new List<FavoriteItem>();

            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT 
                                uf.FAV_ID, uf.USER_ID, uf.POI_ID, uf.NOTES, uf.FAV_TIME,
                                pi.POI_NAME, pi.DISTRICT, pi.ADDR, pi.LAT, pi.LNG,
                                pi.DESC, pi.RATING, pi.PRICE, pi.OPEN_TIME,
                                c.CAT_NAME
                            FROM USER_FAV uf
                            INNER JOIN POI_INFO pi ON uf.POI_ID = pi.POI_ID
                            LEFT JOIN CATEGORY c ON pi.CAT_ID = c.CAT_ID
                            WHERE uf.USER_ID = @userId
                            ORDER BY uf.FAV_TIME DESC";

                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                favorites.Add(new FavoriteItem
                                {
                                    FavoriteId = reader.GetInt32(0),
                                    UserId = reader.GetString(1),
                                    PoiId = reader.GetInt32(2),
                                    Notes = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    FavoriteTime = reader.GetDateTime(4),
                                    Name = reader.GetString(5),
                                    District = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    Address = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    Latitude = reader.GetDecimal(8),
                                    Longitude = reader.GetDecimal(9),
                                    Description = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                    Rating = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                                    Price = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                    OpenTime = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                    CategoryName = reader.IsDBNull(14) ? "未分类" : reader.GetString(14)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载收藏失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return favorites;
        }

        public static bool ClearAllFavorites(string userId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM USER_FAV WHERE USER_ID = @userId";
                        command.Parameters.AddWithValue("@userId", userId);

                        return command.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清空收藏失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool IsFavorite(string userId, int poiId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM USER_FAV WHERE USER_ID = @userId AND POI_ID = @poiId";
                        command.Parameters.AddWithValue("@userId", userId);
                        command.Parameters.AddWithValue("@poiId", poiId);

                        var count = Convert.ToInt32(command.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public class FavoriteItem
    {
        public int FavoriteId { get; set; }
        public string UserId { get; set; }
        public int PoiId { get; set; }
        public string Notes { get; set; }
        public DateTime FavoriteTime { get; set; }
        public string Name { get; set; }
        public string District { get; set; }
        public string Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Description { get; set; }
        public decimal Rating { get; set; }
        public decimal Price { get; set; }
        public string OpenTime { get; set; }
        public string CategoryName { get; set; }

        // UI显示属性
        public string Type => "景点";
        public string DisplayDescription => Description.Length > 50 ? Description.Substring(0, 50) + "..." : Description;
        public string AddedDate => FavoriteTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
