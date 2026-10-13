using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Small and fast. Moves in quick darting hops with short pauses, so it's hard
    /// to keep in the beam - and when it does get caught it tries to bolt.
    /// </summary>
    public class Skitter : Enemy
    {
        const float DashSeconds = 0.45f;
        const float PauseSeconds = 0.3f;
        const float HopHeight = 0.18f;

        float cycle;

        protected override string Configure()
        {
            MaxHealth = 0.45f;
            Speed = 3.3f;
            LitSpeedFactor = 0.6f;   // panics in the light instead of slowing right down
            Points = 15;
            Radius = 0.35f;
            Height = 0.55f;
            AnimationFps = 16f;
            return "skitter";
        }

        protected override void OnSpawned()
        {
            cycle = Random.value * (DashSeconds + PauseSeconds);
            Game.Audio.PlayAt(Sfx.SkitterChitter, Position, 0.5f, 0.15f);
        }

        protected override void Advance(float dt, float speedScale)
        {
            cycle = (cycle + dt) % (DashSeconds + PauseSeconds);
            bool dashing = cycle < DashSeconds;
            float hop = 0f;
            if (dashing)
            {
                Position.x -= Side * Speed * speedScale * dt;
                hop = Mathf.Sin(cycle / DashSeconds * Mathf.PI) * HopHeight;
            }
            Position.y = Layout.WalkY(Position.x) + hop;
        }
    }
}
