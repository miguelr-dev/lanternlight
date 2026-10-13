# Lanternlight

**You are the light. Keep it burning until dawn.**

A single-screen 2D survival game made in Unity. You are the spirit inside a lighthouse
lamp. Each night, shadow creatures crawl out of the sea from both sides, and your only
weapon is the lighthouse beam. The beam burns shadows away, but it runs on oil, and the
same beam is how you haul in oil barrels drifting past. Survive five nights.

## How to play

### The goal

Survive **five nights**. Each night lasts about a minute to a minute and a half. If at
least one of your lighthouse flames is still burning when dawn breaks, you survive that
night. Survive the fifth night and you win.

### Controls

| Input | Action |
| --- | --- |
| Mouse | Aim the beam |
| Hold left click | Shine the beam (uses oil) |
| Space | Flare: burns every shadow on screen (one per night) |
| Esc or P | Pause and resume |
| Q (while paused) | Quit to the title screen |
| 1 / 2, or click a card | Pick a blessing at dawn |
| Click or Enter | Start the game, retry a night, continue from menus |

### Your first night

1. Click to begin. A card shows the night number and a hint about what's coming.
2. Shadows climb out of the sea on the **left and right** and walk toward the lighthouse.
3. Point the mouse at a shadow and **hold left click**. The beam burns it. Keep it in
   the light until it disappears. Shadows also slow down while they're lit, which buys
   you time.
4. Watch your **oil**. The beam drains oil while it's on (a full tank lasts under 8
   seconds of nonstop light). Let go of the mouse button whenever nothing needs
   burning: the tank slowly refills while the beam rests.
5. When an **oil barrel** floats past on the water, shine the beam on it for about a
   second to haul it in. Each barrel refills about a third of the tank.
6. If a shadow reaches the tower, it puts out one of your **flames** (you start with
   three). Lose them all and the night is lost.
7. Hold on until the sky turns and **dawn breaks**. Dawn relights one flame.

### The shadows

| Shadow | First appears | What it does | How to beat it |
| --- | --- | --- | --- |
| **Crawler** | Night 1 | Slow walker, often arrives in packs | A short burn kills it |
| **Skitter** | Night 2 | Fast, darts forward in quick hops | Catch it early, it still moves fast in the light |
| **Brute** | Night 3 | Huge and slow, puts out **two** flames if it reaches you | Needs a long burn, start on it early |
| **Wisp** | Night 4 | Flies straight at the lamp through the air | Aim the beam **up**, not just along the ground |

From night 2 on, **surges** send shadows from both sides at once. Swing the beam back
and forth, and save your flare for these moments.

### Oil

- The oil gauge drains while the beam is on and slowly refills while it's off.
- If you run completely dry, the beam won't relight until a little oil comes back,
  so don't run the tank to zero.
- Barrels are your main oil supply. Grab them whenever it's safe to look away.

### Flares

Press **Space** to fire a flare. It burns every shadow on screen at once, though a
healthy Brute can survive it. You get one per night (two with the Second Flare
blessing), so save it for when several shadows are close to the tower. The game
reminds you with **"SPACE - FLARE!"** the first time you get crowded.

### Dawn blessings

After each night you pick one of two random upgrades:

| Blessing | Effect | Max |
| --- | --- | --- |
| Wide Beam | Beam is 25% wider | 2 |
| Deep Tank | +30 oil capacity | 2 |
| Hotter Flame | Burns shadows 35% faster | 2 |
| Spare Wick | +1 maximum flame, and it's lit right away | 2 |
| Second Flare | +1 flare every night | 1 |
| Steady Wick | Beam uses 25% less oil | 2 |

### Score

- Each shadow is worth points: Crawler 10, Skitter 15, Wisp 20, Brute 40.
- Kill shadows within 2.5 seconds of each other to build a **burn streak**. Each kill in
  a streak adds x0.25 to the points, up to x3. Losing a flame resets the streak.
- Every barrel is worth 25 points.
- Surviving a night is worth 100 points times the night number, plus 50 for every flame
  still burning.
- Your best score is saved and shown on the title screen.

### Losing

