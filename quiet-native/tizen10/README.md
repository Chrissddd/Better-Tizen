# Quiet Home for Tizen 10

The standalone .NET/NUI port installs as **`org.bettertizen.home`**, version
**1.0.2**. It keeps its resources and preferences inside its own application
directories. It does not replace Bixby Promotion or write Samsung's main-operation
default-app assignment.

The source was adapted and tested on Samsung **TQ65QN85FAUXXC**, Tizen 10.0,
ARMv7 and .NET 8.0.11. Device-specific private APIs and source enum mappings need
verification on another model. The older Tizen 9 container implementation remains
in the parent directory.

`AppLibrary.cs`, `NativeData.cs` and `NativeAmbient.cs` are shared through project
links to the parent source. The Tizen 10 folder contains its adapted UI,
Home watcher, guarded startup setup and optional private diagnostics.

## Build an unsigned review package

Initialize the pinned Quiet submodule, install Python 3 and the .NET 8 SDK, then
run from the repository root:

```sh
git submodule update --init --recursive
python3 quiet-native/tizen10/build.py --dotnet /path/to/dotnet --unsigned
```

`--dotnet` also accepts `DOTNET` or the executable on `PATH`. The builder downloads
checksum-pinned official NuGet packages into ignored local storage:

- `Tizen.NET.API13` version `13.0.0.19198` supplies compile-time references.
- `Tizen.NET.Sdk` version `1.1.9` supplies Samsung's official signing task when
  signing is requested.

Reference assemblies are not bundled. The project targets `net8.0` and references
the API13 assemblies directly, so a separate Tizen workload is not required for
this build. NuGet stores API13 references under `ref/net6.0`; they still describe
Tizen API13. A full .NET 8 SDK is required, rather than only its runtime.

Output is under `quiet-native/tizen10/.local/`: `build/`, `stage/`, `packages/`,
`packages-out/` and `build.json`. An unsigned TPK is for inspection and cannot be
installed as a signed Samsung application. Building never contacts a TV.

## Sign with an existing SDK profile

Use certificates already issued for your own TV. This builder does not create a
certificate, register a device, sign into Samsung or change SDK configuration.

```sh
python3 quiet-native/tizen10/build.py \
  --dotnet /path/to/dotnet \
  --profile MyTV \
  --profiles /private/path/profiles.xml \
  --password-file /private/path/certificate-password.env
```

`--profiles` selects the SDK's profile list. Without it the builder checks
`TIZEN_PROFILES`, a `tizen` CLI on `PATH`, then the standard user SDK data folder.
The named profile supplies certificate paths; its encrypted SDK passwords are
not treated as plaintext by the independent .NET signing task.

The explicit password file accepts either a plaintext password or a line such
as `TIZEN_CERTIFICATE_PASSWORD='your password'`. Alternatively set the
`TIZEN_CERTIFICATE_PASSWORD` environment variable. Profiles with different author
and distributor passwords can use `TIZEN_AUTHOR_PASSWORD`,
`TIZEN_DISTRIBUTOR_PASSWORD` and optionally `TIZEN_DISTRIBUTOR2_PASSWORD`.
Passwords are passed in the signing process environment and redacted from
failure output; they are not written to the generated signing project.

The package is signed by Samsung's unmodified `Tizen.NET.Build.Tasks.Sign` task.
`--profile` and `--unsigned` are mutually exclusive. The build report's
`installed: false` refers to the generated artifact, not the live TV.

## Optional local inputs

Default builds omit private media, publisher bitmaps, diagnostics, settings seeds
and usage history. Missing optional artwork uses the app name. Without ambient
media the large card retains its neutral background.

Supply optional files explicitly:

```sh
python3 quiet-native/tizen10/build.py --dotnet /path/to/dotnet --unsigned \
  --media-dir /private/path/ambient-media \
  --artwork-dir /private/path/publisher-artwork
```

`--media-dir` accepts the matching `snoopy-NAME.mp4` and `snoopy-NAME.jpg` pairs
listed in the parent native README. Only complete known pairs are packaged.
`--artwork-dir` can be repeated; PNG/JPEG/SVG files with catalog filenames such as
`nuvio.png` or `disneyplus.png` override the optional artwork. The public SVGs,
fonts, UI icons and license notices are retained by the shared asset packager.

`--diagnostics-config /private/path/config.json` explicitly includes a private
resource configuration. Without it there are no diagnostic callbacks, QA page
captures or automatic startup-selector writes. The application still reads local
foreground/power state for Home re-entry and requests weather normally.

The private configuration's `configureStartup` flag opts into the guarded
startup setup. It also needs a valid local diagnostic callback configuration
(`host`, `port`, and a 32–256-character hexadecimal `token`); see
`src/NativeDiagnostics.cs`. Capture options are `capturePages` and
`captureHomeOnly`. No device-specific configuration is imported automatically.

## Home and startup behavior

The owner-local Home watcher observes the foreground application, waits for a
stable Samsung Home state, confirms current power-on state and requests its own
UI. External app/input launch grace periods prevent it from interrupting those
transitions. It does not rewrite Samsung's main-operation default handler.

Explicit startup configuration changes only `db/appfw/home_appid` and
`db/appfw/startup_appid`, targeting its own installed app. It requires the
recorded Bixby Promotion baseline and auto-start already equal to 1. Auto-start
is never written. A durable journal records the original values before either
string changes; readback must stabilize, and failures roll back only known owned
destinations. Unknown states are refused. This is a guarded recipe for the
tested TV's state, not a universal replacement for another device's settings.

Review and verify the UI before opting into startup setup. Home return,
standby/wake and cold startup are distinct live checks; a successful build or
SDK launch does not establish all of them.

After reviewing the package and preparing your own Developer Mode connection,
standard SDK deployment is:

```sh
tizen install -s TV_IP:26101 -n org.bettertizen.home-1.0.2.tpk -- quiet-native/tizen10/.local/packages-out
tizen run -s TV_IP:26101 -p org.bettertizen.home
```

The builder performs neither command. Do not remove an installed Home target
before restoring its recorded selectors to a valid installed destination.

## Offline checks

These checks do not contact a TV:

```sh
python3 -m unittest discover -s quiet-native/tizen10/tests -p 'test_build.py' -v
dotnet run --project quiet-native/tizen10/tests/DataTests.csproj -c Release
python3 quiet-native/tizen10/tests/run_startup_tests.py --dotnet /path/to/dotnet
```

The startup suite compiles a fake local vconf library with `cc` and supports
macOS/Linux. It exercises clean setup, interrupted states, rollback, concurrent
changes, invalid journals and unexpected auto-start values. The data suite uses
the parent's synthetic fixtures. Packaging checks cover explicit private inputs,
fallback artwork, archive paths and password/profile handling.

Code and asset attribution is preserved in the root `UPSTREAM-NOTICE.md` and in
the packaged Quiet/third-party notices. This folder's source contains no SDK,
certificates, framework binaries, private reports or ambient media.
