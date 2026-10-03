using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.4f, 0.7f, 0.9f)]
[TrackClipType(typeof(EnvironmentClip))]
public class EnvironmentTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<EnvironmentMixerBehaviour>.Create(graph, inputCount);
    }
}