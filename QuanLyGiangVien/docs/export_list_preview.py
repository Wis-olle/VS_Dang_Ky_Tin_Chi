"""Assemble native list-preview captures using their recorded sampling intervals."""
import json
from pathlib import Path
from PIL import Image


def main():
    screenshots = Path(__file__).resolve().parent / "screenshots"
    source = screenshots / "list-preview-frames"
    timestamps = json.loads((source / "timings.json").read_text(encoding="utf-8"))
    paths = sorted(source.glob("frame-*.png"))
    if len(paths) != len(timestamps) or len(paths) < 2:
        raise ValueError("Native frame count must match captured timestamps.")
    durations = [max(10, timestamps[i + 1] - timestamps[i]) for i in range(len(paths) - 1)]
    durations.append(700)
    region = json.loads((source / "capture-region.json").read_text(encoding="utf-8"))
    frames = []
    try:
        for path in paths:
            with Image.open(path) as image:
                frames.append(image.crop(tuple(region)).convert("RGB"))
        output = screenshots / "29-interactive-list-preview.gif"
        frames[0].save(output, save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=2)
        print(output)
    finally:
        for frame in frames:
            frame.close()


if __name__ == "__main__":
    main()
