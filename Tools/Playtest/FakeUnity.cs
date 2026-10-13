// A tiny behavioural stand-in for the parts of Unity the game uses, so the real game
// scripts can be run headless: GameObjects, components with Awake/Update/LateUpdate/OnDestroy,
// a transform hierarchy, real math, scripted input, PNG-backed textures and a render dump.
#pragma warning disable CS0660, CS0661
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    public static class Engine
    {
        public static string ResourcesRoot;
        public static readonly List<GameObject> Objects = new List<GameObject>();
        public static readonly List<string> Errors = new List<string>();
        public static readonly List<string> Sounds = new List<string>();
        static readonly List<Object> destroyQueue = new List<Object>();
        static readonly Dictionary<(Type, string), MethodInfo> methods = new Dictionary<(Type, string), MethodInfo>();
        public static int CreationCounter;

        public static MethodInfo Find(Type t, string name)
        {
            if (methods.TryGetValue((t, name), out var m)) return m;
            for (Type cur = t; cur != null && cur != typeof(MonoBehaviour); cur = cur.BaseType)
            {
                m = cur.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null) break;
            }
            methods[(t, name)] = m;
            return m;
        }

        public static void Call(Component c, string name)
        {
            var m = Find(c.GetType(), name);
            if (m == null) return;
            try { m.Invoke(c, null); }
            catch (TargetInvocationException e)
            {
                Errors.Add($"EXCEPTION in {c.GetType().Name}.{name} frame {Time.frameCount}: {e.InnerException}");
            }
        }

        public static void QueueDestroy(Object o) { if (!destroyQueue.Contains(o)) destroyQueue.Add(o); }

        public static void Step(float dt)
        {
            Time.unscaledDeltaTime = dt;
            Time.deltaTime = dt * Time.timeScale;
            Time.time += Time.deltaTime;
            Time.unscaledTime += dt;
            Time.frameCount++;
            foreach (var mb in Live().ToList()) if (mb.IsLive) Call(mb, "Update");
            foreach (var mb in Live().ToList()) if (mb.IsLive) Call(mb, "LateUpdate");
            FlushDestroy();
            Input.EndFrame();
        }

        static IEnumerable<MonoBehaviour> Live()
        {
            foreach (var go in Objects.ToList())
                if (!go.Destroyed && go.activeInHierarchy)
                    foreach (var c in go.Components.ToList())
                        if (c is MonoBehaviour mb && mb.enabled) yield return mb;
        }

        static void FlushDestroy()
        {
            var queue = destroyQueue.ToList();
            destroyQueue.Clear();
            foreach (var o in queue)
            {
                if (o is GameObject go) DestroyRecursive(go);
            }
        }

        static void DestroyRecursive(GameObject go)
        {
            if (go.Destroyed) return;
            foreach (var child in go.transform.Children.ToList()) DestroyRecursive(child.gameObject);
            foreach (var c in go.Components) { if (c is MonoBehaviour) Call(c, "OnDestroy"); c.Destroyed = true; }
            go.Destroyed = true;
            go.transform.parent?.Children.Remove(go.transform);
            Objects.Remove(go);
        }

        public static IEnumerable<SpriteRenderer> Renderers()
        {
            foreach (var go in Objects)
                if (!go.Destroyed && go.activeInHierarchy)
                    foreach (var c in go.Components)
                        if (c is SpriteRenderer sr && sr.enabled && sr.sprite != null) yield return sr;
        }
    }

    public class Object
    {
        internal bool Destroyed;
        public string name { get; set; }
        public static void Destroy(Object obj) { if (obj is object) Engine.QueueDestroy(obj); }
        static bool IsNull(Object o) => o is null || o.Destroyed;
        public static bool operator ==(Object a, Object b) => IsNull(a) && IsNull(b) || (!IsNull(a) && !IsNull(b) && ReferenceEquals(a, b));
        public static bool operator !=(Object a, Object b) => !(a == b);
        public static implicit operator bool(Object o) => !IsNull(o);
    }

    public class Component : Object
    {
        internal GameObject go;
        public Transform transform => go.transform;
        public GameObject gameObject => go;
        public T GetComponent<T>() => go.GetComponent<T>();
        public string tag { get => go.tag; set => go.tag = value; }
    }
    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
        internal bool IsLive => !Destroyed && !go.Destroyed && go.activeInHierarchy && enabled;
    }
    public class MonoBehaviour : Behaviour { }

    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new List<Component>();
        internal readonly int Created = Engine.CreationCounter++;
        bool active = true;
        public GameObject() : this("GameObject") { }
        public GameObject(string name)
        {
            this.name = name;
            var t = new Transform { go = this };
            Components.Add(t);
            Engine.Objects.Add(this);
        }
        public Transform transform => (Transform)Components[0];
        public string tag { get; set; } = "Untagged";
        public T AddComponent<T>() where T : Component
        {
            var c = (T)Activator.CreateInstance(typeof(T), true);
            c.go = this;
            Components.Add(c);
            if (c is MonoBehaviour) Engine.Call(c, "Awake");
            return c;
        }
        public T GetComponent<T>() => Components.OfType<T>().FirstOrDefault();
        public void SetActive(bool value) => active = value;
        public bool activeSelf => active;
        public bool activeInHierarchy => active && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
    }

    public class Transform : Component
    {
        internal readonly List<Transform> Children = new List<Transform>();
        Transform parentT;
        public Transform parent { get => parentT; set => SetParent(value, true); }
        public void SetParent(Transform p, bool worldPositionStays)
        {
            parentT?.Children.Remove(this);
            parentT = p;
            p?.Children.Add(this);
        }
        public Vector3 localPosition { get; set; }
        public Quaternion localRotation { get; set; } = Quaternion.identity;
        public Vector3 localScale { get; set; } = Vector3.one;
        public Vector3 position
        {
            get
            {
                if (parentT == null) return localPosition;
                var p = parentT.position;
                var s = parentT.lossyScale;
                float a = parentT.rotation.z * Mathf.Deg2Rad;
                float x = localPosition.x * s.x, y = localPosition.y * s.y;
                return new Vector3(p.x + x * Mathf.Cos(a) - y * Mathf.Sin(a), p.y + x * Mathf.Sin(a) + y * Mathf.Cos(a), p.z + localPosition.z);
            }
            set
            {
                localPosition = parentT == null ? value : value - parentT.position;
            }
        }
        public Quaternion rotation
        {
            get => parentT == null ? localRotation : new Quaternion { z = parentT.rotation.z + localRotation.z };
            set => localRotation = value;
        }
        public Vector3 lossyScale
        {
            get
            {
                if (parentT == null) return localScale;
                var p = parentT.lossyScale;
                return new Vector3(p.x * localScale.x, p.y * localScale.y, 1);
            }
        }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 up => new Vector2(0, 1);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : zero; } }
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static float Angle(Vector2 from, Vector2 to)
        {
            float d = (float)Math.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (d < 1e-15f) return 0;
            float dot = Mathf.Clamp((from.x * to.x + from.y * to.y) / d, -1, 1);
            return (float)Math.Acos(dot) * Mathf.Rad2Deg;
        }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public override string ToString() => $"({x:0.00}, {y:0.00})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => a * d;
    }

    /// <summary>Only rotation about Z is needed: z holds the angle in degrees.</summary>
    public struct Quaternion
    {
        public float z;
        public static Quaternion identity => new Quaternion();
        public static Quaternion Euler(float x, float y, float zDeg) => new Quaternion { z = zDeg };
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1) { }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public override string ToString() => $"(x:{x:0.00}, y:{y:0.00}, width:{width:0.00}, height:{height:0.00})";
    }
    public struct Bounds { public Vector3 extents; }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Abs(float v) => Math.Abs(v);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Exp(float p) => (float)Math.Exp(p);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Repeat(float t, float length) => Clamp(t - (float)Math.Floor(t / length) * length, 0, length);
        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }
        public static float LerpAngle(float a, float b, float t) => a + DeltaAngle(a, b) * Clamp01(t);
        public static float MoveTowards(float current, float target, float maxDelta) =>
            Math.Abs(target - current) <= maxDelta ? target : current + Math.Sign(target - current) * maxDelta;
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.ToEven);
        public static float PerlinNoise(float x, float y) => 0.5f + 0.5f * (float)Math.Sin(x * 1.7 + y * 3.1) * (float)Math.Cos(x * 0.6 - y);
    }

    public static class Random
    {
        static readonly System.Random rng = new System.Random(int.Parse(Environment.GetEnvironmentVariable("SIMSEED") ?? "12345"));
        public static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        public static int Range(int min, int max) => max <= min ? min : rng.Next(min, max);
        public static float value => (float)rng.NextDouble();
        public static Vector2 insideUnitCircle
        {
            get { double a = rng.NextDouble() * Math.PI * 2, r = Math.Sqrt(rng.NextDouble()); return new Vector2((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r)); }
        }
    }

    public static class Time
    {
        public static float deltaTime { get; internal set; }
        public static float unscaledDeltaTime { get; internal set; }
        public static float time { get; internal set; }
        public static float unscaledTime { get; internal set; }
        public static int frameCount { get; internal set; }
        public static float timeScale { get; set; } = 1f;
    }
    public static class Application { public static int targetFrameRate { get; set; } }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message) => Engine.Errors.Add("LogError: " + message);
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object
        {
            if (typeof(T) == typeof(Texture2D))
            {
                string file = Path.Combine(Engine.ResourcesRoot, path + ".png");
                if (!File.Exists(file)) return null;
                var bytes = File.ReadAllBytes(file);
                int w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                int h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
                return new Texture2D(w, h) { name = path } as T;
            }
            if (typeof(T) == typeof(AudioClip))
            {
                string file = Path.Combine(Engine.ResourcesRoot, path + ".wav");
                return File.Exists(file) ? new AudioClip { name = Path.GetFileName(path) } as T : null;
            }
            return null;
        }
    }

    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public class Texture : Object
    {
        public int width { get; protected set; }
        public int height { get; protected set; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }
    public sealed class Texture2D : Texture
    {
        public Texture2D(int w, int h) { width = w; height = h; }
        static Texture2D white;
        public static Texture2D whiteTexture => white ??= new Texture2D(4, 4) { name = "white" };
    }
    public enum SpriteMeshType { FullRect, Tight }
    public sealed class Sprite : Object
    {
        public Texture2D texture; public Rect rect; public Vector2 pivot; public float ppu;
        public Bounds bounds => new Bounds { extents = new Vector3(rect.width / ppu / 2, rect.height / ppu / 2, 0) };
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType) =>
            new Sprite { texture = texture, rect = rect, pivot = pivot, ppu = pixelsPerUnit };
    }
    public class Renderer : Component { public bool enabled { get; set; } = true; public int sortingOrder { get; set; } }
    public sealed class SpriteRenderer : Renderer
    {
        public Sprite sprite { get; set; }
        public Color color { get; set; } = Color.white;
        public bool flipX { get; set; }
    }
    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }
    public sealed class Camera : Behaviour
    {
        public static Camera main => Engine.Objects.Where(o => !o.Destroyed && o.tag == "MainCamera").Select(o => o.GetComponent<Camera>()).FirstOrDefault(c => c != null);
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; } = 5;
        public float aspect => ScreenW / ScreenH;
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public const float ScreenW = 1920, ScreenH = 1080;
        public Vector3 ScreenToWorldPoint(Vector3 s)
        {
            var p = transform.position;
            float aspect = ScreenW / ScreenH;
            return new Vector3(p.x + (s.x / ScreenW - 0.5f) * 2 * orthographicSize * aspect, p.y + (s.y / ScreenH - 0.5f) * 2 * orthographicSize, 0);
        }
        public Vector2 WorldToScreen(Vector2 w)
        {
            var p = transform.position;
            float aspect = ScreenW / ScreenH;
            return new Vector2(((w.x - p.x) / (2 * orthographicSize * aspect) + 0.5f) * ScreenW, ((w.y - p.y) / (2 * orthographicSize) + 0.5f) * ScreenH);
        }
    }
    public sealed class AudioListener : Behaviour { public static bool pause { get; set; } }
    public sealed class AudioClip : Object { }
    public sealed class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; } = 1;
        public float pitch { get; set; } = 1;
        public float panStereo { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; } = true;
        public void Play() { if (!loop && clip != null) Engine.Sounds.Add(clip.name); }
    }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        public static int GetInt(string key, int defaultValue) => ints.TryGetValue(key, out int v) ? v : defaultValue;
        public static void SetInt(string key, int value) => ints[key] = value;
        public static void Save() { }
    }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }

    public enum KeyCode { Space, Escape, P, Q, Return, KeypadEnter, Alpha1, Alpha2, Keypad1, Keypad2 }
    public static class Input
    {
        public static Vector3 mousePosition;
        public static bool mouseHeld;
        static bool mouseHeldLast;
        static readonly HashSet<KeyCode> downThisFrame = new HashSet<KeyCode>();
        public static void PressKey(KeyCode k) => downThisFrame.Add(k);
        internal static void EndFrame() { downThisFrame.Clear(); mouseHeldLast = mouseHeld; }
        public static bool GetMouseButton(int b) => b == 0 && mouseHeld;
        public static bool GetMouseButtonDown(int b) => b == 0 && mouseHeld && !mouseHeldLast;
        public static bool GetKeyDown(KeyCode k) => downThisFrame.Contains(k);
    }
}
