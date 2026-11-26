$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Test-UiaGui.ps1')
$results=Join-Path (Split-Path $PSScriptRoot) 'test-results'
[void][IO.Directory]::CreateDirectory($results)
$fake=Join-Path $results 'WuwaTappo.exe';[IO.File]::WriteAllText($fake,'invalid fixture, not executable')
try {
    if([TappoHelper.SetupWizard]::CheckExecutable($fake).Compatible){throw 'An unsupported file must never pass'}
    if([TappoHelper.SetupWizard]::CheckExecutable((Join-Path $results 'missing.exe')).Compatible){throw 'Missing path must not pass'}
    $official=Join-Path (Split-Path $PSScriptRoot) '..\WuwaTappo.exe'
    if(Test-Path -LiteralPath $official){if(-not [TappoHelper.SetupWizard]::CheckExecutable($official).Compatible){throw 'Local signed official update must pass without registering its hash'}}
    Copy-Item -LiteralPath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -Destination $fake -Force
    if([TappoHelper.SetupWizard]::CheckExecutable($fake).Compatible){throw 'Signed executable from another publisher must be rejected'}
    [IO.File]::WriteAllText($fake,'invalid fixture, not executable')
    $wizard=[TappoHelper.SetupWizard]::new($fake,'')
    try{
        $wizard.add_ValidateRequested({$wizard.SetValidation([TappoHelper.SetupWizard]::CheckExecutable($wizard.SelectedPath))})
        $wizard.Advance();if($wizard.Stage -ne 0){throw 'Invalid path advanced the wizard'}
    }finally{$wizard.Dispose()}
    $official=Join-Path (Split-Path $PSScriptRoot) '..\WuwaTappo.exe'
    if(Test-Path -LiteralPath $official){if(-not [TappoHelper.SetupWizard]::CheckExecutable($official).Compatible){throw 'Local signed official update must pass without registering its hash'}}
    Copy-Item -LiteralPath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -Destination $fake -Force
    if([TappoHelper.SetupWizard]::CheckExecutable($fake).Compatible){throw 'Signed executable from another publisher must be rejected'}
    [IO.File]::WriteAllText($fake,'invalid fixture, not executable')
    $wizard=[TappoHelper.SetupWizard]::new($fake,'')
    try{
        $ok=[TappoHelper.ExecutableCheck]::new();$ok.Compatible=$true;$ok.Message='fixture only'
        $wizard.SetValidation($ok);$wizard.Advance()
        if($wizard.Stage -ne 1){throw 'Validated path must reach connection stage'}
        $wizard.Advance();if($wizard.Stage -ne 1){throw 'Unconfirmed connection must not auto-advance'}
        $wizard.SetConnection($true,'fixture connected');$wizard.Advance()
        if($wizard.Stage -ne 2){throw 'Ready connection must reach instructions'}
        $wizard.Advance();if($wizard.DialogResult -ne 'OK'){throw 'Completion must return OK'}
    }finally{$wizard.Dispose()}
    'PASS: missing/unsupported paths rejected, connection gated, explicit completion; no official input or configuration writes'
}finally{Remove-Item -LiteralPath $fake -ErrorAction SilentlyContinue}
