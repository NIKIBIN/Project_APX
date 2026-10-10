using APX.Combat;
using APX.Core;
using APX.Enemies;
using APX.Level;
using APX.Masks;
using APX.Player;
using UnityEngine;

namespace APX.Feel
{
    /// <summary>
    /// Central place for global game feel: turns gameplay events into screen shake, hit-stop / slow motion
    /// and particle bursts, so gameplay code only raises events. Tune everything here; disable the component
    /// to compare with and without.
    /// </summary>
    public sealed class GameFeel : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Defaults to a CameraShake on the main camera (added if missing).")]
        [SerializeField] CameraShake cameraShake;
        [SerializeField] ParticleBurst dust;
        [SerializeField] ParticleBurst sparks;
        [SerializeField] ParticleBurst deathBurst;
        [SerializeField] ParticleBurst doorDust;
        [SerializeField] ParticleBurst magic;
        [SerializeField] CeilingDebris ceilingDebris;

        [Header("Player Movement")]
        [SerializeField, Min(0)] int jumpDust = 6;
        [SerializeField, Min(0)] int skidDust = 5;
        [Tooltip("Dust for the hardest landing; softer landings get proportionally less.")]
        [SerializeField, Min(0)] int landingDust = 14;
        [Tooltip("Landings slower than this make no dust.")]
        [SerializeField, Min(0f)] float minLandingSpeed = 5f;
        [Tooltip("Landings at this speed or faster count as hard: full dust and a small shake.")]
        [SerializeField, Min(0.1f)] float hardLandingSpeed = 18f;
        [SerializeField, Range(0f, 1f)] float hardLandingTrauma = 0.2f;

        [Header("Attack Hits")]
        [SerializeField, Min(0f)] float hitStop = 0.06f;
        [SerializeField, Range(0f, 1f)] float hitTrauma = 0.15f;
        [SerializeField, Min(0)] int hitSparks = 10;

        [Header("Enemy Death")]
        [Tooltip("Extra freeze on the killing blow, on top of the hit-stop.")]
        [SerializeField, Min(0f)] float killStop = 0.04f;
        [SerializeField, Range(0f, 1f)] float enemyDeathTrauma = 0.3f;
        [SerializeField, Min(0)] int enemyDeathParticles = 16;

        [Header("Player Death")]
        [SerializeField, Min(0f)] float deathFreeze = 0.1f;
        [SerializeField, Range(0.05f, 1f)] float deathSlowMotionScale = 0.3f;
        [Tooltip("Slow motion after the freeze, in real seconds.")]
        [SerializeField, Min(0f)] float deathSlowMotion = 0.3f;
        [SerializeField, Range(0f, 1f)] float deathTrauma = 0.5f;
        [SerializeField, Min(0)] int deathParticles = 20;

        [Header("Doors")]
        [SerializeField, Min(0)] int rumbleDust = 6;
        [SerializeField, Min(0)] int slamDust = 18;
        [SerializeField, Range(0f, 1f)] float rumbleTrauma = 0.08f;
        [SerializeField, Range(0f, 1f)] float slamTrauma = 0.35f;

        [Header("Room Rumble")]
        [Tooltip("Steady shake while the room rumbles (debris falls from the ceiling meanwhile).")]
        [SerializeField, Range(0f, 1f)] float roomRumbleTrauma = 0.45f;
        [Tooltip("Seconds for the rumble to build up to full strength. It fades out on its own when it stops.")]
        [SerializeField, Min(0f)] float roomRumbleRampUp = 0.4f;

        [Header("Spike Traps")]
        [SerializeField, Min(0)] int spikeDust = 6;
        [SerializeField, Range(0f, 1f)] float spikeTrauma = 0.06f;

        [Header("Masks")]
        [Tooltip("Magical sparkles when a mask appears, swaps or comes off: half tinted with the mask colour, half white.")]
        [SerializeField, Min(0)] int maskBurstParticles = 24;
        [SerializeField, Range(0f, 1f)] float maskBurstTrauma = 0.08f;

        bool _isRumbling;
        float _rumbleTime;

        void Awake()
        {
            if (cameraShake == null && Camera.main != null)
            {
                if (!Camera.main.TryGetComponent(out cameraShake))
                    cameraShake = Camera.main.gameObject.AddComponent<CameraShake>();
            }
        }

        void OnEnable()
        {
            EventBus<PlayerJumpedEvent>.Subscribe(OnPlayerJumped);
            EventBus<PlayerLandedEvent>.Subscribe(OnPlayerLanded);
            EventBus<PlayerSkiddedEvent>.Subscribe(OnPlayerSkidded);
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<AttackHitEvent>.Subscribe(OnAttackHit);
            EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
            EventBus<DoorRumbledEvent>.Subscribe(OnDoorRumbled);
            EventBus<DoorSlammedEvent>.Subscribe(OnDoorSlammed);
            EventBus<SpikeTrapPoppedEvent>.Subscribe(OnSpikeTrapPopped);
            EventBus<RumbleStartedEvent>.Subscribe(OnRumbleStarted);
            EventBus<RumbleStoppedEvent>.Subscribe(OnRumbleStopped);
            EventBus<MaskBurstEvent>.Subscribe(OnMaskBurst);
        }

