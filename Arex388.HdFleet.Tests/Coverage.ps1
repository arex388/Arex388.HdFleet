#	Generates an HTML code coverage report for Arex388.HdFleet.Tests and drops a
#	Coverage.lnk shortcut to it. Run from this directory.
#
#	Requires the local tools from ../.config/dotnet-tools.json ('dotnet tool restore').
#	The default run is offline (unit tests only); live integration tests are skipped
#	unless HDFLEET_LIVE_TESTS=1 is set and the 'key' user secret exists.

Remove-Item -Path 'reports' -Recurse -ErrorAction SilentlyContinue;
Remove-Item -Path 'TestResults' -Recurse -ErrorAction SilentlyContinue;
Remove-Item -Path 'Coverage.lnk' -ErrorAction SilentlyContinue;

Write-Host 'Building';

dotnet build > $null;

if ($LASTEXITCODE -ne 0) {
	Write-Error 'Build failed.';

	exit 1;
}

Write-Host 'Collecting';

dotnet test --no-build --collect:'XPlat Code Coverage' > $null;

if ($LASTEXITCODE -ne 0) {
	Write-Error 'Tests failed.';

	exit 1;
}

$coberturaPath = Get-ChildItem -Path 'TestResults' -Recurse -Filter 'coverage.cobertura.xml' | Select-Object -First 1;

if ($null -eq $coberturaPath) {
	Write-Error 'No coverage.cobertura.xml was produced by the test run.';

	exit 1;
}

Write-Host 'Reporting';

dotnet reportgenerator -reports:$coberturaPath.FullName -targetdir:'reports' -reporttypes:'Html' > $null;

$indexPath = Get-ChildItem -Path 'reports' -Recurse -Filter 'index.html' | Select-Object -First 1;

if ($null -eq $indexPath) {
	Write-Error 'No report was generated.';

	exit 1;
}

Write-Host 'Shortcutting';

$wscriptShell = New-Object -ComObject WScript.Shell;
$shortcut = $wscriptShell.CreateShortcut("$pwd\Coverage.lnk");
$shortcut.TargetPath = $indexPath.FullName;
$shortcut.Save();

Remove-Item -Path 'TestResults' -Recurse -ErrorAction SilentlyContinue;
