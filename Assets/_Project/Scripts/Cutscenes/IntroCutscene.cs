using System;
using System.Collections;
using APX.Feel;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;

namespace APX.Cutscenes
{
    /// <summary>
    /// Opening cutscene:
    /// <list type="number">
    /// <item>Black screen; the respawn transition opens on the spirit (the hint companion) floating around the room.</item>
    /// <item>The room rumbles and debris falls from the ceiling; the spirit stops, puzzled ("?").</item>
    /// <item>Once it calms down, the camera pans to the ceiling opening and the player drops through it, knocked out.</item>
    /// <item>The spirit notices ("!"), flies over and nudges the player: once, then twice. The player trembles once,
    /// then twice, and hops to their feet, startling the spirit back.</item>
    /// <item>The spirit asks if they're all right; the player can only answer with a sound and a face. The spirit gives
    /// up, flies up to the opening and marvels at the fall.</item>
    /// <item>The spirit heads to its place (the room's companion spot, e.g. by the exit, or beside the player), the
    /// camera zooms out to the room and control returns.</item>
    /// </list>
    /// Without a companion the spirit beats are skipped; without a ceiling opening the player appears above their
    /// placed position instead. Ordinary game systems (gravity, landing dust, squash) play along. Skipping it (Esc)
    /// leaves the player standing where the fall lands and the spirit at its place.
    /// </summary>
    public sealed class IntroCutscene : Cutscene
    {
        [Header("Opening")]
        [Tooltip("Seconds the screen stays completely black before the opening transition starts.")]
        [SerializeField, Min(0f)] float blackScreenTime = 0.5f;
        [Tooltip("Camera zoom during the cutscene: 1 shows the whole room, 2.5 shows 40% of it.")]
        [SerializeField, Min(1f)] float introZoom = 2.5f;
        [Tooltip("Seconds the camera takes to settle when it moves to something new.")]
        [SerializeField, Min(0f)] float cameraMoveTime = 0.45f;

        [Header("Animator States")]
        [SerializeField] string fallState = "IntroFall";
        [SerializeField] string knockedOutState = "IntroKnockedOut";
        [SerializeField] string jumpState = "Jump";
        [Tooltip("Where the player is left standing when the cutscene is skipped.")]
        [SerializeField] string idleState = "Idle";

        [Header("Spirit Floating Around")]
        [Tooltip("Centre of the spirit's floating. Defaults to the middle of the room.")]
        [SerializeField] Transform spiritAnchor;
        [Tooltip("Half-width and half-height of the figure-eight the spirit floats along.")]
        [SerializeField] Vector2 wanderSize = new(2.5f, 0.7f);
        [Tooltip("Seconds per figure-eight.")]
        [SerializeField, Min(0.1f)] float wanderPeriod = 5f;
        [Tooltip("Seconds of floating after the screen opens, before the rumble.")]
        [SerializeField, Min(0f)] float wanderTime = 1.5f;

        [Header("Rumble")]
        [SerializeField, Min(0f)] float rumbleDuration = 2.5f;
        [Tooltip("Seconds into the rumble before the spirit stops and the \"?\" appears.")]
        [SerializeField, Min(0f)] float puzzledDelay = 0.6f;
        [Tooltip("Seconds the spirit takes to slow down to a stop.")]
        [SerializeField, Min(0.01f)] float spiritStopTime = 0.4f;
        [Tooltip("Seconds between the puzzled spirit's glances left and right. 0 = it doesn't look around.")]
        [SerializeField, Min(0f)] float lookAroundInterval = 0.5f;
        [Tooltip("Seconds after the rumble stops (so the last debris lands) before the camera moves to the opening.")]
        [SerializeField, Min(0f)] float pauseAfterRumble = 1f;

        [Header("Fall")]
        [Tooltip("Optional hole in the ceiling the player drops through from off screen. Without one, they appear " +
                 "above their placed position.")]
        [SerializeField] Transform ceilingOpening;
        [Tooltip("How far above the top of the room the player starts falling through the ceiling opening.")]
        [SerializeField, Min(0f)] float offscreenMargin = 1.5f;
        [Tooltip("How far above the player's placed position the fall starts (kept inside the room). Without a ceiling opening only.")]
        [SerializeField, Min(0f)] float fallHeight = 6f;
        [Tooltip("Seconds the camera holds on the opening before the player falls.")]
        [SerializeField, Min(0f)] float holdBeforeFall = 0.6f;

