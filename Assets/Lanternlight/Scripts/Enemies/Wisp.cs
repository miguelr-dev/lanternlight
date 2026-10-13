using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// A flying shadow that ignores the rocks and weaves straight for the lamp,
    /// forcing the player to aim the beam up into the sky.
    /// </summary>
    public class Wisp : Enemy
    {
        const float WeaveAmplitude = 0.9f;
        const float WeaveFrequency = 1.6f;
        const float HitDistance = 0.9f;

        Vector2 basePosition;   // position without the side-to-side weave
        float weavePhase;

        protected override string Configure()
        {
            Flying = true;
            MaxHealth = 0.7f;
            Speed = 1.3f;
            LitSpeedFactor = 0.35f;
            Points = 20;
            Radius = 0.4f;
            Height = 0.8f;
            AnimationFps = 9f;
            EmergeSeconds = 0.5f;
            return "wisp";
        }

        protected override void OnSpawned()
        {
            basePosition = Position;
            weavePhase = Random.value * Mathf.PI * 2f;
            Game.Audio.PlayAt(Sfx.WispWhisper, Position, 0.6f, 0.1f);
        }

        protected override void Advance(float dt, float speedScale)
        {
            Vector2 toLamp = Layout.LampPosition - basePosition;
            Vector2 heading = toLamp.normalized;
            basePosition += heading * Speed * speedScale * dt;

            // Weave perpendicular to the heading, tapering off near the lamp.
            weavePhase += dt * WeaveFrequency * Mathf.PI * 2f;
            Vector2 perpendicular = new Vector2(-heading.y, heading.x);
            float taper = Mathf.Clamp01(toLamp.magnitude / 4f);
            Position = basePosition + perpendicular * Mathf.Sin(weavePhase) * WeaveAmplitude * taper;
            Face(heading.x);
        }

        protected override bool HasReachedLighthouse()
        {
            return Vector2.Distance(Position, Layout.LampPosition) <= HitDistance;
        }
    }
}
