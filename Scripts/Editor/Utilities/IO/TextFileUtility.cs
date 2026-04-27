#if UNITY_EDITOR
using System.IO;

namespace HoyoToon.Editor.Utilities.IO
{
    public static class TextFileUtility
    {
        public static bool TryReadAllText(string path, out string text)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                text = null;
                return false;
            }

            try
            {
                text = File.ReadAllText(path);
                return true;
            }
            catch
            {
                text = null;
                return false;
            }
        }
    }
}
#endif