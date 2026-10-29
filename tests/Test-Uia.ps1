$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach($file in @('UiaHelper.ps1','Launch-UiaHelper.ps1','Run-UiaHelper.ps1')){
    $tokens=$null;$errors=$null
    [void][Management.Automation.Language.Parser]::ParseInput([IO.File]::ReadAllText((Join-Path $root $file),[Text.Encoding]::UTF8),[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ($errors|Out-String)}
}
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase,System.Windows.Forms,System.Drawing
$refs=@([Windows.Automation.AutomationElement].Assembly.Location,[Windows.Automation.AutomationProperty].Assembly.Location,[Windows.Point].Assembly.Location,[Windows.Forms.Form].Assembly.Location,[Drawing.Bitmap].Assembly.Location,[Collections.Generic.HashSet[int]].Assembly.Location,[regex].Assembly.Location)|Select-Object -Unique
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $root 'UiaEngine.cs'),[Text.Encoding]::UTF8)) -ReferencedAssemblies $refs
function Assert($condition,[string]$message){if(-not $condition){throw $message}}
function Node([string]$cls,[string]$name,[string]$parents,[int]$index=0){
    $n=[TappoHelper.Node]::new();$n.Classes=$cls;$n.Name=$name;$n.Ancestors=$parents;$n.Enabled=$true;$n.CanInvoke=$true;$n.Index=$index;return $n
}
$balance=Node 'text web-bold-font' '3,903' 'pet-info;pet-info-container' 1
$unrelated=Node 'uid' '999999999' 'account-panel' 2
$left=Node 'reward-btn extra-state' '' 'reward-anchor reward-anchor--left' 3
$right=Node 'reward-countdown' '12:34' 'reward-anchor reward-anchor--right' 4
$s=[TappoHelper.Semantics]::Evaluate([TappoHelper.Node[]]@($unrelated,$right,$balance,$left))
Assert ($s.BalanceValid -and $s.Balance -eq 3903) 'Balance must be scoped; never take largest unrelated number'
Assert ($s.Left.Ready -and $s.Left.Index -eq 3 -and $s.Right.State -eq 'cooldown') 'Parent container defines side, independent of enumeration order or extra classes'
$onlyRight=Node 'reward-btn' '' 'reward-anchor reward-anchor--right' 5
$single=[TappoHelper.Semantics]::Evaluate([TappoHelper.Node[]]@($balance,$onlyRight))
Assert (-not $single.Left.Present -and $single.Right.Ready) 'A lone right chest must not become left'
$disabled=Node 'reward-btn reward-btn--loading' '' 'reward-anchor--left' 2
$blocked=[TappoHelper.Semantics]::Evaluate([TappoHelper.Node[]]@($balance,$disabled))
Assert (-not $blocked.Left.Ready) 'Loading button must not be claimable'
$secondBalance=Node 'text' '3904' 'pet-info' 9
$ambiguous=[TappoHelper.Semantics]::Evaluate([TappoHelper.Node[]]@($balance,$secondBalance))
Assert (-not $ambiguous.BalanceValid) 'Ambiguous counter cannot permit actions'
$capped=Node 'text' '99999999+' 'pet-info' 1
Assert (-not ([TappoHelper.Semantics]::Evaluate([TappoHelper.Node[]]@($capped))).BalanceValid) 'Capped counter cannot verify spend'
$s.Connected=$true;$s.Identity='fixture';$s.UtcTicks=[DateTime]::UtcNow.Ticks
Assert ([TappoHelper.Semantics]::PermitsInvoke($s,'left','fixture')) 'Fresh valid ready state permits invoke'
Assert (-not [TappoHelper.Semantics]::PermitsInvoke($s,'left','different')) 'Changed window identity must reject invoke'
$s.Busy=$true
Assert (-not [TappoHelper.Semantics]::PermitsInvoke($s,'left','fixture')) 'Busy animation must block invoke'
$s.Busy=$false;$s.UtcTicks=[DateTime]::UtcNow.AddSeconds(-6).Ticks
Assert (-not [TappoHelper.Semantics]::PermitsInvoke($s,'left','fixture')) 'Stale state must reject invoke'
Assert ([TappoHelper.Native]::InputSize() -eq $(if([IntPtr]::Size -eq 8){40}else{28})) 'SendInput structure must match Windows ABI'
$quiet=[TappoHelper.InputGuard]::AutomaticQuietMilliseconds
Assert ($quiet -eq 1000) 'Automation keyboard quiet time must be one second'
$activityFixture=[TappoHelper.ActivityTracker]::new(100)
$activityFixture.Record($false,150);$activityFixture.Record($false,180)
Assert ($activityFixture.Count -eq 2 -and $activityFixture.LastKeyboardTimestamp -eq 100) 'Mouse movement, wheel or click must still count for probe attribution without extending the automation keyboard wait'
$activityFixture.Record($true,200)
Assert ($activityFixture.Count -eq 3 -and $activityFixture.LastKeyboardTimestamp -eq 200) 'An external keyboard event must reset automation keyboard quiet time'
Assert ([TappoHelper.InputGuard]::Decide($true,999,$quiet,$false,'') -eq 'user_active') 'Keyboard input must pause for the full one second'
Assert ([TappoHelper.InputGuard]::Decide($true,1000,$quiet,$false,'') -eq '') 'An idle safe input context can resume at one second'
Assert ([TappoHelper.InputGuard]::Decide($true,20000,$quiet,$true,'') -eq 'keys_held') 'A held key or mouse button must block even after the idle threshold'
Assert ([TappoHelper.InputGuard]::Decide($true,60000,$quiet,$false,'editor_focus') -eq 'editor_focus') 'Text editing must remain protected after prolonged inactivity'
Assert ([TappoHelper.InputGuard]::Decide($true,60000,$quiet,$false,'candidate_window') -eq 'candidate_window') 'A visible candidate window must remain protected'
$unknownContext=[TappoHelper.InputGuard].GetMethod('Decide').Invoke($null,[object[]]@($true,[double]60000,$quiet,$false,$null))
Assert ($unknownContext -eq 'input_context_unknown') 'Unknown context must fail closed'
Assert (-not [TappoHelper.FocusSemantics]::IsTextEntry('Document','Chrome','Chrome_RenderWidgetHostHWND',$false,$false,$true)) 'Browsing a web page must not be treated as an editor merely because its document text pattern is writable'
Assert ([TappoHelper.FocusSemantics]::IsTextEntry('Edit','Chrome','',$false,$false,$false)) 'A browser text field must protect Xunfei and other IMEs without language checks'
Assert ([TappoHelper.FocusSemantics]::IsTextEntry('Custom','','',$false,$true,$false)) 'Writable custom controls must be protected'
Assert (-not [TappoHelper.FocusSemantics]::IsTextEntry('ListItem','DirectUI','',$false,$true,$false)) 'A rename-capable Explorer item must not be treated as an active text editor'
Assert (-not [TappoHelper.FocusSemantics]::IsTextEntry('TreeItem','Win32','',$false,$true,$false)) 'A rename-capable tree item must not block ordinary file navigation'
Assert ([TappoHelper.FocusSemantics]::IsTextEntry('Document','WPF','',$false,$false,$true)) 'A writable document editor must be protected'
Assert ([TappoHelper.FocusSemantics]::IsTextEntry('Custom','','',$true,$false,$false)) 'Password editors must be protected without reading text'
Assert ([TappoHelper.FocusSemantics]::IsCandidateClass('IFlyIMECompositionWnd')) 'Recognizable Xunfei composition windows must be protected'
Assert (-not [TappoHelper.FocusSemantics]::IsCandidateClass('IFlyIMEStatusWnd')) 'The Xunfei status bar must not permanently block automation'
Assert ([TappoHelper.ActivityTracker]::IsPointerFocusMessage(0x202)) 'A click release must request a fresh focus observation'
Assert (-not [TappoHelper.ActivityTracker]::IsPointerFocusMessage(0x200)) 'Mouse movement must not invalidate focus observations'
Assert (-not [TappoHelper.ActivityTracker]::IsPointerFocusMessage(0x20a)) 'Scrolling must not invalidate focus observations'
$ticks=[Diagnostics.Stopwatch]::GetTimestamp()
Assert ([TappoHelper.FocusSemantics]::CacheDecision(1,2,0,$ticks,1,2,$ticks,'') -eq '') 'Fresh confirmed non-editor focus permits automation, independent of keyboard locale'
Assert ([TappoHelper.FocusSemantics]::CacheDecision(1,2,$ticks,$ticks,1,2,$ticks,'') -eq 'focus_pending') 'A click after or during a focus read must wait for fresh confirmation'
Assert ([TappoHelper.FocusSemantics]::CacheDecision(1,3,0,$ticks,1,2,$ticks,'') -eq 'focus_pending') 'A changed focus must not use stale permissions'
Assert ([TappoHelper.FocusSemantics]::CacheDecision(1,2,0,$ticks,1,2,($ticks-2*[Diagnostics.Stopwatch]::Frequency),'') -eq 'focus_pending') 'Stale focus observations must fail closed'
Assert ([TappoHelper.FocusSemantics]::CacheDecision(1,2,0,$ticks,1,2,($ticks-2*[Diagnostics.Stopwatch]::Frequency),'',$ticks) -eq '') 'A completed slow provider read must not be permanently stale solely because it took more than 1.5 seconds'
Assert ([TappoHelper.ActivityMonitor]::IsOwnKeyboardEvent($true,[TappoHelper.Native]::InputTag)) 'Own F24 events must not restart idle timing'
Assert (-not [TappoHelper.ActivityMonitor]::IsOwnKeyboardEvent($false,[TappoHelper.Native]::InputTag)) 'Physical input must always be observed'
Assert (-not [TappoHelper.ActivityMonitor]::IsOwnKeyboardEvent($true,[UIntPtr]::Zero)) 'Other injected input must also suspend automation'
$noObserver=[TappoHelper.Native]::TrySendF24($null,$quiet)
Assert ($noObserver.State -eq 'deferred' -and $noObserver.Reason -eq 'observer_unavailable') 'Missing observer must defer before querying foreground or injecting input'
$session=[TappoHelper.Session]::new('fixture-not-a-process')
try{
    $first=$session.RequestInvoke('left','fixture')
    Assert ($first -gt 0 -and $session.RequestInvoke('right','fixture') -eq 0) 'At most one queued claim'
    $session.Cancel()
    Assert ($session.RequestInvoke('right','fixture') -gt $first) 'Cancellation invalidates old queue and allows fresh request'
    Assert ($null -eq $session.LastResult()) 'Session must not execute anything before Start'
}finally{$session.Dispose()}
. ([scriptblock]::Create([IO.File]::ReadAllText((Join-Path $root 'SmartLogic.ps1'),[Text.Encoding]::UTF8)))
$cycle=New-SmartState
Update-SmartState $cycle 0 2000 @($true,$true)
Update-SmartState $cycle 1000 2000 @($true,$true)
Assert ($cycle.Action -eq 'claim0') 'Automatic cycle starts with one stable ready chest'
Start-SmartClaim $cycle 0 1000
Update-SmartState $cycle 3000 1000 @($false,$true)
Assert ($cycle.Confirmed -eq 1 -and $cycle.Action -eq 'wait') 'First spend confirmed, animation wait required'
Update-SmartState $cycle 11000 1000 @($false,$true)
Assert ($cycle.Action -eq 'claim1') 'Second side only after first animation wait'
Start-SmartClaim $cycle 1 11000
Update-SmartState $cycle 13000 0 @($false,$false)
Assert ($cycle.Confirmed -eq 2) 'Both visible spends confirmed separately'
Update-SmartState $cycle 21000 0 @($false,$false)
Assert ($cycle.Action -eq 'tap') 'Refill only after claims and animation wait'
for($value=5;$value -le 2000;$value+=5){$cycle.SentSinceObservation=5;Update-SmartState $cycle (21000+200*$value) $value @($false,$false)}
Assert ($cycle.Action -eq 'wait' -and $cycle.LastBalance -eq 2000) 'Stop refilling at reserve instead of running forever'
Write-Output 'PASS: full automatic cycle: two sequential claims, animation waits, feedback refill, and stop at 2000'
Write-Output 'PASS: C# compilation, scoped balance, side mapping, class tokens, ambiguity, invoke guards, cancellation and SendInput ABI'
Write-Output 'PASS: one-second keyboard quiet, mouse independence, text/candidate protection, browser page allowance, focus-change cache invalidation, and missing-observer deferral'
Write-Output 'No UIA query, keyboard injection, input hook or official-client operation was performed.'
