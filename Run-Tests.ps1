$ErrorActionPreference='Stop'
[void][IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot 'test-results'))
$powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
foreach($name in @('Test-Uia.ps1','Test-UiaGui.ps1','Test-SetupWizard.ps1','Test-SetupCancel.ps1','Test-TrayLifecycle.ps1','Test-FocusControls.ps1','Test-Launcher.ps1')){
    & $powershell -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot ('tests\'+$name))
    if($LASTEXITCODE -ne 0){throw ('Failed: '+$name)}
}
