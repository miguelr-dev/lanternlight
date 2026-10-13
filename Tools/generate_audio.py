"""
Lanternlight - procedural sound generator.

Synthesizes every sound effect, ambience loop and musical cue in the game:

    python3 Tools/generate_audio.py Assets/Lanternlight/Resources/Audio

There is no background music track. The "music" is built from gameplay:
  * note_0..7   - plucked notes that climb a pentatonic scale as your burn streak grows
  * chime_0..9  - a bell melody that rings each time the night is another 10% closer to dawn
  * drone       - a tension layer whose volume follows how close the shadows are
  * heartbeat   - plays when the lighthouse is down to its last flame
"""
import os
import sys
import wave

import numpy as np
from scipy import signal

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Lanternlight/Resources/Audio"
os.makedirs(OUT, exist_ok=True)
SR = 44100
rng = np.random.default_rng(42)


# ----------------------------------------------------------------------------
# Building blocks
# ----------------------------------------------------------------------------
def t_axis(seconds):
    return np.arange(int(SR * seconds)) / SR


def noise(seconds):
    return rng.uniform(-1, 1, int(SR * seconds))


def env(n, attack, decay_power=1.0, hold=0.0):
    """Attack-then-decay envelope over n samples (attack/hold in seconds)."""
    t = np.arange(n) / SR
    total = n / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    rest = np.clip((t - attack - hold) / max(total - attack - hold, 1e-4), 0, 1)
    return a * (1 - rest) ** decay_power


def exp_env(n, tau):
    return np.exp(-np.arange(n) / SR / tau)


def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff / (SR / 2), 0.99), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, cutoff / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi / (SR / 2), 0.99)], "band")
    return signal.lfilter(b, a, x)


def swept_filter(x, kind, cutoff_at, chunk=512):
    """Filter whose cutoff moves over time. cutoff_at(progress 0..1) returns Hz (or (lo, hi) for bandpass).
    Filter state carries across chunks so there are no clicks."""
    out = np.zeros_like(x)
    zi = None
    for i in range(0, len(x), chunk):
        c = cutoff_at(i / len(x))
        if kind == "band":
            lo, hi = c
            b, a = signal.butter(2, [lo / (SR / 2), min(hi / (SR / 2), 0.99)], "band")
        else:
            b, a = signal.butter(2, min(c / (SR / 2), 0.99), "low")
        if zi is None:
            zi = np.zeros(max(len(a), len(b)) - 1)
        out[i:i + chunk], zi = signal.lfilter(b, a, x[i:i + chunk], zi=zi)
    return out


def sweep_sine(f0, f1, seconds, curve=1.0):
    """Sine whose pitch glides from f0 to f1."""
    t = t_axis(seconds)
    f = f0 + (f1 - f0) * (t / seconds) ** curve
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def bell(freq, seconds, brightness=1.0):
    """Inharmonic bell: a few partials, each decaying at its own rate."""
    t = t_axis(seconds)
    partials = [(0.5, 0.35, 2.2), (1.0, 1.0, 1.6), (1.19, 0.45, 1.0), (1.56, 0.3, 0.8),
                (2.0, 0.35 * brightness, 0.6), (2.74, 0.18 * brightness, 0.4), (3.0, 0.12 * brightness, 0.3)]
    out = np.zeros_like(t)
    for ratio, amp, tau in partials:
        out += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t / (tau * seconds / 2))
    return out * np.clip(t / 0.004, 0, 1)


def pluck(freq, seconds, damping=0.996):
    """Karplus-Strong plucked string."""
    n = int(SR * seconds)
    period = int(SR / freq)
    buf = rng.uniform(-1, 1, period)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = damping * 0.5 * (buf[i % period] + buf[(i + 1) % period])
    return lowpass(out, 6000)


