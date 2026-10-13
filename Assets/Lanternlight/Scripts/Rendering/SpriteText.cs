using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    public enum TextAlign
    {
        Left,
        Centre,
        Right,
    }

    /// <summary>
    /// Pixel-font text drawn with one SpriteRenderer per character, using the
    /// 5x7 bitmap font in Resources/Art/font.png. Avoids UI packages entirely and
    /// keeps every word in the same crisp pixel style as the rest of the art.
    /// </summary>
    public class SpriteText : MonoBehaviour
    {
        /// <summary>Glyph order in font.png. Must match CHARSET in Tools/generate_art.py.</summary>
        public const string Charset = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,!?:;-+/'\"()%<>*#=_&[]";
        const int CellWidth = 6;    // 5 px glyph + 1 px spacing
        const int CellHeight = 8;   // 7 px glyph + 1 px spacing
        const int LineHeight = 10;
        static readonly Color ShadowColor = new Color(0.02f, 0.01f, 0.06f, 0.85f);

        readonly List<SpriteRenderer> glyphs = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> shadows = new List<SpriteRenderer>();
        string text = "";
        float scale = 1f;
        TextAlign align;
        int sortingOrder;
        bool withShadow;
        Color color = Color.white;
        float alpha = 1f;

        public string Text => text;

        /// <summary>Width of a single line of <paramref name="length"/> characters at the given scale, in world units.</summary>
        public static float LineWidth(int length, float scale)
        {
            return (length * CellWidth - 1) * scale / Layout.PixelsPerUnit;
        }

        public static SpriteText Create(Transform parent, string text, Vector2 position, float scale = 1f,
                                        TextAlign align = TextAlign.Centre, int sortingOrder = SortOrder.Overlay,
                                        bool shadow = true)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var st = go.AddComponent<SpriteText>();
            st.scale = scale;
            st.align = align;
            st.sortingOrder = sortingOrder;
            st.withShadow = shadow;
            st.SetText(text);
            return st;
        }

        public void SetText(string value)
        {
            value = (value ?? "").ToUpperInvariant();
            if (value == text && glyphs.Count > 0)
            {
                return;
            }
            text = value;
            Rebuild();
        }

        public void SetColor(Color value)
        {
            color = value;
            ApplyColors();
        }

        public void SetAlpha(float value)
        {
            alpha = value;
            ApplyColors();
        }

        void Rebuild()
        {
            // Reuse renderers where possible; hide any left over.
            string[] lines = text.Split('\n');
            float px = scale / Layout.PixelsPerUnit;   // one font pixel in world units
            float totalHeight = (lines.Length - 1) * LineHeight * px;
            int used = 0;

            for (int line = 0; line < lines.Length; line++)
            {
                string s = lines[line];
                float width = LineWidth(s.Length, scale);
                float startX = align == TextAlign.Left ? 0f : align == TextAlign.Centre ? -width / 2f : -width;
                // Vertically centre the block of lines on the object's position.
                float y = totalHeight / 2f - line * LineHeight * px - 3.5f * px;

                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == ' ')
                    {
                        continue;
                    }
                    Sprite glyph = Glyph(s[i]);
                    Vector2 pos = new Vector2(startX + i * CellWidth * px, y - px);
                    Place(glyphs, used, glyph, pos, sortingOrder);
                    if (withShadow)
                    {
                        Place(shadows, used, glyph, pos + new Vector2(px, -px), sortingOrder - 1);
                    }
                    used++;
                }
            }
            for (int i = used; i < glyphs.Count; i++)
            {
                glyphs[i].enabled = false;
            }
            for (int i = used; i < shadows.Count; i++)
            {
                shadows[i].enabled = false;
            }
            ApplyColors();
        }

        void Place(List<SpriteRenderer> list, int index, Sprite glyph, Vector2 pos, int order)
        {
            if (index >= list.Count)
            {
                SpriteRenderer sr = Art.CreateSprite("Glyph", transform, glyph, order);
                list.Add(sr);
            }
            SpriteRenderer r = list[index];
            r.enabled = true;
            r.sprite = glyph;
            r.sortingOrder = order;
            r.transform.localPosition = pos;
            r.transform.localScale = new Vector3(scale, scale, 1f);
        }

        void ApplyColors()
        {
            Color c = color;
            c.a *= alpha;
            foreach (SpriteRenderer g in glyphs)
            {
                g.color = c;
            }
            Color sc = ShadowColor;
            sc.a *= alpha * color.a;
            foreach (SpriteRenderer s in shadows)
            {
                s.color = sc;
            }
        }

        static Sprite Glyph(char c)
        {
            int index = Charset.IndexOf(c);
            if (index < 0)
            {
                index = Charset.IndexOf('?');
            }
            return Art.Slice("font", new Rect(index * CellWidth, 0, CellWidth, CellHeight), Art.BottomLeft);
        }
    }
}
