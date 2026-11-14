# Quiet Home: native QN85F adaptations

The current Tizen 10 implementation is a standalone signed .NET/NUI application
in [`tizen10/`](tizen10/). The original Tizen 9 container deployment remains below
under [Legacy Tizen 9 deployment](#legacy-tizen-9-deployment).

## Tizen 10 standalone application

- App and package ID: **`org.bettertizen.home`**; UI label: **Quiet Home**.
- Managed entry point: `BetterTizenHome.dll`, .NET 8, API13 references.
- Verified device profile: Samsung **TQ65QN85FAUXXC**, Tizen **10.0**, ARMv7,
  **.NET 8.0.11**. This result does not establish support for other firmware.
- Resources and persistent preferences use the application's own package/data
  directories. Samsung's Bixby Promotion files, manifest and native-image cache
  are retained.

The renderer, geometry, navigation, artwork handling and ambient video come from
the existing native Quiet adaptation. The new package keeps real app/HDMI
discovery, locally stored favorites, independent recent selections, Oslo weather
and optional local media. Availability is discovered on the TV; the artwork
catalog does not make an app installed. Nuvio's background services and temporary
probe apps are excluded from consumer app tiles.

### Home and startup mechanisms

A native timer remains active while the UI is paused. It reads the current
foreground value and requests Quiet's return only after two stable observations
of the measured Samsung Home ID, separated by at least 500 ms. Immediately before
each request it requires a fresh explicit power-on response from the TV's loopback
API, rereads the foreground value, and applies generation, cooldown and external
launch guards. An active streaming app or HDMI source is left on screen.

This is foreground recovery after Samsung Home appears, rather than a successful
change to the global native main-operation handler: that setter was denied on
this profile. On 14 November 2026 the user confirmed that pressing **Home from
Nuvio returned to Quiet**. A successful launch call alone was not counted as that
visual verification.

Startup configuration is a separate, explicit setup operation. It accepts the
measured Bixby/Bixby string baseline with auto-start already equal to 1, saves an
atomic private `startup-baseline.json` journal, sets only the two writable Home
and startup strings, and requires five consecutive matching readbacks. Partial
failure rolls the changed strings back; a valid journal permits recovery of
known interrupted states. It contains no auto-start integer setter and does not
call the denied global main-operation setter.

The two strings have been configured and read back on the tested TV. On
**14 November 2026**, the user separately confirmed automatic return after their
power/restart test: **Samsung's standard Home appears for a few seconds, then
Quiet opens automatically**. The behavior is not an instant or exclusive
replacement of Samsung's launcher.

Passive reports retained the same kernel boot ID across that test. It therefore
does not establish a fresh kernel cold boot. **A full mains-disconnected cold
start with the new app remains untested.** For that full cold-start path, the
current evidence is the configured selectors and stable readbacks, rather than
an observed new-kernel startup. Home-key return and the user's observed power-on
return are confirmed separately from that remaining boundary.

### Build and sign

Requirements: Python 3, .NET SDK 8 or newer and the pinned Quiet submodule. The
builder downloads and verifies pinned official `Tizen.NET.API13` **13.0.0.19198**
and `Tizen.NET.Sdk` **1.1.9** packages for compilation/signing. Framework references remain
reference-only and are not bundled in the TPK.

From the repository root:

```sh
git submodule update --init --recursive
python3 quiet-native/tizen10/build.py --dotnet /path/to/dotnet --unsigned
```

For installation, use your own Samsung TV certificate profile:

```sh
python3 quiet-native/tizen10/build.py --dotnet /path/to/dotnet \
  --profile YOUR_TV_PROFILE --profiles /private/path/profiles.xml \
  --password-file /private/path/certificate-password
```

The password file can contain the plaintext password or
`TIZEN_CERTIFICATE_PASSWORD=...`; the corresponding environment variable is also
supported. Never commit that file, the profile, certificates or their passwords.
The builder performs no TV connection, certificate issuance or account login.
Output stays under ignored `tizen10/.local/`. See the [standalone build notes](tizen10/README.md)
for the complete CLI and package output paths.

Public builds package public UI assets, SVGs and metadata only. Optional media is
an explicit `--media-dir PATH` input with matching MP4/JPEG pairs. Optional
publisher bitmaps require explicit, repeatable `--artwork-dir PATH` inputs. No
private preferences or usage seed is imported automatically. Missing optional
artwork uses text; missing media leaves a neutral ambient area. Installed-app
availability and preferences are subsequently stored/read on the TV.

### Private setup and visual QA

Normal builds have no diagnostic callback or startup-setting write. An explicit
`--diagnostics-config /private/path/config.json` opts into a bounded local setup
or QA configuration. `configureStartup:true` is accepted only through a valid
private configuration and after UI/data readiness checks. It is a device-specific
setup operation, not a universal installer switch.

Optional `captureHomeOnly:true` captures just Home; `capturePages:true` deliberately
cycles all three pages and is inappropriate while someone is watching the TV.
The app waits for layout/artwork readiness and captures its own NUI scene to PNG,
then sends an authenticated bounded callback to the configured private IPv4
listener. These captures do not establish another app's foreground state and may
omit hardware video content. They have been used to inspect the actual Tizen 10
UI, including branding and proportions.

Use a short `expiresUtc` for temporary setup/QA and omit the private configuration
from a normal package. Expiry disables new configuration requests and callbacks; it
**does not undo previously saved startup selectors**. The native foreground
watcher is independent of the optional QA configuration.

### Local standalone tests

From the repository root, with Python 3 and the .NET 8 SDK:

```sh
python3 -m unittest discover -s quiet-native/tizen10/tests -p 'test_build.py' -v
dotnet run --project quiet-native/tizen10/tests/DataTests.csproj -c Release
python3 quiet-native/tizen10/tests/run_startup_tests.py --dotnet /path/to/dotnet
```

The data suite uses synthetic source and app fixtures. Startup tests use a local
native-library shim to check journal persistence, interrupted writes, rollback
and unexpected-value guards. These checks do not connect to a TV or establish
actual Home-button, power or media behavior.

### Recovery and removal

Preserve the application-private `startup-baseline.json` and your signing
material before uninstalling or replacing this package. The journal records the
actual pre-change selector values. A failed setup has an internal guarded
rollback; that is not a general uninstall restoration command. A dedicated
normal removal/recovery procedure is separate work. Restore the measured
original selector values through a compatible reviewed helper before removing
their configured destination, and verify the result.

The legacy `restore-quiet-native.sh`, `HomeAssign.dll` and `QuietBoot.dll` paths
below belong to the old container deployment. They do not restore or uninstall
the standalone Tizen 10 app. Do not transplant Tizen 9 stock binaries, caches or
database snapshots over the upgraded vendor package.

## Legacy Tizen 9 deployment

The following documents the previously verified Tizen 9 installation, including
its historical build and recovery commands. It is retained for that exact
baseline; it is not the installation procedure for the standalone app above.

This is the native .NET/NUI launcher from [Quiet Tizen](https://github.com/jss8649/quiet-tizen/tree/cf518d7a9278dca7990e27756218258e44044a29), adapted for Samsung **TQ65QN85FAUXXC**, Tizen 9 / ARMv7 / .NET 6.0.9. The tested firmware is `T-PTMFDEUC-0090-REL-202607141945`. This is a separate hardware profile from upstream's U8000F case study.

The application runs in the existing native container **`com.samsung.tv.bixby-promotion`**, replacing `BixbyPromotion.dll` and adding `res/quiet-home/`. This repurposes Bixby Promotion. The previous **`DuskLaunch.Dusk`** web launcher is the recovery destination used by the supplied restoration script.

### Source and behavior

`src/Home.cs` directly adapts upstream native rendering, focus/navigation, app launching, settings and lifecycle code. `src/AppLibrary.cs` is unchanged from the pinned upstream version. This is native Quiet code, rather than a web recreation of its appearance.

`src/NativeData.cs` adds live source and installed-app discovery, locally persisted usage, recent selections and manual favorites. Only successful native launch requests update usage. The twelve-entry recent-selection history is separate from favorite order; its first two relevant entries populate the right-hand Home cards. Existing usage counts do not manufacture a recent-selection history.

`NativeAmbient.cs` adds muted local video playback, matching posters and pause/resume handling. The video and poster share contained 16:9 bounds inside the wider card, including during focus changes. Without optional media the card retains its neutral background.

`source-ports.json` in the generated package translates native source enum values to HDMI port numbers. Its current mapping, `13`–`16` to HDMI `1`–`4`, was observed on the tested TV. Device names and connected state come from live `get_source_list()`. Review that mapping on another model. The implementation recognizes named idle devices and filters audio-only sources from video favorites.

App artwork mappings do not make an app installed. Native discovery determines availability, and background services such as Nuvio's EngineFs and Plugin services are excluded from consumer app tiles. The weather client currently requests **Oslo** from Open-Meteo; change both `WeatherCity` and the coordinates/timezone in `src/NativeData.cs` for a different location.

### Build

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

### Local tests

Run from the repository root with Python 3 and the .NET 6 SDK:

```sh
python3 -m unittest discover -s quiet-native/tests -p 'test_build.py' -v
dotnet run --project quiet-native/tests/DataTests.csproj -c Release
```

These suites use temporary files and bundled synthetic fixtures. They need no TV, private framework assemblies or network connection. They check packaging without optional private inputs, media pairing, artwork fallbacks, explicit settings import, source parsing, favorites and persisted recent-selection order. They do not replace visual and input checks on the target TV.

### Optional media and settings

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

### Deployment boundaries

The scripts in `../scripts/` document the reviewed QN85F deployment. Read their current guards before adapting them: they contain recorded paths, original-assembly hashes, ownership, SMACK labels and a fixed first-backup location. Some installation checks also expect the private media used for that deployment. A successful portable build does not make those scripts portable installers.

Use an SDB installation obtained separately and an authenticated connection to your own TV. Obtain the temporary-root capability according to the pinned upstream documentation and establish the target device's actual model, firmware, package layout and framework before mutation. Do not copy upstream source IDs or owner/profile assumptions onto a different device without checking them.

These historical host tools use Python 3.12 or later. Their payloads can be built
from the pinned upstream source, separately from the launcher:

```sh
python3 research/quiet-tizen/tools/build.py payloads --dotnet /path/to/dotnet
```

`TV_ROOT_PAYLOADS` selects the resulting payload directory. This does not make
the old command route compatible with the tested TV's Tizen 10 upgrade.

The reviewed sequence is:

1. Preserve the original native package, native-image cache, package registry fields and Home/startup settings. `backup-quiet-native.sh` creates the initial snapshot and refuses to overwrite it.
2. Build and stage the package and the required helper assemblies. Keep a separately transferred backup available off the TV.
3. Review and run `install-quiet-native.sh` for the exact recorded baseline. It verifies the original assembly hash, preserves its native-image cache, applies the observed owner/SMACK label and requests a manual launch. It leaves Home routing alone.
4. Verify the native UI, D-pad/Select/Back, artwork, video aspect ratio, app/HDMI switching, picture, audio and settings before changing Home or startup routing.
5. Use the matching `HomeAssign.dll` and `QuietBoot.dll` helpers only after that verification, then check Home from another app, standby/wake and a cold start.

`update-quiet-native.sh` is a hash-guarded update for the recorded deployment, not a generic updater. A failed shell script may already have completed earlier filesystem steps. Keep SDB and temporary-root operations sequential, and allow session cleanup to finish before restarting the TV.

This port does not require applying upstream advertising, voice, browser, sidebar or other cleanup groups. Those are independent changes.

### Recovery

The supplied `restore-quiet-native.sh` restores the original Bixby Promotion assembly and its native-image cache, restores only that package's saved registry fields, and returns Home/startup to the previous **Dusk** launcher. It does not restore entire live databases or return to Samsung factory Home.

For that recovery path, **Dusk must remain installed**, and both `QuietBoot.dll` and `HomeAssign.dll` must remain staged where the script expects them. The intermediate `HomeAssign.dll restore` is required because the guarded assignment helper accepts a new destination only from Samsung's original `com.samsung.tv.csfs` main-operation handler. Preserve the first backup; do not overwrite it to make a retry succeed.

Restoration leaves extra Quiet resources inert for inspection. After recovery and temporary-root cleanup, verify Home, picture, audio and input switching. An installation with a different original Home or package baseline needs its own reviewed recovery path.

### Licenses

The pinned upstream MIT/NOTICE, Free My TV MIT, Inter OFL and Material Symbols Apache notices must accompany their corresponding code/assets. The build retains upstream notices under `quiet-home/QUIET-LICENSE`, `QUIET-NOTICE.md` and `licenses/`. See the root [UPSTREAM-NOTICE.md](../UPSTREAM-NOTICE.md) for provenance. These licenses do not grant rights to omitted videos or third-party publisher artwork.
