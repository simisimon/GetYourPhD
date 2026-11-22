"""Builds Assets/Resources/Audio from the downloaded CC0 / public-domain sources.

usage: python prep_audio.py <download_root> <unity_project>
  <download_root> holds dl/ (single files) and ex/ (extracted packs) as fetched from
  OpenGameArt and Wikimedia Commons (see Assets/Resources/Audio/CREDITS.md for every source).

SFX are trimmed, peak-normalised, faded and written as mono WAV (Unity compresses them on import).
A sound with several sources becomes <id>_1.wav, <id>_2.wav ...; the game picks one at random.
Music is written as OGG Vorbis.
Needs numpy + soundfile.
"""
import sys, os, glob
import numpy as np
import soundfile as sf

ROOT, PROJ = sys.argv[1], sys.argv[2]
OUT = os.path.join(PROJ, "Assets", "Resources", "Audio")

def src(p):
    m = glob.glob(os.path.join(ROOT, p), recursive=True)
    if not m: raise SystemExit("missing source " + p)
    return m[0]

# id: (list of sources | list of lists to concatenate, options)
# options: t=(start,end) trim, peak=target peak, mono, fade=seconds, loop=True (no fade-in/out clicks), sr=resample hint
KI = "ex/kenney_interfaceSounds/Audio/"
C0 = "ex/100-CC0-SFX_0/"
BF = "ex/sfx_breaking_and_falling/"
QB = "ex/qubodup-bottle-and-drink-sounds/**/"
WS = "ex/workshop/**/workshop - "
RPG = "ex/RPGsounds_Kenney/OGG/"
SFX = {
    # UI / gates
    "ui_click":      ([KI + "click_002.ogg"], {}),
    "ui_confirm":    ([KI + "confirmation_001.ogg"], {}),
    "key_beep":      ([KI + "select_003.ogg"], {"peak": 0.5}),
    "key_wrong":     ([KI + "error_006.ogg"], {"t": (0, 0.3)}),
    "gate_open":     ([C0 + "door_open.ogg"], {}),
    "jingle_win":    (["ex/ogg_cozy_puzzle_jingle_result/**/Cozy Puzzle Clear (Jingle).ogg"], {"mono": False, "peak": 0.75}),
    "jingle_fail":   (["ex/ogg_cozy_puzzle_jingle_result/**/Cozy Puzzle Failure (Jingle).ogg"], {"mono": False, "peak": 0.75}),
    "applause":      (["dl/applause-clapping-church-crowd-immersive.wav"], {"t": (0, 9), "fade": 2.5, "mono": False, "peak": 0.7}),
    "deadline":      ([C0 + "gong_02.ogg"], {}),
    "bell":          (["dl/bell.wav"], {"peak": 0.6}),
    # Johannes
    "jump":          ([KI + "pluck_001.ogg"], {"peak": 0.55}),
    "jump2":         ([KI + "pluck_002.ogg"], {"peak": 0.55}),
    "mate_drink":    ([[QB + "bottle-open-03.flac", QB + "swallow-03.flac", QB + "swallow-04.flac"]], {"peak": 0.8}),
    "mate_throw":    ([f"ex/swishes/**/swish-{i}.wav" for i in (3, 4, 7, 9)], {"peak": 0.55}),
    "mate_shatter":  ([BF + f"bfh1_glass_breaking_0{i}.ogg" for i in (1, 2, 3)], {"t": (0, 1.2), "peak": 0.6}),
    "mate_pickup":   ([C0 + f"glass_0{i}.ogg" for i in (1, 2, 3)], {"t": (0, 0.6), "peak": 0.6}),
    "mate_empty":    ([KI + "error_004.ogg"], {"peak": 0.5}),
    "pickup":        ([KI + "select_001.ogg", KI + "select_002.ogg"], {"peak": 0.45}),
    "pickup_big":    ([RPG + f"bookFlip{i}.ogg" for i in (1, 2, 3)], {"peak": 0.8}),
    "flash":         (["dl/photo.ogg"], {"peak": 0.75}),
    "hurt":          ([C0 + f"hit_0{i}.ogg" for i in (1, 2, 3)], {"t": (0, 0.6), "peak": 0.8}),
    "fall":          ([BF + "bfh1_falling_01.ogg"], {"t": (0, 1.2), "peak": 0.7}),
    "stomp":         ([C0 + "plop_01.ogg", C0 + "plop_02.ogg"], {"peak": 0.8}),
    "switch":        ([C0 + "switch_01.ogg", C0 + "switch_02.ogg"], {"peak": 0.7}),
    "reveal":        ([KI + "maximize_004.ogg"], {"peak": 0.6}),
    "toast_good":    ([KI + "confirmation_002.ogg"], {"peak": 0.55}),
    "toast_bad":     ([KI + "error_002.ogg"], {"peak": 0.5}),
    "submit":        ([RPG + "bookClose.ogg"], {"peak": 0.9}),
    # Level 1 boss
    "boss_hit":      ([BF + f"bfh1_metal_hit_0{i}.ogg" for i in (1, 2, 3)], {"t": (0, 0.8), "peak": 0.75}),
    "boss_block":    ([C0 + "metal_01.ogg"], {"t": (0, 0.6), "peak": 0.5}),
    "boss_die":      ([C0 + "explosion.ogg"], {"peak": 0.8}),
    # Level 2
    "typing":        (["ex/unicae_games_keyboard_soundpack_1_0/**/human_vel-006.wav"], {"t": (0.09, 5.3), "loop": True, "peak": 0.6}),
    "baby_cry":      (["dl/wm_Baby_long_wanting_cry.ogg.mp3"], {"t": (0.0, 18.3), "loop": True, "peak": 0.75}),
    "washer":        (["dl/washer01.wav"], {"t": (20, 30), "loop": True, "peak": 0.6}),
    "splash":        (["ex/ezwa-water_splash/**/water_splash-01.flac", "ex/ezwa-water_splash/**/water_splash-04.flac"], {"peak": 0.7}),
    "doorbell":      (["dl/doorbell.wav"], {"peak": 0.8}),
    "tools_fall":    ([WS + "tool clatter.wav", WS + "metal clatter.wav", WS + "metal drop.wav"], {"t": (0, 1.4), "peak": 0.75}),
    "tools_hang":    ([WS + "jingle.wav"], {"t": (0, 1.0), "peak": 0.6}),
    "fridge":        ([C0 + "microwave_door_open.ogg"], {"peak": 0.7}),
    "wife_order":    ([KI + "question_001.ogg"], {"peak": 0.6}),
    "wife_joke":     ([KI + "question_002.ogg"], {"peak": 0.5}),
    "cooking":       (["dl/fryingpan01.wav"], {"t": (5, 17), "loop": True, "peak": 0.5}),
    # Level 3
    "engine":        (["dl/loop_0.wav"], {"loop": True, "peak": 0.6}),
    "engine_start":  (["dl/generic_car_startengine.wav"], {"peak": 0.7}),
    "crash":         (["dl/qubodup-crash.ogg"], {"peak": 0.9}),
    "horn":          (["dl/car2.wav"], {"peak": 0.7}),
    "moo":           (["ex/qubodup-sci-fi-cow-alien-soundpack/**/moo-notification.wav"], {"peak": 0.8}),
    "fuel":          ([QB + "bottle-shake-10.flac"], {"peak": 0.7}),
    "postcard":      ([KI + "confirmation_004.ogg"], {"peak": 0.6}),
    "barrier":       ([C0 + "switch_02.ogg"], {"peak": 0.7}),
    "repair":        ([WS + "ratchet1.wav", WS + "metal hammering.wav"], {"t": (0, 1.6), "peak": 0.7}),
    # Level 4
    "bounce":        ([KI + "pluck_001.ogg", KI + "pluck_002.ogg"], {"peak": 0.35}),
    "spring":        (["dl/boing.flac"], {"t": (0, 0.9), "peak": 0.6}),
    "crumble":       ([BF + "bfh1_wood_breaking_01.ogg", BF + "bfh1_wood_breaking_02.ogg"], {"t": (0, 1.0), "peak": 0.7}),
    "paper_hit":     ([C0 + f"paper_0{i}.ogg" for i in (1, 2, 3)], {"peak": 0.8}),
    "coffee":        ([C0 + "splash_01.ogg", C0 + "splash_02.ogg"], {"peak": 0.7}),
    "boss_phase":    ([C0 + "gong_01.ogg"], {"peak": 0.6}),
    "boss_throw":    (["dl/whoosh2_0.wav"], {"t": (0, 0.7), "peak": 0.4}),
}