def make_loop(x, fade_seconds):
    """Crossfade the tail into the head so the clip loops without a click."""
    f = int(SR * fade_seconds)
    body = x[:-f].copy()
    ramp = np.linspace(0, 1, f)
    body[:f] = body[:f] * ramp + x[-f:] * (1 - ramp)
    return body


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def save(name, x, peak=0.89, loop=False):
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x)
    m = np.max(np.abs(x))
    if m > 0:
        x = x / m * peak
    if not loop:
        # Tiny fade at the very end avoids clicks on one-shots (loops must stay seamless).
        tail = min(256, len(x))
        x[-tail:] *= np.linspace(1, 0, tail)
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ----------------------------------------------------------------------------
# The beam
# ----------------------------------------------------------------------------
def beam_sounds():
    n = int(SR * 0.22)
    thump = sweep_sine(80, 150, 0.22) * exp_env(n, 0.06)
    click = highpass(noise(0.22), 3000) * exp_env(n, 0.008)
    shimmer = bandpass(noise(0.22), 2000, 6000) * env(n, 0.03, 2) * 0.3
    save("beam_on", mix(thump, click * 0.6, shimmer))

    n = int(SR * 0.18)
    save("beam_off", mix(sweep_sine(170, 60, 0.18) * exp_env(n, 0.05), highpass(noise(0.18), 2500) * exp_env(n, 0.01) * 0.4))

    # Hum: a warm electric buzz with a slow wobble. Frequencies fit whole cycles into 2 s.
    t = t_axis(2.4)
    hum = (np.sin(2 * np.pi * 110 * t) + 0.5 * np.sin(2 * np.pi * 220 * t) + 0.25 * np.sin(2 * np.pi * 330 * t)
           + 0.12 * signal.sawtooth(2 * np.pi * 55 * t))
    hum *= 0.8 + 0.2 * np.sin(2 * np.pi * 6 * t)
    crackle = lowpass(noise(2.4), 1800) * 0.25
    save("beam_hum", make_loop(lowpass(hum, 1400) + crackle, 0.4), peak=0.6, loop=True)

    # Sizzle: bright hiss with random pops, used while a shadow is burning.
    x = highpass(noise(1.9), 2500) * 0.5
    pops = np.zeros_like(x)
    for p in rng.integers(0, len(x) - 400, 45):
        pops[p:p + 300] += rng.uniform(0.4, 1.0) * np.exp(-np.arange(300) / 40) * rng.choice([-1, 1])
    x = x * (0.7 + 0.3 * lowpass(noise(1.9), 8)) + highpass(pops, 1200)
    save("burn_loop", make_loop(x, 0.4), peak=0.55, loop=True)


# ----------------------------------------------------------------------------
# Shadows
# ----------------------------------------------------------------------------
def shadow_sounds():
    # Death "poof": a burst of noise whose filter closes as it fades, plus a soft thud and sparkle.
    n = int(SR * 0.5)
    poof = swept_filter(noise(0.5), "low", lambda p: 4000 * (1 - p) ** 2 + 200) * exp_env(n, 0.09)
    sparkle = sum(np.sin(2 * np.pi * f * t_axis(0.5)) * exp_env(n, 0.05) * 0.15 for f in (1760, 2637))
    thud = sweep_sine(140, 50, 0.5) * exp_env(n, 0.05) * 0.8
    save("enemy_die", mix(poof, sparkle, thud))

    # Spawn splash: water burst plus a few bubbles.
    n = int(SR * 0.55)
    splash = bandpass(noise(0.55), 300, 3500) * exp_env(n, 0.12)
    bubbles = np.zeros(n)
    for k in range(5):
        start = int(SR * (0.05 + 0.07 * k))
        dur = 0.06
        b = sweep_sine(400 + 120 * k, 900 + 150 * k, dur) * exp_env(int(SR * dur), 0.02)
        bubbles[start:start + len(b)] += b * 0.35
    save("spawn_splash", mix(splash, bubbles), peak=0.7)

    # Skitter chitter: fast insect clicks.
    n = int(SR * 0.32)
    ch = np.zeros(n)
    for k in range(9):
        s = int(SR * 0.033 * k)
        c = bandpass(noise(0.012), 2500, 7000) * exp_env(int(SR * 0.012), 0.003)
        ch[s:s + len(c)] += c * rng.uniform(0.5, 1)
    save("skitter_chitter", ch, peak=0.6)

    # Brute roar: low growling saw with vibrato and grit.
    t = t_axis(1.1)
    f = 62 + 8 * np.sin(2 * np.pi * 7 * t) - 15 * t
    saw = signal.sawtooth(2 * np.pi * np.cumsum(f) / SR)
    grit = lowpass(noise(1.1), 600) * 0.6
    roar = lowpass(saw + grit, 900) * env(len(t), 0.12, 1.5, 0.3)
    save("brute_roar", roar)

    # Wisp whisper: breathy noise with a sweeping formant.
    n = int(SR * 0.9)
    centre = lambda p: 900 + 1400 * np.sin(np.pi * p)
    out = swept_filter(noise(0.9), "band", lambda p: (centre(p) * 0.7, centre(p) * 1.3))
    save("wisp_whisper", out * env(n, 0.2, 1.2), peak=0.55)


