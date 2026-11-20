#!/usr/bin/env python3
"""Generates the placeholder music loops (CREATIVE_DIRECTION.md §5, CD-7). No third-party material.

  ambient_loop.wav  30 s: wind, a low drone, a slow three-note cello-like motif, a distant bell
  boss_loop.wav     16 s: low drums at 75 bpm and a swelling low brass-like chord

Both are seamless loops: every tone completes whole cycles in the loop and the wind is crossfaded
over the seam. Output: Assets/_Game/Resources/Audio/, 44.1 kHz mono 16-bit. Deterministic (fixed seed).
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "_Game" / "Resources" / "Audio"


def loop_freq(f, seconds):
    """Nearest frequency that completes whole cycles in the loop (no click at the seam)."""
    return round(f * seconds) / seconds


def env(t, start, attack, length):
    """0 → 1 over attack, then a smooth fall to 0 at start + length."""
    x = t - start
    if x < 0 or x > length:
        return 0.0
    if x < attack:
        return x / attack
    return 0.5 * (1 + math.cos(math.pi * (x - attack) / (length - attack)))


def wind(n, seconds, rng):
    out, lp, lp2 = [0.0] * n, 0.0, 0.0
    for i in range(n):
        lp += 0.02 * (rng.uniform(-1, 1) - lp)
        lp2 += 0.05 * (lp - lp2)
        gust = 0.6 + 0.4 * math.sin(2 * math.pi * i / RATE * 3 / seconds)
        out[i] = lp2 * gust * 6.0
    fade = RATE  # crossfade the last second into the first
    for k in range(fade):
        a = k / fade
        out[k] = out[k] * a + out[n - fade + k] * (1 - a)
    return out


def ambient(seconds=30.0):
    n = int(RATE * seconds)
    rng = random.Random(7)
    out = wind(n, seconds, rng)
    drone = [loop_freq(f, seconds) for f in (55.0, 82.41)]
    motif = [(0.0, 146.83), (10.0, 174.61), (20.0, 220.0)]       # D3, F3, A3
    bells = [3.75 + 7.5 * k for k in range(4)]
    for i in range(n):
        t = i / RATE
        s = 0.18 * math.sin(2 * math.pi * drone[0] * t) + 0.10 * math.sin(2 * math.pi * drone[1] * t)
        for start, f in motif:
            e = env(t, start, 1.5, 9.5)
            if e:
                s += 0.22 * e * (math.sin(2 * math.pi * f * t) + 0.35 * math.sin(4 * math.pi * f * t) + 0.12 * math.sin(6 * math.pi * f * t))
        for b in bells:
            x = t - b
            if 0 <= x < 4:
                d = math.exp(-x * 1.4)
                s += 0.10 * d * (math.sin(2 * math.pi * 880 * x) + 0.5 * math.sin(2 * math.pi * 2210 * x))
        out[i] += s
    return out


def boss(seconds=16.0):
    n = int(RATE * seconds)
    rng = random.Random(11)
    out = [0.0] * n
    beat = 60.0 / 75.0
    chord = [loop_freq(f, seconds) for f in (73.42, 110.0, 146.83, 174.61)]   # D2 A2 D3 F3
    lp = 0.0
    for i in range(n):
        t = i / RATE
        # drums
        x = t % beat
        k = int(t / beat)
        accent = 1.0 if k % 4 == 0 else 0.6
        f = 45 + 45 * math.exp(-x * 18)
        s = accent * 0.9 * math.exp(-x * 7) * math.sin(2 * math.pi * f * x)
        s += accent * 0.15 * math.exp(-x * 40) * rng.uniform(-1, 1)
        # brass: sawtooth chord through a one-pole low-pass, swelling every 8 s
        saw = sum(2 * ((c * t) % 1.0) - 1 for c in chord) / len(chord)
        lp += 0.06 * (saw - lp)
        swell = 0.55 + 0.45 * math.sin(2 * math.pi * t / 8.0 - math.pi / 2)
        out[i] = s + 0.5 * swell * lp
    return out


def write(name, samples, peak_db):
    peak = max(abs(v) for v in samples) or 1.0
    gain = 10 ** (peak_db / 20) / peak
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / name
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v * gain)) * 32767)) for v in samples))
    print(f"{name} {len(samples) / RATE:.1f} s peak {peak_db:.0f} dB")


if __name__ == "__main__":
    write("ambient_loop.wav", ambient(), -6)
    write("boss_loop.wav", boss(), -3)
