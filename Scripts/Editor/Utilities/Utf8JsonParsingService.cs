#if UNITY_EDITOR
using System;
using System.IO;
using Utf8Json;
using Utf8Json.Resolvers;
using HoyoToon.Editor.API;

namespace HoyoToon.Editor.Utilities
{
    public sealed class Utf8JsonParsingService : HoyoToon.Editor.API.IJsonParsingService
    {
        static Utf8JsonParsingService()
        {
            if (JsonSerializer.DefaultResolver == null)
            {
                JsonSerializer.SetDefaultResolver(StandardResolver.Default);
            }
        }

        public bool TryParse<T>(string json, out T result, out string error) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                result = null;
                error = "JSON string is null or empty";
                return false;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return TryDeserializeCore(() => JsonSerializer.Deserialize<T>(bytes), out result, out error);
        }

        public bool TryParse<T>(byte[] utf8Bytes, out T result, out string error) where T : class
        {
            if (utf8Bytes == null || utf8Bytes.Length == 0)
            {
                result = null;
                error = "Byte buffer is null or empty";
                return false;
            }

            return TryDeserializeCore(() => JsonSerializer.Deserialize<T>(utf8Bytes), out result, out error);
        }

        public bool TryParse<T>(Stream stream, out T result, out string error) where T : class
        {
            if (stream == null)
            {
                result = null;
                error = "Stream is null";
                return false;
            }

            return TryDeserializeCore(() => JsonSerializer.Deserialize<T>(stream), out result, out error);
        }

        public bool TryParseFile<T>(string absolutePath, out T result, out string error) where T : class
        {
            result = null;
            error = null;
            if (string.IsNullOrWhiteSpace(absolutePath)) { error = "Path is null or empty"; return false; }
            try
            {
                using (var fs = File.OpenRead(absolutePath))
                {
                    return TryParse<T>(fs, out result, out error);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                result = null;
                return false;
            }
        }

        private static bool TryDeserializeCore<T>(Func<T> deserialize, out T result, out string error) where T : class
        {
            try
            {
                result = deserialize();
                error = null;
                return result != null;
            }
            catch (Exception ex)
            {
                result = null;
                error = ex.Message;
                return false;
            }
        }
    }
}
#endif
