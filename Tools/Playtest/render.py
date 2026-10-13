"""Render a sprite dump from the headless simulation into a PNG (what the game camera would show)."""
import json
import math
import sys

from PIL import Image

RES = sys.argv[1]
SCALE = 4                       # output pixels per art pixel
W, H = 320 * SCALE, 180 * SCALE
PX_PER_UNIT = 16 * SCALE
cache = {}


def tex(name):
    if name not in cache:
        cache[name] = Image.open(f"{RES}/{name}.png").convert("RGBA")
    return cache[name]


def render(dump_path, out_path):
    data = json.load(open(dump_path))
    cam = data["cam"]
    img = Image.new("RGBA", (W, H), (5, 8, 20, 255))
    sprites = sorted(data["sprites"], key=lambda s: (s["order"], s["created"]))
    for s in sprites:
        t = tex(s["tex"])
        x, y, w, h = s["rect"]
        crop = t.crop((int(x), int(t.height - y - h), int(x + w), int(t.height - y)))
        sx, sy = s["scale"]
        flip = s["flip"] ^ (sx < 0)
        if flip:
            crop = crop.transpose(Image.FLIP_LEFT_RIGHT)
        if sy < 0:
            crop = crop.transpose(Image.FLIP_TOP_BOTTOM)
        ow = abs(w / s["ppu"] * sx) * PX_PER_UNIT
        oh = abs(h / s["ppu"] * sy) * PX_PER_UNIT
        if ow < 0.5 or oh < 0.5:
            continue
        crop = crop.resize((max(1, round(ow)), max(1, round(oh))), Image.NEAREST)
        r, g, b, a = s["color"]
        if (r, g, b, a) != (1, 1, 1, 1):
            ch = crop.split()
            crop = Image.merge("RGBA", [ch[0].point(lambda v: v * r), ch[1].point(lambda v: v * g),
                                        ch[2].point(lambda v: v * b), ch[3].point(lambda v: v * a)])
        pvx = s["pivot"][0] * crop.width
        pvy = (1 - s["pivot"][1]) * crop.height
        if flip:
            pvx = crop.width - pvx
        sxp = W / 2 + (s["pos"][0] - cam[0]) * PX_PER_UNIT
        syp = H / 2 - (s["pos"][1] - cam[1]) * PX_PER_UNIT
        rot = s["rot"]
        if abs(rot) > 0.01:
            # Rotate about the pivot: pad so the pivot is the centre, then rotate.
            pad = int(math.hypot(crop.width, crop.height)) * 2 + 4
            big = Image.new("RGBA", (pad, pad))
            big.paste(crop, (int(pad / 2 - pvx), int(pad / 2 - pvy)))
            big = big.rotate(rot, resample=Image.BICUBIC, center=(pad / 2, pad / 2))
            paste_clipped(img, big, sxp - pad / 2, syp - pad / 2)
        else:
            paste_clipped(img, crop, sxp - pvx, syp - pvy)
    img.save(out_path)


def paste_clipped(dst, src, x, y):
    x, y = int(round(x)), int(round(y))
    left, top = max(0, -x), max(0, -y)
    right, bottom = min(src.width, dst.width - x), min(src.height, dst.height - y)
    if right <= left or bottom <= top:
        return
    dst.alpha_composite(src.crop((left, top, right, bottom)), (x + left, y + top))


if __name__ == "__main__":
    for path in sys.argv[2:]:
        render(path, path.replace(".json", ".png"))
        print("rendered", path)
