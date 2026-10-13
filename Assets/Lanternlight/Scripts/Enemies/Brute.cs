using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// A hulking shadow that soaks up a long burn and barely slows in the light.
    /// If it reaches the lighthouse it puts out two flames, so it can't be ignored.
    /// </summary>
    public class Brute : Enemy
    {
        float stepPhase;

        protected override string Configure()
        {
            MaxHealth = 3f;
            Speed = 0.55f;
            LitSpeedFactor = 0.75f;
            Points = 40;
            Damage = 2;
            Radius = 0.8f;
            Height = 1.5f;
            AnimationFps = 4f;
            EmergeSeconds = 1.3f;
            return "brute";
        }

        protected override void OnSpawned()
        {
            Game.Audio.PlayAt(Sfx.BruteRoar, Position, 0.8f, 0.05f);
            Game.Shake.Add(0.2f);
        }

        protected override void Advance(float dt, float speedScale)
        {
            // Lurching gait: surges forward on each heavy step.
            stepPhase += dt * AnimationFps * Mathf.PI * 0.5f;
            float lurch = 0.6f + 0.8f * Mathf.Abs(Mathf.Sin(stepPhase));
            Position.x -= Side * Speed * speedScale * lurch * dt;
            Position.y = Layout.WalkY(Position.x);
        }
    }
}
