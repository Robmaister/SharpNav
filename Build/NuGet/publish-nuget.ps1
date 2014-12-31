foreach ($file in Get-ChildItem *.nuspec) {
	NuGet Pack $file | out-null
}

$packages = Get-ChildItem *.nupkg

"Packages to publish:"
$packages | % { write-host ("`t- " + $_.Name) }

$yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", "Publish"
$no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", "Don't Publish"
$options = [System.Management.Automation.Host.ChoiceDescription[]]($no, $yes)
 
$result = $host.ui.PromptForChoice("Upload packages", "Are you sure you want to publish these packages?", $options, 0)

if ($result -eq 0) {
	"Upload aborted, nothing published."
}
elseif ($result -eq 1) {
	foreach ($package in $packages) {
		NuGet Push $package
	}
}