If your last flame goes out, the night is lost. **Click to retry that night** from exactly
where it started, with the same score, flames and blessings you had at dusk. Press Esc
instead to go back to the title screen.

### Tips

- Don't hold the beam down all the time. Short bursts aimed at one shadow at a time use
  far less oil than sweeping.
- Burn the closest shadow first. A Brute far away can wait, a Skitter next to the tower
  can't.
- Grab barrels during quiet moments, before you need the oil.
- Use headphones. The music is made by your play (see below), and the heartbeat warns
  you when you're on your last flame.

## Opening the project

1. Install **Unity 6** with Unity Hub. The project was saved with Unity 6000.6.2f1; a
   newer Unity 6 version also works.
2. In Unity Hub choose **Add > Add project from disk** and select this folder.
   The first open takes a minute while Unity rebuilds its `Library` cache.
3. Press **Play**. The game builds itself from code (`Assets/Lanternlight/Scripts/Core/Bootstrap.cs`),
   so it works from any open scene, even an empty one.
4. Optional: **Lanternlight > Create Main Scene** in the menu bar saves a scene and adds it
   to Build Settings, if you want to make a standalone build.

**Tips**

- Set the Game view to **16:9** or **1920x1080** for the intended framing.
- If the art ever looks blurry, use **Lanternlight > Reimport Art and Audio**.
- A 2D (Built-in Render Pipeline) project is the simplest setup. If you open it in a URP
  2D project and the sprites look dark, add a Global Light 2D to the scene.
- If your project only has the new Input System enabled, the game uses it automatically.

## Sound and music

There is no looping background track. The music comes from play:

- every kill in a streak plucks the next note up a pentatonic scale,
- a bell melody climbs one note each time the night is 10% closer to dawn,
- a low drone fades in as shadows close in on the tower,
- a heartbeat starts when you're down to your last flame.

## Art and audio (original)

All art and audio was made for this project by two generator scripts:

- `Tools/generate_art.py` draws all the pixel art (sky, sea, island, lighthouse, creatures,
  UI icons, and the 5x7 bitmap font) into `Assets/Lanternlight/Resources/Art`.
- `Tools/generate_audio.py` synthesizes every sound effect, ambience loop and musical cue
  into `Assets/Lanternlight/Resources/Audio`.

To regenerate (Python 3 with numpy, scipy and Pillow):

```
python3 Tools/generate_art.py Assets/Lanternlight/Resources/Art
python3 Tools/generate_audio.py Assets/Lanternlight/Resources/Audio
```

## Code layout

```
Assets/Lanternlight/
  Scripts/
    Core/       Bootstrap, GameManager (game flow), RunState, NightDefinition (night data),
                Tuning (all balance numbers), Layout (screen geometry), GameInput
    World/      WorldBuilder (scenery), Lighthouse, Beam, Barrel, BarrelSpawner
    Enemies/    Enemy (shared base class), Crawler, Skitter, Brute, Wisp, EnemySpawner
    Audio/      AudioManager (one-shots and loops), MusicDirector (gameplay-driven music)
    UI/         Hud, ScreenOverlay (all menus), UiButton
    Effects/    ParticleFx, CameraShake, ScreenFlash, FloatingText
    Rendering/  Art (loads sprites from Resources), SpriteText (bitmap font text)
  Editor/       Import settings for pixel art and audio, menu helpers
  Resources/    Art/ and Audio/
Tools/          Art and audio generators, headless playtest bot
```

Balance numbers live in `Tuning.cs` and `NightDefinition.cs`.

## Headless playtest

`Tools/Playtest/run.sh` compiles the real game scripts against a tiny fake Unity runtime and
lets a bot play all five nights, printing state changes, flames lost, oil use and any errors.
It was used to tune difficulty. Needs the .NET 8 SDK.

```
Tools/Playtest/run.sh good 0.35,12,0.45   # bot reaction (s), mouse speed, aim wobble
Tools/Playtest/run.sh idle                # never plays: tests game over and retry
Tools/Playtest/run.sh pause               # tests pause and quit to title
```

See `CHANGES.txt` for how the game changed from the design document.
