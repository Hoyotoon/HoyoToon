// Copyright (c) Jason Ma

#if MATERIAL_KEYWORDS_TRACK_REQUIRES_TIMELINE

using System;
using UnityEngine.Playables;

namespace LWGUI.Runtime.Timeline
{
    // Runtime representation of a MaterialKeywordToggleClip.
    // The Serializable attribute is required to be animated by timeline, and used as a template.
    [Serializable]
    public class MaterialKeywordTogglePlayableBehaviour : PlayableBehaviour
    {
        public float value;
    }
}

#endif
