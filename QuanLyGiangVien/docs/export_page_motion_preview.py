"""Export the opt-in WinForms harness frames; does not generate a mockup or capture a desktop."""
import json
from pathlib import Path

from PIL import Image


def main():
    folder = Path(__file__).resolve().parent / "screenshots"
    frame_folder = folder / "page-motion-frames"
    timestamps = json.loads((frame_folder / "timings.json").read_text(encoding="utf-8-sig"))
    frames = []
    durations = []
    for index, path in enumerate(sorted(frame_folder.glob("frame-*.png"))):
        with Image.open(path) as source:
            frame = source.convert("RGB")
            width = min(1080, frame.width)
            frame = frame.resize((width, round(frame.height * width / frame.width)), Image.Resampling.LANCZOS)
            frames.append(frame)
        durations.append(max(30, timestamps[index + 1] - timestamps[index]) if index + 1 < len(timestamps) else 1000)
    if not frames:
        raise SystemExit("Run the native LECTURER_UI_CHECK harness first.")
    output = folder / "16-page-motion.gif"
    frames[0].save(output, save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=2, optimize=False)
    print(f"{output}: {len(frames)} native frames, {output.stat().st_size} bytes")


if __name__ == "__main__":
    main()
