"""Renders the SevMech icon (images/icon.png and images/icon-large.png): a golden Omega (TOP) split by an ice crack (FRU/Eden) over a
Kefka-coloured glow, framed like a FFXIV ability icon so it sits next to AnoMech's."""
import math, pathlib
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

S = 1024
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

def radial(size, cx, cy, color, radius):
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(40, 0, -1):
        r = radius * i / 40
        a = int(color[3] * (1 - i / 40) ** 1.6)
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color[:3] + (a,))
    return layer.filter(ImageFilter.GaussianBlur(size / 40))

def background():
    img = Image.new("RGBA", (S, S), (8, 10, 24, 255))
    # Kefka: harlequin diamonds in red / green / violet, dim, behind everything.
    d = ImageDraw.Draw(img)
    cols = [(120, 20, 40, 255), (20, 70, 50, 255), (70, 25, 110, 255)]
    step = S // 6
    k = 0
    for y in range(-step, S + step, step):
        for x in range(-step, S + step, step):
            c = cols[k % 3]; k += 1
            d.polygon([(x, y - step // 2), (x + step // 2, y), (x, y + step // 2), (x - step // 2, y)], fill=c)
    img = img.filter(ImageFilter.GaussianBlur(6))
    dark = Image.new("RGBA", (S, S), (8, 10, 24, 150))
    img = Image.alpha_composite(img, dark)
    # Light of Judgment rays from the top.
    rays = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rays)
    for i in range(9):
        ang = math.radians(-90 + (i - 4) * 13)
        x2 = S / 2 + math.cos(ang) * S * 1.3
        y2 = S * 0.05 + math.sin(ang) * S * 1.3
        rd.line([(S / 2, S * 0.05), (x2, y2)], fill=(255, 120, 200, 70), width=int(S * 0.025))
    rays = rays.filter(ImageFilter.GaussianBlur(S / 60))
    img = Image.alpha_composite(img, rays)
    # Three-way glow: Kefka magenta top-left, FRU ice top-right, Omega cyan bottom.
    img = Image.alpha_composite(img, radial(S, S * 0.22, S * 0.22, (230, 50, 120, 170), S * 0.6))
    img = Image.alpha_composite(img, radial(S, S * 0.80, S * 0.20, (190, 225, 255, 160), S * 0.55))
    img = Image.alpha_composite(img, radial(S, S * 0.50, S * 0.85, (40, 200, 255, 190), S * 0.65))
    return img

def omega_mask():
    m = Image.new("L", (S, S), 0)
    font = ImageFont.truetype(FONT, int(S * 0.82))
    d = ImageDraw.Draw(m)
    bbox = d.textbbox((0, 0), "Ω", font=font)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    d.text(((S - w) / 2 - bbox[0], (S - h) / 2 - bbox[1] + S * 0.02), "Ω", font=font, fill=255)
    return m

CRACK = [(0.16, -0.05), (0.30, 0.16), (0.24, 0.30), (0.42, 0.42), (0.38, 0.56), (0.58, 0.66),
         (0.56, 0.80), (0.74, 0.92), (0.80, 1.05)]

def crack_mask(width):
    pts = [(x * S, y * S) for x, y in CRACK]
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    d.line(pts, fill=255, width=width, joint="curve")
    for i in range(1, len(pts) - 1, 2):
        dx = S * 0.10 if i % 4 == 1 else -S * 0.10
        d.line([pts[i], (pts[i][0] + dx, pts[i][1] + S * 0.05)], fill=255, width=max(1, width * 2 // 3))
    return m

def gold_fill():
    g = Image.new("RGBA", (S, S))
    px = g.load()
    for y in range(S):
        t = y / S
        for x in range(S):
            s = (x / S + (1 - t)) / 2
            r = int(255 - 60 * t + 20 * math.sin(s * 9))
            gg = int(215 - 90 * t + 20 * math.sin(s * 9))
            b = int(110 - 70 * t)
            px[x, y] = (min(255, max(0, r)), min(255, max(0, gg)), min(255, max(0, b)), 255)
    return g

def shards():
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    # Ice splinters flying off the crack, pointing away from it.
    for (cx, cy, l, ang) in [(0.20, 0.22, 0.12, -150), (0.34, 0.33, 0.07, 20), (0.34, 0.50, 0.08, -160),
                             (0.62, 0.60, 0.07, 10), (0.52, 0.74, 0.08, -170), (0.80, 0.86, 0.11, 30)]:
        cx, cy, l = cx * S, cy * S, l * S
        a = math.radians(ang)
        tip = (cx + math.cos(a) * l, cy + math.sin(a) * l)
        base = (cx - math.cos(a) * l * 0.3, cy - math.sin(a) * l * 0.3)
        n = (-math.sin(a) * l * 0.16, math.cos(a) * l * 0.16)
        d.polygon([tip, (base[0] + n[0], base[1] + n[1]), (base[0] - n[0], base[1] - n[1])], fill=(225, 245, 255, 235))
    glow = layer.filter(ImageFilter.GaussianBlur(S / 50))
    return Image.alpha_composite(glow, layer)

def frame(img):
    d = ImageDraw.Draw(img)
    w = int(S * 0.045)
    for i in range(w):
        t = i / w
        c = int(70 + 110 * (1 - abs(t - 0.5) * 2))
        d.rectangle((i, i, S - 1 - i, S - 1 - i), outline=(c, c + 4, c + 10, 255))
    d.rectangle((w, w, S - 1 - w, S - 1 - w), outline=(20, 24, 36, 255), width=max(2, S // 170))
    return img

def main(out64, out512):
    img = background()
    om = omega_mask()
    crack = crack_mask(int(S * 0.055))
    body = ImageChops.subtract(om, crack)
    # Outer glow of the omega, dark edge, then gold body with a bright rim.
    glow = Image.new("RGBA", (S, S), (255, 200, 90, 0)); glow.putalpha(body.filter(ImageFilter.GaussianBlur(S / 30)).point(lambda v: int(v * 0.8)))
    img = Image.alpha_composite(img, glow)
    edge = Image.new("RGBA", (S, S), (60, 35, 5, 255)); edge.putalpha(body.filter(ImageFilter.MaxFilter(int(S * 0.012) | 1)))
    img = Image.alpha_composite(img, edge)
    gold = gold_fill(); gold.putalpha(body)
    img = Image.alpha_composite(img, gold)
    rim = Image.new("RGBA", (S, S), (255, 250, 220, 255))
    rim.putalpha(ImageChops.subtract(body, body.filter(ImageFilter.MinFilter(int(S * 0.012) | 1))).point(lambda v: int(v * 0.7)))
    img = Image.alpha_composite(img, rim)
    # Ice inside the crack, confined to the omega, plus its glow spilling out.
    ice_in = ImageChops.multiply(om, crack)
    ice_glow = Image.new("RGBA", (S, S), (140, 220, 255, 0)); ice_glow.putalpha(ice_in.filter(ImageFilter.GaussianBlur(S / 45)))
    img = Image.alpha_composite(img, ice_glow)
    ice = Image.new("RGBA", (S, S), (150, 215, 255, 255)); ice.putalpha(ImageChops.multiply(om, crack_mask(int(S * 0.048))))
    img = Image.alpha_composite(img, ice)
    core = Image.new("RGBA", (S, S), (235, 250, 255, 255)); core.putalpha(ImageChops.multiply(om, crack_mask(int(S * 0.02))))
    img = Image.alpha_composite(img, core)
    img = Image.alpha_composite(img, shards())
    img = frame(img)
    img.resize((512, 512), Image.LANCZOS).save(out512)
    img.resize((64, 64), Image.LANCZOS).save(out64)

if __name__ == "__main__":
    images = pathlib.Path(__file__).resolve().parent.parent / "images"
    main(images / "icon.png", images / "icon-large.png")
