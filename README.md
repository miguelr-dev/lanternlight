# Lanternlight

**You are the light. Keep it burning until dawn.**

A single-screen 2D survival game made in Unity. You are the spirit inside a lighthouse
lamp. Each night, shadow creatures crawl out of the sea from both sides, and your only
weapon is the lighthouse beam. The beam burns shadows away, but it runs on oil, and the
same beam is how you haul in oil barrels drifting past. Survive five nights.

## Controls

| Input | Action |
| --- | --- |
| Mouse | Aim the beam |
| Hold left click | Shine the beam (uses oil; resting it lets the tank recover a little) |
| Shine on a barrel | Haul it in for oil |
| Space | Flare: burns every shadow on screen, once per night |
| Esc | Pause (Q from the pause menu quits to title) |
| 1 / 2 or click | Pick a blessing at dawn |

## Opening the project

1. Install **Unity 2022.3 LTS or Unity 6** with Unity Hub.
2. In Unity Hub choose **Add > Add project from disk** and select this folder.
   Pick your installed editor version if Hub asks. The first open takes a minute while
   Unity creates the `Packages`, `ProjectSettings` and `.meta` files.
3. Press **Play**. The game builds itself from code (`Assets/Lanternlight/Scripts/Core/Bootstrap.cs`),
   so it works from any open scene, even an empty one.
4. Optional: **Lanternlight > Create Main Scene** in the menu bar saves a scene and adds it
   to Build Settings, if you want to make a standalone build.

After the first open, commit the files Unity generated (`Packages/`, `ProjectSettings/`
and every `.meta` file) so the repo is a complete Unity project.

**Tips**

- Set the Game view to **16:9** or **1920x1080** for the intended framing.
- If the art ever looks blurry, use **Lanternlight > Reimport Art and Audio**.
- A 2D (Built-in Render Pipeline) project is the simplest setup. If you open it in a URP
  2D project and the sprites look dark, add a Global Light 2D to the scene.
- If your project only has the new Input System enabled, the game uses it automatically.

## How it plays

- **Shadows**: Crawlers (slow, come in packs), Skitters (fast, darting), Brutes (huge,
  need a long burn, put out two flames) and Wisps (fly straight for the lamp).
- **Oil**: drains while the beam is on and trickles back while it rests. Barrels refill a lot.
- **Flames**: three to start. A shadow reaching the tower puts one out. Dawn relights one.
- **Dawn blessings**: after each night, choose one of two upgrades.
- **Score**: quick kills build a burn streak (up to x3). Barrels and surviving dawn give bonuses.
- **Losing** a night lets you retry it from exactly where it started.

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
