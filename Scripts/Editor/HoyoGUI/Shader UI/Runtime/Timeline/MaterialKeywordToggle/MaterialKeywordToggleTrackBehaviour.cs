// Copyright (c) Jason Ma

#if MATERIAL_KEYWORDS_TRACK_REQUIRES_TIMELINE

using UnityEngine;
using UnityEngine.Playables;

namespace LWGUI.Runtime.Timeline
{
    // The runtime instance of a the MaterialKeywordToggleTrack. It is responsible for blending and setting the final data
    public class MaterialKeywordToggleTrackBehaviour : PlayableBehaviour
    {
        public string keywordName;

        private bool _defaultEnabled;
        private Renderer _targetRenderer;
    
        // Called every frame that the timeline is evaluated. ProcessFrame is invoked after its' inputs.
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            SetDefaults(playerData as Renderer);
            if (_targetRenderer == null || string.IsNullOrEmpty(keywordName))
                return;

            bool enabled = false;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                ScriptPlayable<MaterialKeywordTogglePlayableBehaviour> inputPlayable = (ScriptPlayable<MaterialKeywordTogglePlayableBehaviour>)playable.GetInput(i);
                MaterialKeywordTogglePlayableBehaviour input = inputPlayable.GetBehaviour();

                enabled = inputWeight > 0 && input.value > 0;
                
                if (enabled)
                    break;
            }
            
            foreach (var mat in Application.isPlaying ? _targetRenderer.materials : _targetRenderer.sharedMaterials)
            {
                if (enabled)
                    mat.EnableKeyword(keywordName);
                else
                    mat.DisableKeyword(keywordName);
            }
        }

        // Invoked when the playable graph is destroyed, typically when PlayableDirector.Stop is called or the timeline
        // is complete.
        public override void OnPlayableDestroy(Playable playable)
        {
            RestoreDefaults();
            _targetRenderer = null;
        }
    
        private void SetDefaults(Renderer renderer)
        {
            if (renderer == _targetRenderer)
                return;
    
            _targetRenderer = renderer;
            if (_targetRenderer != null && !string.IsNullOrEmpty(keywordName))
            {
                var mat = Application.isPlaying ? _targetRenderer.material : _targetRenderer.sharedMaterial;
                if (mat != null)
                {
                    _defaultEnabled = mat.IsKeywordEnabled(keywordName);
                }
            }
        }
    
        private void RestoreDefaults()
        {
            if (_targetRenderer == null || string.IsNullOrEmpty(keywordName))
                return;

            foreach (var mat in Application.isPlaying ? _targetRenderer.materials : _targetRenderer.sharedMaterials)
            {
                if (_defaultEnabled)
                    mat.EnableKeyword(keywordName);
                else
                    mat.DisableKeyword(keywordName);
            }
        }
    }
}

#endif
