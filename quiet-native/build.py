#!/usr/bin/env python3
"""Build the Quiet native fork; owner-TV references and settings stay local."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parent
WORKSPACE = ROOT.parent
UPSTREAM_COMMIT = "cf518d7a9278dca7990e27756218258e44044a29"
FRAMEWORK_NAMES = ("Tizen.NUI", "Tizen.Applications.Common", "Tizen.System", "Tizen")
AMBIENT_NAMES = ("cards", "sleep", "cinema", "space", "football", "records",
                 "cardtower", "painting", "stargazing", "stage")
CATALOG = [
    ("org.tizen.netflix-app", "Netflix", "netflix.svg", "#171717"),
    ("NuvioTV001.NuvioTV", "Nuvio", "nuvio.png", "#171727"),
    ("9Ur5IzDKqV.TizenYouTube", "YouTube", "youtube.svg", "#E82C42"),
    ("PyZfkXAf88.tv2sumoTizen", "TV 2 Play", "tv2play.svg", "#31558E"),
    ("MCmYXNxgcu.DisneyPlus", "Disney+", "disneyplus.png", "#09264B"),
    ("IkWsFHtOY9.Stremio", "Stremio", "stremio.svg", "#6C58A7"),
    ("niYSnzL6h1.Viaplay", "Viaplay", "viaplay.svg", "#171717"),
    ("skysh0WTIM.SkyShowtime", "SkyShowtime", "skyshowtime.svg", "#171717"),
    ("org.tizen.primevideo", "Prime Video", "primevideo.png", "#0779FF"),
    ("com.samsung.tv.aria-video", "Apple TV", "appletv.svg", "#303C50"),
    ("rJeHak5zRg.Spotify", "Spotify", "spotify.svg", "#14765F"),
    ("Jz7aUrV2Q4.Twitch", "Twitch", "twitch.svg", "#8C45E7"),
    ("com.samsung.tv.cobalt-ytkids", "YouTube Kids", "youtubekids.svg", "#E82C42"),
    ("com.samsung.tv.cobalt-yttv", "YouTube TV", "youtubetv.svg", "#E82C42"),
    ("com.samsung.tv.cobalt-gpm", "Google Play Movies", "googleplay.svg", "#27364D"),
    ("org.tizen.browser", "Internet", "browser.svg", "#FFFFFF"),
    ("vbUQClczfR.Wuakitv", "Rakuten TV", "rakuten.jpg", "#FFFFFF"),
    ("WSRedi0001.WorkspaceRedi", "Workspace", "workspace.png", "#FFFFFF"),
    ("RBElmUtjfo.PerplexityAI", "Perplexity", "perplexity.png", "#FFFFFF"),
]


def default_dotnet():
    """Use an explicit runtime, PATH, or the existing private SDK, in that order."""
    if os.environ.get("DOTNET"):
        return os.environ["DOTNET"]
    found = shutil.which("dotnet")
    if found:
        return found
    local_sdk = WORKSPACE / "runtime/dotnet/dotnet"
    return str(local_sdk) if local_sdk.is_file() else "dotnet"


def load_seed(path):
    """Import only a deliberately supplied JSON settings object, never a report."""
    seed = json.loads(path.read_text())
    if isinstance(seed, dict) and "settings" in seed:
        seed = seed["settings"]
    if not isinstance(seed, dict):
        raise ValueError("Seed settings must be a JSON object or {\"settings\": {...}}.")
    # Retain the prior explicit migration behavior without importing owner history
    # implicitly when someone builds a public checkout.
    return {**seed, "pinNuvio": True}


def package_assets(workspace, native_root, destination, seed=None):
    upstream = workspace / "research/quiet-tizen"
    # Rebuilding without an optional input must also remove its previous copy.
    # In particular, never leave a stale owner's settings seed in a public build.
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(upstream / "launcher/assets", destination)
    shutil.copytree(upstream / "licenses", destination / "licenses", dirs_exist_ok=True)
    brands = destination / "brands"
    brands.mkdir(exist_ok=True)
    for source in (workspace / "launcher/assets/brands", native_root / "assets"):
        if source.is_dir():
            shutil.copytree(source, brands, dirs_exist_ok=True)
    ambient = destination / "ambient"
    ambient.mkdir(exist_ok=True)
    for name in AMBIENT_NAMES:
        pair = [workspace / "launcher/assets" / f"snoopy-{name}.{extension}"
                for extension in ("mp4", "jpg")]
        if all(path.is_file() for path in pair):
            for path in pair:
                shutil.copy2(path, ambient / path.name)
    for name in ("LICENSE", "NOTICE.md"):
        shutil.copy2(upstream / name, destination / ("QUIET-" + name))
    portrait = workspace / "launcher/assets/quiet/portrait-ps5.svg"
    if portrait.is_file():
        shutil.copy2(portrait, destination / portrait.name)
    catalog = []
    for index, (app_id, name, artwork, background) in enumerate(CATALOG):
        # Publisher bitmaps are optional local inputs. An empty Asset invokes
        # Home.cs's existing text rendering rather than a broken image view.
        asset = "brands/" + artwork if (brands / artwork).is_file() else ""
        square = app_id == "vbUQClczfR.Wuakitv"
        catalog.append(dict(Id=app_id, Name=name, Asset=asset,
                            Description="Streaming" if index < 13 else "Installert på TV-en",
                            Fit="contain", Background=background,
                            Width=320 if square else 640, Height=320 if square else 360))
    (destination / "apps.json").write_text(json.dumps(catalog, indent=2) + "\n")
    if seed is not None:
        (destination / "dusk-settings.json").write_text(json.dumps(seed, indent=2) + "\n")
    (destination / "source-ports.json").write_text(
        json.dumps({"13": 1, "14": 2, "15": 3, "16": 4}, indent=2) + "\n")
    return len(list(ambient.glob("*.mp4")))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=default_dotnet(), help=".NET 6 SDK executable (or DOTNET/PATH)")
    parser.add_argument("--framework", type=Path, default=ROOT / ".local/framework",
                        help="Private directory containing the four TV reference assemblies")
    parser.add_argument("--seed-settings", type=Path,
                        help="Optional plain JSON Dusk settings to migrate; excluded by default")
    args = parser.parse_args()
    if not shutil.which(args.dotnet):
        parser.error(".NET SDK not found. Install the .NET 6 SDK or pass --dotnet PATH.")
    for name in FRAMEWORK_NAMES:
        if not (args.framework / f"{name}.dll").is_file():
            parser.error(f"Missing {name}.dll. Supply your TV references with --framework DIR; they are not bundled.")
    upstream = WORKSPACE / "research/quiet-tizen"
    if not (upstream / "launcher/assets").is_dir() or not (upstream / "licenses").is_dir():
        parser.error("Quiet Tizen assets are missing. Run git submodule update --init research/quiet-tizen.")
    seed = None
    if args.seed_settings:
        try:
            seed = load_seed(args.seed_settings)
        except (OSError, ValueError) as error:
            parser.error(f"Cannot import --seed-settings: {error}")
    local = ROOT / ".local"
    out = local / "build"
    package = local / "package"
    package.mkdir(parents=True, exist_ok=True)
    subprocess.run([args.dotnet, "build", str(ROOT / "QuietHome.csproj"), "-c", "Release", "--nologo",
                    "-o", str(out), f"-p:TV_FRAMEWORK_DIR={args.framework.resolve()}"],
                   check=True, env={**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
    shutil.copy2(out / "BixbyPromotion.dll", package / "BixbyPromotion.dll")
    media_count = package_assets(WORKSPACE, ROOT, package / "quiet-home", seed)
    report = dict(upstream_commit=UPSTREAM_COMMIT, native=True, reference_assemblies_distributed=False,
                  dll_sha256=hashlib.sha256((package / "BixbyPromotion.dll").read_bytes()).hexdigest(),
                  media_count=media_count, seed_settings_imported=seed is not None, installed=False)
    (local / "build.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
