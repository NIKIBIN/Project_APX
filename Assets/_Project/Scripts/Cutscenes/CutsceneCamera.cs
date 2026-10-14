using System;
using System.Collections;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;

namespace APX.Cutscenes
{
    /// <summary>
    /// Camera shots for cutscenes, through the <see cref="RoomCamera"/>: point it at something (followed as it
    /// moves) at a zoom, where 1 shows the whole room and 2 half of it. The view never leaves the room.
    /// <c>Focus</c> moves on at once; <c>FocusOn</c> waits for the camera to get there. The camera zooms back out
    /// to the room with <see cref="Release"/>, or at once when the cutscene ends.
    /// </summary>
    public sealed class CutsceneCamera
    {
        /// <summary>Seconds a shot takes to settle by default.</summary>
        public const float DefaultMoveTime = 0.45f;

        const float Timeout = 3f;

        readonly RoomCamera _camera;

        internal CutsceneCamera(RoomCamera camera) => _camera = camera;

        public RoomCamera RoomCamera => _camera;

        /// <summary>Points the camera at <paramref name="target"/>; <paramref name="snap"/> cuts there instead of easing.</summary>
        public void Focus(Transform target, float zoom, float moveTime = DefaultMoveTime, bool snap = false) =>
            Focus(() => target.position, () => zoom, moveTime, snap);

        public void Focus(Vector2 point, float zoom, float moveTime = DefaultMoveTime, bool snap = false) =>
            Focus(() => point, () => zoom, moveTime, snap);

        public void Focus(Func<Vector2> target, float zoom, float moveTime = DefaultMoveTime, bool snap = false) =>
            Focus(target, () => zoom, moveTime, snap);

        /// <summary>Points the camera at a moving target with a changing zoom, both re-read every frame.</summary>
        public void Focus(Func<Vector2> target, Func<float> zoom, float moveTime = DefaultMoveTime, bool snap = false)
        {
            if (_camera != null)
                _camera.Direct(target, zoom, moveTime, snap);
        }

        /// <summary>Keeps both <paramref name="a"/> and <paramref name="b"/> in view, with <paramref name="margin"/>
        /// around them, zoomed in no further than <paramref name="maxZoom"/>.</summary>
        public void FocusOnBoth(Transform a, Transform b, Vector2 margin, float maxZoom, float moveTime = DefaultMoveTime) =>
            Focus(() => (a.position + b.position) * 0.5f, () => FitZoom(a.position, b.position, margin, maxZoom), moveTime);

        public IEnumerator FocusOn(Transform target, float zoom, float moveTime = DefaultMoveTime)
        {
            Focus(target, zoom, moveTime);
            yield return WaitForCamera();
        }

        public IEnumerator FocusOn(Vector2 point, float zoom, float moveTime = DefaultMoveTime)
        {
            Focus(point, zoom, moveTime);
            yield return WaitForCamera();
        }

        /// <summary>Waits until the camera has reached the current shot (giving up after a few seconds).</summary>
        public IEnumerator WaitForCamera()
        {
            for (float waited = 0f; waited < Timeout && _camera != null && !_camera.HasReachedShot; waited += Time.deltaTime)
                yield return null;
        }

        /// <summary>Zooms back out to the whole room over <paramref name="duration"/> seconds and waits for it.</summary>
        public IEnumerator Release(float duration = 0.8f, Ease ease = Ease.InOutSine)
        {
            if (_camera == null)
                yield break;

            _camera.Release(duration, ease);
            while (_camera != null && _camera.IsDirected)
                yield return null;
        }

        /// <summary>Zoom that fits both points with <paramref name="margin"/> around them, between the whole room
        /// and <paramref name="maxZoom"/>.</summary>
        public float FitZoom(Vector2 a, Vector2 b, Vector2 margin, float maxZoom)
        {
            Room room = RoomManager.CurrentRoom;
            if (room == null)
                return maxZoom;

            Vector2 span = new(Mathf.Abs(a.x - b.x) + margin.x * 2f, Mathf.Abs(a.y - b.y) + margin.y * 2f);
            float zoom = Mathf.Min(room.Size.x / span.x, room.Size.y / span.y);
            return Mathf.Clamp(zoom, 1f, Mathf.Max(1f, maxZoom));
        }

        internal void ReleaseNow()
        {
            if (_camera != null)
                _camera.Release();
        }
    }
}