        [Header("Waking Up")]
        [Tooltip("Seconds lying still after landing before the spirit notices (\"!\").")]
        [SerializeField, Min(0f)] float surpriseDelay = 0.6f;
        [Tooltip("Seconds the \"!\" shows before the spirit flies over.")]
        [SerializeField, Min(0f)] float surpriseTime = 0.7f;
        [Tooltip("Room the camera leaves around the spirit and the player while both are in view.")]
        [SerializeField] Vector2 twoShotMargin = new(3f, 2.5f);
        [Tooltip("Speed of the spirit's flight to the player, in units per second.")]
        [SerializeField, Min(0.1f)] float spiritSpeed = 7f;
        [Tooltip("Where the spirit hovers to nudge the player, from the player's centre (x towards their head).")]
        [SerializeField] Vector2 nudgeSpot = new(0.95f, 0f);
        [SerializeField, Min(0f)] float nudgeDistance = 0.3f;
        [SerializeField, Min(0.01f)] float nudgeInTime = 0.07f;
        [SerializeField, Min(0.01f)] float nudgeOutTime = 0.16f;
        [Tooltip("How far each nudge shoves the player's sprite.")]
        [SerializeField, Min(0f)] float nudgePush = 0.06f;
        [SerializeField, Min(0f)] float pauseAfterFirstNudge = 0.7f;
        [SerializeField, Min(0f)] float quickNudgeGap = 0.08f;
        [SerializeField, Min(0f)] float pauseBeforeTremble = 0.6f;
        [SerializeField, Min(0f)] float firstTrembleStrength = 0.04f;
        [SerializeField, Min(0f)] float firstTrembleDuration = 0.25f;
        [SerializeField, Min(0f)] float pauseBetweenTrembles = 0.5f;
        [SerializeField, Min(0f)] float quickTrembleStrength = 0.08f;
        [SerializeField, Min(0f)] float quickTrembleDuration = 0.18f;
        [SerializeField, Min(0f)] float quickTrembleGap = 0.08f;
        [SerializeField, Min(0f)] float pauseBeforeHop = 0.35f;
        [Tooltip("Upward speed of the hop to the feet.")]
        [SerializeField, Min(0f)] float hopSpeed = 9f;

        [Header("Talking")]
        [Tooltip("How far the spirit backs off (away from the player, and up) when they spring to their feet.")]
        [SerializeField] Vector2 spiritBackOff = new(1.1f, 0.5f);
        [SerializeField, Min(0.01f)] float backOffTime = 0.35f;
        [Tooltip("Seconds after the player lands before the spirit speaks.")]
        [SerializeField, Min(0f)] float pauseBeforeTalking = 0.3f;
        [SerializeField, TextArea(1, 3)] string askLine = "Você está bem?";
        [Tooltip("The player's answer: this sound...")]
        [SerializeField] AudioClip reactionSound;
        [Tooltip("...while showing this sprite for a moment.")]
        [SerializeField] Sprite reactionSprite;
        [SerializeField, Min(0f)] float reactionTime = 0.5f;
        [Tooltip("The spirit's lines after the player's answer.")]
        [SerializeField, TextArea(1, 3)] string[] replyLines =
        {
            "...",
            "O que foi, o gato comeu a sua... ah, esquece.",
        };
        [Tooltip("Where the spirit hovers to talk about the fall, from the ceiling opening (or from above the player without one).")]
        [SerializeField] Vector2 openingTalkOffset = new(0f, -2.5f);
        [Tooltip("The spirit's lines up by the ceiling opening; the cutscene ends after the last one.")]
        [SerializeField, TextArea(1, 3)] string[] fallLines =
        {
            "Mas minha nossa, foi uma queda e tanto, estou surpreso de você estar inteiro.",
            "Mas é como dizem, gatos têm 9 vidas, não é mesmo?",
        };

        [Header("After the Intro")]
        [Tooltip("Seconds the spirit gets to head to its place before the camera zooms out.")]
        [SerializeField, Min(0f)] float pauseBeforeZoomOut = 0.4f;
        [SerializeField, Min(0f)] float zoomOutDuration = 1.2f;
        [SerializeField] Ease zoomOutEase = Ease.InOutSine;
        [Tooltip("Give the player control as soon as the zoom-out starts instead of when it ends.")]
        [SerializeField] bool controlDuringZoomOut;
        [Tooltip("Keep the spirit as the hint companion beside the player afterwards (through the MaskManager).")]
        [SerializeField] bool summonCompanion = true;

        // Spirit floating.
        Coroutine _wander;
        Vector2 _wanderCenter;
        float _wanderPhase;
        float _wanderSpeed;
        Vector2 _placedPosition;

