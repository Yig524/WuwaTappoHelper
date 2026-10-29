$ErrorActionPreference='Stop'
$package=Join-Path $PSScriptRoot 'dist\WuwaTappoHelper'
[void][IO.Directory]::CreateDirectory($package)
& (Join-Path $PSScriptRoot 'Build-UiaHost.ps1') -OutputPath (Join-Path $package 'WuwaTappoHelper.exe')
$files=@('WuwaTappoHelper.exe.config','UiaHelper.ps1','UiaEngine.cs','Dashboard.cs','SetupWizard.cs','SmartLogic.ps1','Run-UiaHelper.ps1','README.md','CHANGELOG.md','开始使用.html','LICENSE','THIRD_PARTY_NOTICES.md','helper-config.example.json')
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $package $name) -Force}
foreach($dir in @('assets','third-party','docs')){[void][IO.Directory]::CreateDirectory((Join-Path $package $dir))}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\screenshot.png') -Destination (Join-Path $package 'docs\screenshot.png') -Force
foreach($name in @('INSTALL.md','DISCLAIMER.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('docs\'+$name)) -Destination (Join-Path $package ('docs\'+$name)) -Force}
foreach($name in @('app.ico','app-icon.png')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('assets\'+$name)) -Destination (Join-Path $package ('assets\'+$name)) -Force}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'third-party\WuwaTappoHelper-LICENSE.txt') -Destination (Join-Path $package 'third-party\WuwaTappoHelper-LICENSE.txt') -Force
# Zip only declared release files, even if this output folder has been run locally.
# Local logs/configuration in an existing output folder must never enter a release.
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$zipFiles=@('WuwaTappoHelper.exe')+$files+@('docs\screenshot.png','docs\INSTALL.md','docs\DISCLAIMER.md','assets\app.ico','assets\app-icon.png','third-party\WuwaTappoHelper-LICENSE.txt')
$stream=[IO.File]::Open((Join-Path $PSScriptRoot 'dist\WuwaTappoHelper-windows.zip'),[IO.FileMode]::Create)
$archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($name in $zipFiles){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $package $name),('WuwaTappoHelper/'+$name.Replace('\','/')))}}finally{$archive.Dispose();$stream.Dispose()}

# Package only reviewed source paths. Never include local configuration or reports.
$sourceFiles=@('.gitignore','.gitattributes','AppearanceAssets.cs','Build-AppearanceAssets.ps1','Build-Release.ps1','Build-UiaHost.ps1','CHANGELOG.md','Dashboard.cs','helper-config.example.json','Launch-UiaHelper.ps1','LICENSE','README.md','Run-Tests.ps1','Run-UiaHelper.ps1','SetupWizard.cs','SmartLogic.ps1','Start.cmd','THIRD_PARTY_NOTICES.md','UiaEngine.cs','UiaHelper.ps1','UiaHost.cs','WuwaTappoHelper.exe.config','开始使用.html')
foreach($dir in @('assets','docs','tests','third-party')){
    foreach($item in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $dir) -File -Recurse){
        $sourceFiles+= $item.FullName.Substring($PSScriptRoot.Length+1)
    }
}
$sourceStream=[IO.File]::Open((Join-Path $PSScriptRoot 'dist\WuwaTappoHelper-source.zip'),[IO.FileMode]::Create)
$sourceArchive=[IO.Compression.ZipArchive]::new($sourceStream,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($name in $sourceFiles){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceArchive,(Join-Path $PSScriptRoot $name),('WuwaTappoHelper/'+$name.Replace('\','/')))}}finally{$sourceArchive.Dispose();$sourceStream.Dispose()}

$checksums=foreach($name in @('WuwaTappoHelper-windows.zip','WuwaTappoHelper-source.zip')){
    $hash=Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('dist\'+$name)) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant()+'  '+$name
}
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'dist\SHA256SUMS.txt'),$checksums,[Text.UTF8Encoding]::new($false))
