#if UNITY_EDITOR
using System.IO;

namespace HoyoToon.API
{
    /// <summary>
    /// Abstraction for fast, robust JSON parsing that can be swapped independently.
    /// Editor-only for now.
    /// </summary>
    public interface IJsonParsingService
    {
        bool TryParse<T>(string json, out T result, out string error) where T : class;
        bool TryParse<T>(byte[] utf8Bytes, out T result, out string error) where T : class;
        bool TryParse<T>(Stream stream, out T result, out string error) where T : class;
        bool TryParseFile<T>(string absolutePath, out T result, out string error) where T : class;
    }
}
#endif
