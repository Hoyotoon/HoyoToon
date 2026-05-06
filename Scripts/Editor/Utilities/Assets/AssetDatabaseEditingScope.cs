#if UNITY_EDITOR
using System;
using UnityEditor;

namespace HoyoToon.Editor.Utilities.Assets
{
    internal sealed class AssetDatabaseEditingScope : IDisposable
    {
        private readonly bool m_DisallowedAutoRefresh;
        private readonly bool m_StartedAssetEditing;
        private bool m_Disposed;

        private AssetDatabaseEditingScope(bool disallowAutoRefresh, bool startAssetEditing)
        {
            m_DisallowedAutoRefresh = disallowAutoRefresh;
            m_StartedAssetEditing = startAssetEditing;

            if (m_DisallowedAutoRefresh)
                AssetDatabase.DisallowAutoRefresh();

            if (m_StartedAssetEditing)
                AssetDatabase.StartAssetEditing();
        }

        internal static AssetDatabaseEditingScope Begin(bool disallowAutoRefresh = true, bool startAssetEditing = true)
        {
            return new AssetDatabaseEditingScope(disallowAutoRefresh, startAssetEditing);
        }

        internal static void Refresh(string assetPath = null, ImportAssetOptions options = ImportAssetOptions.Default)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                AssetDatabase.Refresh(options);
            else
                AssetDatabase.ImportAsset(assetPath, options);
        }

        internal static void ImportAsset(string assetPath, ImportAssetOptions options = ImportAssetOptions.Default)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
                AssetDatabase.ImportAsset(assetPath, options);
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;

            m_Disposed = true;
            if (m_StartedAssetEditing)
                AssetDatabase.StopAssetEditing();

            if (m_DisallowedAutoRefresh)
                AssetDatabase.AllowAutoRefresh();
        }
    }
}
#endif
