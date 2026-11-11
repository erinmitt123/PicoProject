"""Generates Palette Room's spoken tutorial with Piper text-to-speech (offline).

Uses the same English voice and Piper setup as the SpatialML_Words project (en_US-ljspeech-medium,
trained on the public-domain LJ Speech dataset). Runs on a PC as a build step; only the .ogg files
it writes ship in the app, not Piper.

Reads  Assets/PaletteRoom/Resources/Tutorial/tutorial.json
Writes Assets/PaletteRoom/Resources/Tutorial/Voice/<step id>.ogg
       Assets/PaletteRoom/Resources/Tutorial/Voice/manifest.json (text and hash per clip)
A clip is only regenerated when its text changes, so re-running after editing a step is quick.
Each step says its "say" text if it has one, else "title. body tryIt".

Run (with the SpatialML_Words Piper venv; see its tools/generate_audio.py for setting that up):
  "<SpatialML_Words>/Builds/piper-venv/Scripts/python.exe" tools/generate_narration.py [--force]
or pass --voices <folder with en_US-ljspeech-medium.onnx> if the voices live elsewhere.
"""
import argparse, hashlib, json, os, sys

import numpy as np
import soundfile as sf
from piper import PiperVoice, SynthesisConfig

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TUTORIAL = os.path.join(ROOT, "Assets", "PaletteRoom", "Resources", "Tutorial", "tutorial.json")
OUT = os.path.join(ROOT, "Assets", "PaletteRoom", "Resources", "Tutorial", "Voice")
DEFAULT_VOICES = os.path.join(os.path.dirname(ROOT), "..", "SpatialML_Words", "Builds", "piper-voices")
VOICE = "en_US-ljspeech-medium"
LENGTH_SCALE = 1.0          # natural pace for instructions
TARGET_PEAK = 0.89          # about -1 dBFS
SILENCE = 0.01
PAD_SECONDS = 0.1
SENTENCE_GAP = 0.28         # a breath between sentences


def spoken_text(step):
    if step.get("say"):
        return step["say"].strip()
    parts = [step.get("title", "").strip().rstrip(".") + ".", step.get("body", "").strip(), step.get("tryIt", "").strip()]
    text = " ".join(p for p in parts if p and p != ".")
    # Read "+" as the word; the card shows the symbol.
    return text.replace("Press +", "Press plus").replace("each +", "each plus").replace("Each +", "Each plus")


def clip_hash(text):
    return hashlib.sha1(f"{VOICE}|{LENGTH_SCALE}|{text}".encode("utf-8")).hexdigest()[:16]


def synthesize(voice, text):
    chunks = list(voice.synthesize(text, syn_config=SynthesisConfig(length_scale=LENGTH_SCALE)))
    rate = chunks[0].sample_rate
    gap = np.zeros(int(rate * SENTENCE_GAP), dtype=np.float32)
    parts = []
    for i, chunk in enumerate(chunks):
        if i:
            parts.append(gap)
        parts.append(chunk.audio_float_array.astype(np.float32))
    audio = np.concatenate(parts)
    loud = np.flatnonzero(np.abs(audio) > SILENCE)
    if len(loud):
        pad = int(rate * PAD_SECONDS)
        audio = audio[max(0, loud[0] - pad): loud[-1] + pad]
    peak = float(np.max(np.abs(audio))) if len(audio) else 0.0
    if peak > 0:
        audio = audio * (TARGET_PEAK / peak)
    return audio, rate


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--voices", default=os.path.normpath(DEFAULT_VOICES), help="folder with the Piper voice")
    parser.add_argument("--force", action="store_true", help="regenerate every clip")
    args = parser.parse_args()

    model = os.path.join(args.voices, VOICE + ".onnx")
    if not os.path.exists(model):
        sys.exit(f"Voice not found: {model}")
    with open(TUTORIAL, encoding="utf-8") as f:
        steps = json.load(f)["steps"]
    os.makedirs(OUT, exist_ok=True)
    manifest_path = os.path.join(OUT, "manifest.json")
    manifest = {}
    if os.path.exists(manifest_path) and not args.force:
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f)

    voice = PiperVoice.load(model)
    made = kept = 0
    ids = set()
    for step in steps:
        sid = step["id"]
        ids.add(sid)
        text = spoken_text(step)
        h = clip_hash(text)
        path = os.path.join(OUT, sid + ".ogg")
        if not args.force and manifest.get(sid, {}).get("hash") == h and os.path.exists(path):
            kept += 1
            continue
        audio, rate = synthesize(voice, text)
        sf.write(path, audio, rate, format="OGG", subtype="VORBIS")
        manifest[sid] = {"text": text, "voice": VOICE, "hash": h, "seconds": round(len(audio) / rate, 2)}
        made += 1
        print(f"  {sid}: {len(audio) / rate:.1f} s")
    # Steps that were removed from the tutorial take their clips with them.
    for sid in list(manifest):
        if sid not in ids:
            manifest.pop(sid)
            stale = os.path.join(OUT, sid + ".ogg")
            if os.path.exists(stale):
                os.remove(stale)
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
    print(f"Narration: {made} made, {kept} unchanged, in {OUT}")


if __name__ == "__main__":
    main()
