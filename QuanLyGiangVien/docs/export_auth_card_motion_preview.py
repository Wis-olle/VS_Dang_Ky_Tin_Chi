"""Assemble native Auth Card captures with their recorded sampling intervals."""
import json
from pathlib import Path
from PIL import Image


def main():
    screenshots = Path(__file__).resolve().parent / "screenshots"
    source = screenshots / "auth-card-motion-frames"
    timestamps = json.loads((source / "timings.json").read_text(encoding="utf-8"))
    paths = sorted(source.glob("frame-*.png"))
    if len(paths) != len(timestamps) or len(paths) < 2:
        raise ValueError("Native frame count must match the captured timestamps.")
    durations = [max(10, timestamps[i + 1] - timestamps[i]) for i in range(len(paths) - 1)]
    durations.append(700)
    frames = []
    for path in paths:
        with Image.open(path) as image:
            frames.append(image.convert("RGB"))
    output = screenshots / "26-auth-card-motion.gif"
    try:
        frames[0].save(output, save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=2)
    finally:
        for frame in frames:
            frame.close()
    print(output)


if __name__ == "__main__":
    main()
