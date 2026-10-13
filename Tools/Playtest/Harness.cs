// Drives the real game scripts headless with scripted players and reports problems.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Lanternlight;
using UnityEngine;

public static class Harness
{
    const float Dt = 1f / 60f;
    static string outDir;
    static GameManager Game => GameManager.Instance;
    static readonly List<string> log = new List<string>();
    static GameState lastState = (GameState)(-1);
    static readonly HashSet<string> snapped = new HashSet<string>();

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Engine.ResourcesRoot = args[0];
        outDir = args[1];
        string scenario = args.Length > 2 ? args[2] : "good";
        if (args.Length > 3) { var k = args[3].Split(','); Reaction = float.Parse(k[0]); MouseSpeed = float.Parse(k[1]); Wobble = float.Parse(k[2]); }
        Directory.CreateDirectory(outDir);

        var cam = new GameObject("Main Camera") { tag = "MainCamera" };
        cam.AddComponent<Camera>();
        cam.AddComponent<AudioListener>();
        typeof(Bootstrap).GetMethod("StartGame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        switch (scenario)
        {
            case "good": RunGood(); break;
            case "idle": RunIdle(); break;
            case "pause": RunPause(); break;
        }

        Console.WriteLine(string.Join("\n", log));
        var soundCounts = Engine.Sounds.GroupBy(s => s).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}x{g.Count()}");
        Console.WriteLine("SOUNDS: " + string.Join(" ", soundCounts));
        Console.WriteLine($"ERRORS ({Engine.Errors.Count}):");
        foreach (var e in Engine.Errors.Distinct().Take(15)) Console.WriteLine("  " + e);
        Console.WriteLine($"Objects alive at end: {Engine.Objects.Count}  top: " + string.Join(", ",
            Engine.Objects.GroupBy(o => o.name).OrderByDescending(g => g.Count()).Take(6).Select(g => $"{g.Key}={g.Count()}")));
        return Engine.Errors.Count == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- scenarios
    static void RunGood()
    {
        float t = 0;
        int frames = 0;
        while (t < 900)
        {
            TrackState();
            switch (Game.State)
            {
                case GameState.Title:
                    Snap("title", 2.0f);
                    if (StateAge() > 2.2f) Click(); else Release();
                    break;
                case GameState.NightIntro:
                    Snap($"intro{Game.Run.NightIndex + 1}", 1.0f);
                    Release();
                    break;
                case GameState.Playing:
                    PlayWell();
                    if (Game.Enemies.Active.Count >= 4) Snap($"play{Game.Run.NightIndex + 1}", 0f);
                    if (Game.Run.NightIndex == 0 && Game.NightTime > 3.2f) Snap("hint", 0f);
                    break;
                case GameState.Dawn:
                    Snap("dawn", 1.5f);
                    Release();
                    break;
                case GameState.ChoosingUpgrade:
                    Snap("upgrade", 0.8f);
                    Release();
                    if (StateAge() > 1.0f) Input.PressKey(KeyCode.Alpha1);
                    break;
                case GameState.GameOver:
                    Snap("gameover", 1.0f);
                    if (StateAge() > 1.5f) Click(); else Release();
                    break;
                case GameState.Victory:
                    Snap("victory", 1.5f);
                    if (StateAge() > 2.5f) { log.Add($"VICTORY score {Game.Run.Score}"); return; }
                    break;
            }
            Engine.Step(Dt);
            t += Dt;
            frames++;
        }
        log.Add("Timed out");
    }

    static void RunIdle()
    {
        // Never touch anything: the lighthouse should fall, then retry, then quit to title.
        int retries = 0;
        for (float t = 0; t < 400; t += Dt)
        {
            TrackState();
            if (Game.State == GameState.Title)
            {
                if (retries >= 1 && StateAge() > 1f) { log.Add("Back at title after game over - OK"); return; }
                if (StateAge() > 1f) Click(); else Release();
            }
            else if (Game.State == GameState.GameOver)
            {
                if (StateAge() > 0.2f && StateAge() < 0.3f) Click(); // too early: must be ignored
                else if (StateAge() > 1.2f && retries == 0) { Click(); retries++; }
                else if (StateAge() > 1.2f && retries == 1) { Release(); Input.PressKey(KeyCode.Escape); }
                else Release();
            }
            else Release();
            Engine.Step(Dt);
        }
        log.Add("Idle scenario timed out");
    }

