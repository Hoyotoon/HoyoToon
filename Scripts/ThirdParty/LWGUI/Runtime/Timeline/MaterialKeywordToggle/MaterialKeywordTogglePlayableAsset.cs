// Copyright (c) Jason Ma

#if MATERIAL_KEYWORDS_TRACK_REQUIRES_TIMELINE

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LWGUI.Runtime.Timeline
{
    // Represents the serialized data for a clip on the MaterialKeywordToggleTrack
    [Serializable]
    public class MaterialKeywordTogglePlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        public MaterialKeywordTogglePlayableBehaviour template = new ();

        // Implementation of ITimelineClipAsset. This specifies the capabilities of this timeline clip inside the editor.
        public ClipCaps clipCaps => ClipCaps.None;

        // Creates the playable that represents the instance of this clip.
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            // Using a template will clone the serialized values
            return ScriptPlayable<MaterialKeywordTogglePlayableBehaviour>.Create(graph, template);
        }
    }
}

#endif
