# ASCII entry wrapper: surface startup failures rather than silently exiting.
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
try {
    $source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UiaHelper.ps1'),[Text.Encoding]::UTF8)
    & ([scriptblock]::Create($source))
} catch {
    $directory=Join-Path $PSScriptRoot 'diagnostics'
    [void][IO.Directory]::CreateDirectory($directory)
    $message=$_.Exception.Message+[Environment]::NewLine+$_.ScriptStackTrace
    [IO.File]::AppendAllText((Join-Path $directory 'uia-startup.log'),$message+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
    Add-Type -AssemblyName System.Windows.Forms
    [void][Windows.Forms.MessageBox]::Show($message,'Wuwa Tappo helper startup error')
    throw
}