    static void RunPause()
    {
        bool paused = false, quit = false;
        for (float t = 0; t < 120; t += Dt)
        {
            TrackState();
            if (Game.State == GameState.Title)
            {
                if (quit) { log.Add("Quit to title from pause - OK"); return; }
                if (StateAge() > 1f) Click(); else Release();
            }
            else if (Game.State == GameState.Playing)
            {
                PlayWell();
                if (!paused && Game.NightTime > 10f) { Input.PressKey(KeyCode.Escape); paused = true; }
                else if (paused && Game.NightTime > 15f && !quit) { Input.PressKey(KeyCode.Escape); quit = true; }
            }
            else if (Game.State == GameState.Paused)
            {
                Snap("pause", 0.5f);
                if (StateAge() > 1f && !quit) { Input.PressKey(KeyCode.Escape); }
                if (StateAge() > 1f && quit) Input.PressKey(KeyCode.Q);
            }
            Engine.Step(Dt);
        }
        log.Add("Pause scenario timed out");
    }

    // ---------------------------------------------------------------- bot
    public static float Reaction = 0.3f;     // seconds between target decisions
    public static float MouseSpeed = 14f;    // world units per second the "hand" can move the mouse
    public static float Wobble = 0.35f;      // aim error in world units
    static float decisionTimer;
    static Vector2 desiredAim = new Vector2(0, 0);
    static bool desiredShine;
    static Vector2 mouseWorld = new Vector2(0, 0);

    static void PlayWell()
    {
        decisionTimer -= Dt;
        if (decisionTimer <= 0)
        {
            decisionTimer = Reaction;
            Decide();
        }
        Vector2 wobble = new Vector2(Mathf.Sin(Time.time * 3.1f), Mathf.Cos(Time.time * 2.3f)) * Wobble;
        Vector2 goal = desiredAim + wobble;
        Vector2 delta = goal - mouseWorld;
        float step = MouseSpeed * Dt;
        mouseWorld = delta.magnitude <= step ? goal : mouseWorld + delta.normalized * step;
        Vector2 s = Game.Cam.WorldToScreen(mouseWorld);
        Input.mousePosition = new Vector3(s.x, s.y, 0);
        Input.mouseHeld = desiredShine && Vector2.Distance(mouseWorld, goal) < 1.2f;
    }

    static void Decide()
    {
        Vector2? aim = null;
        bool shine = false;

        Enemy target = null;
        float best = float.MaxValue;
        foreach (var e in Game.Enemies.Active)
        {
            if (!e.IsAlive) continue;
            float d = e.Kind == EnemyKind.Wisp ? Vector2.Distance(e.Centre, Layout.LampPosition) : Mathf.Abs(e.Centre.x);
            if (e.Kind == EnemyKind.Brute) d -= 1.5f;
            if (d < best) { best = d; target = e; }
        }

        if (target != null && best < 7.5f)
        {
            aim = target.Centre;
            shine = Game.Beam.Contains(target.Centre, target.Radius);
        }
        else
        {
            Barrel barrel = Game.Barrels.Active.Where(b => b.CanBePulled && Vector2.Distance(b.Centre, Layout.LampPosition) < 10.5f)
                                             .OrderBy(b => Mathf.Abs(b.Centre.x)).FirstOrDefault();
            if (barrel != null && Game.Beam.OilFraction < 0.8f)
            {
                aim = barrel.Centre;
                shine = Game.Beam.Contains(barrel.Centre, barrel.Radius);
            }
            else if (target != null)
            {
                aim = target.Centre;
            }
        }

        if (aim.HasValue) desiredAim = aim.Value;
        desiredShine = shine || (aim.HasValue && target != null && best < 7.5f);

        int close = Game.Enemies.Active.Count(e => e.IsAlive && e.Kind != EnemyKind.Wisp && Mathf.Abs(e.Centre.x) < 2.6f);
        if (close >= 3 && Game.FlaresLeft > 0)
        {
            Input.PressKey(KeyCode.Space);
            log.Add($"  flare at night {Game.Run.NightIndex + 1} t={Game.NightTime:0.0}");
        }
    }

