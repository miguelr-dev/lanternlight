using UnityEngine;

namespace Lanternlight
{
    /// <summary>The basic shadow: slow, steady, often arrives in packs.</summary>
    public class Crawler : Enemy
    {
        protected override string Configure()
        {
            MaxHealth = 0.7f;
            Speed = 1.1f;
            Points = 10;
            Radius = 0.45f;
            Height = 0.8f;
            AnimationFps = 7f;
            return "crawler";
        }

        protected override void Advance(float dt, float speedScale)
        {
            Position.x -= Side * Speed * speedScale * dt;   // walk toward x = 0
            Position.y = Layout.WalkY(Position.x);
        }
    }
}
