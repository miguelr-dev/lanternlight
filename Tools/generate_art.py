"""
Lanternlight - procedural pixel-art generator.

Every sprite in the game is drawn by this script, so the art can be tweaked
and regenerated at any time:

    python3 Tools/generate_art.py Assets/Lanternlight/Resources/Art

Conventions shared with the C# code (see Assets/Lanternlight/Scripts/Core/Layout.cs):
  * 16 pixels per world unit, 320 x 180 pixel screen (20 x 11.25 world units).
  * The island's walkable surface follows ground_y() below; Layout.GroundY()
    implements the exact same curve so enemies walk on the rocks.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Lanternlight/Resources/Art"
os.makedirs(OUT, exist_ok=True)
rng = random.Random(1337)
np_rng = np.random.default_rng(1337)

PPU = 16
SCREEN_W, SCREEN_H = 320, 180

# Bayer matrix used for ordered dithering (gives gradients a hand-made pixel-art look).
BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


# ----------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------
def hexc(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


def canvas(w, h):
    return np.zeros((h, w, 4), dtype=np.uint8)


def save(arr, name):
    Image.fromarray(arr, "RGBA").save(os.path.join(OUT, name + ".png"))


def dithered_gradient(w, h, stops):
    """Vertical gradient through a list of colour stops, ordered-dithered between bands."""
    img = canvas(w, h)
    cols = [hexc(c) for c in stops]
    n = len(cols) - 1
    for y in range(h):
        f = (y / (h - 1)) * n
        i = min(int(f), n - 1)
        frac = f - i
        for x in range(w):
            pick = cols[i + 1] if frac > BAYER4[y % 4, x % 4] else cols[i]
            img[y, x] = pick
    return img


def shade(c, k):
    """Multiply an RGBA tuple's RGB by k."""
    return (min(255, int(c[0] * k)), min(255, int(c[1] * k)), min(255, int(c[2] * k)), c[3])


def from_ascii(rows, palette):
    """Build a sprite from an ASCII map. '.' is transparent, other characters index the palette."""
    h, w = len(rows), len(rows[0])
    img = canvas(w, h)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img[y, x] = palette[ch]
    return img


def value_noise(w, h, cell, seed):
    """Smooth 2D value noise in [0, 1]."""
    r = np.random.default_rng(seed)
    gw, gh = w // cell + 2, h // cell + 2
    grid = r.random((gh, gw))
    ys, xs = np.mgrid[0:h, 0:w]
    gx, gy = xs / cell, ys / cell
    x0, y0 = gx.astype(int), gy.astype(int)
    tx, ty = gx - x0, gy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a = grid[y0, x0] * (1 - tx) + grid[y0, x0 + 1] * tx
    b = grid[y0 + 1, x0] * (1 - tx) + grid[y0 + 1, x0 + 1] * tx
    return a * (1 - ty) + b * ty


# ----------------------------------------------------------------------------
# Sky (night + dawn), stars, clouds
# ----------------------------------------------------------------------------
def make_sky():
    night = dithered_gradient(SCREEN_W, SCREEN_H,
                              ["#05081a", "#0a0f2e", "#121842", "#1c1c50", "#2a235e", "#3d2c68", "#4a3270"])
    dawn = dithered_gradient(SCREEN_W, SCREEN_H,
                             ["#1d2759", "#33397a", "#5c4589", "#a2577f", "#e07a6c", "#ffab70", "#ffd38a"])

    # Moon with a dithered halo (upper left).
    mx, my, mr = 62, 30, 9
    for y in range(SCREEN_H):
        for x in range(SCREEN_W):
            d = math.hypot(x - mx, y - my)
            if d < mr:
                c = hexc("#efe9cf")
                if math.hypot(x - mx - 3, y - my + 2) < 2.2 or math.hypot(x - mx + 2, y - my - 3) < 1.6:
                    c = hexc("#cfc7a3")  # craters
                if x - mx > mr * 0.45:
                    c = shade(c, 0.88)
                night[y, x] = c
            elif d < mr + 10:
                glow = 1 - (d - mr) / 10
                if glow * 0.55 > BAYER4[y % 4, x % 4]:
                    night[y, x] = shade(night[y, x], 1.25)

    # Static stars (twinkling stars are separate sprites animated in code).
    for _ in range(90):
        x, y = rng.randrange(SCREEN_W), rng.randrange(0, 118)
        if math.hypot(x - mx, y - my) < mr + 12:
            continue
        night[y, x] = hexc(rng.choice(["#cfd6ff", "#8f9ad6", "#b8b3e8", "#ffffff"]))
    save(night, "sky_night")
    save(dawn, "sky_dawn")

    star = from_ascii([
        "..#..",
        "..#..",
        "##O##",
        "..#..",
        "..#..",
    ], {"#": hexc("#9fa8e8", 200), "O": hexc("#ffffff")})
    save(star, "star")