        protected override IEnumerator Perform(CutsceneContext c)
        {
            CutscenePlayer player = c.Player;
            CutsceneCompanion spirit = c.Companion;

            // The player waits off screen (or hidden) until their fall.
            _placedPosition = player.Transform.position;
            Vector2 fallStart = GetFallStart(_placedPosition);
            player.SetPhysics(false);
            player.Teleport(fallStart);
            player.PlayAnimation(fallState);
            if (ceilingOpening == null)
                player.SetVisible(false);

            Func<Vector2> openingFocus = () => fallStart;
            if (spirit.Exists)
            {
                _wanderCenter = spiritAnchor != null ? spiritAnchor.position : RoomCenter();
                _wanderPhase = 0f;
                _wanderSpeed = 1f;
                spirit.Take(_wanderCenter);
                _wander = c.Run(Wander(spirit));
                openingFocus = () => spirit.Transform.position;
            }

            c.Camera.Focus(openingFocus, introZoom, cameraMoveTime, snap: true);
            yield return c.OpenFromBlack(blackScreenTime, openingFocus);

            if (spirit.Exists)
            {
                yield return c.Wait(wanderTime);
                yield return Rumble(c, spirit);
            }

            // To the opening, then drop.
            Vector2 dropPoint = ceilingOpening != null ? (Vector2)ceilingOpening.position : fallStart;
            yield return c.Camera.FocusOn(dropPoint, introZoom, cameraMoveTime);
            yield return c.Wait(holdBeforeFall);
            yield return Fall(c);

            player.PlayAnimation(knockedOutState);
            yield return c.Wait(surpriseDelay);
            if (spirit.Exists)
                yield return SpiritWakesPlayer(c, spirit);

            yield return player.Tremble(firstTrembleStrength, firstTrembleDuration);
            yield return c.Wait(pauseBetweenTrembles);
            yield return player.Tremble(quickTrembleStrength, quickTrembleDuration);
            yield return c.Wait(quickTrembleGap);
            yield return player.Tremble(quickTrembleStrength, quickTrembleDuration);
            yield return c.Wait(pauseBeforeHop);

            // Hop up (the regular jump/fall/land animations take it from here); the startled spirit backs off.
            int head = player.FacingDirection;
            player.Hop(hopSpeed);
            player.PlayAnimation(jumpState);
            if (spirit.Exists)
                c.Run(spirit.MoveTo(spirit.Transform.position + new Vector3(spiritBackOff.x * head, spiritBackOff.y, 0f), backOffTime));
            yield return player.WaitForLanding();

            if (spirit.Exists)
            {
                yield return Talk(c, spirit);

                // The spirit heads off to its place (by the room's exit, if the room has a companion spot).
                spirit.Release(summon: summonCompanion);
                yield return c.Wait(pauseBeforeZoomOut);
            }

            if (controlDuringZoomOut)
                player.ReturnControl();
            yield return c.Camera.Release(zoomOutDuration, zoomOutEase);
        }

        /// <summary>
        /// As the intro leaves things: the player standing where the fall lands (under the ceiling opening, or at
        /// their placed position without one) and the spirit at its place.
        /// </summary>
        protected override void OnSkipped(CutsceneContext c)
        {
            CutscenePlayer player = c.Player;
            if (ceilingOpening != null)
                player.PlaceOnGround(ceilingOpening.position);
            else
                player.Teleport(_placedPosition);
            player.PlayAnimation(idleState);
            c.Companion.Release(summon: summonCompanion, snapToPlace: true);
        }

        IEnumerator Rumble(CutsceneContext c, CutsceneCompanion spirit)
        {
            c.StartRumble();
            yield return c.Wait(Mathf.Min(puzzledDelay, rumbleDuration));
            c.Track(DOVirtual.Float(_wanderSpeed, 0f, spiritStopTime, speed => _wanderSpeed = speed).SetEase(Ease.OutQuad));
            spirit.Emote(Emote.Question);

            // Glances left and right while the room shakes.
            float remaining = rumbleDuration - puzzledDelay;
            int look = -1;
            while (remaining > 0f)
            {
                float step = lookAroundInterval > 0f ? Mathf.Min(lookAroundInterval, remaining) : remaining;
                yield return c.Wait(step);
                remaining -= step;
                if (lookAroundInterval > 0f && remaining > 0f)
                {
                    spirit.Face(look);
                    look = -look;
                }
            }

            c.StopRumble();
            yield return c.Wait(pauseAfterRumble);
            spirit.HideEmote();
            c.Stop(_wander);
        }

        IEnumerator Fall(CutsceneContext c)
        {
            CutscenePlayer player = c.Player;
            int landings = player.LandingCount;
            player.SetVisible(true);
            player.SetPhysics(true);

            // The camera holds on the opening until the player drops past it, then follows them down.
            if (ceilingOpening != null)
            {
                while (player.Transform.position.y > ceilingOpening.position.y && player.LandingCount == landings)
                    yield return null;
            }

            c.Camera.Focus(player.Transform, introZoom, 0f);
            while (player.LandingCount == landings)
                yield return null;
        }

