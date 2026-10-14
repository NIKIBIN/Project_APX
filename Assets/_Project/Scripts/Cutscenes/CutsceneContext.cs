using System;
using System.Collections;
using System.Collections.Generic;
using APX.Core;
using APX.Hints;
using APX.Level;
using APX.Masks;
using APX.Player;
using APX.Rooms;
using APX.UI;
using DG.Tweening;
using InputPrompts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace APX.Cutscenes
{
    /// <summary>The button a cutscene waits for.</summary>
    public enum CutsceneButton
    {
        Jump,
        Attack,
        Either,
    }

    /// <summary>
    /// Everything a <see cref="Cutscene"/> script can do: timing, speech, quick-time events and screen effects here;
    /// the camera, the player and the companion through <see cref="Camera"/>, <see cref="Player"/> and
    /// <see cref="Companion"/>. Steps that take time return an <see cref="IEnumerator"/> to <c>yield return</c>
    /// (calling one without yielding it does nothing); the others act at once. Whatever the steps take over is handed
    /// back when the cutscene ends.
    /// </summary>
    public sealed class CutsceneContext
    {
        const float MaxBlackStep = 1f / 30f;

        readonly Cutscene _owner;
        readonly List<Coroutine> _running = new();
        readonly List<Tween> _tweens = new();
        readonly InputReader _input;
        readonly SpeechBubbleView _speech;
        readonly string _continueHint;
        ButtonPromptView _prompt;
        AudioSource _audio;
        int _presses;
        bool _isRumbling;
        bool _isSlowed;

        internal CutsceneContext(Cutscene owner)
        {
            _owner = owner;
            var player = Object.FindAnyObjectByType<PlayerController>();
            Player = new CutscenePlayer(this, player);
            Companion = new CutsceneCompanion(this, Object.FindAnyObjectByType<HintCompanion>(), Object.FindAnyObjectByType<MaskManager>());
            Camera = new CutsceneCamera(Object.FindAnyObjectByType<RoomCamera>());
            _speech = Object.FindAnyObjectByType<SpeechBubbleView>();
            _input = player != null ? player.Input : null;

            // Lines end with a pulsing button icon (shown once typed) telling the player how to move on.
            if (_speech != null && _speech.Icons != null)
            {
                string token = _speech.Icons.TryGetIcon("continue", ControlScheme.KeyboardMouse, out _) ? "continue" : "jump";
                _continueHint = $" <pulse>{{{token}}}</pulse>";
            }
            else
            {
                _continueHint = string.Empty;
            }
        }

        public CutsceneCamera Camera { get; }

        public CutscenePlayer Player { get; }

        public CutsceneCompanion Companion { get; }

        /// <summary>The cutscene's own object, e.g. to start coroutines or link tweens to.</summary>
        public MonoBehaviour Owner => _owner;

        // ---------------------------------------------------------------- Timing

        /// <summary>Waits <paramref name="seconds"/> of game time (slowed down along with everything else).</summary>
        public IEnumerator Wait(float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.deltaTime)
                yield return null;
        }

        public IEnumerator WaitUntil(Func<bool> condition)
        {
            while (!condition())
                yield return null;
        }

        /// <summary>Runs <paramref name="step"/> alongside the script; it's stopped with the cutscene if still going.</summary>
        public Coroutine Run(IEnumerator step)
        {
            Coroutine coroutine = _owner.StartCoroutine(step);
            _running.Add(coroutine);
            return coroutine;
        }

        public void Stop(Coroutine coroutine)
        {
            if (coroutine == null)
                return;

            _owner.StopCoroutine(coroutine);
            _running.Remove(coroutine);
        }

        /// <summary>Links <paramref name="tween"/> to the cutscene, so it's killed if the cutscene stops early.</summary>
        public T Track<T>(T tween) where T : Tween
        {
            tween.SetLink(_owner.gameObject);
            _tweens.Add(tween);
            return tween;
        }

        /// <summary>Plays <paramref name="tween"/> (see <see cref="Track{T}"/>) and waits for it to finish.</summary>
        public IEnumerator Play(Tween tween)
        {
            Track(tween);
            yield return tween.WaitForCompletion();
        }

        // ---------------------------------------------------------------- Speech

        /// <summary>
        /// Lines spoken by <paramref name="speaker"/>, one after another, in a bubble over them (pointing at their
        /// <see cref="SpeechAnchor"/> if they have one). Jump or attack shows a line still being typed at once; once
        /// it's all there, another press moves on. The bubble closes after the last line.
        /// </summary>
        public IEnumerator Say(Component speaker, params string[] lines)
        {
            if (_speech == null || speaker == null)
            {
                Debug.LogWarning("Cutscene line skipped: no speech bubble or speaker in the scene.", _owner);
                yield break;
            }

            Transform anchor = SpeechAnchor.Find(speaker, out Vector2? offset);
            foreach (string line in lines)
            {
                _speech.Speak(anchor, line + _continueHint, offset);

                // Only presses made after the line appeared count, one per frame.
                int handled = _presses;
                while (true)
                {
                    yield return null;
                    if (_presses == handled)
                        continue;

                    handled = _presses;
                    if (_speech.IsTyping)
                        _speech.FinishTyping();
                    else
                        break;
                }
            }

            _speech.EndSpeech();
            while (_speech.IsShowing)
                yield return null;
        }

        // ---------------------------------------------------------------- Quick-time events

        /// <summary>
        /// Quick-time event: shows <paramref name="button"/>'s icon over <paramref name="promptOver"/> (the player by
        /// default) and slows time down, more and more, until it's pressed. It can't be failed. Time is back to normal
        /// as soon as the button is pressed.
        /// </summary>
        /// <param name="slowDownTime">Real seconds for time to slow all the way to <paramref name="slowest"/>.</param>
        public IEnumerator QuickTimeEvent(CutsceneButton button, Transform promptOver = null, float slowDownTime = 1.5f, float slowest = 0.05f)
        {
            if (_input == null)
                yield break;

            if (_prompt == null && _speech != null)
                _prompt = ButtonPromptView.For(_speech);
            Transform target = promptOver != null ? promptOver : Player.Transform;
            if (_prompt != null && target != null)
                _prompt.Show(target, button == CutsceneButton.Attack ? "attack" : "jump");

            bool pressed = false;
            Action onPress = () => pressed = true;
            if (button != CutsceneButton.Attack)
                _input.JumpPressed += onPress;
            if (button != CutsceneButton.Jump)
                _input.AttackPressed += onPress;

            _isSlowed = true;
            float elapsed = 0f;
            while (!pressed)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(slowDownTime, 0.01f));
                GameTime.SetTimeScale(Mathf.Lerp(1f, slowest, 1f - (1f - t) * (1f - t)));
                yield return null;
            }

            _input.JumpPressed -= onPress;
            _input.AttackPressed -= onPress;
            RestoreTime();
            if (_prompt != null)
                _prompt.Hide(pressed: true);
        }

        // ---------------------------------------------------------------- Sound

        /// <summary>Plays a sound effect, heard the same wherever it happens on screen.</summary>
        public void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
                return;

            if (_audio == null && !_owner.TryGetComponent(out _audio))
            {
                _audio = _owner.gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            _audio.PlayOneShot(clip, volume);
        }

        // ---------------------------------------------------------------- Screen

        /// <summary>
        /// Opens the scene from black: the screen stays black for <paramref name="blackTime"/> seconds, then the
        /// transition (e.g. the iris) opens on <paramref name="focus"/>. Without a transition it just carries on.
        /// </summary>
        public IEnumerator OpenFromBlack(float blackTime, Func<Vector2> focus)
        {
            IRespawnTransition transition = FindTransition();
            if (transition == null)
                yield break;

            transition.Cover();

            // Counted in capped steps: the first frames after loading can stall for most of a second and would
            // otherwise swallow the black screen.
            for (float black = 0f; black < blackTime; black += Mathf.Min(Time.unscaledDeltaTime, MaxBlackStep))
                yield return null;

            yield return transition.Open(focus);
        }

        /// <summary>The room starts rumbling (screen shake, debris from the ceiling) until <see cref="StopRumble"/>.</summary>
        public void StartRumble()
        {
            if (_isRumbling)
                return;

            Room room = RoomManager.CurrentRoom;
            Rect area = room != null ? room.Bounds : new Rect(Player.Transform != null ? (Vector2)Player.Transform.position : Vector2.zero, Vector2.one);
            _isRumbling = true;
            EventBus<RumbleStartedEvent>.Raise(new RumbleStartedEvent(area));
        }

        public void StopRumble()
        {
            if (!_isRumbling)
                return;

            _isRumbling = false;
            EventBus<RumbleStoppedEvent>.Raise(default);
        }

        public IEnumerator Rumble(float duration)
        {
            StartRumble();
            yield return Wait(duration);
            StopRumble();
        }

        /// <summary>Quickly covers the screen (the respawn transition, closing on the middle of the view).</summary>
        internal IEnumerator CoverScreen()
        {
            IRespawnTransition transition = FindTransition();
            if (transition != null)
                yield return transition.Close(ViewCenter, quick: true);
        }

        /// <summary>Quickly opens the screen again, on the player.</summary>
        internal IEnumerator RevealScreen()
        {
            IRespawnTransition transition = FindTransition();
            if (transition != null)
                yield return transition.Open(() => Player.Transform != null ? Player.Transform.position : ViewCenter(), quick: true);
        }

        static Vector2 ViewCenter() => UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform.position : Vector2.zero;

        // ---------------------------------------------------------------- Lifetime

        internal void Begin()
        {
            EventBus<CutsceneStartedEvent>.Raise(default);
            Player.TakeControl();
            if (_input != null)
            {
                _input.JumpPressed += CountPress;
                _input.AttackPressed += CountPress;
                _input.SkipPressed += _owner.Skip;
            }
        }

        /// <summary>
        /// Stops everything the script left running (steps alongside it, tweens, rumble, slow motion, a line or a
        /// button prompt on screen) without handing anything back yet, e.g. when it's skipped.
        /// </summary>
        internal void Halt()
        {
            foreach (Coroutine coroutine in _running)
            {
                if (coroutine != null)
                    _owner.StopCoroutine(coroutine);
            }

            _running.Clear();
            foreach (Tween tween in _tweens)
                tween?.Kill();
            _tweens.Clear();

            StopRumble();
            RestoreTime();
            if (_prompt != null)
                _prompt.Hide(pressed: false);
            if (_speech != null)
                _speech.EndSpeech();
        }

        internal void End(bool returnControl)
        {
            Halt();
            if (_input != null)
            {
                _input.JumpPressed -= CountPress;
                _input.AttackPressed -= CountPress;
                _input.SkipPressed -= _owner.Skip;
            }

            Camera.ReleaseNow();
            Companion.Restore();
            Player.Restore(returnControl);
            EventBus<CutsceneEndedEvent>.Raise(default);
        }

        void CountPress() => _presses++;

        void RestoreTime()
        {
            if (!_isSlowed)
                return;

            _isSlowed = false;
            GameTime.ResetTimeScale();
        }

        static IRespawnTransition FindTransition()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour is IRespawnTransition transition)
                    return transition;
            }

            return null;
        }
    }
}
