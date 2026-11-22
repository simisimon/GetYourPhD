"""Level 4 (Climb the Paulinum) art: procedural tiles + cleaned PixelLab downloads (PixelLabRaw/climb) -> Assets/Art/Climb."""
import os, random, shutil
from PIL import Image, ImageDraw

RAW = 'C:/Unity/GetYourPhD/PixelLabRaw/climb'
OUT = 'C:/Unity/GetYourPhD/Assets/Art/Climb'
JOH = 'C:/Unity/GetYourPhD/Assets/Art/Johannes/east.png'
os.makedirs(OUT, exist_ok=True)
rnd = random.Random(4)


def new(w, h, c=(0, 0, 0, 0)):
    return Image.new('RGBA', (w, h), c)


def save(im, n):
    im.save(f'{OUT}/{n}.png')


def crop(im):
    bb = im.getbbox()
    return im.crop(bb) if bb else im


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3]) + ((c[3],) if len(c) > 3 else (255,))


# ---------------- walls (64x64 tiles) ----------------
def wall_foyer():
    im = new(64, 64); d = ImageDraw.Draw(im)
    for y in range(64):
        t = y / 63
        c = (int(150 + 40 * t), int(195 + 25 * t), int(230 + 10 * t), 255)
        d.line([(0, y), (63, y)], fill=c)
    # reflections
    for i in range(3):
        x0 = 6 + i * 22
        for k in range(10):
            d.point((x0 + k, 50 - k * 2), fill=(225, 240, 255, 255))
            d.point((x0 + k + 1, 50 - k * 2), fill=(210, 232, 250, 255))
    # mullions + transoms
    for x in (0, 31, 32):
        d.line([(x, 0), (x, 63)], fill=(235, 238, 242, 255))
    d.line([(0, 0), (63, 0)], fill=(235, 238, 242, 255))
    d.line([(0, 1), (63, 1)], fill=(170, 175, 185, 255))
    d.line([(1, 0), (1, 63)], fill=(170, 175, 185, 255))
    d.line([(33, 0), (33, 63)], fill=(170, 175, 185, 255))
    return im


def wall_lecture():
    im = new(64, 64, (120, 78, 48, 255)); d = ImageDraw.Draw(im)
    for x in range(0, 64, 8):
        base = rnd.choice([(128, 84, 52), (118, 76, 46), (134, 90, 56)])
        d.rectangle([x, 0, x + 7, 63], fill=base + (255,))
        d.line([(x, 0), (x, 63)], fill=(92, 58, 34, 255))
        d.line([(x + 1, 0), (x + 1, 63)], fill=shade(base, 1.12))
        for _ in range(3):
            yy = rnd.randint(0, 60)
            d.line([(x + 3, yy), (x + 3, yy + 3)], fill=shade(base, 0.9))
    d.line([(0, 40), (63, 40)], fill=(80, 50, 30, 255))
    d.line([(0, 41), (63, 41)], fill=(150, 104, 66, 255))
    return im


BOOK = [(150, 40, 40), (40, 70, 140), (40, 110, 60), (190, 150, 50), (110, 50, 110), (180, 90, 40), (60, 60, 70), (200, 190, 160), (60, 120, 130)]


def wall_library():
    im = new(64, 64, (70, 44, 28, 255)); d = ImageDraw.Draw(im)
    for row in range(3):
        y0 = row * 21 + 2
        x = 0
        while x < 64:
            w = rnd.randint(3, 5)
            h = rnd.randint(13, 18)
            c = rnd.choice(BOOK)
            d.rectangle([x, y0 + 18 - h, x + w - 1, y0 + 17], fill=c + (255,))
            d.line([(x, y0 + 18 - h), (x, y0 + 17)], fill=shade(c, 1.25))
            d.line([(x, y0 + 21 - h), (x + w - 1, y0 + 21 - h)], fill=shade(c, 0.6))
            x += w
        d.rectangle([0, y0 + 18, 63, y0 + 20], fill=(98, 64, 40, 255))
        d.line([(0, y0 + 18), (63, y0 + 18)], fill=(130, 90, 58, 255))
    return im


def wall_aula():
    im = new(64, 64, (236, 232, 222, 255)); d = ImageDraw.Draw(im)
    for y in (0, 32):
        d.line([(0, y), (63, y)], fill=(214, 208, 196, 255))
    for (x, y) in ((0, 0), (32, 0), (16, 32), (48, 32)):
        d.line([(x, y), (x, y + 31)], fill=(214, 208, 196, 255))
    for _ in range(40):
        d.point((rnd.randint(0, 63), rnd.randint(0, 63)), fill=(228, 224, 214, 255))
    return im


