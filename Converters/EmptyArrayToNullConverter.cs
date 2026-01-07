using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SmartNanjingTravel.Converters
{
    /// <summary>
    /// 处理高德API返回的空数组转null/空字符串
    /// 用于BizExt.Rating等字段
    /// </summary>
    public class EmptyArrayToNullConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // 如果遇到空数组标记，返回空字符串
            if (reader.TokenType == JsonToken.StartArray)
            {
                // 跳过这个空数组
                serializer.Deserialize(reader);
                return string.Empty;
            }

            // 正常字符串直接返回
            return reader.Value?.ToString() ?? string.Empty;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // 序列化时保持原样
            serializer.Serialize(writer, value);
        }
    }
}
