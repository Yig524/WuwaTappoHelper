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
if($fill.Checked -or $harvest.Checked -or $null -ne $script:activity){throw 'GUI must not enable input or hooks before explicit startup'}
if($script:session.Peek().Connected){throw 'Background UIA worker must not start before form shown'}
if($script:dashboard.CountControls() -lt 30){throw 'Missing dashboard controls'}
if($form.IsHandleCreated){throw 'GUI construction must not register global hotkeys before display'}
# Exercise the actual production timer with synthetic snapshots only.
$script:identity='fixture';$harvest.Checked=$true;$script:sourceRejected=$false
$tick=[Windows.Forms.Timer].GetMethod('OnTick',[Reflection.BindingFlags]'Instance,NonPublic')
$latestField=[TappoHelper.Session].GetField('latest',[Reflection.BindingFlags]'Instance,NonPublic')
for($i=1;$i -le 4;$i++){
    $unreadable=[TappoHelper.Snapshot]::new();$unreadable.Sequence=$i;$unreadable.UtcTicks=[DateTime]::UtcNow.Ticks
    $latestField.SetValue($script:session,$unreadable);$tick.Invoke($timer,@([EventArgs]::Empty))
    if(-not $harvest.Checked -or $script:state.Action -ne 'wait'){throw 'Transient UIA loss must retain enabled task with all actions suspended'}
}
$recovered=[TappoHelper.Snapshot]::new();$recovered.Connected=$true;$recovered.BalanceValid=$true;$recovered.Identity='fixture';$recovered.Balance=500
for($i=5;$i -le 6;$i++){
    $recovered.Sequence=$i;$recovered.UtcTicks=[DateTime]::UtcNow.Ticks
    $latestField.SetValue($script:session,$recovered);$tick.Invoke($timer,@([EventArgs]::Empty))
    if(-not $harvest.Checked){throw 'Same-window recovery must not disable automation'}
    if($i -eq 5 -and $script:state.RecoveryReads -ne 1){throw 'First fresh sample must remain in recovery'}
}
if($script:state.InvalidReads -ne 0){throw 'Two fresh samples must finish recovery'}
# Balance drop and non-invoked claim result must recover cleanly without unchecking tasks.
$script:state.LastBalance=2000;$recovered.Balance=1000;$recovered.Sequence=7;$recovered.UtcTicks=[DateTime]::UtcNow.Ticks
$latestField.SetValue($script:session,$recovered);$tick.Invoke($timer,@([EventArgs]::Empty))
if(-not $harvest.Checked -or $script:state.LastBalance -ne 1000){throw 'Balance decrease must re-baseline without disabling always-on tasks'}
$script:pending=99;$script:ack=$false;$script:state.Pending=0
$rejectedResult=[TappoHelper.InvokeResult]::new();$rejectedResult.Id=99;$rejectedResult.Status='rejected';$rejectedResult.Error='Fresh UIA state does not permit this claim'
[TappoHelper.Session].GetField('result',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($script:session,$rejectedResult)
$recovered.Sequence=8;$recovered.UtcTicks=[DateTime]::UtcNow.Ticks
$latestField.SetValue($script:session,$recovered);$tick.Invoke($timer,@([EventArgs]::Empty))
if(-not $harvest.Checked -or $script:pending -ne 0 -or $script:state.Pending -ge 0){throw 'Rejected automatic claim must back off without disabling always-on tasks'}
Stop-Uia 'test-stop'
if($fill.Checked -or $harvest.Checked){throw 'Explicit stop must turn off both task switches'}
[TappoHelper.Session].GetField('result',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($script:session,$null)
$script:state=New-SmartState;$script:inputProtection=''
$fixture=[TappoHelper.Snapshot]::new();$fixture.Connected=$true;$fixture.BalanceValid=$true;$fixture.Balance=2000;$fixture.Identity='fixture';$fixture.UtcTicks=[DateTime]::UtcNow.Ticks;$fixture.Left.Ready=$true;$fixture.Left.Present=$true
[TappoHelper.Session].GetField('latest',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($script:session,$fixture)
Request-UiaClaim 'left' $false
if($script:pending -ne 0 -or $script:state.Pending -ge 0 -or $script:inputProtection -ne 'observer_unavailable'){throw 'Automatic claim must defer safely when input cannot be observed'}
if($null -ne $script:session.LastResult()){throw 'Deferred automatic claim must not execute an Invoke'}
$timer.Dispose();$script:session.Dispose();$script:trayHost.Dispose();$form.Dispose();$script:dashboard.Dispose()
Write-Output 'PASS: GUI construction, both automation switches off, no input hook or UIA worker started'
Write-Output 'PASS: automatic claim safely defers with no observer, without queuing or claiming a box'
'@
& ([scriptblock]::Create($source.Substring(0,$end)+$checks))
