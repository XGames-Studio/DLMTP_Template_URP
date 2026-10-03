using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class EnvironmentClip : PlayableAsset, ITimelineClipAsset
{
    public EnvironmentBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<EnvironmentBehaviour>.Create(graph, template);
    }
}