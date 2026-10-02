"""Makes the Kill Popups sound effects.

Run it with Python 3 (no extra packages needed):

    python3 make_sounds.py

It writes the .wav files into ../KillPopups/Sounds. Change the numbers below to
make different sounds.
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "KillPopups", "Sounds")


def tone(freq, length, volume=1.0, decay=8.0, shape="sine", attack=0.004, start=0.0, slide=0.0):
    """One note. Returns (start time, samples)."""
    samples = []
    phase = 0.0
    count = int(length * RATE)
    for i in range(count):
        t = i / RATE
        f = freq + slide * t
        phase += 2 * math.pi * f / RATE
        if shape == "sine":
            v = math.sin(phase)
        elif shape == "square":
            v = 0.6 if math.sin(phase) >= 0 else -0.6
        elif shape == "saw":
            v = 0.8 * (((phase / (2 * math.pi)) % 1.0) * 2 - 1)
        elif shape == "tri":
            v = 2 * abs(((phase / (2 * math.pi)) % 1.0) * 2 - 1) - 1
        else:
            raise ValueError(shape)
        env = min(1.0, t / attack) * math.exp(-decay * t)
        samples.append(v * env * volume)
    return start, samples


def noise(length, volume=1.0, decay=20.0, start=0.0, seed=1):
    rng = random.Random(seed)
    samples = []
    last = 0.0
    for i in range(int(length * RATE)):
        t = i / RATE
        # Low-passed noise sounds like a thump instead of hiss.
        last = last * 0.85 + rng.uniform(-1, 1) * 0.15
        samples.append(last * 4 * volume * math.exp(-decay * t))
    return start, samples


def mix(*parts, volume=0.8):
    length = max(int(start * RATE) + len(s) for start, s in parts)
    out = [0.0] * length
    for start, s in parts:
        offset = int(start * RATE)
        for i, v in enumerate(s):
            out[offset + i] += v
    peak = max(abs(v) for v in out) or 1.0
    fade = int(0.01 * RATE)
    result = []
    for i, v in enumerate(out):
        if i > length - fade:
            v *= (length - i) / fade
        result.append(v / peak * volume)
    return result


def save(name, samples):
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in samples))
    print("wrote", os.path.relpath(path))


def note(n):
    """Frequency of a note, n semitones above A4."""
    return 440.0 * 2 ** (n / 12)


def main():
    os.makedirs(OUT, exist_ok=True)

    # Short click, for every kill.
    save("Kill", mix(
        tone(1500, 0.08, decay=45),
        tone(2250, 0.06, 0.5, decay=60),
        noise(0.03, 0.4, decay=120),
        volume=0.55))

    # Metal "ding", for headshots.
    save("Headshot", mix(
        tone(2600, 0.45, decay=9),
        tone(3900, 0.35, 0.6, decay=12),
        tone(5470, 0.25, 0.35, decay=16),
        noise(0.02, 0.5, decay=150),
        volume=0.7))

    # Two note chime, for most medals.
    save("Medal", mix(
        tone(note(19), 0.25, decay=10, shape="tri"),
        tone(note(26), 0.4, decay=7, shape="tri", start=0.08),
        volume=0.7))

    # Fast rising notes, for multi-kills.
    save("Multikill", mix(
        tone(note(15), 0.18, 0.8, decay=14, shape="square"),
        tone(note(19), 0.18, 0.8, decay=14, shape="square", start=0.06),
        tone(note(22), 0.18, 0.8, decay=14, shape="square", start=0.12),
        tone(note(27), 0.45, decay=6, shape="square", start=0.18),
        tone(note(27) * 2, 0.45, 0.3, decay=8, start=0.18),
        volume=0.65))

    # Little fanfare, for kill streaks.
    save("Streak", mix(
        tone(note(3), 0.15, 0.8, decay=6, shape="saw"),
        tone(note(3), 0.15, 0.8, decay=6, shape="saw", start=0.13),
        tone(note(10), 0.7, decay=3, shape="saw", start=0.26),
        tone(note(14), 0.7, 0.8, decay=3, shape="saw", start=0.26),
        tone(note(17), 0.7, 0.7, decay=3, shape="tri", start=0.26),
        volume=0.6))

    # Boom and a big chord, for squad kills.
    save("SquadKill", mix(
        noise(0.5, 1.5, decay=7),
        tone(55, 0.6, 1.2, decay=5, slide=-30),
        tone(note(-2), 0.9, 0.6, decay=2.5, shape="saw", start=0.1),
        tone(note(2), 0.9, 0.6, decay=2.5, shape="saw", start=0.16),
        tone(note(5), 0.9, 0.6, decay=2.5, shape="saw", start=0.22),
        tone(note(10), 0.9, 0.7, decay=2.5, shape="tri", start=0.28),
        volume=0.75))

    # Low buzzer, for team kills and suicides.
    save("Bad", mix(
        tone(220, 0.45, decay=3, shape="saw", slide=-160),
        tone(223, 0.45, 0.8, decay=3, shape="square", slide=-160),
        volume=0.55))


if __name__ == "__main__":
    main()
