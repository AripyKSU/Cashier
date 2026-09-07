from io import BytesIO
from pathlib import Path
import subprocess

from PIL import Image


ROOT = Path(r"F:\UnityProject\Cashier")
ART = ROOT / "Assets" / "DystopiaPrototype" / "Art"
OUTPUT = ROOT / "output" / "imagegen"
GENERATED = Path(r"C:\Users\imsoh\.codex\generated_images\01a07b95-2bcc-7e52-9a9f-a41c5509c088")


def tracked_original(asset_name: str) -> Image.Image:
    relative_path = f"Assets/DystopiaPrototype/Art/{asset_name}"
    data = subprocess.check_output(["git", "show", f"HEAD:{relative_path}"], cwd=ROOT)
    return Image.open(BytesIO(data)).convert("RGBA")


def key_green(generated_name: str, size: tuple[int, int]) -> Image.Image:
    source = Image.open(GENERATED / generated_name).convert("RGB")
    result = Image.new("RGBA", size)
    result.paste(source, (0, 0))
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            red, green, blue, _ = pixels[x, y]
            if green - max(red, blue) >= 24:
                pixels[x, y] = (0, 0, 0, 0)
                continue
            green = min(green, max(red, blue))
            pixels[x, y] = (red, green, blue, 255)
    return result


def clear_box(image: Image.Image, box: tuple[int, int, int, int]) -> None:
    image.paste(Image.new("RGBA", (box[2] - box[0], box[3] - box[1])), box[:2])


def keep_box(image: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    result = Image.new("RGBA", image.size)
    result.paste(image.crop(box), box[:2])
    return result


def save_asset(image: Image.Image, asset_name: str) -> None:
    image.save(ART / asset_name)
    image.save(OUTPUT / asset_name.replace(".png", "-transparent.png"))
    preview = Image.new("RGBA", image.size, (124, 134, 140, 255))
    preview.alpha_composite(image)
    preview.save(OUTPUT / asset_name.replace(".png", "-applied-preview.png"))


left = tracked_original("LeftWatchTower.png")
left_generated = key_green("exec-0809d1a8-20b8-49cf-9f23-793f9c522c59.png", left.size)
left_lower = (108, 450, 238, 941)
clear_box(left, left_lower)
left.alpha_composite(keep_box(left_generated, left_lower))
save_asset(left, "LeftWatchTower.png")

right = tracked_original("RightWatchTower.png")
right_generated = key_green("exec-a83e801b-831d-48c8-9ddc-f3ad63a63ec4.png", right.size)
right_lower = (1470, 450, 1575, 941)
right_bin = (1575, 780, 1672, 941)
right_post_lower = (1575, 780, 1645, 910)
clear_box(right, right_lower)
clear_box(right, right_bin)
right.alpha_composite(keep_box(right_generated, right_lower))
right.alpha_composite(keep_box(right_generated, right_post_lower))
save_asset(right, "RightWatchTower.png")

guard = key_green("exec-f963848c-c57d-4f6a-8a2e-f2ec181efdc2.png", (1256, 1256))
bounds = guard.getbbox()
if bounds is None:
    raise RuntimeError("Watch guard extraction produced no opaque pixels")
guard = guard.crop(bounds)
guard.save(ART / "WatchGuard.png")
guard.save(OUTPUT / "WatchGuard-transparent.png")
