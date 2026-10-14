using System.Collections;
using APX.Player;
using DG.Tweening;
using UnityEngine;

namespace APX.Cutscenes
{
    /// <summary>
    /// The player during a cutscene: no input control, but physics and the regular animations keep running, so a
    /// script can teleport, launch or animate them and let the game take it from there. Sprite effects (trembling,
    /// being pushed) move the sprite only, and it's put back where it belongs when the cutscene ends, along with
    /// physics, visibility and (unless the player died) control.
    /// </summary>
    public sealed class CutscenePlayer
    {
        readonly CutsceneContext _context;
        readonly PlayerController _player;
        readonly PlayerAttack _attack;
        readonly Rigidbody2D _body;
        readonly Collider2D _collider;
        readonly Animator _animator;
        readonly Transform _sprite;
        readonly SpriteRenderer _renderer;
        Vector3 _spriteRest;
        Tween _spriteTween;
        bool _hasControl = true;
        bool _isShowingSprite;

        internal CutscenePlayer(CutsceneContext context, PlayerController player)
        {
            _context = context;
            _player = player;
            if (player == null)
                return;

            _attack = player.GetComponent<PlayerAttack>();
            _body = player.GetComponent<Rigidbody2D>();
            _collider = player.GetComponent<Collider2D>();
            _animator = player.GetComponentInChildren<Animator>();
            _sprite = _animator != null ? _animator.transform : null;
            _renderer = _sprite != null ? _sprite.GetComponentInChildren<SpriteRenderer>() : null;
        }

        public PlayerController Controller => _player;

        public Transform Transform => _player != null ? _player.transform : null;

        public Animator Animator => _animator;

        public int FacingDirection => _player != null ? _player.FacingDirection : 1;

        /// <summary>Times the player has touched down since the cutscene started.</summary>
        public int LandingCount { get; private set; }

        public void Teleport(Vector2 position) => _player.Teleport(position);

        /// <summary>
        /// Teleports the player onto the ground straight below <paramref name="above"/> (a point in open space), e.g.
        /// where a fall from there would have landed. Turns physics back on. With no ground below, they go to
        /// <paramref name="above"/> itself.
        /// </summary>
        public void PlaceOnGround(Vector2 above, float maxDistance = 40f)
        {
            // Bounds are only valid while the body is in the physics world.
            _body.simulated = true;
            Vector2 position = above;
            if (_collider != null)
            {
                Bounds bounds = _collider.bounds;
                Vector2 offset = (Vector2)bounds.center - (Vector2)_player.transform.position;
                var size = new Vector2(bounds.size.x * 0.9f, bounds.size.y);
                RaycastHit2D hit = Physics2D.BoxCast(above + offset, size, 0f, Vector2.down, maxDistance, _player.GroundLayers);
                if (hit.collider != null)
                    position = hit.centroid - offset;
            }

            _player.Teleport(position);
        }

        /// <summary>Turns physics off (frozen in place, e.g. waiting off screen) or back on.</summary>
        public void SetPhysics(bool simulated) => _body.simulated = simulated;

        public void SetVisible(bool visible)
        {
            if (_renderer != null)
                _renderer.enabled = visible;
        }

        public void PlayAnimation(string state)
        {
            StopShowingSprite();
            _animator.Play(state, 0, 0f);
        }

        public void FaceTowards(float worldX) => _player.FaceTowards(worldX);

        /// <summary>Jumps straight up at <paramref name="speed"/>; the regular jump, fall and land animations follow.</summary>
        public void Hop(float speed) => _player.Launch(new Vector2(0f, speed));

        /// <summary>Waits until the player next touches down (e.g. after <see cref="Hop"/>).</summary>
        public IEnumerator WaitForLanding()
        {
            int landings = LandingCount;
            while (LandingCount == landings && _player != null)
                yield return null;
        }

        /// <summary>Shakes the player's sprite sideways for <paramref name="duration"/> seconds, e.g. waking up.</summary>
        public IEnumerator Tremble(float strength, float duration)
        {
            if (_sprite == null || strength <= 0f || duration <= 0f)
                yield break;

            ResetSprite();
            _spriteTween = _context.Track(_sprite.DOShakePosition(duration, new Vector3(strength, 0f, 0f), vibrato: 25, randomness: 0f, snapping: false, fadeOut: true));
            yield return _spriteTween.WaitForCompletion();
            ResetSprite();
        }

        /// <summary>Shoves the player's sprite <paramref name="distance"/> towards <paramref name="direction"/> (1 right,
        /// -1 left) and lets it spring back, e.g. when bumped.</summary>
        public void Push(int direction, float distance)
        {
            if (_sprite == null)
                return;

            ResetSprite();

            // The punch is in local space, which is mirrored while the player faces left.
            float mirror = _sprite.parent != null && _sprite.parent.lossyScale.x < 0f ? -1f : 1f;
            _spriteTween = _context.Track(_sprite.DOPunchPosition(new Vector3(distance * direction * mirror, 0f, 0f), 0.2f, vibrato: 4, elasticity: 0.5f)
                .OnComplete(ResetSprite));
        }

        /// <summary>Shows <paramref name="sprite"/> instead of the animation for <paramref name="duration"/> seconds
        /// (e.g. a quick reaction), then lets the animations take over again.</summary>
        public IEnumerator ShowSprite(Sprite sprite, float duration)
        {
            if (_renderer != null && sprite != null)
            {
                // The animator would put its own sprite back every frame.
                if (_animator != null)
                    _animator.enabled = false;
                _renderer.sprite = sprite;
                _isShowingSprite = true;
            }

            yield return _context.Wait(duration);
            StopShowingSprite();
        }

        /// <summary>Gives control back before the cutscene is over, e.g. during its last camera move.</summary>
        public void ReturnControl() => SetControl(true);

        internal void TakeControl()
        {
            if (_player == null)
                return;

            _spriteRest = _sprite != null ? _sprite.localPosition : Vector3.zero;
            _player.Landed += OnLanded;
            SetControl(false);
        }

        internal void Restore(bool returnControl)
        {
            if (_player == null)
                return;

            _player.Landed -= OnLanded;
            _spriteTween?.Kill();
            ResetSprite();
            StopShowingSprite();
            if (_body != null)
                _body.simulated = true;
            SetVisible(true);
            if (returnControl)
                SetControl(true);
        }

        void SetControl(bool hasControl)
        {
            if (_player == null || hasControl == _hasControl)
                return;

            _hasControl = hasControl;
            _player.SetControl(hasControl);
            if (_attack != null)
                _attack.enabled = hasControl;
        }

        void ResetSprite()
        {
            if (_sprite != null)
                _sprite.localPosition = _spriteRest;
        }

        void StopShowingSprite()
        {
            if (!_isShowingSprite)
                return;

            _isShowingSprite = false;
            if (_animator != null)
                _animator.enabled = true;
        }

        void OnLanded(float impactSpeed) => LandingCount++;
    }
}
