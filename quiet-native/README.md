# Quiet Home: QN85F native adaptation

This is the native .NET/NUI launcher from [Quiet Tizen](https://github.com/jss8649/quiet-tizen/tree/cf518d7a9278dca7990e27756218258e44044a29), adapted for Samsung **TQ65QN85FAUXXC**, Tizen 9 / ARMv7 / .NET 6.0.9. The tested firmware is `T-PTMFDEUC-0090-REL-202607141945`. This is a separate hardware profile from upstream's U8000F case study.

The application runs in the existing native container **`com.samsung.tv.bixby-promotion`**, replacing `BixbyPromotion.dll` and adding `res/quiet-home/`. This repurposes Bixby Promotion. The previous **`DuskLaunch.Dusk`** web launcher is the recovery destination used by the supplied restoration script.

## Source and behavior

`src/Home.cs` directly adapts upstream native rendering, focus/navigation, app launching, settings and lifecycle code. `src/AppLibrary.cs` is unchanged from the pinned upstream version. This is native Quiet code, rather than a web recreation of its appearance.

`src/NativeData.cs` adds live source and installed-app discovery, locally persisted usage, recent selections and manual favorites. Only successful native launch requests update usage. The twelve-entry recent-selection history is separate from favorite order; its first two relevant entries populate the right-hand Home cards. Existing usage counts do not manufacture a recent-selection history.

`NativeAmbient.cs` adds muted local video playback, matching posters and pause/resume handling. The video and poster share contained 16:9 bounds inside the wider card, including during focus changes. Without optional media the card retains its neutral background.

`source-ports.json` in the generated package translates native source enum values to HDMI port numbers. Its current mapping, `13`–`16` to HDMI `1`–`4`, was observed on the tested TV. Device names and connected state come from live `get_source_list()`. Review that mapping on another model. The implementation recognizes named idle devices and filters audio-only sources from video favorites.

App artwork mappings do not make an app installed. Native discovery determines availability, and background services such as Nuvio's EngineFs and Plugin services are excluded from consumer app tiles. The weather client currently requests **Oslo** from Open-Meteo; change both `WeatherCity` and the coordinates/timezone in `src/NativeData.cs` for a different location.

## Build

Install Python 3 and the .NET 6 SDK. Clone the repository with its pinned upstream source:

```sh
git clone --recurse-submodules https://github.com/Chrissddd/Better-Tizen.git
cd Better-Tizen
```

For an existing clone:

```sh
git submodule update --init --recursive
```

Obtain these four reference assemblies from your own compatible TV's `/usr/share/dotnet.tizen/framework/` and keep them in a private local directory:

- `Tizen.NUI.dll`
- `Tizen.Applications.Common.dll`
- `Tizen.System.dll`
- `Tizen.dll`

Build from the repository root:

```sh
python3 quiet-native/build.py --dotnet /path/to/dotnet --framework /path/to/tv-framework
/path/to/dotnet build quiet-native/boot/Boot.csproj -c Release -o quiet-native/.local/boot
```

`--dotnet` is optional when `dotnet` is on `PATH` or `DOTNET` points to it. `--framework` defaults to `quiet-native/.local/framework/`. Framework references use `Private=false` and are not copied into the package.

The main build creates `quiet-native/.local/package/BixbyPromotion.dll`, `quiet-native/.local/package/quiet-home/` and `.local/build.json`. The report's `installed` flag describes the build artifact; it is not a live TV check. The boot build produces `QuietBoot.dll` separately. Neither command installs anything on a TV.

The asset directory is rebuilt on each run, so removed optional media or a previously supplied settings seed is not silently retained. Build outputs, TV references, private profiles and backups belong in ignored local directories.

## Local tests

Run from the repository root with Python 3 and the .NET 6 SDK:

```sh
python3 -m unittest discover -s quiet-native/tests -p 'test_build.py' -v
dotnet run --project quiet-native/tests/DataTests.csproj -c Release
```

These suites use temporary files and bundled synthetic fixtures. They need no TV, private framework assemblies or network connection. They check packaging without optional private inputs, media pairing, artwork fallbacks, explicit settings import, source parsing, favorites and persisted recent-selection order. They do not replace visual and input checks on the target TV.

## Optional media and settings

The public repository omits Snoopy media, personal wallpapers and private artwork extracted from a TV. Only supply files you are entitled to use. For ambient playback, place a 16:9 MP4 and matching JPEG poster together in `launcher/assets/`, using one or more of these basename pairs:

```text
snoopy-cards       snoopy-sleep       snoopy-cinema
snoopy-space       snoopy-football    snoopy-records
snoopy-cardtower   snoopy-painting    snoopy-stargazing
snoopy-stage
```

For example, `snoopy-space.mp4` and `snoopy-space.jpg` form one variant. The build packages only complete pairs. Without them the native launcher still builds and runs with a neutral ambient area. Optional publisher bitmaps that are absent from the checkout use the app name as a fallback instead of a broken image.

To migrate existing Dusk preferences, explicitly provide a private JSON file:

```sh
python3 quiet-native/build.py --framework /path/to/tv-framework --seed-settings /private/path/dusk-settings.json
```

The seed accepts either the settings object itself or `{"settings": {...}}`. It is packaged into the private build output and may contain favorite order and usage history. No session report is imported automatically. The native application subsequently persists preferences locally on the TV.

## Deployment boundaries

The scripts in `../scripts/` document the reviewed QN85F deployment. Read their current guards before adapting them: they contain recorded paths, original-assembly hashes, ownership, SMACK labels and a fixed first-backup location. Some installation checks also expect the private media used for that deployment. A successful portable build does not make those scripts portable installers.

Use an SDB installation obtained separately and an authenticated connection to your own TV. Obtain the temporary-root capability according to the pinned upstream documentation and establish the target device's actual model, firmware, package layout and framework before mutation. Do not copy upstream source IDs or owner/profile assumptions onto a different device without checking them.

The reviewed sequence is:

1. Preserve the original native package, native-image cache, package registry fields and Home/startup settings. `backup-quiet-native.sh` creates the initial snapshot and refuses to overwrite it.
2. Build and stage the package and the required helper assemblies. Keep a separately transferred backup available off the TV.
3. Review and run `install-quiet-native.sh` for the exact recorded baseline. It verifies the original assembly hash, preserves its native-image cache, applies the observed owner/SMACK label and requests a manual launch. It leaves Home routing alone.
4. Verify the native UI, D-pad/Select/Back, artwork, video aspect ratio, app/HDMI switching, picture, audio and settings before changing Home or startup routing.
5. Use the matching `HomeAssign.dll` and `QuietBoot.dll` helpers only after that verification, then check Home from another app, standby/wake and a cold start.

`update-quiet-native.sh` is a hash-guarded update for the recorded deployment, not a generic updater. A failed shell script may already have completed earlier filesystem steps. Keep SDB and temporary-root operations sequential, and allow session cleanup to finish before restarting the TV.

This port does not require applying upstream advertising, voice, browser, sidebar or other cleanup groups. Those are independent changes.

## Recovery

The supplied `restore-quiet-native.sh` restores the original Bixby Promotion assembly and its native-image cache, restores only that package's saved registry fields, and returns Home/startup to the previous **Dusk** launcher. It does not restore entire live databases or return to Samsung factory Home.

For that recovery path, **Dusk must remain installed**, and both `QuietBoot.dll` and `HomeAssign.dll` must remain staged where the script expects them. The intermediate `HomeAssign.dll restore` is required because the guarded assignment helper accepts a new destination only from Samsung's original `com.samsung.tv.csfs` main-operation handler. Preserve the first backup; do not overwrite it to make a retry succeed.

Restoration leaves extra Quiet resources inert for inspection. After recovery and temporary-root cleanup, verify Home, picture, audio and input switching. An installation with a different original Home or package baseline needs its own reviewed recovery path.

## Licenses

The pinned upstream MIT/NOTICE, Free My TV MIT, Inter OFL and Material Symbols Apache notices must accompany their corresponding code/assets. The build retains upstream notices under `quiet-home/QUIET-LICENSE`, `QUIET-NOTICE.md` and `licenses/`. See the root [UPSTREAM-NOTICE.md](../UPSTREAM-NOTICE.md) for provenance. These licenses do not grant rights to omitted videos or third-party publisher artwork.
