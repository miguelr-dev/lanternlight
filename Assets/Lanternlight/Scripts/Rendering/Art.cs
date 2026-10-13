using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Loads the pixel art from Resources/Art and builds sprites from it at runtime,
    /// so no prefab or sprite asset needs to be set up by hand in the editor.
    /// </summary>
    public static class Art
    {
        public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
        public static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
        public static readonly Vector2 LeftCentre = new Vector2(0f, 0.5f);
        public static readonly Vector2 BottomLeft = Vector2.zero;

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        /// <summary>Loads a texture once and forces crisp pixel-art filtering.</summary>
        public static Texture2D Texture(string name)
        {
            if (textures.TryGetValue(name, out Texture2D tex))
            {
                return tex;
            }
            tex = Resources.Load<Texture2D>("Art/" + name);
            if (tex == null)
            {
                Debug.LogError($"Lanternlight: missing art 'Resources/Art/{name}.png'");
                tex = Texture2D.whiteTexture;
            }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            textures[name] = tex;
            return tex;
        }

        /// <summary>A sprite covering a whole texture, with the given pivot and pixels-per-unit.</summary>
        public static Sprite Get(string name, Vector2 pivot, float pixelsPerUnit = Layout.PixelsPerUnit)
        {
            string key = $"{name}|{pivot.x}|{pivot.y}|{pixelsPerUnit}";
            if (sprites.TryGetValue(key, out Sprite sprite))
            {
                return sprite;
            }
            Texture2D tex = Texture(name);
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprites[key] = sprite;
            return sprite;
        }

        public static Sprite Get(string name)
        {
            return Get(name, Centre);
        }

        /// <summary>A numbered animation: prefix_0, prefix_1, ...</summary>
        public static Sprite[] Frames(string prefix, int count, Vector2 pivot)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Get($"{prefix}_{i}", pivot);
            }
            return frames;
        }

        /// <summary>A sub-rectangle of a texture, used for the font atlas.</summary>
        public static Sprite Slice(string name, Rect rect, Vector2 pivot)
        {
            string key = $"{name}|{rect}|{pivot.x}|{pivot.y}";
            if (!sprites.TryGetValue(key, out Sprite sprite))
            {
                sprite = Sprite.Create(Texture(name), rect, pivot, Layout.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[key] = sprite;
            }
            return sprite;
        }

        /// <summary>Creates a GameObject with a SpriteRenderer in one call.</summary>
        public static SpriteRenderer CreateSprite(string objectName, Transform parent, Sprite sprite, int sortingOrder,
                                                  Vector2 localPosition = default)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>A solid rectangle (a stretched white pixel) of size in world units.</summary>
        public static SpriteRenderer CreateRect(string objectName, Transform parent, Vector2 centre, Vector2 size,
                                                Color color, int sortingOrder)
        {
            SpriteRenderer sr = CreateSprite(objectName, parent, Get("pixel", Centre, 1f), sortingOrder, centre);
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.color = color;
            return sr;
        }
    }
}
