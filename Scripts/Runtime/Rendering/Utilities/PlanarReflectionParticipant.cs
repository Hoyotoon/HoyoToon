using System.Collections.Generic;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Rendering.Utilities
{
    public enum PlanarReflectionRole
    {
        Caster = 0,
        Receiver = 1,
        CasterAndReceiver = 2
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Rendering/Planar Reflection Participant")]
    public sealed class PlanarReflectionParticipant : MonoBehaviour
    {
        [SerializeField] PlanarReflectionRole role = PlanarReflectionRole.Caster;
        [SerializeField] Renderer[] renderers = System.Array.Empty<Renderer>();

        readonly List<Renderer> m_RendererScratch = new List<Renderer>(8);
        int m_TopologyVersion;

        internal bool CastsReflection => role == PlanarReflectionRole.Caster
            || role == PlanarReflectionRole.CasterAndReceiver;

        internal bool ReceivesReflection => role == PlanarReflectionRole.Receiver
            || role == PlanarReflectionRole.CasterAndReceiver;

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
                Renderer renderer = renderers[i];
                if (IsActiveRenderer(renderer))
                    return true;
            }

            return false;
        }

        internal static bool IsActiveRenderer(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && renderer.gameObject != null
                && renderer.gameObject.activeInHierarchy;
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

