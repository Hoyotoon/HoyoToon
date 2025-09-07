#if UNITY_EDITOR
using System;
using System.IO;
using Utf8Json;
using Utf8Json.Resolvers;
using HoyoToon.API;

namespace HoyoToon.Parsing
{
    /// <summary>
    /// High-performance parser powered by Utf8Json. Uses StandardResolver by default.
    /// </summary>
    public sealed class Utf8JsonParsingService : HoyoToon.API.IJsonParsingService
    {
        static Utf8JsonParsingService()
        {
            // Ensure a sensible default resolver is set once.
            if (JsonSerializer.DefaultResolver == null)
            {
                JsonSerializer.SetDefaultResolver(StandardResolver.Default);
            }
        }

        public bool TryParse<T>(string json, out T result, out string error) where T : class
        {
            result = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "JSON string is null or empty"; return false; }
            try
            {
                // Utf8Json prefers bytes; convert once.
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                result = JsonSerializer.Deserialize<T>(bytes);
                return result != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                result = null;
                return false;
            }
        }

        public bool TryParse<T>(byte[] utf8Bytes, out T result, out string error) where T : class
        {
            result = null;
            error = null;
            if (utf8Bytes == null || utf8Bytes.Length == 0) { error = "Byte buffer is null or empty"; return false; }
            try
            {
                result = JsonSerializer.Deserialize<T>(utf8Bytes);
                return result != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                result = null;
                return false;
            }
        }

        public bool TryParse<T>(Stream stream, out T result, out string error) where T : class
        {
            result = null;
            error = null;
            if (stream == null) { error = "Stream is null"; return false; }
            try
            {
                // Utf8Json's Stream overload reads entire stream.
                result = JsonSerializer.Deserialize<T>(stream);
                return result != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                result = null;
                return false;
            }
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
    }
}
#endif
