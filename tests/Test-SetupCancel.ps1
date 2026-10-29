$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$fixture=Join-Path $repo ('test-results\setup-cancel-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
foreach($file in @('UiaEngine.cs','Dashboard.cs','SetupWizard.cs')){Copy-Item -LiteralPath (Join-Path $repo $file) -Destination (Join-Path $fixture $file)}
$configPath=Join-Path $fixture 'helper-config.json'
$before='{"OfficialExecutable":"missing/WuwaTappo.exe","ExtraSetting":"preserve","SetupVersion":0}'
[IO.File]::WriteAllText($configPath,$before,[Text.UTF8Encoding]::new($false))
$source=[IO.File]::ReadAllText((Join-Path $repo 'UiaHelper.ps1'),[Text.Encoding]::UTF8)
$source=$source.Substring(0,$source.IndexOf('. ([scriptblock]::Create('))
$source=$source.Replace('Local\WuwaTappoF24UIA',('Local\SetupCancelFixture'+[Guid]::NewGuid().ToString('N')))
$source=$source.Replace('[void][TappoHelper.Native]::DetachOwnConsole()','')
# Execute the real startup cancellation branch without displaying/activating a
# dialog. No validation/connection button is pressed, so no UIA worker is started.
$source=$source.Replace('$wizard.ShowDialog()','[Windows.Forms.DialogResult]::Cancel')
$oldLocation=Get-Location
try{
    Set-Location -LiteralPath $fixture
    & ([scriptblock]::Create($source))
    if([IO.File]::ReadAllText($configPath,[Text.Encoding]::UTF8) -ne $before){throw 'Cancellation modified existing configuration'}
    if(Test-Path -LiteralPath ($configPath+'.tmp')){throw 'Cancellation created a pending configuration'}
    'PASS: real startup cancellation preserves configuration and creates no temporary config; no dialog, hooks, UIA or input'
}finally{
    Set-Location -LiteralPath $oldLocation.Path
    $expected=[IO.Path]::GetFullPath((Join-Path $repo 'test-results'))+[IO.Path]::DirectorySeparatorChar
    if([IO.Path]::GetFullPath($fixture).StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $fixture -Recurse -Force}
}
