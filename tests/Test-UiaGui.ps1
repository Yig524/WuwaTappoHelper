$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $repoRoot
$source=[IO.File]::ReadAllText((Join-Path $repoRoot 'UiaHelper.ps1'),[Text.Encoding]::UTF8)
# Exclude live-instance cleanup/locking and startup log writes from construction test.
$begin=$source.IndexOf('# Live instance selection.')
$finish=$source.IndexOf('. ([scriptblock]::Create(')
if($begin -lt 0 -or $finish -le $begin){throw 'Cannot isolate startup side effects'}
$source=$source.Remove($begin,$finish-$begin)
$officialBegin=$source.IndexOf('$exePath=');$officialEnd=$source.IndexOf('Add-Type -AssemblyName');$source=$source.Remove($officialBegin,$officialEnd-$officialBegin)
$source=$source.Replace("Write-UiaStartup 'begin'",'').Replace("Write-UiaStartup 'native-compiled'",'')
$source=$source.Replace('[void][TappoHelper.Native]::DetachOwnConsole()','')
$end=$source.IndexOf('$form.Add_Shown')
$checks=@'
if($fill.Checked -or $harvest.Checked -or $script:running -or $null -ne $script:activity){throw 'GUI must not enable input or hooks before explicit startup'}
if($script:session.Peek().Connected){throw 'Background UIA worker must not start before form shown'}
if($script:dashboard.CountControls() -lt 30){throw 'Missing dashboard controls'}
if($form.IsHandleCreated){throw 'GUI construction must not register global hotkeys before display'}
$fixture=[TappoHelper.Snapshot]::new();$fixture.Connected=$true;$fixture.BalanceValid=$true;$fixture.Balance=2000;$fixture.Identity='fixture';$fixture.UtcTicks=[DateTime]::UtcNow.Ticks;$fixture.Left.Ready=$true
[TappoHelper.Session].GetField('latest',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($script:session,$fixture)
Request-UiaClaim 'left' $false
if($script:pending -ne 0 -or $script:state.Pending -ge 0 -or $script:inputProtection -ne 'observer_unavailable'){throw 'Automatic claim must defer safely when input cannot be observed'}
if($null -ne $script:session.LastResult()){throw 'Deferred automatic claim must not execute an Invoke'}
$timer.Dispose();$script:session.Dispose();$script:trayHost.Dispose();$form.Dispose();$script:dashboard.Dispose()
Write-Output 'PASS: GUI construction, both automation switches off, no input hook or UIA worker started'
Write-Output 'PASS: automatic claim safely defers with no observer, without queuing or claiming a box'
'@
& ([scriptblock]::Create($source.Substring(0,$end)+$checks))