def make_clouds():
    for idx, (w, h) in enumerate([(72, 16), (56, 13), (92, 20)]):
        img = canvas(w, h)
        blobs = [(rng.uniform(w * 0.15, w * 0.85), rng.uniform(h * 0.45, h * 0.7),
                  rng.uniform(w * 0.12, w * 0.24), rng.uniform(h * 0.3, h * 0.48)) for _ in range(6)]
        for y in range(h):
            for x in range(w):
                v = max(1 - ((x - bx) / rx) ** 2 - ((y - by) / ry) ** 2 for bx, by, rx, ry in blobs)
                if v <= 0:
                    continue
                if v < 0.18 and v / 0.18 < BAYER4[y % 4, x % 4]:
                    continue  # dithered soft edge
                top_lit = y < h * 0.45 and v < 0.55
                img[y, x] = hexc("#353470", 230) if top_lit else hexc("#1f1f4c", 215)
        save(img, f"cloud_{idx}")


# ----------------------------------------------------------------------------
# Sea (back horizon band + 4 animated front frames)
# ----------------------------------------------------------------------------
def make_sea():
    back = canvas(SCREEN_W, 14)
    for y in range(14):
        for x in range(SCREEN_W):
            back[y, x] = hexc("#14244f") if (y / 14) < BAYER4[y % 4, x % 4] + 0.25 else hexc("#0f1c40")
    for y in range(1, 14, 2):  # broken moon reflection under the moon
        for x in range(56, 69):
            if rng.random() < 0.55 - y * 0.03:
                back[y, x] = hexc("#8ea6d8")
    for _ in range(40):  # glints on the far water
        x, y = rng.randrange(SCREEN_W), rng.randrange(14)
        back[y, x] = hexc("#2c4a86")
    save(back, "sea_back")

    for frame in range(4):
        img = dithered_gradient(SCREEN_W, 40, ["#16305e", "#11264d", "#0b1a39", "#081228"])
        phase = frame / 4.0
        for x in range(SCREEN_W):
            crest = int(round(1.5 + 1.5 * math.sin(2 * math.pi * (x / 40.0 + phase))))
            for y in range(crest):
                img[y, x] = (0, 0, 0, 0)  # wavy top edge
            img[crest, x] = hexc("#a8c6ee")  # foam line
            if crest + 1 < 40:
                img[crest + 1, x] = hexc("#3d68a8")
        # Moving wave streaks on lanes of water, shifted every frame so the sea scrolls.
        for lane, (row, length, gap, col) in enumerate([(8, 7, 23, "#2d5390"), (15, 10, 31, "#24467e"),
                                                        (24, 12, 37, "#1c3a6c"), (33, 14, 43, "#16305c")]):
            offset = int(frame * (2 + lane))
            for start in range(-gap, SCREEN_W, gap):
                s = start + offset + lane * 9
                for x in range(s, s + length):
                    if 0 <= x < SCREEN_W:
                        img[row, x] = hexc(col)
        save(img, f"sea_{frame}")