# ----------------------------------------------------------------------------
# Oil, the lighthouse, the flare
# ----------------------------------------------------------------------------
def world_sounds():
    # Barrel collected: wooden clunk, then a glug-glug pour, then a bright confirming ding.
    n = int(SR * 0.75)
    clunk = (np.sin(2 * np.pi * 180 * t_axis(0.75)) * exp_env(n, 0.04)
             + bandpass(noise(0.75), 400, 1600) * exp_env(n, 0.02) * 0.8)
    glug = np.zeros(n)
    for k, start in enumerate((0.12, 0.22, 0.32)):
        g = sweep_sine(220 + 40 * k, 520 + 60 * k, 0.08) * env(int(SR * 0.08), 0.01, 2)
        s = int(SR * start)
        glug[s:s + len(g)] += g * 0.5
    ding = np.zeros(n)
    d = bell(1318.5, 0.35, 0.6) * 0.35
    ding[int(SR * 0.38):int(SR * 0.38) + len(d)] += d[:n - int(SR * 0.38)]
    save("barrel_collect", mix(clunk, glug, ding))

    # Oil empty: the lamp sputters out.
    n = int(SR * 0.8)
    sp = np.zeros(n)
    for k in range(12):
        s = int(SR * (0.06 * k + rng.uniform(0, 0.02)))
        pop = lowpass(noise(0.03), 1500) * exp_env(int(SR * 0.03), 0.008)
        sp[s:s + len(pop)] += pop * (1 - k / 12)
    save("oil_empty", mix(sp, sweep_sine(300, 90, 0.8) * exp_env(n, 0.2) * 0.3))

    # Oil low warning: two soft square blips.
    blip = lowpass(signal.square(2 * np.pi * 440 * t_axis(0.07)), 2000) * env(int(SR * 0.07), 0.005, 1.5)
    save("oil_low", np.concatenate([blip, np.zeros(int(SR * 0.05)), blip * 0.8]), peak=0.5)

    # Flame lost: a heavy boom with a hissing tail.
    n = int(SR * 1.0)
    boom = sweep_sine(90, 32, 1.0, 0.5) * exp_env(n, 0.22)
    hiss = lowpass(noise(1.0), 2500) * env(n, 0.02, 2.5) * 0.6
    crunch = bandpass(noise(1.0), 150, 900) * exp_env(n, 0.05)
    save("flame_lost", mix(boom, hiss, crunch))

    # Flare: rising whoosh into a shimmering chord.
    n = int(SR * 1.6)
    rise = lambda p: 300 + 5000 * min(p * 1.6 / 0.35, 1) ** 1.5   # peaks 0.35 s in
    whoosh = swept_filter(noise(1.6), "band", lambda p: (rise(p) * 0.6, rise(p) * 1.4)) * env(n, 0.3, 3)
    t = t_axis(1.6)
    chord = sum(np.sin(2 * np.pi * f * t) for f in (880, 1108.7, 1318.5, 1760)) / 4
    chord *= np.clip((t - 0.25) / 0.05, 0, 1) * np.exp(-np.maximum(t - 0.25, 0) / 0.5) * (0.8 + 0.2 * np.sin(2 * np.pi * 9 * t))
    save("flare", mix(whoosh, chord * 1.2))

    # Heartbeat (lub-dub) for the last flame.
    n = int(SR * 0.7)
    hb = np.zeros(n)
    for start, amp in ((0.0, 1.0), (0.2, 0.7)):
        b = sweep_sine(70, 40, 0.16) * exp_env(int(SR * 0.16), 0.04)
        s = int(SR * start)
        hb[s:s + len(b)] += lowpass(b, 300) * amp
    save("heartbeat", hb)


