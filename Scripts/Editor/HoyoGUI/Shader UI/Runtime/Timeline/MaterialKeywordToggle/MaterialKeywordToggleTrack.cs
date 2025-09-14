// Copyright (c) Jason Ma

#if MATERIAL_KEYWORDS_TRACK_REQUIRES_TIMELINE

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LWGUI.Runtime.Timeline
{
    /// <summary>
    /// When recording material parameter animation, Keyword changes are automatically captured and the track is added to the Timeline Asset.
    /// The Keyword state is set according to the float value during runtime.
    /// 
    /// Supports Toggle-type Drawer with Keyword.
    /// </summary>
    [TrackColor(127.0f * 0.7f / 255.0f, 214.0f * 0.7f / 255.0f, 252.0f * 0.7f / 255.0f)]
    [TrackClipType(typeof(MaterialKeywordTogglePlayableAsset))]
    [TrackBindingType(typeof(Renderer))]
    public class MaterialKeywordToggleTrack : TrackAsset
    {
        public string keywordName;
        public string propName;
        public AnimationTrack srcAnimationTrack = null;
        public AnimationClip srcAnimationClip = null;

        // Creates a runtime instance of the track, represented by a PlayableBehaviour.
        // The runtime instance performs mixing on the timeline clips.
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
#if UNITY_EDITOR
            foreach (var clip in GetClips())
            {
                clip.displayName = keywordName + " (Enabled Material Keyword)";
            }
#endif

            var template = new MaterialKeywordToggleTrackBehaviour
            {
                keywordName = keywordName
            };

            return ScriptPlayable<MaterialKeywordToggleTrackBehaviour>.Create(graph, template, inputCount);
        }
    }
}

#endif