        IEnumerator SpiritWakesPlayer(CutsceneContext c, CutsceneCompanion spirit)
        {
            CutscenePlayer player = c.Player;
            int head = player.FacingDirection;

            // Both in view while the spirit notices and flies over; the "!" waits for the camera to get there.
            c.Camera.FocusOnBoth(spirit.Transform, player.Transform, twoShotMargin, introZoom, cameraMoveTime);
            yield return c.Camera.WaitForCamera();
            spirit.FaceTowards(player.Transform.position.x);
            spirit.Emote(Emote.Exclamation);
            yield return c.Wait(surpriseTime);
            spirit.HideEmote();

            Vector3 spot = player.Transform.position + new Vector3(nudgeSpot.x * head, nudgeSpot.y, 0f);
            yield return spirit.FlyTo(spot, spiritSpeed);
            spirit.Face(-head);

            // Once, a pause, then twice in quick succession.
            yield return Nudge(c, spirit, -head);
            yield return c.Wait(pauseAfterFirstNudge);
            yield return Nudge(c, spirit, -head);
            yield return c.Wait(quickNudgeGap);
            yield return Nudge(c, spirit, -head);
            yield return c.Wait(pauseBeforeTremble);
        }

        /// <summary>
        /// The spirit asks if the player is all right; they can only answer with a sound and a face, so it gives up
        /// and flies up to the opening they fell through to marvel at the fall.
        /// </summary>
        IEnumerator Talk(CutsceneContext c, CutsceneCompanion spirit)
        {
            CutscenePlayer player = c.Player;
            yield return c.Wait(pauseBeforeTalking);
            spirit.FaceTowards(player.Transform.position.x);
            yield return spirit.Say(askLine);

            c.PlaySound(reactionSound);
            yield return player.ShowSprite(reactionSprite, reactionTime);
            yield return spirit.Say(replyLines);

            // The camera keeps both in view, so it widens as the spirit rises.
            Vector3 above = ceilingOpening != null ? ceilingOpening.position : player.Transform.position + Vector3.up * 6f;
            yield return spirit.FlyTo(above + (Vector3)openingTalkOffset, spiritSpeed);
            spirit.FaceTowards(player.Transform.position.x);
            yield return spirit.Say(fallLines);
        }

        /// <summary>The spirit bumps into the player (towards <paramref name="direction"/>) and backs off.</summary>
        IEnumerator Nudge(CutsceneContext c, CutsceneCompanion spirit, int direction)
        {
            Transform transform = spirit.Transform;
            Vector3 rest = transform.position;
            Vector3 bump = rest + new Vector3(nudgeDistance * direction, 0f, 0f);
            yield return c.Play(DOTween.Sequence()
                .Append(transform.DOMove(bump, nudgeInTime).SetEase(Ease.InQuad))
                .AppendCallback(() => c.Player.Push(direction, nudgePush))
                .Append(transform.DOMove(rest, nudgeOutTime).SetEase(Ease.OutQuad)));
        }

        IEnumerator Wander(CutsceneCompanion spirit)
        {
            while (true)
            {
                _wanderPhase += Time.deltaTime * _wanderSpeed * 2f * Mathf.PI / wanderPeriod;
                spirit.Transform.position = _wanderCenter + new Vector2(
                    Mathf.Sin(_wanderPhase) * wanderSize.x,
                    Mathf.Sin(_wanderPhase * 2f) * wanderSize.y);

                if (_wanderSpeed > 0.2f)
                    spirit.Face(Mathf.Cos(_wanderPhase) >= 0f ? 1 : -1);
                yield return null;
            }
        }

        static Vector2 RoomCenter() => RoomManager.CurrentRoom != null ? RoomManager.CurrentRoom.Center : Vector2.zero;

        /// <summary>
        /// Start of the fall: just above the top of the room over the ceiling opening (off screen); or, without
        /// one, above the placed position but inside the room.
        /// </summary>
        Vector2 GetFallStart(Vector2 placedPosition)
        {
            Room room = RoomManager.CurrentRoom;
            if (ceilingOpening != null)
            {
                // The camera never shows past the room's top, so above it is off screen.
                float top = room != null ? Mathf.Max(room.Bounds.yMax, ceilingOpening.position.y) : ceilingOpening.position.y;
                return new Vector2(ceilingOpening.position.x, top + offscreenMargin);
            }

            Vector2 start = placedPosition + Vector2.up * fallHeight;
            if (room != null)
                start.y = Mathf.Min(start.y, room.Bounds.yMax - 1.5f);
            return start;
        }
    }
}
