param([string]$OutputPath='')
# Run with the system Windows PowerShell 5.1. Compiles only the separate helper.
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)){$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
$hostPath=if($OutputPath){[IO.Path]::GetFullPath($OutputPath)}else{Join-Path $PSScriptRoot 'WuwaTappoHelper.exe'}
$iconPath=Join-Path $PSScriptRoot 'assets\app.ico'
if(-not (Test-Path -LiteralPath $iconPath) -or (Get-Item -LiteralPath $iconPath).LastWriteTimeUtc -lt (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'AppearanceAssets.cs')).LastWriteTimeUtc){& (Join-Path $PSScriptRoot 'Build-AppearanceAssets.ps1')}
$automationAssembly=[Management.Automation.PowerShell].Assembly.Location
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ (('/out:{0}' -f $hostPath)) (('/win32icon:{0}' -f $iconPath)) (('/reference:{0}' -f $automationAssembly)) /reference:System.Windows.Forms.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'UiaHost.cs')
if($LASTEXITCODE -ne 0){throw 'GUI host compilation failed'}
Write-Output $hostPath
