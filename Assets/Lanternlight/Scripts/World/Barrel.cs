using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// An oil barrel drifting across the sea. Holding the beam on it fills a pull
    /// meter; when full, the barrel is hauled up into the lamp and refuels it.
    /// </summary>
    public class Barrel : MonoBehaviour
    {
        const float CollectFlightSeconds = 0.55f;
        static readonly Color PulledTint = new Color(1f, 0.92f, 0.55f);

        enum Phase { Floating, Flying }

        SpriteRenderer sprite;
        Phase phase = Phase.Floating;
        Vector2 position;
        Vector2 flightStart;
        float direction;          // +1 drifts right, -1 drifts left
        float pull;               // 0..1 progress toward being hauled in
        int lastPulledFrame = -10;
        float bobPhase;
        float flightTime;
        float sparkleTimer;

        public Vector2 Centre => position;
        public float Radius => 0.4f;
        public bool CanBePulled => phase == Phase.Floating;
        bool BeingPulled => Time.frameCount - lastPulledFrame <= 1;

        GameManager Game => GameManager.Instance;

        public void Init(float startX, float laneY, float driftDirection)
        {
            position = new Vector2(startX, laneY);
            direction = driftDirection;
            bobPhase = Random.value * 6f;
            sprite = Art.CreateSprite("Barrel sprite", transform, Art.Get("barrel"), SortOrder.Barrels);
            Apply();
        }

        /// <summary>Called by the beam every frame the barrel is lit.</summary>
        public void Pull(float dt)
        {
            if (phase != Phase.Floating)
            {
                return;
            }
            lastPulledFrame = Time.frameCount;
            pull += dt / Tuning.BarrelPullSeconds;
            if (pull >= 1f)
            {
                phase = Phase.Flying;
                flightStart = position;
                flightTime = 0f;
                Game.Audio.PlayAt(Sfx.UiConfirm, position, 0.5f);
                Game.Particles.Emit(position, 12, ParticleStyle.Splash);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (phase == Phase.Floating)
            {
                Float(dt);
            }
            else
            {
                FlyToLamp(dt);
            }
        }

        void Float(float dt)
        {
            // Drifting stops while the beam holds it - it's being reeled in.
            if (!BeingPulled)
            {
                position.x += direction * Tuning.BarrelDriftSpeed * dt;
                pull = Mathf.Max(0f, pull - Tuning.BarrelPullDecay * dt);
            }
            else
            {
                sparkleTimer -= dt;
                if (sparkleTimer <= 0f)
                {
                    sparkleTimer = 0.1f;
                    Game.Particles.Emit(position + Random.insideUnitCircle * 0.3f, 1, ParticleStyle.Ember);
                }
            }

            bobPhase += dt * 2.4f;
            if (Mathf.Abs(position.x) > Layout.BarrelSpawnX + 0.5f)
            {
                Game.Barrels.Remove(this); // drifted off the far side
                return;
            }
            Apply();
        }

        void FlyToLamp(float dt)
        {
            flightTime += dt;
            float t = Mathf.Clamp01(flightTime / CollectFlightSeconds);
            // A simple arc: straight line to the lamp plus a hump in the middle.
            Vector2 straight = Vector2.Lerp(flightStart, Layout.LampPosition, t * t);
            position = straight + Vector2.up * Mathf.Sin(t * Mathf.PI) * 1.5f;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);
            sprite.transform.localRotation = Quaternion.Euler(0f, 0f, t * 540f * -direction);
            Apply();
            if (t >= 1f)
            {
                Game.OnBarrelCollected(Layout.LampPosition);
                Game.Barrels.Remove(this);
            }
        }

        void Apply()
        {
            float bob = phase == Phase.Floating ? Mathf.Sin(bobPhase) * 0.06f : 0f;
            transform.localPosition = position + Vector2.up * bob;
            if (phase == Phase.Floating)
            {
                sprite.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(bobPhase * 0.7f) * 6f);
                // Tint and swell slightly as the beam reels it in.
                sprite.color = Color.Lerp(Color.white, PulledTint, pull);
                transform.localScale = Vector3.one * (1f + 0.15f * pull);
            }
        }
    }
}
