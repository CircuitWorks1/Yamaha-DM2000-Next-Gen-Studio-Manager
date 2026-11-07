# DM2000 Studio

An independent Windows desktop controller for the Yamaha DM2000. Written in C# with Windows Forms on .NET Framework 4, using Windows WinMM MIDI and Yamaha parameter-change SysEx messages. This project is not affiliated with Yamaha.

## Features

- Four banks of 24 input channels with wide faders and integrated meters.
- Pan, mute, solo, editable long and short channel names.
- Buses, aux masters, matrices, and a single stereo master fader with two meters.
- Aux sends on faders, send enable, and pre/post controls.
- Movable, resizable inspector with source, names, card labels, metering, and channel destinations.
- Separate input/output patching and signal-flow window, with routing readback confirmation.
- Source and card metadata captured at startup; confirmed app routing changes update source labels.
- Card model aliases saved locally. Numeric card IDs are not automatically mapped to product names.

## Zero inputs

The **Zero inputs** toolbar button sets all 96 main input faders to **0 dB** and centers their pans. Aux-send levels, bus/aux/matrix output faders, and the stereo master are unchanged. Each channel is freshly read, written, and confirmed through a separate readback. The toolbar shows progress; **Stop zero** stops further channels and retains changes already made. Missing confirmation stops the operation and identifies the channel to check. No reset runs automatically at startup.

Offline self-tests cover all 96 targets, stale data rejection, readback confirmation, cancellation, and timeout handling. These tests do not reset the console.

## Requirements

Windows x64, .NET Framework 4 or a compatible later 4.x runtime, and the Yamaha USB-MIDI driver for hardware access. The build script uses the standard Windows .NET Framework C# compiler; no NuGet packages are required. A connected console is not needed to build or run the offline checks.

## Build

Extract the complete folder, open Windows PowerShell in it, and run:

```powershell
.\build.ps1
```

If local PowerShell policy prevents running a downloaded script, review the script and use a process-only policy for this build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The output is `DM2000 Studio Faders.exe` in this folder. Keep the included `.txt`, `.csv`, and `routing-*.tsv` data files beside it.

## Run and connect

Launch the executable, choose the Yamaha USB-MIDI port configured for parameter changes on your console, and click Connect. Console MIDI parameter transmission/reception must be configured correctly. Connection configuration is saved locally in `connection.ini`; device numbering in that file is zero based. Port choices vary with the console setup. This source package contains no saved connection or personal card labels.

Opening or reconnecting does not upload saved mix levels. Controls write to the physical console when edited. Routing changes are checked against current desk state and confirmed by readback.

## Validation

Offline checks:

```powershell
& '.\DM2000 Studio Faders.exe' --self-test
& '.\DM2000 Studio Faders.exe' --layout-test
```

The executable is a GUI application; reports are written beside it (`test-result.txt`, `layout-test.txt`). To wait for an exit code from PowerShell, use `Start-Process -Wait -PassThru`.

Hardware-dependent checks and probes are included in the source. Several write tests temporarily alter console controls, and routing tests send same-value writes. Read each test before running it against an active system.

## Source guide

- `Studio.cs`, `Responsive.cs`, `MixerUi.cs`: main window and channel strips.
- `InspectorLayout.cs`, `InspectorDestinations.cs`, `SlotCards.cs`: inspector layout, routing controls, and persistent card labels.
- `FaderConnection.cs`, `MixerFeatures.cs`: background connection, parameter addressing, state, and write queues.
- `Faders.cs`, `FaderWriting.cs`, `Pan.cs`, `Meters.cs`: MIDI transport, fader/pan controls, and meters.
- `StereoMaster.cs`: linked stereo master behavior.
- `InputSources.cs`, `StartupSources.cs`: source labels and startup snapshots.
- `Routing.cs`: patching window and verified routing transactions.
- `ZeroInputs.cs`: input reset operation, progress, stop control, and offline checks.
- `FeatureTests.cs`, `LatencyTest.cs`: additional validation and probes.

## Protocol references and data

The included numeric conversion and patch-label tables are derived from Yamaha's DM2000 V2 protocol documentation. Yamaha documentation archives, drivers, and manuals are not bundled.

- [DM2000 V2 MIDI specification download](https://usa.yamaha.com/files/download/other_assets/8/311518/dm2kv2_midiptcl100.zip)
- [DM2000 V2 owner's manual](https://data.yamaha.com/files/download/other_assets/7/334227/dm2000v2_en_om_g0.pdf)

The flow view groups channel processing; detailed insert order, direct-out destination editing, and aux paths are not expanded. The project is an evolving controller, not a complete replacement for every Studio Manager function.

## License

No open-source license has been selected for this source snapshot. Add your chosen license before inviting reuse or contributions. Yamaha names and referenced materials remain those of their respective owners.
