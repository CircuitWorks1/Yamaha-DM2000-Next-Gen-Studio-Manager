$ErrorActionPreference = 'Stop'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /out:"$PSScriptRoot\DM2000 Studio Faders.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\Studio.cs" "$PSScriptRoot\Faders.cs" "$PSScriptRoot\LatencyTest.cs" "$PSScriptRoot\FaderConnection.cs" "$PSScriptRoot\Meters.cs" "$PSScriptRoot\Responsive.cs" "$PSScriptRoot\FaderWriting.cs" "$PSScriptRoot\Pan.cs" "$PSScriptRoot\MixerFeatures.cs" "$PSScriptRoot\MixerUi.cs" "$PSScriptRoot\FeatureTests.cs" "$PSScriptRoot\StereoMaster.cs" "$PSScriptRoot\InputSources.cs" "$PSScriptRoot\SlotCards.cs" "$PSScriptRoot\StartupSources.cs" "$PSScriptRoot\Routing.cs" "$PSScriptRoot\InspectorDestinations.cs" "$PSScriptRoot\InspectorLayout.cs" "$PSScriptRoot\ZeroInputs.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }













