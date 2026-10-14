using System.Collections;
using APX.Feel;
using APX.Hints;
using APX.Masks;
using DG.Tweening;
using UnityEngine;

namespace APX.Cutscenes
{
    /// <summary>
    /// The hint companion (the spirit) during a cutscene. Once taken over it shows up and stays wherever the script
    /// moves it, without following the player or offering hints, until it's released (or the cutscene ends, which
    /// lets it go back to what it was doing). Every step is skipped quietly in scenes without a companion.
    /// </summary>
    public sealed class CutsceneCompanion
    {
        readonly CutsceneContext _context;
        readonly HintCompanion _companion;
        readonly MaskManager _masks;
        Tween _move;
        bool _isTaken;

        internal CutsceneCompanion(CutsceneContext context, HintCompanion companion, MaskManager masks)
        {
            _context = context;
            _companion = companion;
            _masks = masks;
        }

        public bool Exists => _companion != null;

        public HintCompanion Component => _companion;

        public Transform Transform => _companion != null ? _companion.transform : null;

        /// <summary>Takes the companion over where it is (popping it into view if it was away).</summary>
        public void Take()
        {
            if (_companion != null)
                Take(_companion.transform.position);
        }

        /// <summary>Takes the companion over and shows it at <paramref name="position"/>.</summary>
        public void Take(Vector3 position)
        {
            if (_companion == null)
                return;

            _companion.BeginScripted(position);
            _isTaken = true;
        }

        /// <summary>Flies to <paramref name="position"/> at about <paramref name="speed"/> units per second, easing in
        /// and out and facing where it's going.</summary>
        public IEnumerator FlyTo(Vector3 position, float speed = 7f, float minDuration = 0.4f)
        {
            if (_companion == null)
                yield break;

            Transform transform = _companion.transform;
            FaceTowards(position.x);
            _move?.Kill();
            float duration = Mathf.Max(minDuration, Vector2.Distance(transform.position, position) / Mathf.Max(speed, 0.01f));
            _move = _context.Track(transform.DOMove(position, duration).SetEase(Ease.InOutSine));
            yield return _move.WaitForCompletion();
        }

        /// <summary>Moves to <paramref name="position"/> over <paramref name="duration"/> seconds without turning
        /// (e.g. backing away from something).</summary>
        public IEnumerator MoveTo(Vector3 position, float duration, Ease ease = Ease.OutQuad)
        {
            if (_companion == null)
                yield break;

            _move?.Kill();
            _move = _context.Track(_companion.transform.DOMove(position, duration).SetEase(ease));
            yield return _move.WaitForCompletion();
        }

        /// <summary>Looks right (1) or left (-1).</summary>
        public void Face(int direction)
        {
            if (_companion != null)
                _companion.SetFacing(direction);
        }

        public void FaceTowards(float worldX)
        {
            if (_companion != null)
                Face(worldX < _companion.transform.position.x ? -1 : 1);
        }

        public void Emote(Emote emote)
        {
            if (_companion != null)
                _companion.ShowEmote(emote);
        }

        public void HideEmote()
        {
            if (_companion != null)
                _companion.HideEmote();
        }

        /// <summary>Lines spoken by the companion (see <see cref="CutsceneContext.Say"/>).</summary>
        public IEnumerator Say(params string[] lines) => _context.Say(_companion, lines);

        /// <summary>
        /// Hands the companion back: <paramref name="summon"/> keeps it as the hint companion beside the player
        /// (through the mask manager, gliding over from where it is); otherwise it goes back to what it was doing.
        /// <paramref name="snapToPlace"/> puts it at its place at once instead of gliding (e.g. after a skip).
        /// </summary>
        public void Release(bool summon, bool snapToPlace = false)
        {
            if (_companion == null)
                return;

            if (_isTaken)
            {
                _isTaken = false;
                _move?.Kill();
                _companion.HideEmote();
                if (summon && _masks != null && !MaskManager.IsCompanionSummoned)
                    _masks.SummonCompanion();
                else
                    _companion.EndScripted(); // Already summoned: it just goes back to following the player.
            }

            // Also when released earlier and still on its way there.
            if (snapToPlace && _companion.IsSummoned)
                _companion.SnapToPlace();
        }

        internal void Restore()
        {
            if (_isTaken)
                Release(summon: false);
        }
    }
}
