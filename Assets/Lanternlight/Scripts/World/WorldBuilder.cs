using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Builds the scenery (sky, stars, clouds, sea, island) and keeps it alive:
    /// clouds drift, stars twinkle, waves roll, and the sky warms toward dawn
    /// as the night runs out.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        const float WaveFrameRate = 4f;
        const int TwinkleStarCount = 14;

        class Cloud
        {
            public Transform Transform;
            public float Speed;
            public float HalfWidth;
        }

        class Star
        {
            public SpriteRenderer Renderer;
            public float Phase;
            public float Speed;
        }

        SpriteRenderer dawnSky;
        SpriteRenderer seaFront;
        Sprite[] seaFrames;
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<Star> stars = new List<Star>();
        float dawn;           // 0 = deep night, 1 = full sunrise
        float waveTimer;

        GameManager Game => GameManager.Instance;

        void Awake()
        {
            Art.CreateSprite("Sky (night)", transform, Art.Get("sky_night"), SortOrder.Sky);
            dawnSky = Art.CreateSprite("Sky (dawn)", transform, Art.Get("sky_dawn"), SortOrder.SkyDawn);
            dawnSky.color = new Color(1f, 1f, 1f, 0f);

            for (int i = 0; i < TwinkleStarCount; i++)
            {
                Vector2 pos = new Vector2(Random.Range(-9.5f, 9.5f), Random.Range(0.5f, 5.3f));
                if (Vector2.Distance(pos, Layout.LampPosition) < 2f)
                {
                    pos.x += 4f; // keep stars out of the lamp's halo
                }
                SpriteRenderer sr = Art.CreateSprite("Star", transform, Art.Get("star"), SortOrder.Stars, pos);
                stars.Add(new Star { Renderer = sr, Phase = Random.value * 10f, Speed = Random.Range(1.5f, 3.5f) });
            }

            for (int i = 0; i < 3; i++)
            {
                Sprite sprite = Art.Get("cloud_" + i);
                Vector2 pos = new Vector2(Random.Range(-9f, 9f), 2.6f + i * 0.9f);
                SpriteRenderer sr = Art.CreateSprite("Cloud", transform, sprite, SortOrder.Clouds, pos);
                clouds.Add(new Cloud
                {
                    Transform = sr.transform,
                    Speed = Random.Range(0.12f, 0.3f),
                    HalfWidth = sprite.bounds.extents.x,
                });
            }

            Art.CreateSprite("Sea (horizon)", transform, Art.Get("sea_back", Art.TopCentre), SortOrder.SeaBack,
                             new Vector2(0f, Layout.SeaBackTopY));
            Art.CreateSprite("Island", transform, Art.Get("island"), SortOrder.Island, new Vector2(0f, Layout.IslandCentreY));

            seaFrames = Art.Frames("sea", 4, Art.TopCentre);
            seaFront = Art.CreateSprite("Sea", transform, seaFrames[0], SortOrder.SeaFront, new Vector2(0f, Layout.SeaFrontTopY));
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Waves.
            waveTimer += dt;
            seaFront.sprite = seaFrames[Mathf.FloorToInt(waveTimer * WaveFrameRate) % seaFrames.Length];

            // Clouds drift left to right and wrap around.
            foreach (Cloud c in clouds)
            {
                Vector3 p = c.Transform.localPosition;
                p.x += c.Speed * dt;
                if (p.x - c.HalfWidth > Layout.HalfWidth)
                {
                    p.x = -Layout.HalfWidth - c.HalfWidth;
                }
                c.Transform.localPosition = p;
            }

            // Sky colour follows the night's progress toward dawn.
            dawn = Mathf.MoveTowards(dawn, TargetDawn(), dt * 0.5f);
            dawnSky.color = new Color(1f, 1f, 1f, dawn);

            // Stars twinkle, then fade out as the sky brightens.
            foreach (Star s in stars)
            {
                float twinkle = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * s.Speed + s.Phase));
                s.Renderer.color = new Color(1f, 1f, 1f, twinkle * (1f - dawn));
            }
        }

        float TargetDawn()
        {
            switch (Game.State)
            {
                case GameState.Dawn:
                case GameState.ChoosingUpgrade:
                case GameState.Victory:
                    return 1f;
                case GameState.Title:
                    return 0.1f;
                case GameState.Playing:
                case GameState.Paused:
                case GameState.GameOver:
                    // The last third of the night slowly warms the sky.
                    float t = Mathf.Clamp01((Game.DawnProgress - 0.65f) / 0.35f);
                    return 0.7f * t * t;
                default:
                    return 0f;
            }
        }
    }
}
