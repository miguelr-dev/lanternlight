using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Every full-screen message: title, night cards, pause, dawn, the blessing
    /// choice, game over and victory. Each Show method clears the last screen and
    /// lays out a new one with the pixel font.
    /// </summary>
    public class ScreenOverlay : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
        static readonly Color Pale = new Color(0.85f, 0.85f, 0.95f);
        static readonly Color Dim = new Color(0.6f, 0.58f, 0.75f);
        static readonly Color Danger = new Color(1f, 0.42f, 0.38f);
        static readonly Color PanelColor = new Color(0.03f, 0.02f, 0.08f, 0.65f);

        Transform panel;
        readonly List<SpriteText> blinkers = new List<SpriteText>();
        readonly List<UiButton> buttons = new List<UiButton>();
        float shownAt;

        /// <summary>False for a moment after a screen appears, so a held click doesn't skip it.</summary>
        public bool InputReady => Time.unscaledTime - shownAt > Tuning.InputLockSeconds;

        // Text sizes: 1 = one font pixel per art pixel. Body text uses half-size pixels to stay compact.
        const float Big = 1.5f;
        const float Medium = 1f;
        const float Small = 0.5f;

        // --- Screens --------------------------------------------------------
        public void ShowTitle(int bestScore)
        {
            Begin(0.15f);
            const float leftX = -5.4f, rightX = 5.4f, panelY = 1.3f;
            Panel(new Vector2(leftX, panelY), new Vector2(7.7f, 4.7f));
            Panel(new Vector2(rightX, panelY), new Vector2(7.7f, 4.7f));

            Text("LANTERNLIGHT", new Vector2(leftX, 3.0f), Big, Gold);
            Text("KEEP THE LIGHT BURNING UNTIL DAWN", new Vector2(leftX, 2.1f), Small, Pale);
            Text("FIVE NIGHTS. ONE BEAM. NOT ENOUGH OIL.", new Vector2(leftX, 1.65f), Small, Dim);
            blinkers.Add(Text("CLICK TO BEGIN", new Vector2(leftX, 0.5f), Medium, Gold));
            if (bestScore > 0)
            {
                Text("BEST SCORE " + bestScore, new Vector2(leftX, -0.3f), Small, Dim);
            }

            Text("HOW TO PLAY", new Vector2(rightX, 3.0f), Medium, Gold);
            string[] lines =
            {
                "MOUSE - AIM THE BEAM",
                "HOLD LEFT CLICK - SHINE IT",
                "SHINING BURNS OIL",
                "LIGHT A BARREL TO HAUL IN OIL",
                "SPACE - FLARE, BURNS EVERY SHADOW",
                "ESC - PAUSE",
            };
            for (int i = 0; i < lines.Length; i++)
            {
                Text(lines[i], new Vector2(rightX, 2.25f - i * 0.42f), Small, Pale);
            }
            Text("SHADOWS THAT REACH THE TOWER PUT\nOUT A FLAME. LAST FIVE NIGHTS.", new Vector2(rightX, -0.5f), Small, Dim);
        }

        public void ShowNightIntro(int nightNumber, int totalNights, string hint)
        {
            Begin(0f);
            Band(1f, 1.7f);
            Text(nightNumber == totalNights ? "FINAL NIGHT" : "NIGHT " + nightNumber, new Vector2(0f, 1.35f), Big, Gold);
            Text(hint, new Vector2(0f, 0.55f), Small, Pale);
        }

        public void ShowPause()
        {
            Begin(0.55f);
            Text("PAUSED", new Vector2(0f, 1.3f), Big, Gold);
            Text("ESC - RESUME", new Vector2(0f, 0.45f), Small, Pale);
            Text("Q - QUIT TO TITLE", new Vector2(0f, 0.05f), Small, Pale);
        }

        public void ShowDawn(int nightNumber, int bonus, bool flameRestored)
        {
            Begin(0f);
            Band(1f, 2f);
            Text("DAWN BREAKS", new Vector2(0f, 1.5f), Big, Gold);
            Text($"NIGHT {nightNumber} SURVIVED   +{bonus}", new Vector2(0f, 0.8f), Small, Pale);
            if (flameRestored)
            {
                Text("THE SUNRISE RELIGHTS A FLAME", new Vector2(0f, 0.4f), Small, Gold);
            }
        }

        public void ShowUpgradeChoice(List<UpgradeInfo> choices, Action<UpgradeInfo> onChosen)
        {
            Begin(0.45f);
            Text("CHOOSE A BLESSING", new Vector2(0f, 3f), Medium, Gold);
            Text("FOR THE NIGHT AHEAD", new Vector2(0f, 2.4f), Small, Pale);

            float[] xs = choices.Count == 1 ? new[] { 0f } : new[] { -4.1f, 4.1f };
            for (int i = 0; i < choices.Count; i++)
            {
                UpgradeInfo info = choices[i];
                Vector2 centre = new Vector2(xs[i], 0.6f);
                UiButton button = UiButton.Create(panel, centre, new Vector2(5.4f, 2.6f), () => onChosen(info), () => InputReady);
                buttons.Add(button);
                Transform card = button.transform;
                Text($"[{i + 1}]", new Vector2(-2.25f, 0.95f), Small, Dim, card);
                Text(info.Title, new Vector2(0f, 0.4f), Medium, Gold, card);
                Text(info.Line1, new Vector2(0f, -0.3f), Small, Pale, card);
                Text(info.Line2, new Vector2(0f, -0.65f), Small, Pale, card);
            }
            Text("CLICK A CARD OR PRESS 1 / 2", new Vector2(0f, -1.25f), Small, Dim);
        }

        public void ShowGameOver(int nightNumber, int score)
        {
            Begin(0.55f);
            Text("THE LIGHT GOES OUT", new Vector2(0f, 2.4f), Big, Danger);
            Text($"NIGHT {nightNumber}   SCORE {score}", new Vector2(0f, 1.6f), Small, Pale);
            blinkers.Add(Text($"CLICK TO RETRY NIGHT {nightNumber}", new Vector2(0f, 0.7f), Medium, Gold));
            Text("ESC - BACK TO TITLE", new Vector2(0f, 0.1f), Small, Dim);
        }

        public void ShowVictory(int score, int best, bool newBest)
        {
            Begin(0.35f);
            Band(1.55f, 3.9f);
            Text("DAWN, AT LAST", new Vector2(0f, 3f), Big, Gold);
            Text($"YOU KEPT THE LIGHT FOR {NightDefinition.All.Length} NIGHTS", new Vector2(0f, 2.3f), Small, Pale);
            Text("FINAL SCORE " + score, new Vector2(0f, 1.55f), Medium, Pale);
            if (newBest)
            {
                blinkers.Add(Text("NEW BEST!", new Vector2(0f, 0.95f), Small, Gold));
            }
            else
            {
                Text("BEST " + best, new Vector2(0f, 0.95f), Small, Dim);
            }
            blinkers.Add(Text("CLICK TO PLAY AGAIN", new Vector2(0f, 0.15f), Medium, Gold));
        }

        public void Hide()
        {
            Begin(0f);
        }

        /// <summary>Keyboard shortcut for the Nth button on screen (dawn choices).</summary>
        public void PressButton(int index)
        {
            if (index >= 0 && index < buttons.Count && InputReady)
            {
                buttons[index].Press();
            }
        }

        // --- Building blocks ------------------------------------------------
        void Begin(float dimAlpha)
        {
            if (panel != null)
            {
                Destroy(panel.gameObject);
            }
            blinkers.Clear();
            buttons.Clear();
            panel = new GameObject("Screen").transform;
            panel.SetParent(transform, false);
            shownAt = Time.unscaledTime;
            if (dimAlpha > 0f)
            {
                Art.CreateRect("Dim", panel, Vector2.zero, new Vector2(40f, 30f), new Color(0f, 0f, 0.02f, dimAlpha), SortOrder.Overlay - 5);
            }
        }

        void Panel(Vector2 centre, Vector2 size)
        {
            Art.CreateRect("Panel", panel, centre, size, PanelColor, SortOrder.Overlay - 4);
        }

        void Band(float centreY, float height)
        {
            Art.CreateRect("Band", panel, new Vector2(0f, centreY), new Vector2(40f, height), PanelColor, SortOrder.Overlay - 4);
        }

        SpriteText Text(string text, Vector2 position, float scale, Color color, Transform parent = null)
        {
            SpriteText st = SpriteText.Create(parent != null ? parent : panel, text, position, scale);
            st.SetColor(color);
            return st;
        }

        void Update()
        {
            bool on = Mathf.Repeat(Time.unscaledTime * 1.6f, 1f) < 0.7f;
            foreach (SpriteText st in blinkers)
            {
                st.SetAlpha(on ? 1f : 0.25f);
            }
        }
    }
}
