"""Renders the mod's about/icon.png and about/preview.png (original art: no game or Archipelago assets).

Needs Pillow and the Windows fonts Constantia and Segoe UI. Usage:
    python tools/make_mod_art.py mod/SoDArchipelago/about
"""
import math
import random
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = sys.argv[1]
SS = 4  # supersampling factor

TOP = (18, 14, 44)
BOTTOM = (58, 28, 86)
ISLAND = (42, 36, 84)
ISLAND_RIM = (150, 128, 230)
NODE = (255, 236, 250)
ROUTE = (214, 190, 255)
GLOW = (190, 140, 255)


def gradient(w, h):
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        c = tuple(int(TOP[i] + (BOTTOM[i] - TOP[i]) * t) for i in range(3))
        for x in range(w):
            px[x, y] = c
    return img


def blob(cx, cy, r, seed, points=48):
    rng = random.Random(seed)
    phases = [rng.uniform(0, math.tau) for _ in range(3)]
    amps = [0.10, 0.06, 0.04]
    pts = []
    for i in range(points):
        a = math.tau * i / points
        k = 1 + sum(amp * math.sin((n + 2) * a + ph) for n, (amp, ph) in enumerate(zip(amps, phases)))
        pts.append((cx + r * k * math.cos(a), cy + r * 0.62 * k * math.sin(a)))
    return pts


def bezier(p0, p1, bend, steps=200):
    mx, my = (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    c = (mx - dy * bend, my + dx * bend)
    out = []
    for i in range(steps + 1):
        t = i / steps
        out.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * p1[0],
                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * p1[1]))
    return out


def dotted(draw, path, spacing, radius, color):
    acc = 0.0
    prev = path[0]
    for p in path[1:]:
        acc += math.dist(prev, p)
        prev = p
        if acc >= spacing:
            acc = 0.0
            draw.ellipse((p[0] - radius, p[1] - radius, p[0] + radius, p[1] + radius), fill=color)


def glow_layer(size, shapes, blur):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for kind, args, color in shapes:
        getattr(d, kind)(args, fill=color)
    return layer.filter(ImageFilter.GaussianBlur(blur))


def scene(w, h, islands, routes, stars, star_seed, node_r, route_w, keep_clear=None):
    """islands: [(cx, cy, r, seed)], routes: [(i, j, bend)], all in final-pixel units."""
    W, H = w * SS, h * SS
    img = gradient(W, H).convert("RGBA")

    rng = random.Random(star_seed)
    sd = ImageDraw.Draw(img)
    for _ in range(stars):
        x, y = rng.uniform(0, W), rng.uniform(0, H * 0.9)
        if keep_clear and keep_clear[0] * SS <= x <= keep_clear[2] * SS and keep_clear[1] * SS <= y <= keep_clear[3] * SS:
            continue
        r = rng.choice([0.6, 0.8, 1.0, 1.4]) * SS
        a = rng.randint(90, 230)
        sd.ellipse((x - r, y - r, x + r, y + r), fill=(255, 245, 255, a))

    nodes = [(cx * SS, (cy - r * 0.08) * SS) for cx, cy, r, _ in islands]

    # soft haze under each island
    img.alpha_composite(glow_layer((W, H), [
        ("ellipse", (cx * SS - r * 1.5 * SS, cy * SS - r * 0.8 * SS, cx * SS + r * 1.5 * SS, cy * SS + r * 0.9 * SS),
         GLOW + (70,)) for cx, cy, r, _ in islands], 28 * SS))

    # routes
    rl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rl)
    for i, j, bend in routes:
        dotted(rd, bezier(nodes[i], nodes[j], bend), route_w * 3.2 * SS, route_w * SS, ROUTE + (235,))
    img.alpha_composite(rl.filter(ImageFilter.GaussianBlur(route_w * 1.5 * SS)))
    img.alpha_composite(rl)

    # islands: rim, body, top highlight
    for cx, cy, r, seed in islands:
        body = blob(cx * SS, cy * SS, r * SS, seed)
        rim = blob(cx * SS, cy * SS, r * 1.07 * SS, seed)
        top = blob(cx * SS, (cy - r * 0.12) * SS, r * 0.8 * SS, seed + 1)
        d = ImageDraw.Draw(img)
        d.polygon(rim, fill=ISLAND_RIM + (255,))
        d.polygon(body, fill=ISLAND + (255,))
        d.polygon(top, fill=(62, 52, 118, 255))

    # glowing nodes
    img.alpha_composite(glow_layer((W, H), [
        ("ellipse", (x - node_r * 4 * SS, y - node_r * 4 * SS, x + node_r * 4 * SS, y + node_r * 4 * SS), GLOW + (200,))
        for x, y in nodes], node_r * 2.5 * SS))
    d = ImageDraw.Draw(img)
    for x, y in nodes:
        d.ellipse((x - node_r * SS, y - node_r * SS, x + node_r * SS, y + node_r * SS), fill=NODE + (255,))
    return img


def icon():
    islands = [(40, 46, 22, 3), (92, 58, 20, 7), (58, 96, 24, 11)]
    routes = [(0, 1, -0.25), (1, 2, -0.25), (2, 0, -0.25)]
    img = scene(128, 128, islands, routes, stars=22, star_seed=5, node_r=4.2, route_w=1.6)
    img.convert("RGB").resize((128, 128), Image.LANCZOS).save(f"{OUT}/icon.png", optimize=True)


def preview():
    w, h = 1280, 720
    islands = [(760, 470, 70, 3), (960, 380, 58, 7), (1130, 520, 64, 11), (900, 610, 46, 19), (1110, 250, 42, 23)]
    routes = [(0, 1, -0.2), (1, 2, -0.2), (2, 3, -0.25), (3, 0, -0.2), (1, 4, 0.2), (0, 2, 0.12)]
    img = scene(w, h, islands, routes, stars=260, star_seed=9, node_r=7, route_w=2.6,
                keep_clear=(60, 130, 700, 370))

    d = ImageDraw.Draw(img)
    title = ImageFont.truetype("C:/Windows/Fonts/constanb.ttf", 118 * SS)
    sub = ImageFont.truetype("C:/Windows/Fonts/segoeuil.ttf", 40 * SS)
    x = 80 * SS
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).text((x, 150 * SS), "Archipelago", font=title, fill=GLOW + (180,))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(14 * SS)))
    d.text((x, 150 * SS), "Archipelago", font=title, fill=NODE + (255,))
    d.text((x + 6 * SS, 300 * SS), "Shape of Dreams", font=sub, fill=(222, 206, 255, 255))
    img.convert("RGB").resize((w, h), Image.LANCZOS).save(f"{OUT}/preview.png", optimize=True)


icon()
preview()
