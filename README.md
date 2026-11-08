# Better-Tizen

A custom Samsung Tizen Home menu based on the native .NET/NUI launcher from [Quiet Tizen](https://github.com/jss8649/quiet-tizen). It reuses upstream rendering, navigation, app launching and settings code, with additions for live sources, installed apps and locally stored preferences.

The native launcher is in [`quiet-native/`](quiet-native/). The earlier, separate web launcher is in [`launcher/`](launcher/).

## Features

- Home, Apps and Settings pages with remote-control navigation.
- Live HDMI and installed-app discovery, including device artwork for Apple TV and PlayStation.
- Two recent-source cards that follow successful app or HDMI selections, independently of favorite order.
- Automatic favorites, manual favorites and usage history stored locally on the TV.
- Optional local ambient videos with matching posters, preserved aspect ratio and pause/resume handling.
- Clock, date and cached weather. The current weather implementation is configured for **Oslo** in `quiet-native/src/NativeData.cs`.
- Home-button and startup routing through the native deployment helpers.

The app catalog contains artwork mappings, not a fixed list of installed apps. Nuvio appears when installed; its background services are excluded from app tiles. Nuvio itself is obtained separately from the [official releases](https://github.com/NuvioMedia/NuvioTVSmart/releases).

## Tested hardware

This adaptation was tested on **Samsung TQ65QN85FAUXXC**, Tizen 9, ARMv7, .NET 6.0.9, firmware `T-PTMFDEUC-0090-REL-202607141945`. Quiet Tizen's original hardware profile is different. Private Samsung APIs, source identifiers, package layout and Home routing must be checked against the target TV.

The native deployment replaces the assembly inside the existing `com.samsung.tv.bixby-promotion` container. It repurposes Bixby Promotion; it is not a standalone signed TPK or a replacement operating system. The deployment scripts record a specific tested installation and contain firmware, path and hash assumptions. They are not a universal installer.

## Build

Requirements: Python 3, .NET 6 SDK, the pinned upstream submodule and four framework assemblies obtained from your own compatible TV. Samsung framework assemblies, SDK tools, signing certificates and TV backups are not distributed here.

```sh
git clone --recurse-submodules https://github.com/Chrissddd/Better-Tizen.git
cd Better-Tizen
python3 quiet-native/build.py --dotnet /path/to/dotnet --framework /path/to/tv-framework
```

The framework directory must contain `Tizen.NUI.dll`, `Tizen.Applications.Common.dll`, `Tizen.System.dll` and `Tizen.dll`. Build output goes to the ignored `quiet-native/.local/` directory. See the [native build and deployment notes](quiet-native/README.md) before deploying.

Local packaging and data tests use synthetic fixtures and do not contact a TV:

```sh
python3 -m unittest discover -s quiet-native/tests -p 'test_build.py' -v
dotnet run --project quiet-native/tests/DataTests.csproj -c Release
```

## Local device configuration

Copy [`scripts/device.example.json`](scripts/device.example.json) to the ignored `.local/device.json` and set your TV address, the computer's reachable callback address, and paths to your separately obtained SDB and temporary-root payloads. The example addresses are documentation placeholders. These settings are for the operation tools, not the native launcher build.

Environment overrides are `TV_IP`, `TV_CALLBACK_IP`, `SDB` and `TV_ROOT_PAYLOADS`; `TV_CONFIG` selects a different private configuration file. Keep your actual device configuration local. Review the deployment notes and scripts before running tools that change a TV.

The operation and temporary-root tools require **Python 3.12 or later**. Build the private payloads from the pinned [upstream build tool](research/quiet-tizen/tools/build.py) with `python3 research/quiet-tizen/tools/build.py payloads --dotnet /path/to/dotnet`, then point `TV_ROOT_PAYLOADS` at its payload output. The native launcher build and synthetic tests use Python 3.

## Media and private files

Snoopy videos, matching poster images, personal wallpapers, private app settings and artwork retrieved from the TV are omitted from the public repository. Supply media you are entitled to use; the native build also works without ambient videos. Missing optional app artwork uses a text fallback. Instructions and expected filenames are in the [native README](quiet-native/README.md#optional-media-and-settings).

Device snapshots, reports, credentials, generated packages and proprietary binaries stay local. The public source does not configure a Samsung account or a Nuvio account.

## Credits and licenses

The native launcher starts from [Quiet Tizen](https://github.com/jss8649/quiet-tizen), pinned as the `research/quiet-tizen` submodule. Its upstream code, artwork, fonts and third-party notices retain their original licenses. See [UPSTREAM-NOTICE.md](UPSTREAM-NOTICE.md) and the notices distributed alongside the assets. Product names and logos belong to their respective owners.
