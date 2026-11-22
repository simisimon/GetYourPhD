"""Turns the raw PixelLab downloads (PixelLabRaw/road) into Assets/Art/Road sprites + procedural road tiles."""
import os, random, colorsys
from PIL import Image, ImageDraw

RAW = 'C:/Unity/GetYourPhD/PixelLabRaw/road'
OUT = 'C:/Unity/GetYourPhD/Assets/Art/Road'
os.makedirs(OUT, exist_ok=True)

def load(n): return Image.open(f'{RAW}/{n}.png').convert('RGBA')
def crop(im):
    bb = im.getbbox()
    return im.crop(bb) if bb else im
def save(im, n): im.save(f'{OUT}/{n}.png')

# --- vehicles: PixelLab drew them facing down; the road goes up ---
for n in ['camper', 'sportscar', 'vespa', 'smallcar', 'wagon']:
    save(crop(load(n).rotate(180)), n)

# Reviewer 2: rotate, then put the "2" back the right way up
r2 = load('reviewer2')
patch = r2.crop((14, 18, 34, 39))
rot = r2.rotate(180)
rot.paste(patch, (48 - 34, 96 - 39))
save(crop(rot), 'reviewer2')

# traffic colour variants of the wagon (hue shift on saturated pixels)
def hue_shift(im, dh, sat=1.0, val=1.0):
    im = im.copy(); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0: continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s > 0.35:
                h = (h + dh) % 1.0; s = min(1, s * sat); v = min(1, v * val)
            rr, gg, bb = colorsys.hsv_to_rgb(h, s, v)
            px[x, y] = (int(rr * 255), int(gg * 255), int(bb * 255), a)
    return im
wagon = Image.open(f'{OUT}/wagon.png')
save(hue_shift(wagon, 0.45), 'car_red')
save(hue_shift(wagon, 0.75, 0.8), 'car_green')
save(hue_shift(wagon, 0.0, 0.15, 1.15), 'car_silver')

# --- dented camper variants (dark dents + scratches only on the body) ---
camper = Image.open(f'{OUT}/camper.png')
def dents(im, spots, seed):
    im = im.copy(); px = im.load(); rnd = random.Random(seed)
    w, h = im.size
    for (cx, cy, rad) in spots:
        for y in range(cy - rad, cy + rad + 1):
            for x in range(cx - rad, cx + rad + 1):
                if not (0 <= x < w and 0 <= y < h): continue
                if (x - cx) ** 2 + (y - cy) ** 2 > rad * rad + rnd.randint(-2, 2): continue
                r, g, b, a = px[x, y]
                if a < 200: continue
                dd = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5 / max(1, rad)
                k = 0.35 + 0.35 * dd if (x * 3 + y) % 4 else 0.3
                px[x, y] = (int(r * k), int(g * k), int(b * k * 1.05), a)
        # scratch
        for i in range(rad + 3):
            x, y = cx - rad + i, cy + rad // 2 - i // 2
            if 0 <= x < w and 0 <= y < h and px[x, y][3] > 200:
                px[x, y] = (70, 70, 80, 255)
    return im
w, h = camper.size
d1 = dents(camper, [(int(w * 0.2), int(h * 0.3), 6), (int(w * 0.78), int(h * 0.8), 5)], 1)
d2 = dents(d1, [(int(w * 0.75), int(h * 0.42), 6), (int(w * 0.28), int(h * 0.68), 6), (int(w * 0.55), int(h * 0.1), 5)], 2)
save(d1, 'camper_dent1'); save(d2, 'camper_dent2')

# --- everything else: crop only ---
for n in ['cow', 'rock', 'jerry', 'paper', 'stamp', 'citation', 'pothole', 'barrier', 'gas', 'workshop', 'booth', 'venue',
          'tree', 'pine', 'cypress', 'paulinum', 'house_de', 'chalet', 'house_it', 'alprocks', 'olive', 'haybale']:
    if os.path.exists(f'{RAW}/{n}.png'): save(crop(load(n)), n)
for n in ['pc_de', 'pc_at', 'pc_it', 'pc_leipzig']:
    save(load(n), n)

# small postcard icons for the road: white frame + mini card + red stamp corner
def icon(src, name):
    pc = Image.new('RGBA', (30, 22), (255, 255, 255, 255))
    pc.paste(load(src).resize((26, 18), Image.LANCZOS), (2, 2))
    dr = ImageDraw.Draw(pc); dr.rectangle((22, 2, 27, 7), fill=(220, 60, 60, 255))
    dr.rectangle((0, 0, 29, 21), outline=(120, 100, 80, 255))
    save(pc, name)