# ---------------- platforms (tile horizontally) ----------------
def plat_foyer():
    im = new(32, 10); d = ImageDraw.Draw(im)
    d.rectangle([0, 1, 31, 6], fill=(170, 225, 240, 200))
    d.line([(0, 0), (31, 0)], fill=(250, 255, 255, 255))
    d.line([(0, 1), (31, 1)], fill=(210, 245, 255, 255))
    d.rectangle([0, 7, 31, 9], fill=(120, 130, 145, 255))
    d.line([(0, 9), (31, 9)], fill=(60, 66, 78, 255))
    for k in range(4):
        d.point((4 + k * 2, 4 - k // 2), fill=(240, 255, 255, 230))
    return im


def plat_lecture():
    im = new(32, 12); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 31, 3], fill=(196, 150, 96, 255))
    d.line([(0, 0), (31, 0)], fill=(228, 186, 128, 255))
    d.line([(0, 3), (31, 3)], fill=(120, 82, 48, 255))
    d.rectangle([1, 4, 30, 11], fill=(98, 66, 40, 255))
    d.line([(16, 4), (16, 11)], fill=(80, 52, 30, 255))
    d.line([(1, 11), (30, 11)], fill=(60, 38, 22, 255))
    return im


def books(colors, stamp=False):
    im = new(32, 12); d = ImageDraw.Draw(im)
    y = 0
    for i, h in enumerate((4, 4, 4)):
        x0 = rnd.randint(-2, 2)
        c = colors[i % len(colors)]
        d.rectangle([max(0, x0), y, min(31, 31 + x0), y + h - 1], fill=c + (255,))
        d.line([(0, y), (31, y)], fill=shade(c, 1.3))
        d.line([(0, y + h - 1), (31, y + h - 1)], fill=shade(c, 0.6))
        for px in range(3, 32, 9):
            d.line([(px, y + 1), (px + 3, y + 1)], fill=(230, 210, 140, 255))
        y += h
    if stamp:
        for k in range(-2, 34, 6):
            d.line([(k, 11), (k + 6, 0)], fill=(200, 30, 30, 160))
    return im


def plat_aula():
    im = new(32, 10, (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 31, 6], fill=(246, 244, 238, 255))
    d.line([(0, 0), (31, 0)], fill=(255, 255, 255, 255))
    for (a, b) in ((3, 2), (12, 4), (21, 1), (27, 5)):
        d.line([(a, b), (a + 4, b + 1)], fill=(205, 205, 210, 255))
    d.rectangle([0, 7, 31, 9], fill=(214, 176, 80, 255))
    d.line([(0, 9), (31, 9)], fill=(150, 116, 40, 255))
    return im


def slab():
    im = new(32, 14); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 31, 13], fill=(150, 150, 156, 255))
    d.line([(0, 0), (31, 0)], fill=(200, 200, 206, 255))
    d.line([(0, 1), (31, 1)], fill=(178, 178, 184, 255))
    d.line([(0, 13), (31, 13)], fill=(96, 96, 104, 255))
    for _ in range(14):
        d.point((rnd.randint(0, 31), rnd.randint(3, 12)), fill=(136, 136, 142, 255))
    return im


def beamer():
    im = new(48, 16); d = ImageDraw.Draw(im)
    d.rectangle([2, 0, 45, 11], fill=(196, 200, 208, 255), outline=(40, 44, 54, 255))
    d.line([(3, 1), (44, 1)], fill=(236, 238, 242, 255))
    d.rectangle([3, 9, 44, 10], fill=(150, 154, 164, 255))
    d.ellipse([34, 3, 42, 11], fill=(30, 34, 44, 255), outline=(40, 44, 54, 255))
    d.ellipse([36, 5, 40, 9], fill=(150, 220, 255, 255))
    d.point((37, 6), fill=(255, 255, 255, 255))
    for x in range(6, 28, 3):
        d.line([(x, 4), (x, 7)], fill=(110, 114, 124, 255))
    d.rectangle([8, 12, 11, 15], fill=(80, 84, 94, 255))
    d.rectangle([36, 12, 39, 15], fill=(80, 84, 94, 255))
    return im


def pipe():
    im = new(16, 48); d = ImageDraw.Draw(im)
    d.rectangle([3, 2, 12, 47], fill=(196, 202, 214, 255), outline=(60, 64, 78, 255))
    d.line([(5, 3), (5, 46)], fill=(240, 244, 250, 255))
    d.line([(10, 3), (10, 46)], fill=(150, 156, 170, 255))
    d.rectangle([2, 0, 13, 2], fill=(214, 176, 80, 255), outline=(120, 90, 30, 255))
    d.polygon([(4, 30), (11, 30), (9, 34), (6, 34)], fill=(40, 40, 52, 255))
    return im


def pillar():
    im = new(24, 64); d = ImageDraw.Draw(im)
    d.rectangle([2, 0, 21, 63], fill=(244, 242, 236, 255))
    for x in (5, 9, 13, 17):
        d.line([(x, 0), (x, 63)], fill=(214, 210, 200, 255))
    d.line([(2, 0), (2, 63)], fill=(200, 196, 186, 255))
    d.line([(21, 0), (21, 63)], fill=(190, 186, 176, 255))
    d.line([(3, 0), (3, 63)], fill=(255, 255, 255, 255))
    return im


