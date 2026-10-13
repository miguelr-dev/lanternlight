using System;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// A clickable card made of sprites (used for the dawn blessing choices).
    /// Highlights under the mouse and fires its action on click or on its number key.
    /// </summary>
    public class UiButton : MonoBehaviour
    {
        static readonly Color Fill = new Color(0.06f, 0.05f, 0.14f, 0.92f);
        static readonly Color FillHover = new Color(0.16f, 0.12f, 0.26f, 0.95f);
        static readonly Color Border = new Color(0.55f, 0.5f, 0.7f);
        static readonly Color BorderHover = new Color(1f, 0.82f, 0.4f);
        const float BorderWidth = 1f / Layout.PixelsPerUnit;

        Vector2 size;
        SpriteRenderer fill;
        SpriteRenderer[] edges;
        Action onClick;
        Func<bool> isEnabled;
        bool hovered;

        public static UiButton Create(Transform parent, Vector2 centre, Vector2 size, Action onClick, Func<bool> isEnabled)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var button = go.AddComponent<UiButton>();
            button.size = size;
            button.onClick = onClick;
            button.isEnabled = isEnabled;
            button.Build();
            return button;
        }

        void Build()
        {
            fill = Art.CreateRect("Fill", transform, Vector2.zero, size, Fill, SortOrder.Overlay - 2);
            float hx = size.x / 2f, hy = size.y / 2f;
            edges = new[]
            {
                Art.CreateRect("Top", transform, new Vector2(0f, hy), new Vector2(size.x, BorderWidth), Border, SortOrder.Overlay),
                Art.CreateRect("Bottom", transform, new Vector2(0f, -hy), new Vector2(size.x, BorderWidth), Border, SortOrder.Overlay),
                Art.CreateRect("Left", transform, new Vector2(-hx, 0f), new Vector2(BorderWidth, size.y), Border, SortOrder.Overlay),
                Art.CreateRect("Right", transform, new Vector2(hx, 0f), new Vector2(BorderWidth, size.y), Border, SortOrder.Overlay),
            };
        }

        /// <summary>Activates the button as if clicked (used for keyboard shortcuts).</summary>
        public void Press()
        {
            if (isEnabled())
            {
                onClick();
            }
        }

        void Update()
        {
            Vector2 mouse = GameInput.MouseWorld(GameManager.Instance.Cam);
            Vector2 local = mouse - (Vector2)transform.position;
            hovered = Mathf.Abs(local.x) <= size.x / 2f && Mathf.Abs(local.y) <= size.y / 2f;

            fill.color = hovered ? FillHover : Fill;
            foreach (SpriteRenderer edge in edges)
            {
                edge.color = hovered ? BorderHover : Border;
            }
            // Gentle lift on hover.
            float scale = hovered ? 1.04f : 1f;
            transform.localScale = Vector3.one * Mathf.MoveTowards(transform.localScale.x, scale, Time.unscaledDeltaTime * 0.6f);

            if (hovered && GameInput.ClickPressed)
            {
                Press();
            }
        }
    }
}
