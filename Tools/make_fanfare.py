"""Synthesizes Assets/Resources/Sfx/quest_fanfare.wav: a soft, cozy "quest done" jingle.
A rising major arpeggio (C-E-G-C) on a music-box/bell tone, then a gentle sparkle and a
warm held chord underneath. Run from the sunod-game folder:  python Tools/make_fanfare.py"""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets/Resources/Sfx/quest_fanfare.wav")


def bell(freq, dur, amp=1.0, decay=4.0):
    t = np.arange(int(SR * dur)) / SR
    # music-box partials: fundamental, a soft octave, a quiet 3rd harmonic
    tone = np.sin(2 * np.pi * freq * t) + 0.35 * np.sin(2 * np.pi * freq * 2 * t) + 0.12 * np.sin(2 * np.pi * freq * 3 * t)
    attack = np.minimum(1.0, t / 0.006)
    env = attack * np.exp(-decay * t)
    return amp * tone * env


def pad(freqs, dur, amp=0.18):
    t = np.arange(int(SR * dur)) / SR
    tone = sum(np.sin(2 * np.pi * f * t) for f in freqs) / len(freqs)
    env = np.minimum(1.0, t / 0.25) * np.exp(-1.6 * t)
    return amp * tone * env


def mix(track, sound, at):
    i = int(SR * at)
    end = min(len(track), i + len(sound))
    track[i:end] += sound[:end - i]


total = 1.9
track = np.zeros(int(SR * total))
C5, E5, G5, C6, E6, G6 = 523.25, 659.25, 783.99, 1046.5, 1318.5, 1568.0

# rising arpeggio
for k, f in enumerate([C5, E5, G5, C6]):
    mix(track, bell(f, 1.2, 0.55 if k < 3 else 0.7, decay=3.5 if k < 3 else 2.4), 0.09 * k)
# warm chord underneath the last note
mix(track, pad([C5 / 2, E5 / 2, G5 / 2], 1.5), 0.27)
# little sparkle on top
for k, f in enumerate([E6, G6, C6 * 2]):
    mix(track, bell(f, 0.5, 0.16, decay=7.0), 0.42 + 0.05 * k)

# gentle low-pass (one-pole) so it isn't shrill on phone speakers
out = np.zeros_like(track)
a = 0.35
for i in range(1, len(track)):
    out[i] = out[i - 1] + a * (track[i] - out[i - 1])
out /= np.max(np.abs(out)) + 1e-9
out *= 0.8
fade = int(SR * 0.15)
out[-fade:] *= np.linspace(1, 0, fade)

with wave.open(OUT, "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((out * 32767).astype(np.int16).tobytes())
print("wrote", OUT)
