using System.Collections.Generic;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Rendering.Utilities
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Rendering/HSR Manikin Shadow Receiver")]
    public sealed class HsrManikinShadowReceiver : MonoBehaviour
    {
        [SerializeField] Renderer[] renderers = System.Array.Empty<Renderer>();

        readonly List<Renderer> m_RendererScratch = new List<Renderer>(4);
        int m_TopologyVersion;

        internal IReadOnlyList<Renderer> Renderers => renderers;
        internal UnityScene Scene => gameObject != null ? gameObject.scene : default;
        internal int TopologyVersion => m_TopologyVersion;

        public void RefreshRenderers()
        {
            m_RendererScratch.Clear();
            GetComponentsInChildren(true, m_RendererScratch);
            renderers = m_RendererScratch.ToArray();
            m_RendererScratch.Clear();
            MarkTopologyChanged();
        }

        internal bool HasActiveRenderer()
        {
            if (renderers == null)
                return false;

            for (int i = 0; i < renderers.Length; ++i)
            {
                if (PlanarReflectionParticipant.IsActiveRenderer(renderers[i]))
                    return true;
            }

            return false;
        }

        void OnEnable()
        {
            RefreshRenderers();
            RenderParticipantRegistry.Register(this);
        }

        void OnDisable()
        {
            RenderParticipantRegistry.Unregister(this);
        }

        void OnDestroy()
        {
            RenderParticipantRegistry.MarkPruneNeeded(Scene);
        }

        void OnTransformChildrenChanged()
        {
            RefreshRenderers();
        }

        void OnTransformParentChanged()
        {
            MarkTopologyChanged();
        }

        void OnValidate()
        {
            RefreshRenderers();
        }

        void MarkTopologyChanged()
        {
            unchecked
            {
                m_TopologyVersion++;
            }

            RenderParticipantRegistry.MarkDirty(Scene);
        }
    }
}