# ----------------------------------------------------------------------------
# Island - its top edge is the path the shadows walk on
# ----------------------------------------------------------------------------
def ground_y(x):
    """World-space height of the walkable rock surface (mirrors Layout.GroundY in C#)."""
    ax = abs(x)
    if ax <= 1.2:
        return -1.6
    t = min((ax - 1.2) / 6.4, 1.0)
    return -1.6 - 1.8 * (t ** 1.35)


def make_island():
    w, h = 272, 36           # covers x in [-8.5, 8.5], top row at y = -1.5
    top_y = -1.5
    img = canvas(w, h)
    noise = value_noise(w, h, 6, 3)
    fine = value_noise(w, h, 2, 4)
    dark, mid, light, edge = hexc("#15131f"), hexc("#221f30"), hexc("#302b42"), hexc("#5b5274")
    moss, moss_hi = hexc("#22372f"), hexc("#35554a")
    for x in range(w):
        wx = (x + 0.5) / PPU - 8.5
        surf = int(round((top_y - ground_y(wx)) * PPU))
        for y in range(max(surf, 0), h):
            depth = y - surf
            n = noise[y, x] * 0.7 + fine[y, x] * 0.3
            c = light if n > 0.62 else mid if n > 0.38 else dark
            if depth > 18:
                c = shade(c, 0.8)
            # Strata cracks every few rows give the rock some structure.
            if (y + int(noise[y, x] * 6)) % 7 == 0 and fine[y, x] > 0.5:
                c = dark
            img[y, x] = c
        if 0 <= surf < h:
            img[surf, x] = edge                      # moonlit rim
            if surf + 1 < h and fine[surf, x] > 0.45:
                img[surf + 1, x] = light
            if noise[0, x] > 0.6 and abs(wx) < 6.5:  # moss tufts on the slope
                img[surf, x] = moss_hi
                if surf + 1 < h:
                    img[surf + 1, x] = moss
    save(img, "island")


