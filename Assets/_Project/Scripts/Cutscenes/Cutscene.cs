using System.Collections;
using APX.Core;
using APX.Player;
using APX.Rooms;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace APX.Cutscenes
{
    /// <summary>
    /// Base for in-game cutscenes. A cutscene is a short script (<see cref="Perform"/>) built from the steps of a
    /// <see cref="CutsceneContext"/>: waits, camera shots, lines of speech, quick-time events, and ways to move the
    /// player and the companion. While it plays the player has no control and the mask menu and hints stay away;
    /// everything it took over is handed back when it ends, even if it ends early (e.g. the player died). Only one
    /// cutscene plays at a time. Skippable ones (the default) skip with Esc (Start on a gamepad): the screen dips to
    /// black and <see cref="OnSkipped"/> puts things as the cutscene would have left them.
    /// <example><code>
    /// protected override IEnumerator Perform(CutsceneContext c)
    /// {
    ///     c.Companion.Take();
    ///     yield return c.Camera.FocusOn(statue, zoom: 2f);
    ///     yield return c.Companion.Say("Que estátua estranha...", "Acho que ela se mexeu!");
    ///     yield return c.QuickTimeEvent(CutsceneButton.Jump);
    ///     c.Player.Hop(9f);
    ///     c.Companion.Release(summon: true);
    ///     yield return c.Camera.Release(0.8f);
    /// }
    /// </code></example>
    /// </summary>
    public abstract class Cutscene : MonoBehaviour
    {
        public enum StartTrigger
        {
            Manual,
            OnStart,
            OnRoomEnter,
            OnPlayerTouch,
        }

        [Header("Playback")]
        [Tooltip("Manual: started by calling Play (from code or a UnityEvent). On Start: when the scene starts. " +
                 "On Room Enter: when the player enters the room. On Player Touch: when the player touches a trigger " +
                 "collider on this object.")]
        [FormerlySerializedAs("playOnStart")] // An older bool: true reads as On Start.
        [SerializeField] StartTrigger trigger = StartTrigger.Manual;
        [Tooltip("Room for On Room Enter. Defaults to the room this cutscene sits in.")]
        [SerializeField] Room room;
        [Tooltip("Play only the first time it's triggered.")]
        [SerializeField] bool playOnce = true;
        [Tooltip("The player can skip it with Esc (Start on a gamepad): the screen dips to black and the cutscene " +
                 "jumps to its ending.")]
        [SerializeField] bool skippable = true;
        [Tooltip("Seconds after the cutscene ends before On Finished fires.")]
        [SerializeField, Min(0f)] float finishedEventDelay;
        [Tooltip("Extra actions once the cutscene has played through.")]
        [SerializeField] UnityEvent onFinished = new();

        Coroutine _routine;
        Coroutine _skipRoutine;
        CutsceneContext _context;

        /// <summary>The cutscene playing right now, if any.</summary>
        public static Cutscene Current { get; private set; }

        public bool IsPlaying => _context != null;

        public bool HasPlayed { get; private set; }

        protected virtual void Awake()
        {
            if (room == null)
                room = GetComponentInParent<Room>();
        }

        protected virtual void OnEnable()
        {
            EventBus<RoomChangedEvent>.Subscribe(OnRoomChanged);
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
        }

        protected virtual void OnDisable()
        {
            EventBus<RoomChangedEvent>.Unsubscribe(OnRoomChanged);
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            if (IsPlaying)
                Stop(returnControl: true);
        }

        protected virtual void Start()
        {
            if (trigger == StartTrigger.OnStart)
                Play();
        }

        /// <summary>Starts the cutscene, unless it's already played (and plays once) or another one is playing.</summary>
        public void Play()
        {
            if (IsPlaying || (playOnce && HasPlayed) || Current != null)
                return;

            HasPlayed = true;
            Current = this;
            _context = new CutsceneContext(this);
            _routine = StartCoroutine(Run(_context));
        }

        /// <summary>
        /// Skips the rest of the cutscene, if it's skippable and playing: the screen closes, <see cref="OnSkipped"/>
        /// sets up the ending, and the screen opens with the player in control. Counts as played through (On Finished
        /// still fires).
        /// </summary>
        public void Skip()
        {
            if (!skippable || !IsPlaying || _skipRoutine != null)
                return;

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            _skipRoutine = StartCoroutine(SkipToEnd(_context));
        }

        /// <summary>The cutscene itself. Everything the context's steps take over is handed back afterwards.</summary>
        protected abstract IEnumerator Perform(CutsceneContext c);

        /// <summary>
        /// Called when the cutscene is skipped, behind a black screen and before the context hands everything back:
        /// put things as the cutscene would have left them (e.g. where the player ends up).
        /// </summary>
        protected virtual void OnSkipped(CutsceneContext c)
        {
        }

        /// <summary>
        /// Called when the cutscene stops early, after the context has cleaned up: put back anything the script
        /// changed by other means.
        /// </summary>
        protected virtual void OnStopped()
        {
        }

        IEnumerator Run(CutsceneContext context)
        {
            context.Begin();
            yield return Perform(context);
            End(returnControl: true);
            yield return Finish();
        }

        IEnumerator SkipToEnd(CutsceneContext context)
        {
            context.Halt();
            yield return context.CoverScreen();
            OnSkipped(context);
            End(returnControl: true);
            yield return context.RevealScreen();
            _skipRoutine = null;
            yield return Finish();
        }

        IEnumerator Finish()
        {
            if (finishedEventDelay > 0f)
                yield return new WaitForSeconds(finishedEventDelay);
            onFinished.Invoke();
        }

        void Stop(bool returnControl)
        {
            if (_routine != null)
                StopCoroutine(_routine);
            if (_skipRoutine != null)
                StopCoroutine(_skipRoutine);
            _routine = null;
            _skipRoutine = null;
            End(returnControl);
            OnStopped();
        }

        void End(bool returnControl)
        {
            CutsceneContext context = _context;
            _context = null;
            if (Current == this)
                Current = null;
            context?.End(returnControl);
        }

        void OnRoomChanged(RoomChangedEvent evt)
        {
            if (trigger == StartTrigger.OnRoomEnter && evt.Current != null && evt.Current == room)
                Play();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (trigger == StartTrigger.OnPlayerTouch && other.GetComponentInParent<PlayerController>() != null)
                Play();
        }

        // The respawn puts the player back in control, so the cutscene just lets go.
        void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (IsPlaying)
            {
                Stop(returnControl: false);
            }
            else if (_skipRoutine != null)
            {
                // Still opening the screen after a skip: the respawn transition takes over.
                StopCoroutine(_skipRoutine);
                _skipRoutine = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null; // Domain reload may be off.
    }
}
