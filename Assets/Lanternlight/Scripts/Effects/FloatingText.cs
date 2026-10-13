using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>Score popups ("+15", "STREAK X4") that float up and fade away.</summary>
    public class FloatingText : MonoBehaviour
    {
        const float RiseDistance = 0.9f;

        class Popup
        {
            public SpriteText Text;
            public Vector2 Start;
            public float Age;
            public float Lifetime;
            public bool Active;
        }

        readonly List<Popup> pool = new List<Popup>();

        /// <param name="lifetime">Seconds on screen; tutorial hints stay longer than score popups.</param>
        public void Show(Vector2 position, string message, Color color, float scale = 0.5f, float lifetime = 1f)
        {
            Popup popup = null;
            foreach (Popup p in pool)
            {
                if (!p.Active)
                {
                    popup = p;
                    break;
                }
            }
            if (popup == null)
            {
                popup = new Popup { Text = SpriteText.Create(transform, "", Vector2.zero, 1f, TextAlign.Centre, SortOrder.Popups) };
                pool.Add(popup);
            }
            // Keep popups on screen even when the shadow died at the very edge.
            position.x = Mathf.Clamp(position.x, -Layout.HalfWidth + 1f, Layout.HalfWidth - 1f);
            popup.Text.transform.localScale = Vector3.one * scale;
            popup.Text.SetText(message);
            popup.Text.SetColor(color);
            popup.Text.gameObject.SetActive(true);
            popup.Start = position;
            popup.Age = 0f;
            popup.Lifetime = lifetime;
            popup.Active = true;
        }

        void Update()
        {
            foreach (Popup p in pool)
            {
                if (!p.Active)
                {
                    continue;
                }
                p.Age += Time.deltaTime;
                float t = p.Age / p.Lifetime;
                if (t >= 1f)
                {
                    p.Active = false;
                    p.Text.gameObject.SetActive(false);
                    continue;
                }
                // Rise over the first second, then hold (long-lived hints don't drift away).
                float rise = Mathf.Clamp01(p.Age);
                float ease = 1f - (1f - rise) * (1f - rise);
                p.Text.transform.localPosition = p.Start + Vector2.up * RiseDistance * ease;
                p.Text.SetAlpha(t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
            }
        }

        public void HideAll()
        {
            foreach (Popup p in pool)
            {
                p.Active = false;
                p.Text.gameObject.SetActive(false);
            }
        }
    }
}
