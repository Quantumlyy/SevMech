"""Renders images/social.png, the 1280x640 GitHub social preview, from the icon artwork.
GitHub crops previews on some surfaces, so everything stays 80px inside the edges."""
import math, pathlib
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import make_icon

W, H = 1280, 640
INTER = "/usr/share/fonts/opentype/inter/Inter-{}.otf"

def glow(cx, cy, color, radius):
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(40, 0, -1):
        r = radius * i / 40
        a = int(color[3] * (1 - i / 40) ** 1.6)
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color[:3] + (a,))
    return layer.filter(ImageFilter.GaussianBlur(H / 40))

def background():
    img = Image.new("RGBA", (W, H), (8, 10, 24, 255))
    d = ImageDraw.Draw(img)
    cols = [(120, 20, 40, 255), (20, 70, 50, 255), (70, 25, 110, 255)]
    step = H // 6
    k = 0
    for y in range(-step, H + step, step):
        for x in range(-step, W + step, step):
            c = cols[k % 3]; k += 1
            d.polygon([(x, y - step // 2), (x + step // 2, y), (x, y + step // 2), (x - step // 2, y)], fill=c)
    img = img.filter(ImageFilter.GaussianBlur(4))
    img = Image.alpha_composite(img, Image.new("RGBA", (W, H), (8, 10, 24, 175)))
    rays = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rays)
    for i in range(11):
        ang = math.radians(-90 + (i - 5) * 11)
        rd.line([(W * 0.28, -H * 0.1), (W * 0.28 + math.cos(ang) * W, -H * 0.1 + math.sin(ang) * W)],
                fill=(255, 120, 200, 55), width=int(H * 0.03))
    img = Image.alpha_composite(img, rays.filter(ImageFilter.GaussianBlur(H / 60)))
    img = Image.alpha_composite(img, glow(W * 0.15, H * 0.15, (230, 50, 120, 150), H * 0.9))
    img = Image.alpha_composite(img, glow(W * 0.85, H * 0.10, (190, 225, 255, 120), H * 0.8))
    img = Image.alpha_composite(img, glow(W * 0.55, H * 1.0, (40, 200, 255, 170), H * 1.0))
    return img

def main(out):
    img = background()
    icon = make_icon.render().resize((420, 420), Image.LANCZOS)
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 170), (118, 122, 118 + 420, 122 + 420))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(18)))
    img.alpha_composite(icon, (110, 110))

    d = ImageDraw.Draw(img)
    title = ImageFont.truetype(INTER.format("Bold"), 128)
    sub = ImageFont.truetype(INTER.format("SemiBold"), 44)
    body = ImageFont.truetype(INTER.format("Regular"), 30)
    x = 600
    d.text((x + 4, 154), "SevMech", font=title, fill=(0, 0, 0, 140))
    d.text((x, 150), "SevMech", font=title, fill=(255, 212, 110, 255))
    d.text((x + 6, 300), "Severe Mechanics", font=sub, fill=(225, 245, 255, 255))
    d.text((x + 6, 372), "FFXIV ultimate raid mechanics simulator.", font=body, fill=(205, 212, 230, 255))
    d.text((x + 6, 414), "Practice solo or with friends from any inn.", font=body, fill=(205, 212, 230, 255))
    d.text((x + 6, 480), "A fork of AnoMech by Anomek", font=body, fill=(150, 158, 180, 255))
    img.convert("RGB").save(out)

if __name__ == "__main__":
    main(pathlib.Path(__file__).resolve().parent.parent / "images" / "social.png")
