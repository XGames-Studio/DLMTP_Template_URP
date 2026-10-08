using System.Collections.Generic;
using System.Linq;
using DancingLineFanmade.Level;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DancingLineFanmade.Trigger
{
    [DisallowMultipleComponent, RequireComponent(typeof(Collider))]
    public class TrackSwitchTrigger : MonoBehaviour
    {
        [Title("Timeline Track Switch Control")]
        [Tooltip("Assign the GameObject that has the TimelineTrackSwitcher component attached.")]
        [SerializeField] private TimelineTrackSwitcher trackSwitcher;

        [Tooltip("Name of the preset to apply when the player enters this trigger.")]
        [ValueDropdown(nameof(PresetNameOptions))]
        [SerializeField] private string presetName;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (trackSwitcher == null)
            {
                Debug.LogError($"[TrackSwitchTrigger] '{name}' has no TimelineTrackSwitcher assigned.", this);
                return;
            }

            trackSwitcher.SwitchToState(presetName);
        }

        private IEnumerable<string> PresetNameOptions() =>
            trackSwitcher != null ? trackSwitcher.PresetNames : Enumerable.Empty<string>();
    }
}

// Special thanks to Gemini for assisting with this code!
