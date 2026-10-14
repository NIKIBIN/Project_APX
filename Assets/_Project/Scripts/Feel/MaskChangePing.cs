using System.Collections.Generic;
using APX.Core;
using APX.Masks;
using APX.Rooms;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace APX.Feel
{
    /// <summary>
    /// When the player changes masks, everything in the current room that the change affects pings: a silhouette in
    /// the colour of the mask it answers to pops out of it (also where something just vanished), and a chime plays,
    /// one note per mask. Shows exactly what each mask touches. Changes the game makes (scene start, respawn) stay
    /// quiet.
    /// </summary>
    [RequireComponent(typeof(SpriteGhosts))]
    public sealed class MaskChangePing : MonoBehaviour
    {
        // One note per mask worn after the change (G4, C5, E5, G5), so each mask has its own sound.
        static readonly Dictionary<MaskType, float> s_notes = new()
        {
            { MaskType.None, 392f },
            { MaskType.NiceAndSlow, 523.25f },
            { MaskType.SecondChannel, 659.25f },
            { MaskType.Differentiate, 783.99f },
        };

        [Tooltip("Defaults to the MaskManager in the scene (for each mask's colour).")]
        [SerializeField] MaskManager maskManager;
        [SerializeField, Min(0.01f)] float duration = 0.45f;
        [Tooltip("Silhouette size at the end of the ping, relative to the object.")]
        [SerializeField, Min(1f)] float endScale = 1.3f;
        [SerializeField, Range(0f, 1f)] float alpha = 0.85f;
        [Tooltip("Most silhouettes per change; tilemaps ping tile by tile.")]
        [SerializeField, Min(1)] int maxGhosts = 160;

        [Header("Chime")]
        [Tooltip("Played once per change that pings something. Empty = a soft synthesized bell, one note per mask.")]
        [SerializeField] AudioClip chime;
        [SerializeField, Range(0f, 1f)] float chimeVolume = 0.35f;

        readonly HashSet<GameObject> _pinged = new();
        readonly Dictionary<MaskType, AudioClip> _synthesizedChimes = new();
        SpriteGhosts _ghosts;
        AudioSource _audio;
        int _ghostCount;

        void Awake()
        {
            _ghosts = GetComponent<SpriteGhosts>();
            if (maskManager == null)
                maskManager = FindAnyObjectByType<MaskManager>();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        void OnEnable() => EventBus<MaskChangedEvent>.Subscribe(OnMaskChanged);

        void OnDisable() => EventBus<MaskChangedEvent>.Unsubscribe(OnMaskChanged);

        void OnDestroy()
        {
            foreach (AudioClip clip in _synthesizedChimes.Values)
                Destroy(clip);
        }

        void OnMaskChanged(MaskChangedEvent evt)
        {
            Room room = RoomManager.CurrentRoom;
            if (evt.IsReset || room == null)
                return;

            _pinged.Clear();
            _ghostCount = 0;
            foreach (MaskResponder responder in MaskResponder.Active)
            {
                if (responder.IsMetFor(evt.Previous) == responder.IsMetFor(evt.Current)
                    || responder.GetComponentInParent<Room>() != room
                    || !_pinged.Add(responder.gameObject))
                    continue;

                Color color = ColorOf(responder.Mask);
                color.a = alpha;
                Ping(responder, color);
            }

            if (_ghostCount > 0)
                PlayChime(evt.Current);
        }

        void Ping(MaskResponder responder, Color color)
        {
            // A visibility responder hides its renderers on purpose, so those ping even while hidden (just vanished).
            var visibility = responder as MaskVisibilityResponder;
            IReadOnlyList<Renderer> renderers = visibility != null
                ? visibility.Renderers
                : responder.GetComponentsInChildren<Renderer>();

            foreach (Renderer target in renderers)
            {
                if (_ghostCount >= maxGhosts)
                    return;
                if (target == null || (visibility == null && !target.enabled))
                    continue;

                switch (target)
                {
                    case SpriteRenderer sprite:
                        if (_ghosts.Spawn(sprite, color, duration, endScale, orderOffset: 1) != null)
                            _ghostCount++;
                        break;
                    case TilemapRenderer when target.TryGetComponent(out Tilemap tilemap):
                        PingTiles(tilemap, color);
                        break;
                }
            }
        }

        void PingTiles(Tilemap tilemap, Color color)
        {
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (_ghostCount >= maxGhosts)
                    return;
                if (tilemap.HasTile(cell) && _ghosts.SpawnTile(tilemap, cell, color, duration, endScale, orderOffset: 1) != null)
                    _ghostCount++;
            }
        }

        void PlayChime(MaskType worn)
        {
            AudioClip clip = chime;
            if (clip == null && !_synthesizedChimes.TryGetValue(worn, out clip))
            {
                clip = SynthesizeBell($"MaskChime_{worn}", s_notes[worn]);
                _synthesizedChimes[worn] = clip;
            }

            _audio.PlayOneShot(clip, chimeVolume);
        }

        Color ColorOf(MaskType mask) =>
            maskManager != null && maskManager.TryGetDefinition(mask, out MaskDefinition definition) ? definition.Color : Color.white;

        /// <summary>A soft bell: a few decaying harmonics of <paramref name="frequency"/>.</summary>
        static AudioClip SynthesizeBell(string clipName, float frequency)
        {
            const int sampleRate = 44100;
            const float length = 0.9f;
            int count = Mathf.CeilToInt(sampleRate * length);
            var samples = new float[count];
            float omega = 2f * Mathf.PI * frequency;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float attack = Mathf.Clamp01(t / 0.004f);
                float tone = Mathf.Sin(omega * t) * Mathf.Exp(-t / 0.35f)
                             + 0.45f * Mathf.Sin(2f * omega * t) * Mathf.Exp(-t / 0.18f)
                             + 0.2f * Mathf.Sin(3.01f * omega * t) * Mathf.Exp(-t / 0.09f);
                samples[i] = 0.3f * attack * tone;
            }

            AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
