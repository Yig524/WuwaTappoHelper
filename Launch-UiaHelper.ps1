# ASCII launcher: standard UAC only, no changes to system policy or official files.
$ErrorActionPreference='Stop'
function New-UiaLaunchOptions([string]$helperRoot,[bool]$alreadyElevated){
    $options=@{
        FilePath=(Join-Path $helperRoot 'WuwaTappoHelper.exe')
        WindowStyle='Normal'
        WorkingDirectory=$helperRoot
    }
    if(-not $alreadyElevated){$options.Verb='RunAs'}
    return $options
}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=[Security.Principal.WindowsPrincipal]::new($identity)
$alreadyElevated=$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
# The GUI executable hosts PowerShell in-process; it never allocates a console.
if(-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'WuwaTappoHelper.exe'))){& (Join-Path $PSScriptRoot 'Build-UiaHost.ps1')|Out-Null}
$launchOptions=New-UiaLaunchOptions $PSScriptRoot $alreadyElevated
Start-Process @launchOptions
exit
