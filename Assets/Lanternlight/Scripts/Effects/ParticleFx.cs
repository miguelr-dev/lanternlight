using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    public enum ParticleStyle
    {
        Ember,       // orange sparks off a burning shadow
        ShadowPuff,  // dark smoke when a shadow dies or smothers a flame
        Splash,      // sea spray when something leaves the water
        Spark,       // bright white-gold burst (flare, barrel collected)
        Dawn,        // golden motes when shadows melt at sunrise
    }

    /// <summary>
    /// A small pooled particle system. Particles are plain data plus a SpriteRenderer,
    /// updated in one loop - cheap, and each style's look is easy to read and tweak below.
    /// </summary>
    public class ParticleFx : MonoBehaviour
    {
        const int PoolSize = 400;

        class Particle
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Gravity;
            public float Drag;
            public float StartSize;
            public float EndSize;
            public Color StartColor;
            public Color EndColor;
            public float SpriteSize;   // natural world size of the sprite at scale 1
            public bool Alive;
        }

        readonly List<Particle> pool = new List<Particle>();
        int cursor;

        void Awake()
        {
            Sprite pixel = Art.Get("pixel", Art.Centre, 1f);
            for (int i = 0; i < PoolSize; i++)
            {
                SpriteRenderer sr = Art.CreateSprite("Particle", transform, pixel, SortOrder.Particles);
                sr.enabled = false;
                pool.Add(new Particle { Renderer = sr });
            }
        }

        public void Emit(Vector2 position, int count, ParticleStyle style)
        {
            for (int i = 0; i < count; i++)
            {
                Particle p = NextParticle();
                p.Alive = true;
                p.Position = position;
                Configure(p, style);
                p.Life = p.MaxLife;
                p.Renderer.enabled = true;
                bool puff = style == ParticleStyle.ShadowPuff;
                p.Renderer.sprite = puff ? Art.Get("puff") : Art.Get("pixel", Art.Centre, 1f);
                p.SpriteSize = puff ? 0.5f : 1f;    // puff.png is 8 px = half a unit; pixel.png is 1 unit at 1 PPU
                UpdateVisual(p, 0f);
            }
        }

        /// <summary>Per-style recipe: direction, speed, lifetime, size and colour over life.</summary>
        static void Configure(Particle p, ParticleStyle style)
        {
            float px = 1f / Layout.PixelsPerUnit;
            switch (style)
            {
                case ParticleStyle.Ember:
                    p.Velocity = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.6f, 2.2f));
                    p.MaxLife = Random.Range(0.35f, 0.8f);
                    p.Gravity = -0.5f;
                    p.Drag = 1.5f;
                    p.StartSize = px * Random.Range(1f, 2f);
                    p.EndSize = px;
                    p.StartColor = new Color(1f, Random.Range(0.75f, 0.95f), 0.4f, 1f);
                    p.EndColor = new Color(1f, 0.3f, 0.1f, 0f);
                    break;
                case ParticleStyle.ShadowPuff:
                    p.Velocity = Random.insideUnitCircle * 1.2f + Vector2.up * 0.6f;
                    p.MaxLife = Random.Range(0.5f, 0.9f);
                    p.Gravity = 0f;
                    p.Drag = 2.5f;
                    p.StartSize = 0.6f;
                    p.EndSize = 1.4f;
                    p.StartColor = new Color(0.12f, 0.06f, 0.18f, 0.85f);
                    p.EndColor = new Color(0.05f, 0.02f, 0.08f, 0f);
                    break;
                case ParticleStyle.Splash:
                    p.Velocity = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(1.5f, 3.5f));
                    p.MaxLife = Random.Range(0.4f, 0.7f);
                    p.Gravity = -9f;
                    p.Drag = 0.2f;
                    p.StartSize = px * Random.Range(1f, 2f);
                    p.EndSize = px;
                    p.StartColor = new Color(0.7f, 0.85f, 1f, 1f);
                    p.EndColor = new Color(0.35f, 0.55f, 0.9f, 0f);
                    break;
                case ParticleStyle.Spark:
                    p.Velocity = Random.insideUnitCircle.normalized * Random.Range(2f, 6f);
                    p.MaxLife = Random.Range(0.3f, 0.7f);
                    p.Gravity = -2f;
                    p.Drag = 2f;
                    p.StartSize = px * 2f;
                    p.EndSize = px;
                    p.StartColor = new Color(1f, 1f, 0.85f, 1f);
                    p.EndColor = new Color(1f, 0.7f, 0.2f, 0f);
                    break;
                case ParticleStyle.Dawn:
                    p.Velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 1.4f));
                    p.MaxLife = Random.Range(0.8f, 1.5f);
                    p.Gravity = 0f;
                    p.Drag = 0.5f;
                    p.StartSize = px * 2f;
                    p.EndSize = px;
                    p.StartColor = new Color(1f, 0.85f, 0.5f, 1f);
                    p.EndColor = new Color(1f, 0.6f, 0.4f, 0f);
                    break;
            }
        }

        Particle NextParticle()
        {
            // Round-robin through the pool; if all are busy the oldest gets recycled.
            Particle p = pool[cursor];
            cursor = (cursor + 1) % pool.Count;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (Particle p in pool)
            {
                if (!p.Alive)
                {
                    continue;
                }
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Alive = false;
                    p.Renderer.enabled = false;
                    continue;
                }
                p.Velocity.y += p.Gravity * dt;
                p.Velocity *= 1f / (1f + p.Drag * dt);
                p.Position += p.Velocity * dt;
                UpdateVisual(p, 1f - p.Life / p.MaxLife);
            }
        }

        static void UpdateVisual(Particle p, float t)
        {
            float size = Mathf.Lerp(p.StartSize, p.EndSize, t);   // world-space diameter
            p.Renderer.transform.localPosition = p.Position;
            p.Renderer.transform.localScale = Vector3.one * (size / p.SpriteSize);
            p.Renderer.color = Color.Lerp(p.StartColor, p.EndColor, t);
        }
    }
}
