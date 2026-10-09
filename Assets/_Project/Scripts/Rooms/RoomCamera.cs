using APX.Core;
using UnityEngine;

namespace APX.Rooms
{
    /// <summary>
    /// Screen-by-screen camera: it never follows the player, it cuts to the centre of the current room
    /// whenever the room changes and sizes itself so the whole room is visible. For cutscenes it can zoom
    /// in on a target, following it as far as the room's edges allow.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RoomCamera : MonoBehaviour
    {
        [Tooltip("Resize the orthographic camera so the whole room fits on screen.")]
        [SerializeField] bool fitToRoom = true;

        Camera _camera;
        Room _room;
        float _authoredSize;
        float _zoom = 1f;
        Transform _zoomFocus;

        /// <summary>1 shows the whole room; 2 shows half of it, and so on.</summary>
        public float Zoom => _zoom;

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

        // Re-applied every frame: the window (or WebGL canvas) can be resized and a zoom can be animating.
        void LateUpdate()
        {
            if (_room != null)
                ApplyView();
        }

        /// <summary>
        /// Zooms in (1 = whole room). While zoomed in, <paramref name="focus"/> is kept centred as far as the
        /// room's edges allow, so the camera never shows outside the room.
        /// </summary>
        public void SetZoom(float zoom, Transform focus = null)
        {
            _zoom = Mathf.Max(1f, zoom);
            _zoomFocus = focus;
            if (_room != null)
                ApplyView();
        }

        void OnRoomChanged(RoomChangedEvent evt) => SnapTo(evt.Current);

        void SnapTo(Room room)
        {
            _room = room;
            ApplyView();
        }

        void ApplyView()
        {
            float size = (fitToRoom ? GetFittedSize() : _authoredSize) / _zoom;
            Vector2 center = _room.Center;

            if (_zoomFocus != null && _zoom > 1f)
            {
                Vector2 halfView = new(size * _camera.aspect, size);
                Rect bounds = _room.Bounds;
                Vector2 focus = _zoomFocus.position;
                center = new Vector2(
                    ClampAxis(focus.x, bounds.xMin + halfView.x, bounds.xMax - halfView.x, center.x),
                    ClampAxis(focus.y, bounds.yMin + halfView.y, bounds.yMax - halfView.y, center.y));
            }

            _camera.orthographicSize = size;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
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