    // ---------------------------------------------------------------- helpers
    static float stateEntered;
    static float StateAge() => Time.unscaledTime - stateEntered;

    static int lastFlames = -1;
    static float minOil = 999, dryTime, onTime;
    static void TrackOil()
    {
        if (Game.State != GameState.Playing) return;
        minOil = Mathf.Min(minOil, Game.Beam.OilFraction);
        if (Game.Beam.IsDry) dryTime += Dt;
        if (Game.Beam.IsOn) onTime += Dt;
    }
    static void TrackFlames()
    {
        if (Game.Run == null) { lastFlames = -1; return; }
        if (lastFlames >= 0 && Game.Run.Flames < lastFlames && Game.State != GameState.NightIntro)
            log.Add($"  flame lost: night {Game.Run.NightIndex + 1} t={Game.NightTime:0.0} flames={Game.Run.Flames} oil={Game.Beam.Oil:0}");
        lastFlames = Game.Run.Flames;
    }

    static void TrackState()
    {
        TrackFlames();
        TrackOil();
        if (Game.State == lastState) return;
        if (lastState == GameState.Playing && Game.State != GameState.Paused)
        {
            log.Add($"  night stats: min oil {minOil * 100:0}%  dry {dryTime:0.0}s  beam on {onTime:0}s");
            minOil = 999; dryTime = 0; onTime = 0;
        }
        lastState = Game.State;
        stateEntered = Time.unscaledTime;
        string extra = Game.Run == null ? "" :
            $" night={Game.Run.NightIndex + 1} score={Game.Run.Score} flames={Game.Run.Flames}/{Game.Run.MaxFlames} oil={Game.Beam.Oil:0}/{Game.Beam.OilCapacity:0} flares={Game.FlaresLeft}";
        log.Add($"[{Time.unscaledTime,7:0.0}s] {Game.State}{extra}");
    }

    static void Click()
    {
        Input.mouseHeld = !Input.mouseHeld; // toggles: a press happens on the frame it goes down
    }

    static void Release() => Input.mouseHeld = false;

    static void Snap(string name, float afterSeconds)
    {
        if (snapped.Contains(name) || StateAge() < afterSeconds) return;
        snapped.Add(name);
        var sb = new StringBuilder();
        sb.Append("[");
        bool first = true;
        foreach (var sr in Engine.Renderers())
        {
            var t = sr.transform;
            var sp = sr.sprite;
            var p = t.position;
            var s = t.lossyScale;
            var c = sr.color;
            if (!first) sb.Append(",\n");
            first = false;
            sb.Append($"{{\"tex\":\"{sp.texture.name}\",\"rect\":[{sp.rect.x},{sp.rect.y},{sp.rect.width},{sp.rect.height}]," +
                      $"\"pivot\":[{sp.pivot.x},{sp.pivot.y}],\"ppu\":{sp.ppu},\"pos\":[{p.x},{p.y}],\"rot\":{t.rotation.z}," +
                      $"\"scale\":[{s.x},{s.y}],\"color\":[{c.r},{c.g},{c.b},{c.a}],\"flip\":{(sr.flipX ? "true" : "false")}," +
                      $"\"order\":{sr.sortingOrder},\"created\":{sr.gameObject.Created}}}");
        }
        sb.Append("]");
        var cp = Game.Cam.transform.position;
        File.WriteAllText(Path.Combine(outDir, name + ".json"), $"{{\"cam\":[{cp.x},{cp.y}],\"sprites\":{sb}}}");
    }
}