icon('pc_de', 'postcard_icon'); icon('pc_de', 'postcard_icon_de'); icon('pc_at', 'postcard_icon_at'); icon('pc_it', 'postcard_icon_it'); icon('pc_leipzig', 'postcard_icon_leipzig')
blank = Image.new('RGBA', (60, 40), (235, 230, 215, 255))
dr = ImageDraw.Draw(blank); dr.rectangle((0, 0, 59, 39), outline=(140, 130, 110, 255))
dr.line((30, 6, 30, 34), fill=(160, 150, 130, 255)); dr.rectangle((48, 4, 55, 12), outline=(160, 150, 130, 255))
for yy in (14, 20, 26): dr.line((34, yy, 54, yy), fill=(180, 170, 150, 255))
save(blank, 'postcard_blank')

# --- procedural tiles (32x32) ---
def tile(name, f, w=32, h=32):
    im = Image.new('RGBA', (w, h)); px = im.load()
    for y in range(h):
        for x in range(w): px[x, y] = f(x, y)
    save(im, name)
def noise(x, y, s=0): return ((x * 73856093) ^ (y * 19349663) ^ (s * 83492791)) & 0xFF
def asphalt(base, s):
    def f(x, y):
        n = noise(x, y, s)
        k = 1.0 + ((n % 7) - 3) * 0.025
        if n % 53 == 0: k = 1.18
        return tuple(int(min(255, c * k)) for c in base) + (255,)
    return f
tile('asphalt_de', asphalt((72, 74, 80), 1))
tile('asphalt_at', asphalt((92, 92, 96), 2))
def cobble(x, y):
    # Italian old-town stone setts
    row = y // 8; off = 4 if row % 2 else 0
    if y % 8 == 0 or (x + off) % 8 == 0: return (88, 76, 66, 255)
    n = noise((x + off) // 8, row, 3)
    base = [(150, 128, 104), (138, 118, 96), (160, 138, 112)][n % 3]
    return base + (255,)
tile('asphalt_it', cobble)
def grass(a, b, flowers, s):
    def f(x, y):
        n = noise(x, y, s)
        if n % (97 if s == 5 else 41) == 0 and flowers: return flowers[n % len(flowers)] + (255,)
        return (a if (n % 5) else b) + (255,)
    return f
tile('grass_de', grass((86, 150, 70), (74, 134, 60), [(240, 220, 80)], 4))
tile('grass_at', grass((96, 162, 86), (84, 146, 76), [(250, 250, 250), (200, 140, 220)], 5))
tile('grass_it', grass((150, 150, 84), (134, 136, 74), [(220, 90, 70)], 6))
tile('pixel', lambda x, y: (255, 255, 255, 255), 2, 2)
def finish(x, y):
    return (20, 20, 20, 255) if ((x // 8) + (y // 8)) % 2 else (245, 245, 245, 255)
tile('finish_line', finish)
# roadside sign board 40x20 (blue, white border) on a post
sign = Image.new('RGBA', (44, 28), (0, 0, 0, 0)); dr = ImageDraw.Draw(sign)
dr.rectangle((20, 18, 23, 27), fill=(110, 110, 120, 255))
dr.rectangle((0, 0, 43, 19), fill=(30, 70, 160, 255), outline=(240, 240, 240, 255))
save(sign, 'sign_board')
# border gantry (stretched across the road): EU blue with yellow stars hinted
g = Image.new('RGBA', (32, 16), (30, 60, 150, 255)); dr = ImageDraw.Draw(g)
dr.rectangle((0, 0, 31, 15), outline=(230, 230, 240, 255))
save(g, 'gantry')
# warning "!" triangle
warn = Image.new('RGBA', (20, 18), (0, 0, 0, 0)); dr = ImageDraw.Draw(warn)
dr.polygon([(10, 0), (19, 17), (0, 17)], fill=(255, 200, 40, 255), outline=(60, 20, 0, 255))
dr.rectangle((9, 5, 10, 11), fill=(40, 20, 0, 255)); dr.rectangle((9, 13, 10, 14), fill=(40, 20, 0, 255))
save(warn, 'warn')
# soft round shadow for falling rocks
sh = Image.new('RGBA', (40, 30), (0, 0, 0, 0)); dr = ImageDraw.Draw(sh)
dr.ellipse((0, 0, 39, 29), fill=(0, 0, 0, 255))
save(sh, 'shadow')
print('ok', sorted(os.listdir(OUT)))
