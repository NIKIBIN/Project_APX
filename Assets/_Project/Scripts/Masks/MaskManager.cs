using System;
using System.Collections;
using System.Collections.Generic;
using APX.Core;
using APX.Player;
using UnityEngine;

namespace APX.Masks
{
    /// <summary>
    /// Central authority over which mask is worn and which masks the player owns. Scenario objects never reference
    /// it: they read <see cref="CurrentMask"/> and listen to <see cref="MaskChangedEvent"/> through
    /// <see cref="MaskResponder"/> components, and masks lying in the level raise <see cref="MaskPickedUpEvent"/>.
    /// Mask changes play an <see cref="IMaskTransition"/> first (when the scene has one) and only take effect once
    /// it ends, so the gameplay change lands with the visuals.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MaskManager : MonoBehaviour
    {
        static readonly HashSet<MaskType> s_unlocked = new();

        [Tooltip("Presentation data for each option, including MaskType.None (\"no mask\").")]
        [SerializeField] MaskDefinition[] definitions = Array.Empty<MaskDefinition>();
        [SerializeField] MaskType startingMask = MaskType.None;
        [Tooltip("Masks the player owns when the scene starts. Empty for a new game: the rest are found as " +
                 "MaskPickups. \"No mask\" is always available.")]
        [SerializeField] MaskType[] unlockedAtStart = { MaskType.NiceAndSlow, MaskType.SecondChannel, MaskType.Differentiate };
        [Tooltip("Take every mask off when the player respawns, so each room restarts with the curses on. " +
                 "A companion the player had already called stays.")]
        [SerializeField] bool unequipOnRespawn = true;
        [Tooltip("Component implementing IMaskTransition (e.g. the player's PlayerMaskPresenter). Found automatically " +
                 "when empty; without one masks change instantly.")]
        [SerializeField] MonoBehaviour transition;

        IMaskTransition _transition;
        Coroutine _transitionRoutine;

        public static MaskType CurrentMask { get; private set; }

        /// <summary>True only after the player explicitly picked "no mask" in the radial menu.</summary>
        public static bool IsCompanionSummoned { get; private set; }

        /// <summary>True when the player owns at least one mask (otherwise there is nothing to choose).</summary>
        public static bool HasAnyMask => s_unlocked.Count > 0;

        public IReadOnlyList<MaskDefinition> Definitions => definitions;

        /// <summary>A mask change is animating; it takes effect when the transition ends.</summary>
        public bool IsTransitioning => _transitionRoutine != null;

        void Awake()
        {
            s_unlocked.Clear();
            foreach (MaskType mask in unlockedAtStart)
                AddUnlocked(mask);
            AddUnlocked(startingMask);

            ApplyState(startingMask, summonCompanion: false, force: true, isReset: true);
        }

        void OnEnable()
        {
            EventBus<MaskPickedUpEvent>.Subscribe(OnMaskPickedUp);
            EventBus<PlayerRespawnedEvent>.Subscribe(OnPlayerRespawned);
        }

        void OnDisable()
        {
            EventBus<MaskPickedUpEvent>.Unsubscribe(OnMaskPickedUp);
            EventBus<PlayerRespawnedEvent>.Unsubscribe(OnPlayerRespawned);
        }

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

        public void Equip(MaskType mask)
        {
            if (!IsUnlocked(mask))
            {
                Debug.LogWarning($"{nameof(MaskManager)}: {mask} is still locked; the player has to pick it up first.", this);
                return;
            }

            ChangeTo(mask, summonCompanion: false);
        }

        public void UnequipAll() => ChangeTo(MaskType.None, summonCompanion: true);

        /// <summary>"No mask" is always available; the masks once picked up (or unlocked at start).</summary>
        public static bool IsUnlocked(MaskType mask) => mask == MaskType.None || s_unlocked.Contains(mask);

        /// <summary>Makes <paramref name="mask"/> available in the radial menu. Ignored when already owned.</summary>
        public void Unlock(MaskType mask)
        {
            if (AddUnlocked(mask))
                EventBus<MaskUnlockedEvent>.Raise(new MaskUnlockedEvent(mask));
        }

        [ContextMenu("Unlock All Masks")]
        void UnlockAll()
        {
            Unlock(MaskType.NiceAndSlow);
            Unlock(MaskType.SecondChannel);
            Unlock(MaskType.Differentiate);
        }

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

        void OnMaskPickedUp(MaskPickedUpEvent evt) => Unlock(evt.Mask);

        // The screen is covered while the player respawns, so the masks come off at once, without the transition.
        void OnPlayerRespawned(PlayerRespawnedEvent evt)
        {
            if (!unequipOnRespawn)
                return;

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            bool keepCompanion = CurrentMask == MaskType.None && IsCompanionSummoned;
            ApplyState(MaskType.None, keepCompanion, isReset: true);
            _transition?.Show(MaskType.None);
        }

        static bool AddUnlocked(MaskType mask) => mask != MaskType.None && s_unlocked.Add(mask);

        static IMaskTransition FindTransition()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour is IMaskTransition found)
                    return found;
            }

            return null;
        }

        /// <param name="isReset">The game sets the mask (scene start, respawn) rather than the player choosing it.</param>
        void ApplyState(MaskType mask, bool summonCompanion, bool force = false, bool isReset = false)
        {
            MaskType previous = CurrentMask;
            CurrentMask = mask;
            if (force || previous != mask)
                EventBus<MaskChangedEvent>.Raise(new MaskChangedEvent(previous, mask, isReset));

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
            s_unlocked.Clear();
        }
    }
}
