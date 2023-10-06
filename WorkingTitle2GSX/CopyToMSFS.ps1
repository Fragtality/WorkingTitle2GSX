# powershell -ExecutionPolicy Unrestricted -file "$(ProjectDir)CopyToMSFS.ps1" $(ConfigurationName)
$buildConfiguration = $args[0]
$baseDir = "C:\Users\Fragtality\source\repos\WorkingTitle2GSX\WorkingTitle2GSX"
$bindir = "$baseDir\bin\$buildConfiguration"
$destDir = "F:\MSFS2020\WorkingTitle2GSX\"

$makeZip = $true
$overrideZip = $true
$version = "latest"

if ($buildConfiguration -eq "Release") {
	Write-Host "Stop Binary ..."
	Stop-Process -Name "WorkingTitle2GSX" -ErrorAction SilentlyContinue
	Write-Host "Copy new Binaries ..."
	Copy-Item -Path ($bindir + "\WorkingTitle2GSX.exe") -Destination $destDir -Recurse -Force
	# Copy-Item -Path ($bindir + "\WorkingTitle2GSX.exe.config") -Destination $destDir -Recurse -Force
	Copy-Item -Path ($bindir + "\*.dll") -Destination $destDir -Recurse -Force
	
	if ($makeZip) {
		Write-Host "Build Archive."
		$releaseDir = "C:\Users\Fragtality\source\repos\WorkingTitle2GSX\WorkingTitle2GSX\Releases"
		$pluginDir = "WorkingTitle2GSX"
		Copy-Item -Path ($bindir + "\WorkingTitle2GSX.exe") -Destination ($releaseDir + "\" + $pluginDir) -Recurse -Force
		Copy-Item -Path ($bindir + "\WorkingTitle2GSX.exe.config") -Destination ($releaseDir + "\" + $pluginDir) -Recurse -Force
		Copy-Item -Path ($bindir + "\*.dll") -Destination ($releaseDir + "\" + $pluginDir) -Recurse -Force
		$workDir = ($releaseDir + "\" + $pluginDir)
		$zipFile = ("WorkingTitle2GSX-" + $version + ".zip")

		if ($overrideZip -or -not(Test-Path -Path ($releaseDir + "\" + $zipFile))) {
			Write-Host "Zipping Binaries ..."
			& "C:\Program Files\7-Zip\7z.exe" a -tzip ($releaseDir + "\" + $zipFile) $workDir | Out-Null
			Write-Host "Creating Release File for $version ..."
			if ($version -eq "latest") {
				Write-Host "Copy latest File .."
				Copy-Item -Path ($releaseDir + "\" + $zipFile)  -Destination "C:\Users\Fragtality\source\repos\WorkingTitle2GSX\"
			}
		}
	}
}

exit 0
