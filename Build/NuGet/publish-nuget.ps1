$metadataFile = "./Metadata.xml"

#make sure at least the metadata file exists
if (-not (test-path $metadataFile)) {
	throw "Could not find Metadata.xml in the current directory."
}

[xml]$metaXml = Get-Content $metadataFile

#Merge metadata file with each target
foreach ($file in Get-ChildItem SharpNav*.xml) {
	[xml]$xml = Get-Content $file
	#merge top-level nodes
	foreach ($metaXmlNode in $metaXml.package.ChildNodes) {
		$xmlNode = $xml.SelectSingleNode("//package/" + $metaXmlNode.Name)
		if ($xmlNode) {
			foreach ($childNode in $metaXmlNode.ChildNodes) {
				$newNode = $xml.ImportNode($childNode, $true)
				$xmlNode.AppendChild($newNode) | out-null
			}
		}
		else {
			$newNode = $xml.ImportNode($metaXmlNode, $true)
			$xml.package.AppendChild($newNode) | out-null
		}
	}

	#save edited node
	($xml).Save((Join-Path -Path $file.DirectoryName -ChildPath ($file.BaseName + ".nuspec")))
}

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
