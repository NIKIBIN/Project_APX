using System.Collections.Generic;
using APX.Masks;
using APX.Rooms;
using UnityEngine;

namespace APX.Feel
{
    /// <summary>
    /// While a mask slows the world (Nice and Slow), slowed things that move in the current room leave fading
    /// afterimages in the mask's colour. Only the slowed world gets them, never the player, so the mask reads
    /// differently from the radial menu's slow motion (which slows everything and greys the screen out).
    /// </summary>
    [RequireComponent(typeof(SpriteGhosts))]
    public sealed class MaskTimeAfterimages : MonoBehaviour
    {
        struct Sample
        {
            public Vector3 Position;
            public float Angle;
            public float Time;
        }

        struct Parts
        {
            public Room Room;
            public SpriteRenderer[] Sprites;
        }

        [Tooltip("Defaults to the MaskManager in the scene (for each mask's colour).")]
        [SerializeField] MaskManager maskManager;
        [Tooltip("Shortest real time between two afterimages of the same sprite.")]
        [SerializeField, Min(0.01f)] float interval = 0.07f;
        [SerializeField, Min(0.01f)] float lifetime = 0.3f;
        [SerializeField, Range(0f, 1f)] float alpha = 0.45f;
        [Tooltip("World units a sprite must move since its last afterimage to leave another one.")]
        [SerializeField, Min(0f)] float minMove = 0.08f;
        [Tooltip("Degrees a sprite must turn (e.g. a spinning saw) to leave another afterimage.")]
        [SerializeField, Min(0f)] float minTurn = 15f;

        readonly Dictionary<SpriteRenderer, Sample> _samples = new();
        readonly Dictionary<MaskTimeResponder, Parts> _parts = new();
        readonly List<SpriteRenderer> _stale = new();
        SpriteGhosts _ghosts;

        void Awake()
        {
            _ghosts = GetComponent<SpriteGhosts>();
            if (maskManager == null)
                maskManager = FindAnyObjectByType<MaskManager>();
        }

        void LateUpdate()
        {
            Room room = RoomManager.CurrentRoom;
            float now = Time.unscaledTime;
            foreach (MaskResponder responder in MaskResponder.Active)
            {
                if (responder is not MaskTimeResponder time)
                    continue;

                Parts parts = PartsOf(time);
                bool slowed = time.IsConditionMet && time.TimeScale < 1f && room != null && parts.Room == room;
                foreach (SpriteRenderer sprite in parts.Sprites)
                {
                    if (sprite == null)
                        continue;

                    if (!slowed || !sprite.enabled || !sprite.gameObject.activeInHierarchy)
                        _samples.Remove(sprite);
                    else
                        Trail(sprite, time.Mask, now);
                }
            }

            ForgetDestroyed();
        }

        void Trail(SpriteRenderer sprite, MaskType mask, float now)
        {
            Transform target = sprite.transform;
            float angle = target.eulerAngles.z;
            if (!_samples.TryGetValue(sprite, out Sample last))
            {
                _samples[sprite] = new Sample { Position = target.position, Angle = angle, Time = now };
                return;
            }

            bool moved = (target.position - last.Position).sqrMagnitude >= minMove * minMove
                         || Mathf.Abs(Mathf.DeltaAngle(angle, last.Angle)) >= minTurn;
            if (now - last.Time < interval || !moved)
                return;

            Color color = ColorOf(mask);
            color.a = alpha;
            _ghosts.Spawn(sprite, color, lifetime, orderOffset: -1);
            _samples[sprite] = new Sample { Position = target.position, Angle = angle, Time = now };
        }

        Parts PartsOf(MaskTimeResponder time)
        {
            if (!_parts.TryGetValue(time, out Parts parts))
            {
                parts = new Parts
                {
                    Room = time.GetComponentInParent<Room>(true),
                    Sprites = time.GetComponentsInChildren<SpriteRenderer>(true),
                };
                _parts[time] = parts;
            }

            return parts;
        }

        void ForgetDestroyed()
        {
            _stale.Clear();
            foreach (SpriteRenderer sprite in _samples.Keys)
            {
                if (sprite == null)
                    _stale.Add(sprite);
            }

            foreach (SpriteRenderer sprite in _stale)
                _samples.Remove(sprite);
        }

        Color ColorOf(MaskType mask) =>
            maskManager != null && maskManager.TryGetDefinition(mask, out MaskDefinition definition) ? definition.Color : Color.white;
    }
}
