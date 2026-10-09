using System;
using System.Collections;
using System.Collections.Generic;
using APX.Core;
using UnityEngine;

namespace APX.Masks
{
    /// <summary>
    /// Central authority over which mask is worn. Scenario objects never reference it: they read
    /// <see cref="CurrentMask"/> and listen to <see cref="MaskChangedEvent"/> through
    /// <see cref="MaskResponder"/> components. Mask changes play an <see cref="IMaskTransition"/> first (when the
    /// scene has one) and only take effect once it ends, so the gameplay change lands with the visuals.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MaskManager : MonoBehaviour
    {
        [Tooltip("Presentation data for each option, including MaskType.None (\"no mask\").")]
        [SerializeField] MaskDefinition[] definitions = Array.Empty<MaskDefinition>();
        [SerializeField] MaskType startingMask = MaskType.None;
        [Tooltip("Component implementing IMaskTransition (e.g. the player's PlayerMaskPresenter). Found automatically " +
                 "when empty; without one masks change instantly.")]
        [SerializeField] MonoBehaviour transition;

        IMaskTransition _transition;
        Coroutine _transitionRoutine;

        public static MaskType CurrentMask { get; private set; }

        /// <summary>True only after the player explicitly picked "no mask" in the radial menu.</summary>
        public static bool IsCompanionSummoned { get; private set; }

        public IReadOnlyList<MaskDefinition> Definitions => definitions;

        /// <summary>A mask change is animating; it takes effect when the transition ends.</summary>
        public bool IsTransitioning => _transitionRoutine != null;

        void Awake() => ApplyState(startingMask, summonCompanion: false, force: true);

        void Start() => _transition = transition as IMaskTransition ?? FindTransition();

        void OnValidate()
        {
            if (transition != null && transition is not IMaskTransition)
            {
                Debug.LogWarning($"{transition.name} does not implement {nameof(IMaskTransition)}.", this);
                transition = null;
            }
        }

        /// <summary>
        /// Radial menu entry point: a mask equips it; <see cref="MaskType.None"/> unequips every mask
        /// and summons the hint companion.
        /// </summary>
        public void Select(MaskType selection)
        {
            if (selection == MaskType.None)
                UnequipAll();
            else
                Equip(selection);
        }

        public void Equip(MaskType mask) => ChangeTo(mask, summonCompanion: false);

        public void UnequipAll() => ChangeTo(MaskType.None, summonCompanion: true);

        /// <summary>
        /// Brings the hint companion in without touching masks (e.g. after the intro). Ignored while a mask is
        /// equipped, since the companion only keeps the player company with no mask on.
        /// </summary>
        public void SummonCompanion()
        {
            if (CurrentMask == MaskType.None && !IsTransitioning)
                ApplyState(MaskType.None, summonCompanion: true);
        }

        public bool TryGetDefinition(MaskType type, out MaskDefinition definition)
        {
            foreach (MaskDefinition candidate in definitions)
            {
                if (candidate != null && candidate.Type == type)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        void ChangeTo(MaskType mask, bool summonCompanion)
        {
            if (_transitionRoutine != null)
            {
                // A new choice interrupts the running animation; the old target never took effect.
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
                _transition.Show(CurrentMask);
            }

            if (_transition == null || mask == CurrentMask || !isActiveAndEnabled)
            {
                ApplyState(mask, summonCompanion);
                return;
            }

            _transitionRoutine = StartCoroutine(Transition(mask, summonCompanion));
        }

        IEnumerator Transition(MaskType mask, bool summonCompanion)
        {
            yield return _transition.Play(CurrentMask, mask);
            _transitionRoutine = null;
            ApplyState(mask, summonCompanion);
        }

        static IMaskTransition FindTransition()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour is IMaskTransition found)
                    return found;
            }

            return null;
        }

        void ApplyState(MaskType mask, bool summonCompanion, bool force = false)
        {
            MaskType previous = CurrentMask;
            CurrentMask = mask;
            if (force || previous != mask)
                EventBus<MaskChangedEvent>.Raise(new MaskChangedEvent(previous, mask));

            if (force || IsCompanionSummoned != summonCompanion)
            {
                IsCompanionSummoned = summonCompanion;
                EventBus<CompanionSummonChangedEvent>.Raise(new CompanionSummonChangedEvent(summonCompanion));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            CurrentMask = MaskType.None;
            IsCompanionSummoned = false;
        }
    }
}