def sign():
    im = new(48, 24); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 47, 19], fill=(30, 110, 70, 255), outline=(14, 50, 30, 255))
    d.rectangle([2, 2, 45, 17], outline=(220, 240, 225, 255))
    d.rectangle([22, 20, 25, 23], fill=(90, 96, 104, 255))
    return im


def lift():
    im = new(64, 88); d = ImageDraw.Draw(im)
    d.rectangle([0, 6, 63, 87], fill=(120, 124, 134, 255), outline=(50, 54, 64, 255))
    d.rectangle([6, 12, 57, 87], fill=(178, 184, 194, 255), outline=(70, 74, 84, 255))
    d.line([(31, 12), (31, 87)], fill=(90, 94, 104, 255))
    d.line([(32, 12), (32, 87)], fill=(214, 220, 228, 255))
    for x in (12, 38):
        d.line([(x, 14), (x + 12, 14)], fill=(220, 226, 234, 255))
    d.rectangle([22, 0, 41, 6], fill=(30, 30, 36, 255))
    d.text((26, -1), "--", fill=(255, 80, 60, 255))
    # tape cross
    for k in range(0, 52, 6):
        d.line([(6 + k, 30 + k * 0.5), (9 + k, 31.5 + k * 0.5)], fill=(240, 200, 30, 255), width=3)
    return im


def redpen():
    im = new(32, 8); d = ImageDraw.Draw(im)
    d.rectangle([4, 1, 26, 6], fill=(205, 30, 36, 255), outline=(80, 10, 14, 255))
    d.line([(5, 2), (25, 2)], fill=(255, 120, 120, 255))
    d.polygon([(27, 1), (31, 3), (31, 4), (27, 6)], fill=(230, 210, 170, 255), outline=(80, 10, 14, 255))
    d.point((31, 3), fill=(200, 20, 20, 255))
    d.rectangle([1, 1, 4, 6], fill=(40, 40, 46, 255))
    d.line([(8, 0), (16, 0)], fill=(190, 190, 196, 255))
    return im


def imposter():
    src = Image.open(JOH).convert('RGBA')
    sil = new(src.width + 4, src.height + 4)
    px = src.load(); out = sil.load()
    for y in range(src.height):
        for x in range(src.width):
            if px[x, y][3] > 40:
                for dx in (-2, -1, 0, 1, 2):
                    for dy in (-2, -1, 0, 1, 2):
                        if abs(dx) + abs(dy) <= 2 and out[x + 2 + dx, y + 2 + dy][3] == 0:
                            out[x + 2 + dx, y + 2 + dy] = (150, 80, 220, 90)
    for y in range(src.height):
        for x in range(src.width):
            if px[x, y][3] > 40:
                v = sum(px[x, y][:3]) // 3
                out[x + 2, y + 2] = (24 + v // 10, 12 + v // 14, 44 + v // 8, 235)
    # glowing eyes
    return sil


def drone2():
    im = Image.open(f'{RAW}/drone.png').convert('RGBA')
    im = crop(im)
    d = ImageDraw.Draw(im)
    cx, cy = im.width // 2, im.height // 2 + 1
    glyph = ["111", "001", "111", "100", "111"]
    for gy, row in enumerate(glyph):
        for gx, ch in enumerate(row):
            if ch == '1':
                d.point((cx - 1 + gx, cy - 2 + gy), fill=(255, 255, 255, 255))
    return im


def slide():
    im = new(96, 64); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 95, 59], fill=(246, 248, 252, 255), outline=(60, 64, 76, 255))
    d.rectangle([0, 0, 95, 8], fill=(40, 90, 160, 255))
    d.rectangle([46, 60, 49, 63], fill=(60, 64, 76, 255))
    return im


def confetti():
    im = new(4, 4, (255, 255, 255, 255))
    return im


save(wall_foyer(), 'wall_foyer')
save(wall_lecture(), 'wall_lecture')
save(wall_library(), 'wall_library')
save(wall_aula(), 'wall_aula')
save(plat_foyer(), 'plat_foyer')
save(plat_lecture(), 'plat_lecture')
save(books([(150, 40, 40), (40, 70, 140), (40, 110, 60)]), 'plat_library')
save(books([(120, 110, 110), (170, 60, 60), (100, 90, 90)], stamp=True), 'plat_reject')
save(plat_aula(), 'plat_aula')
save(slab(), 'slab')
save(beamer(), 'beamer')
save(pipe(), 'pipe')
save(pillar(), 'pillar')
save(sign(), 'sign')
save(lift(), 'lift')
save(redpen(), 'redpen')
save(imposter(), 'imposter')
save(drone2(), 'drone')
save(slide(), 'slide')
for n in ['coffee', 'configbox', 'paperstack', 'organ', 'infodesk', 'bench']:
    save(crop(Image.open(f'{RAW}/{n}.png').convert('RGBA')), n)
print('ok')
