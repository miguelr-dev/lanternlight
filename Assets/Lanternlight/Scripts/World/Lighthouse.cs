using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// The lighthouse: draws the tower, the glowing lantern and its halo, and takes
    /// damage when a shadow reaches the base. Its health is the number of flames
    /// in the run state; the lantern dims as flames go out.
    /// </summary>
    public class Lighthouse : MonoBehaviour
    {
        static readonly Color HitColor = new Color(1f, 0.35f, 0.35f);

        SpriteRenderer tower;
        SpriteRenderer lampGlow;
        SpriteRenderer halo;
        float hitFlash;      // 1 right after a hit, fades to 0
        float brightness;    // smoothed lantern brightness

        GameManager Game => GameManager.Instance;

        void Awake()
        {
            Vector2 basePos = new Vector2(0f, Layout.LighthouseBaseY);
            halo = Art.CreateSprite("Halo", transform, Art.Get("halo"), SortOrder.Halo, Layout.LampPosition);
            tower = Art.CreateSprite("Tower", transform, Art.Get("lighthouse", Art.BottomCentre), SortOrder.Lighthouse, basePos);
            lampGlow = Art.CreateSprite("Lantern glow", transform, Art.Get("lamp_glow", Art.BottomCentre), SortOrder.LampGlow, basePos);
        }

        /// <summary>A shadow reached the base (or a wisp reached the lamp).</summary>
        public void TakeHit(int damage, Vector2 where)
        {
            RunState run = Game.Run;
            if (run.Flames <= 0)
            {
                return;
            }
            run.Flames = Mathf.Max(0, run.Flames - damage);
            hitFlash = 1f;

            Game.Audio.PlayAt(Sfx.FlameLost, where, 1f, 0.03f);
            Game.Shake.Add(damage > 1 ? 0.75f : 0.55f);
            Game.Flash.Trigger(new Color(0.8f, 0.1f, 0.15f, 0.35f), 0.4f);
            Game.Particles.Emit(where, 14, ParticleStyle.ShadowPuff);
            Game.Particles.Emit(Layout.LampPosition, 10, ParticleStyle.Ember);
            Game.Popups.Show(where + Vector2.up * 1.2f, damage > 1 ? "-2 FLAMES" : "-1 FLAME", new Color(1f, 0.4f, 0.4f));
            Game.OnLighthouseDamaged();
        }

        void Update()
        {
            RunState run = Game.Run;
            float dt = Time.deltaTime;
            hitFlash = Mathf.Max(0f, hitFlash - dt * 2.5f);

            // Lantern brightness: depends on remaining flames, boosted while the beam shines.
            float health = run != null && run.MaxFlames > 0 ? (float)run.Flames / run.MaxFlames : 1f;
            float target = run != null && run.Flames == 0 ? 0.05f : 0.35f + 0.5f * health;
            if (Game.Beam.IsOn)
            {
                target += 0.25f;
            }
            // On the last flame the lantern gutters like a dying candle.
            if (run != null && run.Flames == 1 && Game.State == GameState.Playing)
            {
                target *= 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 0.5f);
            }
            brightness = Mathf.MoveTowards(brightness, Mathf.Clamp01(target), dt * 3f);

            lampGlow.color = new Color(1f, 1f, 1f, brightness);
            float pulse = 1f + 0.04f * Mathf.Sin(Time.time * 2.2f);
            halo.color = new Color(1f, 1f, 1f, brightness * 0.9f);
            halo.transform.localScale = Vector3.one * (0.7f + 0.5f * brightness) * pulse;
            tower.color = Color.Lerp(Color.white, HitColor, hitFlash);
        }
    }
}
