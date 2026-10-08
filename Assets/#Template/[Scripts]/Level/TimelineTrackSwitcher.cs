using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DancingLineFanmade.Level
{
    public class TimelineTrackSwitcher : MonoBehaviour
    {
        private const string PathSeparator = "/";

        [Title("Main Director")]
        [SerializeField] private PlayableDirector director;

        [Title("Checkpoint Memory Settings")]
        [Tooltip("If enabled, group states are saved at checkpoints and restored on player revival.")]
        [SerializeField] private bool enableCheckpointMemory = true;

        [Title("Presets")]
        [Tooltip("Each preset is a named bundle of groups to unmute and groups to mute. " +
                 "Groups that a preset does not mention keep whatever state they already have.")]
        [SerializeField] private List<TrackGroupPreset> presets = new List<TrackGroupPreset>();

        private readonly Dictionary<string, GroupTrack> groupsByPath = new Dictionary<string, GroupTrack>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> initialMutedStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> savedMutedStates = new Dictionary<string, bool>(StringComparer.Ordinal);

        private readonly List<string> groupPathCache = new List<string>();

        /// <summary>
        /// Names of every preset, used by <see cref="DancingLineFanmade.Trigger.TrackSwitchTrigger"/> to build its dropdown.
        /// </summary>
        public IEnumerable<string> PresetNames => presets.Select(preset => preset.presetName);

        private void Awake()
        {
            if (director == null) director = GetComponent<PlayableDirector>();

            RebuildGroupCache();

            // Captured once per run and never refreshed afterwards, so every play session starts from the same state.
            CaptureInitialStates();
        }

        // Refreshes the dropdown contents in edit mode without touching any muted state.
        private void OnValidate()
        {
            if (director == null) director = GetComponent<PlayableDirector>();

            RebuildGroupCache();
        }

        private void OnDestroy() => RestoreInitialStates();

        /// <summary>
        /// Applies the preset with the given name. Returns false when no such preset exists.
        /// </summary>
        public bool SwitchToState(string presetName)
        {
            EnsureInitialized();

            TrackGroupPreset preset = presets.FirstOrDefault(p => string.Equals(p.presetName, presetName, StringComparison.Ordinal));
            if (preset == null)
            {
                Debug.LogError($"[TimelineTrackSwitcher] '{name}' has no preset named '{presetName}'. " +
                               $"Available presets: {string.Join(", ", presets.Select(p => p.presetName))}.", this);
                return false;
            }

            return ApplyPreset(preset);
        }

        /// <summary>
        /// Applies the preset at the given index. Returns false when the index is out of range.
        /// </summary>
        public bool SwitchToState(int presetIndex)
        {
            EnsureInitialized();

            if (presetIndex < 0 || presetIndex >= presets.Count)
            {
                Debug.LogError($"[TimelineTrackSwitcher] Preset index {presetIndex} is out of range on '{name}'. " +
                               $"The switcher holds {presets.Count} preset(s).", this);
                return false;
            }

            return ApplyPreset(presets[presetIndex]);
        }

        /// <summary>
        /// Saves current group states at checkpoints.
        /// </summary>
        public void SaveState()
        {
            if (!enableCheckpointMemory) return;
            EnsureInitialized();

            savedMutedStates.Clear();
            foreach (KeyValuePair<string, GroupTrack> pair in groupsByPath)
            {
                if (pair.Value != null) savedMutedStates[pair.Key] = pair.Value.muted;
            }
        }

        /// <summary>
        /// Restores saved group states upon revival.
        /// </summary>
        public void RestoreState()
        {
            if (!enableCheckpointMemory) return;
            EnsureInitialized();

            if (savedMutedStates.Count == 0) return;

            bool changed = false;
            foreach (KeyValuePair<string, bool> pair in savedMutedStates)
            {
                if (!groupsByPath.TryGetValue(pair.Key, out GroupTrack group) || group == null) continue;
                if (group.muted == pair.Value) continue;

                group.muted = pair.Value;
                changed = true;
            }

            if (changed) RefreshGraphState();
        }

        private bool ApplyPreset(TrackGroupPreset preset)
        {
            Dictionary<string, bool> desiredStates = new Dictionary<string, bool>(StringComparer.Ordinal);

            CollectDesiredStates(preset, preset.unmuteGroups, muted: false, desiredStates);
            CollectDesiredStates(preset, preset.muteGroups, muted: true, desiredStates);

            bool changed = false;
            foreach (KeyValuePair<string, bool> pair in desiredStates)
            {
                if (!groupsByPath.TryGetValue(pair.Key, out GroupTrack group) || group == null) continue;
                if (group.muted == pair.Value) continue;

                group.muted = pair.Value;
                changed = true;
            }

            // Muting a GroupTrack is only baked into the graph on rebuild, so a rebuild is required.
            // Skipping it when nothing moved keeps repeated triggers free.
            if (changed) RefreshGraphState();

            return true;
        }

        private void CollectDesiredStates(
            TrackGroupPreset preset,
            List<string> entries,
            bool muted,
            Dictionary<string, bool> desiredStates)
        {
            if (entries == null) return;

            foreach (string entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                if (!groupsByPath.ContainsKey(entry))
                {
                    Debug.LogError($"[TimelineTrackSwitcher] Preset '{preset.presetName}' on '{name}' references " +
                                   $"'{entry}', which is not a group in the assigned timeline. " +
                                   $"Known groups: {string.Join(", ", groupPathCache)}.", this);
                    continue;
                }

                if (desiredStates.TryGetValue(entry, out bool existing) && existing != muted)
                {
                    Debug.LogError($"[TimelineTrackSwitcher] Preset '{preset.presetName}' on '{name}' lists '{entry}' " +
                                   "in both its unmute and mute lists. Muting wins.", this);
                }

                desiredStates[entry] = muted;
            }
        }

        private void EnsureInitialized()
        {
            if (groupsByPath.Count > 0) return;

            if (director == null) director = GetComponent<PlayableDirector>();

            RebuildGroupCache();
            CaptureInitialStates();
        }

        private void RebuildGroupCache()
        {
            groupsByPath.Clear();
            groupPathCache.Clear();

            if (director == null || director.playableAsset is not TimelineAsset timeline) return;

            List<GroupTrack> groups = new List<GroupTrack>();
            CollectGroupTracks(timeline.GetRootTracks(), groups);

            foreach (GroupTrack group in groups)
            {
                string path = BuildGroupPath(group);
                if (groupsByPath.ContainsKey(path)) continue;

                groupsByPath.Add(path, group);
                groupPathCache.Add(path);
            }

            // Odin cannot resolve members across types from a ValueDropdown attribute, so the preset
            // class pulls its dropdown entries from here instead.
            TrackGroupPreset.AvailableGroupPaths = groupPathCache;
        }

        private static void CollectGroupTracks(IEnumerable<TrackAsset> tracks, List<GroupTrack> output)
        {
            if (tracks == null) return;

            foreach (TrackAsset track in tracks)
            {
                if (track == null) continue;

                // GetOutputTracks() filters GroupTrack out, so the hierarchy has to be walked by hand.
                if (track is GroupTrack group) output.Add(group);

                CollectGroupTracks(track.GetChildTracks(), output);
            }
        }

        private static string BuildGroupPath(TrackAsset track)
        {
            Stack<string> segments = new Stack<string>();
            for (TrackAsset node = track; node != null; node = node.parent as TrackAsset)
            {
                segments.Push(node.name);
            }

            return string.Join(PathSeparator, segments);
        }

        private void CaptureInitialStates()
        {
            initialMutedStates.Clear();
            foreach (KeyValuePair<string, GroupTrack> pair in groupsByPath)
            {
                if (pair.Value != null) initialMutedStates[pair.Key] = pair.Value.muted;
            }
        }

        /// <summary>
        /// Puts every group back to the state it had when this component first ran, so that a play session
        /// never leaves its runtime muting baked into the .playable asset.
        /// </summary>
        private void RestoreInitialStates()
        {
            bool changed = false;
            foreach (KeyValuePair<string, bool> pair in initialMutedStates)
            {
                if (!groupsByPath.TryGetValue(pair.Key, out GroupTrack group) || group == null) continue;
                if (group.muted == pair.Value) continue;

                group.muted = pair.Value;
                changed = true;
            }

            if (changed) RefreshGraphState();
        }

        /// <summary>
        /// Refreshes graph state immediately in Unity 6 without resetting playhead time.
        /// </summary>
        private void RefreshGraphState()
        {
            if (director == null || !director.playableGraph.IsValid()) return;

            double currentTime = director.time;
            bool isPlaying = director.state == PlayState.Playing;

            director.RebuildGraph();
            director.time = currentTime;

            if (isPlaying) director.Play();
            else director.Evaluate();
        }

        [Title("Editor Utilities")]
        [Button("Validate Presets", ButtonSizes.Medium)]
        private void ValidatePresets()
        {
            RebuildGroupCache();

            if (groupsByPath.Count == 0)
            {
                Debug.LogError($"[TimelineTrackSwitcher] '{name}' has no PlayableDirector with a TimelineAsset assigned.", this);
                return;
            }

            List<string> problems = new List<string>();

            foreach (IGrouping<string, TrackGroupPreset> duplicate in presets.GroupBy(p => p.presetName ?? string.Empty).Where(g => g.Count() > 1))
            {
                problems.Add($"Preset name '{duplicate.Key}' is used by {duplicate.Count()} presets.");
            }

            foreach (TrackGroupPreset preset in presets)
            {
                problems.AddRange(ValidateEntries(preset, preset.unmuteGroups, "unmute"));
                problems.AddRange(ValidateEntries(preset, preset.muteGroups, "mute"));

                foreach (string conflict in preset.unmuteGroups.Intersect(preset.muteGroups))
                {
                    problems.Add($"Preset '{preset.presetName}' lists '{conflict}' in both the unmute and the mute list.");
                }
            }

            if (problems.Count > 0)
            {
                Debug.LogError($"[TimelineTrackSwitcher] '{name}' found {problems.Count} problem(s):\n" +
                               string.Join("\n", problems.Select(p => $"  - {p}")), this);
                return;
            }

            Debug.Log($"[TimelineTrackSwitcher] '{name}' validated {presets.Count} preset(s) against " +
                      $"{groupsByPath.Count} group(s). No problems found.", this);
        }

        private IEnumerable<string> ValidateEntries(TrackGroupPreset preset, List<string> entries, string listName)
        {
            if (entries == null) yield break;

            foreach (string entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    yield return $"Preset '{preset.presetName}' has an empty entry in its {listName} list.";
                    continue;
                }

                if (!groupsByPath.ContainsKey(entry))
                {
                    yield return $"Preset '{preset.presetName}' {listName} list references '{entry}', which is not a group in the timeline.";
                }
            }
        }

        [Button("List All Track Groups", ButtonSizes.Medium)]
        private void ListAllTrackGroups()
        {
            RebuildGroupCache();

            if (groupsByPath.Count == 0)
            {
                Debug.LogError($"[TimelineTrackSwitcher] '{name}' has no PlayableDirector with a TimelineAsset assigned.", this);
                return;
            }

            IEnumerable<string> lines = groupPathCache.Select(path =>
            {
                GroupTrack group = groupsByPath[path];
                return $"  {path}  (children: {group.GetChildTracks().Count()}, muted: {group.muted})";
            });

            Debug.Log($"[TimelineTrackSwitcher] {groupsByPath.Count} group(s) found in '{director.playableAsset.name}':\n" +
                      string.Join("\n", lines), this);
        }
    }

    [Serializable]
    public class TrackGroupPreset
    {
        // Group paths of the timeline that the owning TimelineTrackSwitcher points at.
        // Written by the switcher every time it rebuilds its group cache.
        // A static field is required because Odin cannot resolve a ValueDropdown member
        // that lives on another type than the one declaring the drawn field.
        public static List<string> AvailableGroupPaths = new List<string>();

        [Tooltip("Name used by triggers to select this preset. Must be unique within the switcher.")]
        [SerializeField] public string presetName = "NewPreset";

        [Title("Groups To Unmute")]
        [Tooltip("Muting is cleared on these groups. Child tracks inherit the change through mutedInHierarchy.")]
        [ValueDropdown(nameof(AvailableGroupPaths))]
        [SerializeField] public List<string> unmuteGroups = new List<string>();

        [Title("Groups To Mute")]
        [Tooltip("Muting is applied on these groups. Child tracks inherit the change through mutedInHierarchy.")]
        [ValueDropdown(nameof(AvailableGroupPaths))]
        [SerializeField] public List<string> muteGroups = new List<string>();
    }
}

// Special thanks to Gemini and WindBamboo for assisting with this code!
