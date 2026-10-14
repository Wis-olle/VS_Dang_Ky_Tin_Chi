"""Rebuild the exact 48-avatar Notionists set used by QuanLyTacGia.

Default: copy untouched PNG bytes from the sibling QuanLyTacGia project when
all 48 files are present; otherwise download the documented DiceBear 9.x URLs.
Use --download to force online acquisition or --source-dir for another checkout.
Requirements: Python and Pillow. The desktop app loads packaged PNGs offline.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
import hashlib
import json
import os
from pathlib import Path
from urllib.request import Request, urlopen

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
AVATARS = ROOT / "avatars"
FONTS = ROOT / "fonts"
LICENSES = ROOT / "licenses"
SIBLING_AVATARS = ROOT.parent.parent / "QuanLyTacGia" / "Assets" / "avatars"
STYLE_PAGE = "https://www.dicebear.com/styles/notionists/"
ORIGINAL_SOURCE = "https://heyzoish.gumroad.com/l/notionists"
CC0 = "https://creativecommons.org/publicdomain/zero/1.0/"
VERIFIED_ON = "2026-10-10"
# Exact order from QuanLyTacGia/tools/Get-Avatars.ps1; do not cycle or recolor.
BACKGROUNDS = (
    "fef3c7", "fef3c7", "fce7f3", "fce7f3", "e3f1fa", "dcfce7", "ede9fe", "fef3c7", "ede9fe", "dcfce7",
    "fce7f3", "fce7f3", "fef3c7", "e3f1fa", "dcfce7", "dcfce7", "e3f1fa", "e3f1fa", "fce7f3", "fef3c7",
    "ede9fe", "fef3c7", "e3f1fa", "e3f1fa", "fef3c7", "fce7f3", "e3f1fa", "fce7f3", "ede9fe", "e3f1fa",
    "dcfce7", "e3f1fa", "e3f1fa", "e3f1fa", "fce7f3", "ede9fe", "ede9fe", "e3f1fa", "fef3c7", "e3f1fa",
    "fef3c7", "e3f1fa", "ede9fe", "dcfce7", "fef3c7", "dcfce7", "fce7f3", "dcfce7",
)


def fetch(url):
    request = Request(url, headers={"User-Agent": "LecturerUI-AssetDownloader/1.0"})
    with urlopen(request, timeout=45) as response:
        data = response.read(5_000_001)
    if len(data) > 5_000_000:
        raise ValueError("Asset exceeds the expected 5 MB limit: " + url)
    return data


def validate_png(raw, label):
    with Image.open(BytesIO(raw)) as image:
        image.load()
        if image.format != "PNG" or image.size != (128, 128) or image.mode != "RGBA":
            raise ValueError("Expected an unchanged 128 x 128 RGBA PNG: " + label)
    return hashlib.sha256(raw).hexdigest()


def prepare_avatar(index, source_dir, download):
    background = BACKGROUNDS[index - 1]
    url = (f"https://api.dicebear.com/9.x/notionists/png?seed=TG{index:04}"
           f"&size=128&backgroundColor={background}")
    source = source_dir / f"notionists-{index:02}.png"
    raw = fetch(url) if download else source.read_bytes()
    sha = validate_png(raw, url if download else str(source))
    relative_source = None if download else Path(os.path.relpath(source, ROOT.parent)).as_posix()
    entry = {
        "file": f"Assets/avatars/portrait-{index:02}.png",
        "local_source_file": relative_source, "source_project": "QuanLyTacGia",
        "source_generation_script": "../QuanLyTacGia/tools/Get-Avatars.ps1",
        "source_mode": "api-download" if download else "local-copy",
        "download_url": url, "seed": f"TG{index:04}",
        "background_color": "#" + background, "dimensions": [128, 128],
        "format": "PNG RGBA", "style": "Notionists / DiceBear 9.x", "author": "Zoish",
        "style_page": STYLE_PAGE, "original_source": ORIGINAL_SOURCE, "license": CC0,
        "source_sha256": sha, "destination_sha256": sha,
        "copy_method": "Exact original bytes; no recoloring, resizing or re-encoding",
        "verified_on": VERIFIED_ON,
    }
    return raw, entry


def write_avatars(prepared):
    if len(prepared) != 48 or len({entry["source_sha256"] for _, entry in prepared}) != 48:
        raise ValueError("The complete set must contain 48 distinct portraits")
    entries = []
    for raw, entry in prepared:
        target = ROOT.parent / entry["file"]
        pending = target.with_suffix(".pending.png")
        pending.write_bytes(raw)
        pending.replace(target)
        if hashlib.sha256(target.read_bytes()).hexdigest() != entry["source_sha256"]:
            raise ValueError("Destination byte verification failed: " + str(target))
        entries.append(entry)
    return entries


def contact_sheet():
    sheet = Image.new("RGB", (1216, 1216), "#EEF2F3")
    draw = ImageDraw.Draw(sheet)
    for index in range(1, 49):
        x = 12 + ((index - 1) % 8) * 150
        y = 12 + ((index - 1) // 8) * 200
        with Image.open(AVATARS / f"portrait-{index:02}.png") as image:
            sheet.paste(image, (x, y), image)
        draw.text((x, y + 138), f"portrait-{index:02}", fill="#173C49")
    sheet.save(LICENSES / "Notionists-AuthorSet-ContactSheet.png")


def download_fonts():
    source = "https://raw.githubusercontent.com/tokotype/PlusJakartaSans/master"
    for weight in ("Regular", "SemiBold", "Bold"):
        filename = f"PlusJakartaSans-{weight}.ttf"
        (FONTS / filename).write_bytes(fetch(f"{source}/fonts/ttf/{filename}"))
    (LICENSES / "OFL-PlusJakartaSans.txt").write_bytes(fetch(f"{source}/OFL.txt"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true", help="Fetch all 48 original DiceBear 9.x PNGs")
    parser.add_argument("--source-dir", type=Path, default=SIBLING_AVATARS,
                        help="Folder containing notionists-01.png through notionists-48.png")
    parser.add_argument("--avatars-only", action="store_true", help="Keep the existing fonts")
    args = parser.parse_args()
    source_dir = args.source_dir.resolve()
    available = all((source_dir / f"notionists-{index:02}.png").is_file() for index in range(1, 49))
    download = args.download or not available
    for folder in (AVATARS, FONTS, LICENSES):
        folder.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=4) as pool:
        prepared = tuple(pool.map(lambda index: prepare_avatar(index, source_dir, download), range(1, 49)))
    manifest = write_avatars(prepared)
    (LICENSES / "Notionists-AuthorSet-Sources.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    if not download and (source_dir / "README.md").is_file():
        (LICENSES / "Notionists-AuthorSet-Original-Provenance.txt").write_bytes((source_dir / "README.md").read_bytes())
    contact_sheet()
    if not args.avatars_only:
        download_fonts()
    print("Verified 48 distinct original Notionists PNGs; source and destination bytes match.")


if __name__ == "__main__":
    main()
