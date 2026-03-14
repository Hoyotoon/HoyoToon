// Copyright (c) Jason Ma

using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;

#if MATERIAL_KEYWORDS_TRACK_REQUIRES_TIMELINE
using System.Collections.Generic;
using UnityEngine.Timeline;
using LWGUI.Runtime.Timeline;
using UnityEditor.Timeline;
#endif

namespace LWGUI.Timeline
{
    public static class TimelineHelper
    {
        private const string _GroupTrackName_Base = "LWGUI Tracks";
        private const string _GroupTrackName_Toggle = "LWGUI Material Keyword Toggle Tracks";
        private const string _AnimationCurveName_Toggle = "LWGUI Material Keyword Toggle Animation Curve";
        
        public static void SetKeywordToggleToTimeline(MaterialProperty prop, MaterialEditor editor, string keywordName)
        {
#if MATERIAL_KEYWORDS_TRACK_REQUIRES_TIMELINE
            // Check
            {
                if (editor == null || string.IsNullOrEmpty(keywordName) || keywordName == "_")
                    return;
                
                var renderer = editor.GetRendererForAnimationMode();
                if (renderer == null)
                    return;
                
                ReflectionHelper.MaterialAnimationUtility_OverridePropertyColor(prop, renderer, out Color color);
                if (color != AnimationMode.recordedPropertyColor)
                    return;
            }

            var directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            
            foreach (var renderer in editor.GetMeshRenderersByMaterialEditor())
            {
                var parentAnimators = renderer.GetComponentsInParent<Animator>();
                
                Debug.Assert(parentAnimators != null 
                             && parentAnimators.Length > 0, 
                    $"LWGUI: Unable to find parent Animators for MaterialProperty({ prop.name }) and Material({(editor.target as Material).name })!");
                
                PlayableDirector targetDirector = null;
                MaterialKeywordToggleTrack targetToggleTrack = null;
                GroupTrack targetGroupTrack = null;
                GroupTrack baseGroupTrack = null;
                TimelineAsset targetTimelineAsset = null;
                AnimationTrack srcAnimationTrack = null;
                Animator rootAnimator = null;

                // Find the existing track
                foreach (var director in directors)
                {
                    if (director == null || director.playableAsset == null)
                        continue;

                    var timelineAsset = director.playableAsset as TimelineAsset;
                    List<TrackAsset> allTrackAssets = new List<TrackAsset>();

                    foreach (var rootTrack in timelineAsset.GetRootTracks())
                    {
                        FindAllSubTracksRecursively(rootTrack, allTrackAssets);
                    }
                    
                    foreach (var trackAsset in allTrackAssets)
                    {
                        if (trackAsset is AnimationTrack animationTrack)
                        {
                            var bindedAnimator = director.GetGenericBinding(animationTrack);
                            if (parentAnimators.Contains(bindedAnimator))
                            {
                                srcAnimationTrack = animationTrack;
                                targetDirector = director;
                                targetTimelineAsset = timelineAsset;
                                rootAnimator = bindedAnimator as Animator;
                            }
                        }
                        
                        if (trackAsset is MaterialKeywordToggleTrack materialKeywordTrack 
                            && director.GetGenericBinding(materialKeywordTrack) == renderer
                            && materialKeywordTrack.keywordName == keywordName)
                        {
                            targetToggleTrack = materialKeywordTrack;
                        }

                        if (trackAsset is GroupTrack groupTrack)
                        {
                            if (groupTrack.name == _GroupTrackName_Toggle)
                                targetGroupTrack = groupTrack;
                            if (groupTrack.name == _GroupTrackName_Base)
                                baseGroupTrack = groupTrack;
                        }
                    }
                }
                
                Debug.Assert(targetDirector != null 
                             && targetTimelineAsset != null
                             && srcAnimationTrack != null
                             && rootAnimator != null,
                    $"LWGUI: Unable to find the existing Animation Track for MaterialProperty({ prop.name }) and Material({(editor.target as Material).name })!");

                // Create a track
                if (targetToggleTrack == null)
                {
                    if (baseGroupTrack == null)
                    {
                        baseGroupTrack = targetTimelineAsset.CreateTrack<GroupTrack>();
                        baseGroupTrack.name = _GroupTrackName_Base;
                    }
                    
                    if (targetGroupTrack == null)
                    {
                        targetGroupTrack = targetTimelineAsset.CreateTrack<GroupTrack>();
                        targetGroupTrack.name = _GroupTrackName_Toggle;
                        targetGroupTrack.SetGroup(baseGroupTrack);
                    }
                    
                    targetToggleTrack = targetTimelineAsset.CreateTrack<MaterialKeywordToggleTrack>();
                    targetToggleTrack.keywordName = keywordName;
                    targetToggleTrack.propName = prop.name;
                    targetToggleTrack.srcAnimationTrack = srcAnimationTrack;
                    targetToggleTrack.SetGroup(targetGroupTrack);
                    targetDirector.SetGenericBinding(targetToggleTrack, renderer);
                }
                
                // Find the Animation Curve
                AnimationClip srcAnimationClip = srcAnimationTrack.infiniteClip ?? srcAnimationTrack.curves;
                if (srcAnimationClip != null && GetMaterialPropertyEditorCurveFromAnimationClip(renderer, prop.name, rootAnimator, srcAnimationClip,
                        out var srcAnimationCurve))
                {
                    targetToggleTrack.srcAnimationClip = srcAnimationClip;

                    CopyAnimationCurveToMaterialKeywordToggleTrack(srcAnimationTrack, srcAnimationCurve, targetToggleTrack);
                }

                TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.ContentsModified);
            }
#endif
        }

#if MATERIAL_KEYWORDS_TRACK_REQUIRES_TIMELINE
        public static bool CopyAnimationCurveToMaterialKeywordToggleTrack(AnimationTrack srcAnimationTrack, AnimationCurve srcAnimationCurve, 
            MaterialKeywordToggleTrack dstToggleTrack)
        {
            if (srcAnimationCurve == null || dstToggleTrack == null)
                return false;
            
            var clips = dstToggleTrack.GetClips().ToList();
            if (clips.Count > 1)
            {
                foreach (var clip in clips)
                {
                    dstToggleTrack.DeleteClip(clip);
                }
            }

            TimelineClip currentClip = null;
            if (!dstToggleTrack.GetClips().Any())
            {
                currentClip = dstToggleTrack.CreateClip<MaterialKeywordTogglePlayableAsset>();
            }
            else
            {
                currentClip = dstToggleTrack.GetClips().ToArray()[0];
            }

            if (currentClip == null || currentClip.asset is not MaterialKeywordTogglePlayableAsset)
                return false;

            currentClip.start = srcAnimationTrack.start;
            currentClip.duration = srcAnimationTrack.duration;
            
            if (!currentClip.hasCurves)
                currentClip.CreateCurves($"{ dstToggleTrack.keywordName } ({ _AnimationCurveName_Toggle })");
            
            // Bind to MaterialKeywordPlayableBehaviour.value
            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(MaterialKeywordTogglePlayableAsset), "value");
            AnimationUtility.SetEditorCurve(currentClip.curves, binding, srcAnimationCurve);

            return true;
        }

        public static bool GetMaterialPropertyEditorCurveFromAnimationClip(Renderer renderer, string propName, Animator rootAnimator, AnimationClip srcAnimationClip,
            out AnimationCurve outAnimationCurve)
        {
            outAnimationCurve = null;
            
            var bindings = AnimationUtility.GetCurveBindings(srcAnimationClip);
            foreach (var binding in bindings)
            {
                var animatedObj = AnimationUtility.GetAnimatedObject(rootAnimator.gameObject, binding);
                if (animatedObj == renderer
                    && binding.propertyName == "material." + propName)
                {
                    outAnimationCurve = AnimationUtility.GetEditorCurve(srcAnimationClip, binding);
                    return true;
                }
            }

            return false;
        }

        private static void FindAllSubTracksRecursively(TrackAsset trackAsset, List<TrackAsset> outTrackAssets)
        {
            outTrackAssets ??= new List<TrackAsset>();
            outTrackAssets.Add(trackAsset);
            foreach (var childTrack in trackAsset.GetChildTracks())
            {
                FindAllSubTracksRecursively(childTrack, outTrackAssets);
            }
        }
#endif
        
    }
}