# ----------------------------------------------------------------------------
# UI and story beats
# ----------------------------------------------------------------------------
def ui_sounds():
    n = int(SR * 0.05)
    save("ui_click", bandpass(noise(0.05), 1500, 5000) * exp_env(n, 0.006) + np.sin(2 * np.pi * 1200 * t_axis(0.05)) * exp_env(n, 0.01) * 0.5, peak=0.5)
    a = np.sin(2 * np.pi * 660 * t_axis(0.09)) * env(int(SR * 0.09), 0.005, 1.5)
    b = np.sin(2 * np.pi * 990 * t_axis(0.14)) * env(int(SR * 0.14), 0.005, 1.5)
    save("ui_confirm", lowpass(np.concatenate([a, b]), 4000), peak=0.6)

    # Night start: the lighthouse bell tolls twice.
    toll = bell(196, 2.2)
    save("night_start", mix(toll, np.concatenate([np.zeros(int(SR * 0.9)), bell(196, 2.2) * 0.6])))

    # Dawn: a warm major chord swelling in.
    t = t_axis(3.2)
    notes = [261.63, 329.63, 392.0, 587.33, 659.25]
    pad_ = sum(signal.sawtooth(2 * np.pi * f * t * (1 + 0.002 * k)) for k, f in enumerate(notes))
    pad_ = lowpass(pad_, 1800) * env(len(t), 1.0, 1.5, 0.8)
    save("dawn", pad_ + bell(1046.5, 3.2, 0.5) * 0.3)

    # Game over: a falling minor line into wind.
    seq = [(220.0, 0.0), (196.0, 0.35), (174.6, 0.7), (130.8, 1.05)]
    n = int(SR * 2.6)
    go = np.zeros(n)
    for f, start in seq:
        p = pluck(f, 1.4, 0.997)
        s = int(SR * start)
        go[s:s + len(p)] += p[:n - s]
    go += lowpass(noise(2.6), 500) * env(n, 0.8, 2) * 0.3
    save("game_over", go)

    # Victory: a bright ascending arpeggio of bells.
    scale = [523.25, 587.33, 659.25, 783.99, 880.0, 1046.5]
    n = int(SR * 3.4)
    v = np.zeros(n)
    for k, f in enumerate(scale):
        b = bell(f, 1.6, 0.8)
        s = int(SR * 0.16 * k)
        v[s:s + len(b)] += b
    t = t_axis(3.4)
    v += lowpass(sum(signal.sawtooth(2 * np.pi * f * t) for f in (261.63, 329.63, 392.0)), 1500) * env(n, 1.2, 1.2, 0.6) * 0.25
    save("victory", v)


# ----------------------------------------------------------------------------
# Gameplay-driven music pieces
# ----------------------------------------------------------------------------
def music_sounds():
    # Burn-streak notes: A minor pentatonic, low to high.
    for i, f in enumerate([440.0, 523.25, 587.33, 659.25, 783.99, 880.0, 1046.5, 1174.66]):
        save(f"note_{i}", pluck(f, 0.9), peak=0.6)

    # Dawn chimes: D major pentatonic climbing toward sunrise.
    for i, f in enumerate([587.33, 659.25, 739.99, 880.0, 987.77, 1174.66, 1318.51, 1479.98, 1760.0, 1975.53]):
        save(f"chime_{i}", bell(f, 1.8, 0.4), peak=0.5)

    # Tension drone: low A with a dissonant neighbour that beats slowly.
    t = t_axis(4.6)
    d = (np.sin(2 * np.pi * 55 * t) + 0.6 * np.sin(2 * np.pi * 58.3 * t) + 0.4 * np.sin(2 * np.pi * 82.4 * t)
         + 0.2 * signal.sawtooth(2 * np.pi * 110 * t))
    d = lowpass(d, 500) + lowpass(noise(4.6), 300) * 0.5
    save("drone", make_loop(d, 0.6), peak=0.7, loop=True)


def ambience_sounds():
    # Waves: low rumbling surf with slow swells.
    t = t_axis(8.8)
    surf = lowpass(noise(8.8), 700) * (0.55 + 0.45 * np.sin(2 * np.pi * t / 4.4) ** 2)
    hiss = highpass(lowpass(noise(8.8), 5000), 1500) * 0.25 * np.sin(2 * np.pi * t / 4.4 + 0.6) ** 4
    save("waves", make_loop(surf + hiss, 0.8), peak=0.6, loop=True)

    # Wind: hollow whistle drifting in and out.
    centre = lambda p: 500 + 250 * np.sin(2 * np.pi * p * 2)
    out = swept_filter(noise(8.8), "band", lambda p: (centre(p) * 0.8, centre(p) * 1.25))
    save("wind", make_loop(out * (0.6 + 0.4 * np.sin(2 * np.pi * t / 8.8 * 3)), 0.8), peak=0.45, loop=True)


if __name__ == "__main__":
    beam_sounds()
    shadow_sounds()
    world_sounds()
    ui_sounds()
    music_sounds()
    ambience_sounds()
    print("Audio written to", OUT)
