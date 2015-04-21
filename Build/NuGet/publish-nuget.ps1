$msbuild = "C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
$buildfile = "../CoreOnly.proj"
$configs = "Standalone", "OpenTK", "MonoGame", "SharpDX"

""
"                BUILDING                 "
"========================================="
""
foreach ($config in $configs) {
	$config
	"---------------------------"
	&$msbuild ($buildfile, '/m', '/nologo', '/v:m', '/clp:Summary', ('/p:configuration=' + $config))
	if (!$?) {
		write-host "MSBuild exited with an error, aborting." -foregroundcolor "red"
		exit
	}
	"---------------------------"
	""
}
""
"                 PACKING                 "
"========================================="
""
foreach ($file in Get-ChildItem *.nuspec) {
	$file.Name.TrimEnd($file.Extension)
	"---------------------------"
	NuGet Pack $file
	"---------------------------"
	""
}
""

""
"               PUBLISHING                "
"========================================="
""
"Packages ready to publish:"
$packages = Get-ChildItem *.nupkg
$packages | % { write-host ("  - " + $_.Name) }
""

$yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", "Publish"
$no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", "Don't Publish"
$options = [System.Management.Automation.Host.ChoiceDescription[]]($no, $yes)
 
$result = $host.ui.PromptForChoice("", "Are you sure you want to publish these packages?", $options, 0)

if ($result -eq 0) {
	"Upload aborted, nothing published."
}
elseif ($result -eq 1) {
	foreach ($package in $packages) {
		NuGet Push $package
	}
}

""
"Done."
""
