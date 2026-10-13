using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// In-game heads-up display, drawn with sprites: oil gauge and flares (top left),
    /// the night's progress toward dawn (top centre), flames and score (top right).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        const float TopY = 5.05f;
        const float TextScale = 0.5f;          // half-size font pixels keep the HUD compact
        const float OilBarPixels = 52f;
        const float DawnBarPixels = 102f;
        static readonly Color OilColor = new Color(1f, 0.72f, 0.25f);
        static readonly Color OilLowColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color NightColor = new Color(0.45f, 0.5f, 0.95f);
        static readonly Color DawnColor = new Color(1f, 0.75f, 0.4f);

        Transform root;
        Transform oilGauge;
        SpriteRenderer oilFill;
        SpriteRenderer dawnFill;
        SpriteRenderer sunIcon;
        SpriteText nightLabel;
        SpriteText scoreLabel;
        SpriteText streakLabel;
        readonly List<SpriteRenderer> flames = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> flares = new List<SpriteRenderer>();
        float oilGaugeShake;
        float shownScore;

        GameManager Game => GameManager.Instance;

        void Awake()
        {
            root = new GameObject("HUD").transform;
            root.SetParent(transform, false);

            // Oil gauge (grouped so it can shake as one piece).
            oilGauge = new GameObject("Oil gauge").transform;
            oilGauge.SetParent(root, false);
            const float gaugeX = -7.25f;
            Art.CreateSprite("Oil icon", oilGauge, Art.Get("oil_icon"), SortOrder.Hud, new Vector2(-9.4f, TopY));
            Art.CreateSprite("Oil frame", oilGauge, Art.Get("bar_frame"), SortOrder.Hud, new Vector2(gaugeX, TopY));
            oilFill = Art.CreateSprite("Oil fill", oilGauge, Art.Get("pixel", Art.LeftCentre, 1f), SortOrder.Hud + 1,
                                       new Vector2(gaugeX - OilBarPixels / 2f / Layout.PixelsPerUnit, TopY));
            oilFill.color = OilColor;

            // Dawn progress bar between a moon and a sun.
            Art.CreateSprite("Dawn frame", root, Art.Get("dawn_frame"), SortOrder.Hud, new Vector2(0f, TopY));
            dawnFill = Art.CreateSprite("Dawn fill", root, Art.Get("pixel", Art.LeftCentre, 1f), SortOrder.Hud + 1,
                                        new Vector2(-DawnBarPixels / 2f / Layout.PixelsPerUnit, TopY));
            Art.CreateSprite("Moon icon", root, Art.Get("moon_icon"), SortOrder.Hud, new Vector2(-3.7f, TopY));
            sunIcon = Art.CreateSprite("Sun icon", root, Art.Get("sun_icon"), SortOrder.Hud, new Vector2(3.75f, TopY));
            nightLabel = SpriteText.Create(root, "", new Vector2(0f, TopY - 0.45f), TextScale, TextAlign.Centre, SortOrder.Hud);

            scoreLabel = SpriteText.Create(root, "", new Vector2(9.75f, TopY - 0.6f), TextScale, TextAlign.Right, SortOrder.Hud);
            streakLabel = SpriteText.Create(root, "", new Vector2(9.75f, TopY - 0.95f), TextScale, TextAlign.Right, SortOrder.Hud);
            streakLabel.SetColor(new Color(1f, 0.85f, 0.4f));
        }

        /// <summary>Shakes and reddens the oil gauge (pressing the beam with an empty tank).</summary>
        public void FlashOilGauge()
        {
            oilGaugeShake = 0.4f;
        }

        public void SetVisible(bool visible)
        {
            root.gameObject.SetActive(visible);
        }

        void Update()
        {
            RunState run = Game.Run;
            if (run == null || !root.gameObject.activeSelf)
            {
                return;
            }
            float dt = Time.unscaledDeltaTime;
            UpdateOil(dt);
            UpdateDawn();
            UpdateFlames(run);
            UpdateFlares();

            // Score counts up smoothly instead of jumping.
            shownScore = Mathf.MoveTowards(shownScore, run.Score, Mathf.Max(60f, Mathf.Abs(run.Score - shownScore) * 6f) * dt);
            scoreLabel.SetText("SCORE " + Mathf.RoundToInt(shownScore));

            int streak = Game.Streak;
            streakLabel.SetText(streak >= 2 ? $"STREAK X{Game.StreakMultiplier:0.##}" : "");
        }

        void UpdateOil(float dt)
        {
            float fraction = Game.Beam.OilFraction;
            oilFill.transform.localScale = new Vector3(OilBarPixels * fraction / Layout.PixelsPerUnit, 4f / Layout.PixelsPerUnit, 1f);
            bool low = fraction < Tuning.LowOilFraction || Game.Beam.IsDry;
            bool blink = Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f;
            oilFill.color = low && blink ? OilLowColor : OilColor;

            oilGaugeShake = Mathf.Max(0f, oilGaugeShake - dt);
            float shake = oilGaugeShake > 0f ? Mathf.Sin(Time.unscaledTime * 80f) * 0.08f : 0f;
            oilGauge.localPosition = new Vector3(shake, 0f, 0f);
        }

        void UpdateDawn()
        {
            float progress = Game.DawnProgress;
            dawnFill.transform.localScale = new Vector3(DawnBarPixels * progress / Layout.PixelsPerUnit, 3f / Layout.PixelsPerUnit, 1f);
            dawnFill.color = Color.Lerp(NightColor, DawnColor, progress);
            sunIcon.color = new Color(1f, 1f, 1f, 0.35f + 0.65f * progress);
            nightLabel.SetText($"NIGHT {Game.Run.NightIndex + 1}/{NightDefinition.All.Length}");
        }

        void UpdateFlames(RunState run)
        {
            // One icon per max flame, right to left; lit ones first.
            while (flames.Count < run.MaxFlames)
            {
                float x = 9.45f - flames.Count * 0.7f;
                flames.Add(Art.CreateSprite("Flame", root, Art.Get("flame_on"), SortOrder.Hud, new Vector2(x, TopY)));
            }
            for (int i = 0; i < flames.Count; i++)
            {
                bool exists = i < run.MaxFlames;
                flames[i].enabled = exists;
                // Icon 0 is the rightmost; flames go out from the left.
                bool lit = i < run.Flames;
                flames[i].sprite = Art.Get(lit ? "flame_on" : "flame_off");
                float flicker = lit ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 9f + i) : 1f;
                flames[i].transform.localScale = new Vector3(1f, flicker, 1f);
            }
        }

        void UpdateFlares()
        {
            int count = Game.FlaresLeft;
            while (flares.Count < count)
            {
                float x = -9.4f + flares.Count * 0.65f;
                flares.Add(Art.CreateSprite("Flare", root, Art.Get("flare_icon"), SortOrder.Hud, new Vector2(x, TopY - 0.7f)));
            }
            for (int i = 0; i < flares.Count; i++)
            {
                flares[i].enabled = i < count;
            }
        }
    }
}