        void OnDisable()
        {
            EventBus<PlayerJumpedEvent>.Unsubscribe(OnPlayerJumped);
            EventBus<PlayerLandedEvent>.Unsubscribe(OnPlayerLanded);
            EventBus<PlayerSkiddedEvent>.Unsubscribe(OnPlayerSkidded);
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<AttackHitEvent>.Unsubscribe(OnAttackHit);
            EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
            EventBus<DoorRumbledEvent>.Unsubscribe(OnDoorRumbled);
            EventBus<DoorSlammedEvent>.Unsubscribe(OnDoorSlammed);
            EventBus<SpikeTrapPoppedEvent>.Unsubscribe(OnSpikeTrapPopped);
            EventBus<MaskBurstEvent>.Unsubscribe(OnMaskBurst);
            EventBus<RumbleStartedEvent>.Unsubscribe(OnRumbleStarted);
            EventBus<RumbleStoppedEvent>.Unsubscribe(OnRumbleStopped);
            OnRumbleStopped(default);
        }

        void Update()
        {
            if (!_isRumbling || cameraShake == null)
                return;

            _rumbleTime += Time.unscaledDeltaTime;
            float ramp = roomRumbleRampUp > 0f ? Mathf.Clamp01(_rumbleTime / roomRumbleRampUp) : 1f;
            cameraShake.HoldTrauma(roomRumbleTrauma * ramp);
        }

        void OnPlayerJumped(PlayerJumpedEvent evt) => Burst(dust, evt.Feet, jumpDust);

        void OnPlayerSkidded(PlayerSkiddedEvent evt) => Burst(dust, evt.Feet, skidDust);

        void OnPlayerLanded(PlayerLandedEvent evt)
        {
            float strength = Mathf.InverseLerp(minLandingSpeed, hardLandingSpeed, evt.ImpactSpeed);
            if (strength <= 0f)
                return;

            Burst(dust, evt.Feet, Mathf.Max(2, Mathf.RoundToInt(landingDust * strength)));
            if (evt.ImpactSpeed >= hardLandingSpeed)
                Shake(hardLandingTrauma);
        }

        void OnPlayerDied(PlayerDiedEvent evt)
        {
            // Freeze, then a slow-motion beat; the pulses overlap, so the slow-mo starts as the freeze ends.
            GameTime.Pulse(0f, deathFreeze);
            GameTime.Pulse(deathSlowMotionScale, deathFreeze + deathSlowMotion);
            Shake(deathTrauma);
            Burst(deathBurst, evt.Position, deathParticles);
        }

        void OnAttackHit(AttackHitEvent evt)
        {
            GameTime.Pulse(0f, hitStop);
            Shake(hitTrauma);
            Burst(sparks, evt.Point, hitSparks);
        }

        void OnEnemyDied(EnemyDiedEvent evt)
        {
            GameTime.Pulse(0f, hitStop + killStop);
            Shake(enemyDeathTrauma);
            Burst(deathBurst, evt.Position, enemyDeathParticles);
        }

        void OnDoorRumbled(DoorRumbledEvent evt)
        {
            Shake(rumbleTrauma);
            Burst(doorDust, evt.Contact, rumbleDust, evt.Width);
        }

        void OnDoorSlammed(DoorSlammedEvent evt)
        {
            Shake(slamTrauma);
            Burst(doorDust, evt.Contact, slamDust, evt.Width);
        }

        void OnRumbleStarted(RumbleStartedEvent evt)
        {
            _isRumbling = true;
            _rumbleTime = 0f;
            if (ceilingDebris != null)
                ceilingDebris.Begin(evt.Area);
        }

        void OnRumbleStopped(RumbleStoppedEvent evt)
        {
            _isRumbling = false;
            if (ceilingDebris != null)
                ceilingDebris.End();
        }

        void OnSpikeTrapPopped(SpikeTrapPoppedEvent evt)
        {
            Shake(spikeTrauma);
            Burst(doorDust, evt.Surface, spikeDust, evt.Width);
        }

        void OnMaskBurst(MaskBurstEvent evt)
        {
            Shake(maskBurstTrauma);
            if (magic == null)
                return;

            int tinted = maskBurstParticles / 2;
            magic.Play(evt.Position, tinted, evt.Color);
            magic.Play(evt.Position, maskBurstParticles - tinted, Color.white);
        }

        void Shake(float trauma)
        {
            if (cameraShake != null)
                cameraShake.AddTrauma(trauma);
        }

        static void Burst(ParticleBurst burst, Vector2 position, int count, float width = 0f)
        {
            if (burst != null)
                burst.Play(position, count, width);
        }
    }
}