MUSIC = {
    "start":   "dl/cozy_puzzle_title_bpm95_0.ogg",
    "level1":  "dl/cozy_puzzle_in-game_1_bpm118_0.ogg",
    "level2":  "dl/cozy_puzzle_in-game_2_bpm90_0.ogg",
    "night":   "dl/hush_hamlet_loop_0.wav",
    "level3":  "dl/road_trip.wav",
    "level4":  "dl/cozy_puzzle_in-game_3_bpm108_0.ogg",
    "boss":    "dl/Boss Fight Bounce.mp3",
    "victory": "ex/ogg_cozy_puzzle_jingle_result/**/Cozy Puzzle Clear (Loop)_BPM85.ogg",
}

def load(p):
    d, sr = sf.read(src(p), always_2d=True, dtype="float32")
    return d, sr

def resample(d, sr, to):
    if sr == to: return d
    n = int(round(d.shape[0] * to / sr))
    x = np.linspace(0, d.shape[0] - 1, n)
    return np.stack([np.interp(x, np.arange(d.shape[0]), d[:, c]) for c in range(d.shape[1])], axis=1).astype("float32")

def process(sources, o):
    if isinstance(sources, list):
        parts = []
        for s in sources:
            d, sr = load(s); d = resample(d, sr, 44100)
            parts.append(d.mean(axis=1, keepdims=True) if d.shape[1] > 1 else d)
        d, sr = np.concatenate(parts), 44100
    else:
        d, sr = load(sources)
    if "t" in o:
        a, b = o["t"]; d = d[int(a * sr): int(b * sr)]
    if o.get("mono", True) and d.shape[1] > 1: d = d.mean(axis=1, keepdims=True)
    if not o.get("loop"):
        # trim trailing silence
        amp = np.abs(d).max(axis=1)
        nz = np.where(amp > 0.004)[0]
        if len(nz): d = d[: nz[-1] + int(0.02 * sr)]
        f = min(int(o.get("fade", 0.03) * sr), d.shape[0] // 2)
        if f > 0: d[-f:] *= np.linspace(1, 0, f)[:, None]
    else:
        f = int(0.03 * sr)  # crossfade the loop seam
        head, tail = d[:f].copy(), d[-f:].copy()
        w = np.linspace(0, 1, f)[:, None]
        d = d[:-f].copy(); d[:f] = head * w + tail * (1 - w)
    peak = np.abs(d).max()
    if peak > 0: d = d * (o.get("peak", 0.8) / peak)
    return d, sr

def main():
    sfx_dir, mus_dir = os.path.join(OUT, "Sfx"), os.path.join(OUT, "Music")
    os.makedirs(sfx_dir, exist_ok=True); os.makedirs(mus_dir, exist_ok=True)
    for f in glob.glob(os.path.join(sfx_dir, "*.wav")) + glob.glob(os.path.join(mus_dir, "*.ogg")): os.remove(f)
    for sid, (sources, o) in (SFX.items() if "--music-only" not in sys.argv else []):
        names = [sid] if len(sources) == 1 else [f"{sid}_{i + 1}" for i in range(len(sources))]
        for n, s in zip(names, sources):
            d, sr = process(s, o)
            sf.write(os.path.join(sfx_dir, n + ".wav"), d, sr, subtype="PCM_16")
        print(f"sfx {sid}: {len(names)} file(s)")
    for mid, p in MUSIC.items():
        d, sr = load(p)
        if sr != 44100: d = resample(d, sr, 44100); sr = 44100
        peak = np.abs(d).max(); d = d * (0.9 / peak) if peak > 0.9 else d
        # libsndfile crashes on large single Vorbis writes, so write in blocks
        with sf.SoundFile(os.path.join(mus_dir, mid + ".ogg"), "w", sr, d.shape[1], format="OGG", subtype="VORBIS") as f:
            for i in range(0, d.shape[0], 8192): f.write(np.ascontiguousarray(d[i:i + 8192]))
        print(f"music {mid}: {d.shape[0] / sr:.0f}s")

main()