# ----------------------------------------------------------------------------
# Lighthouse (base sprite + glowing lantern overlay + halo)
# ----------------------------------------------------------------------------
def make_lighthouse():
    W, H = 40, 92
    base = canvas(W, H)
    glow = canvas(W, H)
    white, red = hexc("#e9e4d8"), hexc("#a8323e")
    trim, rail, glass_dim = hexc("#2b2733"), hexc("#1d1a24"), hexc("#3a3020")

    def put(img, xb, yb, c):  # yb counts up from the bottom row
        if 0 <= xb < W and 0 <= yb < H:
            img[H - 1 - yb, xb] = c

    # Tower: tapered cylinder with red bands and cylinder shading.
    for yb in range(0, 56):
        half = (30 - 10 * yb / 56) / 2
        left, right = int(round(20 - half)), int(round(20 + half))
        col = red if (yb // 9) % 2 == 1 else white
        for xb in range(left, right):
            s = (xb + 0.5 - 20) / half
            k = 1.1 if s < -0.55 else (0.68 if s > 0.4 else 0.92)
            put(base, xb, yb, shade(col, k))
        put(base, left, yb, trim)
        put(base, right - 1, yb, shade(trim, 0.8))
    # Door and step.
    for yb in range(0, 10):
        for xb in range(17, 23):
            if not (yb == 9 and xb in (17, 22)):
                put(base, xb, yb, hexc("#2a2230"))
    for xb in range(15, 25):
        put(base, xb, 0, hexc("#3e3a4c"))
    # Windows with a faint warm glimmer.
    for wy in (22, 40):
        for yb in range(wy, wy + 4):
            for xb in range(19, 22):
                put(base, xb, yb, hexc("#1a1622"))
        put(base, 20, wy + 2, hexc("#c99a4a"))
    # Gallery platform and railing.
    for yb in range(56, 59):
        for xb in range(5, 35):
            put(base, xb, yb, trim if yb < 58 else hexc("#4a4458"))
    for xb in range(6, 34, 3):
        for yb in range(59, 63):
            put(base, xb, yb, rail)
    for xb in range(6, 34):
        put(base, xb, 63, rail)
    # Lantern room: frame, dim glass, mullions.
    for yb in range(59, 77):
        for xb in range(12, 28):
            edge = xb in (12, 27) or yb in (59, 60, 75, 76)
            mullion = xb in (16, 20, 24)
            if edge or mullion:
                put(base, xb, yb, rail)
            else:
                put(base, xb, yb, glass_dim)
                # The glow overlay fills the same panes, brighter toward the lamp centre.
                d = math.hypot(xb + 0.5 - 20, (yb - 68) * 1.2) / 9
                gc = (255, int(255 - 60 * d), int(200 - 140 * d), 255)
                put(glow, xb, yb, gc)
    # Dome roof, vent ball, spike.
    for yb in range(77, 87):
        half = 10 * math.cos((yb - 77) / 10 * math.pi / 2) + 1
        for xb in range(int(20 - half), int(20 + half)):
            s = (xb + 0.5 - 20) / half
            put(base, xb, yb, shade(red, 1.1 if s < -0.4 else (0.65 if s > 0.35 else 0.9)))
    for yb in range(87, 90):
        for xb in range(19, 21 + (1 if yb == 88 else 0)):
            put(base, xb, yb, hexc("#6d6880"))
    for yb in range(90, 92):
        put(base, 20, yb, rail)
    save(base, "lighthouse")
    save(glow, "lamp_glow")

    halo = canvas(128, 128)
    for y in range(128):
        for x in range(128):
            d = math.hypot(x - 63.5, y - 63.5) / 64
            if d < 1:
                halo[y, x] = (255, 214, 140, int(255 * 0.6 * (1 - d) ** 2.2))
    save(halo, "halo")


# ----------------------------------------------------------------------------
# Beam: a soft warm cone pointing right, origin at the left edge (32 px per unit)
# ----------------------------------------------------------------------------
def make_beam():
    L, Hh = 352, 176            # 11 x 5.5 world units at 32 PPU
    half_angle = math.radians(14)
    img = canvas(L, Hh)
    for y in range(Hh):
        for x in range(L):
            u = (x + 0.5) / L                      # 0 at lamp, 1 at max range
            v = (y + 0.5 - Hh / 2) / (Hh / 2)      # -1..1 across the far end
            reach = max(u * math.tan(half_angle) / math.tan(half_angle), 1e-4)
            across = abs(v) / reach                # 0 at centre line, 1 at the cone edge
            if across >= 1.0 or u <= 0.0:
                continue
            edge = min(1.0, (1 - across) / 0.25)   # soft sides
            core = 0.35 * max(0.0, 1 - across / 0.35)
            fade = (1 - u) ** 0.75                 # fades with distance
            a = (0.42 + core) * edge * fade
            r, g, b = 255, int(238 + 17 * core), int(170 + 70 * core)
            img[y, x] = (r, min(g, 255), min(b, 255), int(255 * min(a, 1)))
    save(img, "beam")


# ----------------------------------------------------------------------------
# Shadow creatures (4 animation frames each, all drawn facing right)
# ----------------------------------------------------------------------------
BODY = hexc("#0a0710")
BODY_IN = hexc("#140d1e")
RIM = hexc("#3d2758")


def finish_creature(mask, eyes, eye_core, eye_glow, alpha=255):
    """Turn a boolean body mask into a sprite with a moonlit rim and glowing eyes."""
    h, w = mask.shape
    img = canvas(w, h)
    for y in range(h):
        for x in range(w):
            if not mask[y, x]:
                continue
            top_edge = y == 0 or not mask[y - 1, x]
            left_edge = x == 0 or not mask[y, x - 1]
            c = RIM if (top_edge or (left_edge and y < h * 0.6)) else (BODY_IN if (x + y) % 5 == 0 else BODY)
            img[y, x] = (c[0], c[1], c[2], alpha)
    for (ex, ey) in eyes:
        for gx, gy in ((ex - 1, ey), (ex + 1, ey), (ex, ey - 1), (ex, ey + 1)):
            if 0 <= gx < w and 0 <= gy < h and mask[gy, gx] and tuple(img[gy, gx][:3]) != eye_core[:3]:
                img[gy, gx] = eye_glow
        img[ey, ex] = eye_core
    return img


def save_creature(img, name):
    """Save a creature frame plus a white silhouette used for the burn-glow overlay in game
    (tinting a black sprite does nothing, so the glow is drawn as a separate layer)."""
    save(img, name)
    mask = np.zeros_like(img)
    mask[img[:, :, 3] > 0] = (255, 255, 255, 255)
    save(mask, name + "_mask")


def ellipse_mask(w, h, cx, cy, rx, ry):
    ys, xs = np.mgrid[0:h, 0:w]
    return ((xs + 0.5 - cx) / rx) ** 2 + ((ys + 0.5 - cy) / ry) ** 2 <= 1.0


def make_crawler():
    w, h = 18, 13
    for f in range(4):
        squash = [0, 0.6, 0, -0.4][f]
        m = ellipse_mask(w, h, 9, 6.5 + squash * 0.5, 8.2, 5.2 - squash)
        # Tendril legs that shuffle every frame.
        for i, lx in enumerate(range(2, 17, 3)):
            length = 2 + ((i + f) % 2)
            for y in range(10, min(10 + length, h)):
                xx = lx + (1 if (i + f) % 2 else 0)
                if 0 <= xx < w:
                    m[y, xx] = True
        # Wispy hairs on the back.
        for i, hx in enumerate((4, 7, 10)):
            top = int(6.5 + squash * 0.5 - (5.2 - squash)) - 1
            if 0 <= top < h:
                m[max(top, 0), hx + ((f + i) % 2)] = True
        save_creature(finish_creature(m, [(12, 5), (15, 5)], hexc("#ffffff"), hexc("#7fe8ff")), f"crawler_{f}")


def make_skitter():
    w, h = 16, 10
    for f in range(4):
        m = ellipse_mask(w, h, 8.5, 4.5, 5.5, 2.8)
        # Six spindly legs that alternate like a spider's.
        for i, base_x in enumerate((4, 7, 10)):
            for side in (-1, 1):
                swing = 1 if (i + f + (side > 0)) % 2 else -1
                x0, y0 = base_x + (1 if side > 0 else 0), 6
                for step in range(4):
                    xx = x0 + side * (step // 2) + (swing if step == 3 else 0)
                    yy = y0 + step
                    if 0 <= xx < w and 0 <= yy < h:
                        m[yy, xx] = True
        for sx in (6, 9, 12):  # spiky back
            m[1 + (f + sx) % 2, sx] = True
        save_creature(finish_creature(m, [(12, 3), (14, 4)], hexc("#fff6b0"), hexc("#ffb43c")), f"skitter_{f}")


def make_brute():
    w, h = 30, 26
    for f in range(4):
        bob = [0, 1, 0, 1][f]
        m = ellipse_mask(w, h, 13, 12 + bob, 11, 9)
        m |= ellipse_mask(w, h, 22, 9 + bob, 6, 5.5)          # hunched head
        swing = [-2, 0, 2, 0][f]
        for (ax, phase) in ((8, 1), (20, -1)):                # heavy knuckle-walking arms
            for y in range(14 + bob, 25):
                xx = ax + int(phase * swing * (y - 14) / 10)
                for t in range(3):
                    if 0 <= xx + t < w:
                        m[y, xx + t] = True
        for i, sx in enumerate((6, 10, 14, 18)):              # spines
            top = 3 + bob
            m[top - (i % 2), sx] = True
            m[top, sx + 1] = True
        save_creature(finish_creature(m, [(23, 8 + bob), (26, 8 + bob), (25, 11 + bob)],
                             hexc("#ffd0c8"), hexc("#ff3b3b")), f"brute_{f}")


def make_wisp():
    w, h = 18, 14
    for f in range(4):
        m = ellipse_mask(w, h, 12, 7, 4.6, 4.6)
        # Flowing tail behind the head.
        for x in range(1, 10):
            amp = 2.2 * math.sin((x / 9) * math.pi * 1.5 + f * math.pi / 2)
            cy = 7 + amp * (1 - x / 12)
            thick = 0.6 + x / 5
            for y in range(h):
                if abs(y + 0.5 - cy) < thick:
                    m[y, x] = True
        save_creature(finish_creature(m, [(13, 6), (15, 6)], hexc("#ffffff"), hexc("#c58bff"), alpha=225), f"wisp_{f}")


# ----------------------------------------------------------------------------
# Props, particles and UI icons
# ----------------------------------------------------------------------------
def make_props():
    barrel = from_ascii([
        "..########..",
        ".#hhhhhhhh#.",
        "#bbbbbbbbbb#",
        "#mmmmmmmmmm#",
        "#wWwwWwwWdd#",
        "#wWwwWwwWdd#",
        "#wWwwOOwWdd#",
        "#wWwOooOwdd#",
        "#wWwwOOwWdd#",
        "#wWwwWwwWdd#",
        "#mmmmmmmmmm#",
        "#wWwwWwwWdd#",
        ".#bbbbbbbb#.",
        "..########..",
    ], {
        "#": hexc("#1b1520"), "h": hexc("#8a6a44"), "b": hexc("#4a3424"), "m": hexc("#55526a"),
        "w": hexc("#6b4a2b"), "W": hexc("#83603a"), "d": hexc("#4a321d"),
        "O": hexc("#e0a030"), "o": hexc("#ffe7a0"),
    })
    save(barrel, "barrel")

    pixel = canvas(1, 1)
    pixel[0, 0] = (255, 255, 255, 255)
    save(pixel, "pixel")

    puff = canvas(8, 8)
    for y in range(8):
        for x in range(8):
            d = math.hypot(x - 3.5, y - 3.5) / 4
            if d < 1:
                puff[y, x] = (255, 255, 255, int(255 * (1 - d) ** 1.5))
    save(puff, "puff")


def make_ui():
    flame_rows = [
        "....#....",
        "....##...",
        "...###...",
        "...####..",
        "..#####..",
        "..##o###.",
        ".###oo##.",
        ".##ooOo##",
        ".#ooOOoo#",
        ".#oOOOOo#",
        ".#oOOOOo#",
        "..#oOOo#.",
        "...####..",
    ]
    save(from_ascii(flame_rows, {"#": hexc("#ff7a2a"), "o": hexc("#ffcc4d"), "O": hexc("#fff6c8")}), "flame_on")
    save(from_ascii(flame_rows, {"#": hexc("#4a4558"), "o": hexc("#33303f"), "O": hexc("#2a2734")}), "flame_off")
    save(from_ascii([
        "....#....",
        "...###...",
        "..#####..",
        "..##o##..",
        ".###o###.",
        ".##ooo##.",
        ".#######.",
        ".#######.",
        "..#####..",
        "...###...",
    ], {"#": hexc("#e0a030"), "o": hexc("#ffe7a0")}), "oil_icon")
    save(from_ascii([
        "....#....",
        "....#....",
        ".#..#..#.",
        "..#####..",
        "###OOO###",
        "..#####..",
        ".#..#..#.",
        "....#....",
        "....#....",
    ], {"#": hexc("#ffe08a"), "O": hexc("#ffffff")}), "flare_icon")
    save(from_ascii([
        "..###..",
        ".##....",
        "##.....",
        "##.....",
        "##.....",
        ".##....",
        "..###..",
    ], {"#": hexc("#d8dcf5")}), "moon_icon")
    save(from_ascii([
        "....#....",
        ".#..#..#.",
        "..#####..",
        ".#######.",
        "#########",
        ".#######.",
        "..#####..",
        ".#..#..#.",
        "....#....",
    ], {"#": hexc("#ffc25a")}), "sun_icon")
    # Gauge / bar frame: light outline around a dark well (fill is drawn in code).
    for name, (w, h) in {"bar_frame": (54, 8), "dawn_frame": (104, 7)}.items():
        img = canvas(w, h)
        img[:, :] = hexc("#191623")
        img[0, :] = img[-1, :] = hexc("#8a83a0")
        img[:, 0] = img[:, -1] = hexc("#8a83a0")
        img[0, 0] = img[0, -1] = img[-1, 0] = img[-1, -1] = (0, 0, 0, 0)
        save(img, name)


# ----------------------------------------------------------------------------
# Bitmap font (5x7 glyphs in 6x8 cells, one row). Order must match FontAtlas.Charset in C#.
# ----------------------------------------------------------------------------
CHARSET = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,!?:;-+/'\"()%<>*#=_&[]"
GLYPHS = {
    " ": ["....."] * 7,
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####"],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": [".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "J": ["..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.."],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "Q": [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#."],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "Z": ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    "0": [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
    "1": ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
    "3": ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
    "4": ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
    "5": ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
    "6": ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
    "7": ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
    "8": [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
    "9": [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],
    ".": [".....", ".....", ".....", ".....", ".....", ".##..", ".##.."],
    ",": [".....", ".....", ".....", ".....", ".##..", "..#..", ".#..."],
    "!": ["..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.."],
    "?": [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
    ":": [".....", ".##..", ".##..", ".....", ".##..", ".##..", "....."],
    ";": [".....", ".##..", ".##..", ".....", ".##..", "..#..", ".#..."],
    "-": [".....", ".....", ".....", ".###.", ".....", ".....", "....."],
    "+": [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
    "/": ["....#", "....#", "...#.", "..#..", ".#...", "#....", "#...."],
    "'": ["..#..", "..#..", ".#...", ".....", ".....", ".....", "....."],
    '"': [".#.#.", ".#.#.", ".....", ".....", ".....", ".....", "....."],
    "(": ["...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#."],
    ")": [".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..."],
    "%": ["##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##"],
    "<": ["...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#."],
    ">": [".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#..."],
    "*": [".....", "..#..", "#.#.#", ".###.", "#.#.#", "..#..", "....."],
    "#": [".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#."],
    "=": [".....", ".....", "#####", ".....", "#####", ".....", "....."],
    "_": [".....", ".....", ".....", ".....", ".....", ".....", "#####"],
    "&": [".##..", "#..#.", "#.#..", ".#...", "#.#.#", "#..#.", ".##.#"],
    "[": [".###.", ".#...", ".#...", ".#...", ".#...", ".#...", ".###."],
    "]": [".###.", "...#.", "...#.", "...#.", "...#.", "...#.", ".###."],
}


def make_font():
    img = canvas(len(CHARSET) * 6, 8)
    for i, ch in enumerate(CHARSET):
        rows = GLYPHS[ch]
        assert len(rows) == 7 and all(len(r) == 5 for r in rows), ch
        for y, row in enumerate(rows):
            for x, px in enumerate(row):
                if px == "#":
                    img[y, i * 6 + x] = (255, 255, 255, 255)
    save(img, "font")


if __name__ == "__main__":
    make_sky()
    make_clouds()
    make_sea()
    make_island()
    make_lighthouse()
    make_beam()
    make_crawler()
    make_skitter()
    make_brute()
    make_wisp()
    make_props()
    make_ui()
    make_font()
    print("Art written to", OUT)
