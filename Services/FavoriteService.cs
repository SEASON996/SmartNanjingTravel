using Microsoft.Data.Sqlite;
using SmartNanjingTravel.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;


namespace SmartNanjingTravel.Services
{
    /// <summary>
    /// 收藏服务 - 专注于收藏点的增删改查
    /// </summary>
    public class FavoriteService
    {
        private string? _databasePath;

        /// 初始化数据库
        public FavoriteService()
        {
            // 获取当前程序集所在目录
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(baseDirectory, "Data");

            // 数据库文件路径
            _databasePath = Path.Combine(dataDirectory, "Travel.db");
        }
        public void InitializeDatabase()
        {
            try
            {

                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS USER_FAVORITES (
                                FAV_ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                USER_ID VARCHAR(50) NOT NULL,
                                POI_ID INTEGER NOT NULL,
                                POI_NAME VARCHAR(100) NOT NULL,
                                DISTRICT VARCHAR(50),
                                ADDRESS TEXT,
                                LATITUDE REAL,
                                LONGITUDE REAL,
                                RATING VARCHAR(20),
                                OPEN_TIME TEXT,
                                PHOTOS TEXT,
                                NOTES TEXT,
                                FAV_TIME TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                UNIQUE (USER_ID, POI_ID)
                            );";
                        command.ExecuteNonQuery();

                        // 创建索引
                        command.CommandText = @"
                            CREATE INDEX IF NOT EXISTS idx_fav_user ON USER_FAVORITES(USER_ID);
                            CREATE INDEX IF NOT EXISTS idx_fav_poi ON USER_FAVORITES(POI_ID);";
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

        /// <summary>
        /// 添加收藏
        /// </summary>
        public bool AddFavorite(FavoriteItem favorite)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                    INSERT OR REPLACE INTO USER_FAVORITES 
                    (USER_ID, POI_ID, POI_NAME, DISTRICT, ADDRESS, 
                     LATITUDE, LONGITUDE, RATING, OPEN_TIME, PHOTOS, NOTES) 
                    VALUES (@userId, @poiId, @poiName, @district, @address,
                            @latitude, @longitude, @rating, @openTime, @photos, @notes)";

                        command.Parameters.AddWithValue("@userId", favorite.UserId);
                        command.Parameters.AddWithValue("@poiId", favorite.PoiId);
                        command.Parameters.AddWithValue("@poiName", favorite.Name ?? "");
                        command.Parameters.AddWithValue("@district", favorite.District ?? "未知区域");
                        command.Parameters.AddWithValue("@address", favorite.Address ?? "");
                        command.Parameters.AddWithValue("@latitude", favorite.Latitude);
                        command.Parameters.AddWithValue("@longitude", favorite.Longitude);
                        command.Parameters.AddWithValue("@rating", favorite.Rating ?? "暂无评分");
                        command.Parameters.AddWithValue("@openTime", favorite.OpenTime ?? "");
                        command.Parameters.AddWithValue("@photos", favorite.Photos ?? "");
                        command.Parameters.AddWithValue("@notes", favorite.Notes ?? "");

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

        /// <summary>
        /// 删除收藏
        /// </summary>
        public bool RemoveFavorite(string userId, int poiId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM USER_FAVORITES WHERE USER_ID = @userId AND POI_ID = @poiId";
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

        /// <summary>
        /// 获取用户的所有收藏
        /// </summary>
        public List<FavoriteItem> GetFavorites(string userId)
        {
            var favorites = new List<FavoriteItem>();

            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                    SELECT 
                        FAV_ID, USER_ID, POI_ID, POI_NAME, DISTRICT, ADDRESS,
                        LATITUDE, LONGITUDE, RATING, OPEN_TIME, PHOTOS, NOTES, FAV_TIME
                    FROM USER_FAVORITES 
                    WHERE USER_ID = @userId 
                    ORDER BY FAV_TIME DESC";

                        command.Parameters.AddWithValue("@userId", userId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var name = reader.GetString(reader.GetOrdinal("POI_NAME"));

                                favorites.Add(new FavoriteItem
                                {
                                    FavoriteId = reader.GetInt32(reader.GetOrdinal("FAV_ID")),
                                    UserId = reader.GetString(reader.GetOrdinal("USER_ID")),
                                    PoiId = reader.GetInt32(reader.GetOrdinal("POI_ID")),
                                    Name = name,
                                    Description = $"收藏的景点：{name}", // 生成描述

                                    // 其他属性
                                    District = reader.IsDBNull(reader.GetOrdinal("DISTRICT"))
                                        ? "未知区域" : reader.GetString(reader.GetOrdinal("DISTRICT")),
                                    Address = reader.IsDBNull(reader.GetOrdinal("ADDRESS"))
                                        ? "" : reader.GetString(reader.GetOrdinal("ADDRESS")),
                                    Latitude = reader.GetDouble(reader.GetOrdinal("LATITUDE")),
                                    Longitude = reader.GetDouble(reader.GetOrdinal("LONGITUDE")),
                                    Rating = reader.IsDBNull(reader.GetOrdinal("RATING"))
                                        ? "暂无评分" : reader.GetString(reader.GetOrdinal("RATING")),
                                    OpenTime = reader.IsDBNull(reader.GetOrdinal("OPEN_TIME"))
                                        ? "" : reader.GetString(reader.GetOrdinal("OPEN_TIME")),
                                    Photos = reader.IsDBNull(reader.GetOrdinal("PHOTOS"))
                                        ? "" : reader.GetString(reader.GetOrdinal("PHOTOS")),
                                    Notes = reader.IsDBNull(reader.GetOrdinal("NOTES"))
                                        ? "" : reader.GetString(reader.GetOrdinal("NOTES")),
                                    FavoriteTime = reader.GetDateTime(reader.GetOrdinal("FAV_TIME"))
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

        /// <summary>
        /// 清空用户的所有收藏
        /// </summary>
        public bool ClearAllFavorites(string userId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM USER_FAVORITES WHERE USER_ID = @userId";
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

        /// <summary>
        /// 检查是否已收藏
        /// </summary>
        public bool IsFavorite(string userId, int poiId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM USER_FAVORITES WHERE USER_ID = @userId AND POI_ID = @poiId";
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

        /// <summary>
        /// 更新收藏备注
        /// </summary>
        public bool UpdateFavoriteNotes(string userId, int poiId, string notes)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            UPDATE USER_FAVORITES 
                            SET NOTES = @notes 
                            WHERE USER_ID = @userId AND POI_ID = @poiId";

                        command.Parameters.AddWithValue("@notes", notes ?? "");
                        command.Parameters.AddWithValue("@userId", userId);
                        command.Parameters.AddWithValue("@poiId", poiId);

                        return command.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新备注失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取收藏数量
        /// </summary>
        public int GetFavoriteCount(string userId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM USER_FAVORITES WHERE USER_ID = @userId";
                        command.Parameters.AddWithValue("@userId", userId);

                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}