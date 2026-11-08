# Upstream code and assets

Better-Tizen adapts the native [Quiet Home launcher](https://github.com/jss8649/quiet-tizen), pinned at `cf518d7a9278dca7990e27756218258e44044a29` in the `research/quiet-tizen` submodule. Quiet is MIT licensed; its license is retained in this repository's `LICENSE` and in the submodule.

`quiet-native/src/AppLibrary.cs` is unchanged upstream code. The home renderer, navigation, lifecycle, application controls, cache and settings in `quiet-native/src/Home.cs` derive from Quiet's `Home.cs`. Local additions include device discovery, local preferences, recently used sources, weather and ambient video integration. The build retains the upstream third-party license files.

Quiet's layout and illustrations also draw on [Free My TV](https://github.com/simbasjungles/free-my-tv/tree/73409804e5085e720dccea9ab95078a515cae098), MIT licensed, at commit `73409804e5085e720dccea9ab95078a515cae098`. The web launcher retains its asset notices under `launcher/assets/quiet`. Inter is licensed under the SIL Open Font License and Material Symbols under Apache License 2.0; the complete texts are in the Quiet submodule's `licenses` directory.

The Simple Icons SVG assets are CC0, with the full text in `launcher/assets/brands/SIMPLE-ICONS-LICENSE.txt`. Brand names and marks belong to their respective owners; asset provenance is recorded alongside the assets.

`transport/samsung_tv_root/` is a source-only subset of [samsung-tv-root](https://github.com/chris-ritsen/samsung-tv-root/tree/4ad2c4d3b0d0e0da4b5b7ebe4a787e94596f416b), distributed under the Unlicense in `transport/LICENSE`. The local `archive_root.py` adds a runtime-discovery fallback for ARM32 hosts whose `dotnet` does not support `--list-runtimes`: it checks for matching CoreCLR and CoreLib files. The remaining included modules are unchanged.

Samsung reference assemblies, compiled device payloads, SDKs, certificates, account data and device backups are not distributed here. Snoopy videos, personal wallpapers and cached publisher artwork remain optional local inputs. This project is not affiliated with Samsung, Sony, Apple or the streaming services whose applications it can launch.
