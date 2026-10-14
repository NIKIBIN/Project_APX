using System;
using APX.Core;
using DG.Tweening;
using UnityEngine;

namespace APX.Rooms
{
    /// <summary>
    /// Screen-by-screen camera: it never follows the player, it cuts to the centre of the current room
    /// whenever the room changes and sizes itself so the whole room is visible. Cutscenes can direct it
    /// (see <see cref="Direct"/>): it eases in on a shot, following it as far as the room's edges allow,
    /// until <see cref="Release"/> zooms back out to the whole room.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RoomCamera : MonoBehaviour
    {
        const float ArrivedDistance = 0.05f;
        const float ArrivedZoom = 0.01f;

        [Tooltip("Resize the orthographic camera so the whole room fits on screen.")]
        [SerializeField] bool fitToRoom = true;

        Camera _camera;
        Room _room;
        float _authoredSize;
        float _zoom = 1f;

        // Cutscene direction: the view centre and zoom ease towards the shot, both re-read every frame.
        Func<Vector2> _shotFocus;
        Func<float> _shotZoom;
        float _shotMoveTime;
        bool _snapToShot;
        Vector2 _viewCenter;
        Vector2 _viewVelocity;
        float _zoomVelocity;

        // Zooming back out after a shot.
        float _releaseDuration;
        float _releaseTime;
        float _releaseFromZoom;
        Ease _releaseEase;
        bool _isReleasing;

        /// <summary>1 shows the whole room; 2 shows half of it, and so on.</summary>
        public float Zoom => _zoom;

        /// <summary>A cutscene is pointing the camera at something, or it is still zooming back out afterwards.</summary>
        public bool IsDirected => _shotFocus != null || _isReleasing;

        /// <summary>The view has reached the current shot (true when nothing is being shot).</summary>
        public bool HasReachedShot
        {
            get
            {
                if (_shotFocus == null || _room == null)
                    return _shotFocus == null;

                float zoom = ShotZoom();
                return Vector2.Distance(_viewCenter, GetViewCenter(_shotFocus(), zoom)) < ArrivedDistance &&
                       Mathf.Abs(_zoom - zoom) < ArrivedZoom;
            }
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _authoredSize = _camera.orthographicSize;
        }

        void OnEnable() => EventBus<RoomChangedEvent>.Subscribe(OnRoomChanged);

        void OnDisable() => EventBus<RoomChangedEvent>.Unsubscribe(OnRoomChanged);

        void Start()
        {
            if (RoomManager.CurrentRoom != null)
                SnapTo(RoomManager.CurrentRoom);
        }

        // Re-applied every frame: the window (or WebGL canvas) can be resized and a shot can be moving.
        void LateUpdate()
        {
            if (_room == null)
                return;

            if (_shotFocus != null)
                UpdateShot(Time.deltaTime);
            else if (_isReleasing)
                UpdateRelease(Time.deltaTime);

            ApplyView();
        }

        /// <summary>
        /// Cutscene shot: eases the view to <paramref name="focus"/> at <paramref name="zoom"/> (1 = whole room) over
        /// about <paramref name="moveTime"/> seconds, never showing outside the room. Both are re-read every frame, so a
        /// shot can follow something moving. <paramref name="snap"/> jumps straight there instead.
        /// </summary>
        public void Direct(Func<Vector2> focus, Func<float> zoom, float moveTime, bool snap = false)
        {
            if (!IsDirected)
            {
                _viewCenter = transform.position;
                _viewVelocity = Vector2.zero;
                _zoomVelocity = 0f;
            }

            _shotFocus = focus;
            _shotZoom = zoom;
            _shotMoveTime = Mathf.Max(0f, moveTime);
            _isReleasing = false;
            _snapToShot |= snap;
        }

        /// <summary>Ends the shot, zooming back out to the whole room over <paramref name="duration"/> seconds.</summary>
        public void Release(float duration = 0f, Ease ease = Ease.InOutSine)
        {
            if (!IsDirected)
                return;

            _shotFocus = null;
            _shotZoom = null;
            _isReleasing = duration > 0f && _zoom > 1f;
            _releaseDuration = duration;
            _releaseTime = 0f;
            _releaseFromZoom = _zoom;
            _releaseEase = ease;
            if (!_isReleasing)
                _zoom = 1f;
        }

        /// <summary>
        /// Where the camera centres when zoomed to <paramref name="zoom"/> on <paramref name="focus"/>: the focus
        /// pulled in as far as needed to keep the view inside the room.
        /// </summary>
        public Vector2 GetViewCenter(Vector2 focus, float zoom)
        {
            if (_room == null)
                return focus;

            zoom = Mathf.Max(1f, zoom);
            return zoom > 1f ? ClampToRoom(focus, GetSize(zoom)) : _room.Center;
        }

        void OnRoomChanged(RoomChangedEvent evt) => SnapTo(evt.Current);

        void SnapTo(Room room)
        {
            _room = room;
            if (_room != null)
                ApplyView();
        }

        void UpdateShot(float deltaTime)
        {
            float zoom = ShotZoom();
            Vector2 target = GetViewCenter(_shotFocus(), zoom);
            if (_snapToShot || _shotMoveTime <= 0f)
            {
                _snapToShot = false;
                _viewCenter = target;
                _zoom = zoom;
                _viewVelocity = Vector2.zero;
                _zoomVelocity = 0f;
                return;
            }

            // Eases the actual view centre (already kept inside the room), so moves stay smooth even where the
            // view presses against the room's edges.
            _viewCenter = Vector2.SmoothDamp(_viewCenter, target, ref _viewVelocity, _shotMoveTime, Mathf.Infinity, deltaTime);
            _zoom = Mathf.SmoothDamp(_zoom, zoom, ref _zoomVelocity, _shotMoveTime, Mathf.Infinity, deltaTime);
        }

        void UpdateRelease(float deltaTime)
        {
            _releaseTime += deltaTime;
            float t = Mathf.Clamp01(_releaseTime / _releaseDuration);
            _zoom = DOVirtual.EasedValue(_releaseFromZoom, 1f, t, _releaseEase);
            if (t >= 1f)
                _isReleasing = false;
        }

        void ApplyView()
        {
            float size = GetSize(_zoom);
            Vector2 center = IsDirected && _zoom > 1f ? ClampToRoom(_viewCenter, size) : _room.Center;
            _camera.orthographicSize = size;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }

        float ShotZoom() => Mathf.Max(1f, _shotZoom?.Invoke() ?? 1f);

        float GetSize(float zoom) => (fitToRoom ? GetFittedSize() : _authoredSize) / Mathf.Max(1f, zoom);

        Vector2 ClampToRoom(Vector2 focus, float size)
        {
            Vector2 halfView = new(size * _camera.aspect, size);
            Rect bounds = _room.Bounds;
            Vector2 center = _room.Center;
            return new Vector2(
                ClampAxis(focus.x, bounds.xMin + halfView.x, bounds.xMax - halfView.x, center.x),
                ClampAxis(focus.y, bounds.yMin + halfView.y, bounds.yMax - halfView.y, center.y));
        }

        float GetFittedSize()
        {
            Vector2 halfSize = _room.Size * 0.5f;
            return Mathf.Max(halfSize.y, halfSize.x / _camera.aspect);
        }

        // When the view is wider than the room on this axis, stay centred on the room.
        static float ClampAxis(float value, float min, float max, float fallback) =>
            min <= max ? Mathf.Clamp(value, min, max) : fallback;
    }
}
