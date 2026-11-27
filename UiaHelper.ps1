$ErrorActionPreference='Stop'
$root=(Get-Location).Path
function Write-UiaStartup([string]$text){
    $dir=Join-Path $root 'diagnostics';[void][IO.Directory]::CreateDirectory($dir)
    [IO.File]::AppendAllText((Join-Path $dir 'uia-startup.log'),([DateTimeOffset]::Now.ToString('o')+' '+$text+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
}
Write-UiaStartup 'begin'
$exePath=[IO.Path]::GetFullPath((Join-Path $root '..\WuwaTappo.exe'))
$configPath=Join-Path $root 'helper-config.json'
$config=$null;$configError=''
if(Test-Path -LiteralPath $configPath){
    try{$config=[IO.File]::ReadAllText($configPath,[Text.Encoding]::UTF8)|ConvertFrom-Json
    if($config.OfficialExecutable){$exePath=if([IO.Path]::IsPathRooted($config.OfficialExecutable)){[IO.Path]::GetFullPath($config.OfficialExecutable)}else{[IO.Path]::GetFullPath((Join-Path $root $config.OfficialExecutable))}}}catch{$config=$null;$configError='原配置无法读取，请重新选择官方程序；取消不会覆盖原配置。'}
}
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase,System.Windows.Forms,System.Drawing
$references=@([Windows.Automation.AutomationElement].Assembly.Location,[Windows.Automation.AutomationProperty].Assembly.Location,[Windows.Point].Assembly.Location,[Windows.Forms.Form].Assembly.Location,[Drawing.Bitmap].Assembly.Location,[Collections.Generic.HashSet[int]].Assembly.Location,[regex].Assembly.Location)|Select-Object -Unique
$uiSources=[IO.File]::ReadAllText((Join-Path $root 'UiaEngine.cs'),[Text.Encoding]::UTF8)+[Environment]::NewLine+[IO.File]::ReadAllText((Join-Path $root 'Dashboard.cs'),[Text.Encoding]::UTF8)
$uiSources+=[Environment]::NewLine+[IO.File]::ReadAllText((Join-Path $root 'SetupWizard.cs'),[Text.Encoding]::UTF8)
Add-Type -TypeDefinition $uiSources -ReferencedAssemblies $references
[TappoHelper.Native]::InitDpi()
Write-UiaStartup 'native-compiled'
[void][TappoHelper.Native]::DetachOwnConsole()
# Live instance selection. Hidden tray instances are legitimate and must not be closed.
$createdInstance=$false
$instanceGuard=[Threading.Mutex]::new($true,'Local\WuwaTappoF24UIA',[ref]$createdInstance)
if(-not $createdInstance){
    $instanceGuard.Dispose()
    if(-not [TappoHelper.Native]::RequestShowExistingHelper()){[void][Windows.Forms.MessageBox]::Show('旧助手仍在运行。请从托盘右键“退出程序”，再重新启动以加载更新。','Wuwa Tappo 助手')}
    return
}
$compatibility=[TappoHelper.SetupWizard]::CheckExecutable($exePath)
if($global:ForceSetup -or $null -eq $config -or $config.SetupVersion -ne [TappoHelper.SetupWizard]::CurrentVersion -or -not $compatibility.Compatible){
    $script:setupSession=$null
    $wizard=[TappoHelper.SetupWizard]::new($exePath,$configError)
    $setupTimer=[Windows.Forms.Timer]::new();$setupTimer.Interval=300
    $wizard.add_ValidateRequested({$wizard.SetValidation([TappoHelper.SetupWizard]::CheckExecutable($wizard.SelectedPath))})
    $wizard.add_ConnectRequested({
        if($null -ne $script:setupSession){$script:setupSession.Dispose()}
        $script:setupSession=[TappoHelper.Session]::new($wizard.SelectedPath);$script:setupSession.Start();$setupTimer.Start()
    })
    $setupTimer.add_Tick({if($null -ne $script:setupSession){$setupSnapshot=$script:setupSession.Peek();$ready=$setupSnapshot.Connected -and $setupSnapshot.BalanceValid -and $setupSnapshot.AgeSeconds -le 5;$wizard.SetConnection($ready,$(if($ready){'已连接官方桌宠，可用余额：'+$setupSnapshot.Balance}else{'等待可读的官方桌宠窗口和余额。请确认已启动并登录。'}))}})
    try{
        if($wizard.ShowDialog() -ne [Windows.Forms.DialogResult]::OK){$instanceGuard.ReleaseMutex();$instanceGuard.Dispose();return}
        $exePath=[IO.Path]::GetFullPath($wizard.SelectedPath)
        if(-not [TappoHelper.SetupWizard]::CheckExecutable($exePath).Compatible){throw '所选官方文件已变化，请重新启动并校验。'}
        $saved=[ordered]@{};if($null -ne $config){foreach($property in $config.PSObject.Properties){$saved[$property.Name]=$property.Value}}
        $saved['OfficialExecutable']=$exePath;$saved['SetupVersion']=[TappoHelper.SetupWizard]::CurrentVersion
        $tempConfig=$configPath+'.tmp'
        [IO.File]::WriteAllText($tempConfig,($saved|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        if(Test-Path -LiteralPath $configPath){[IO.File]::Replace($tempConfig,$configPath,$null)}else{[IO.File]::Move($tempConfig,$configPath)}
    }finally{$setupTimer.Dispose();if($null -ne $script:setupSession){$script:setupSession.Dispose();$script:setupSession=$null};$wizard.Dispose()}
}
. ([scriptblock]::Create([IO.File]::ReadAllText((Join-Path $root 'SmartLogic.ps1'),[Text.Encoding]::UTF8)))
$script:session=[TappoHelper.Session]::new($exePath)
$script:activity=$null;$script:manualClaim=$false;$script:probe=$null
$script:sourceRejected=$false;
$script:keyVerified=$false;$script:identity='';$script:seen=0L;$script:pending=0L;$script:ack=$false
$script:state=New-SmartState;$script:clock=[Diagnostics.Stopwatch]::StartNew();$script:nextKey=0L;$script:sent=0L
$script:claimBaseline=$null;$script:autoDiagnostics=0;$script:savePending=$false
$script:inputProtection=''
$script:focusHistory=[Collections.Generic.Queue[object]]::new();$script:lastFocusRecord=''
$form=[TappoHelper.HelperForm]::new();$form.Text='Wuwa Tappo — F24 + UIA 助手'
$form.StartPosition='CenterScreen'
$script:dashboard=[TappoHelper.DashboardView]::new($form,$root)
$script:trayHost=[TappoHelper.TrayHost]::new($form)
$connection=$script:dashboard.Connection;$balanceLabel=$script:dashboard.Balance;$slotsLabel=$script:dashboard.Slots
$refresh=$script:dashboard.Refresh;$testKey=$script:dashboard.TestKey;$testClaim=$script:dashboard.TestClaim
$fill=$script:dashboard.Fill;$harvest=$script:dashboard.Harvest;$stop=$script:dashboard.Stop
$status=$script:dashboard.Status;$stats=$script:dashboard.Stats

function Write-UiaReport([string]$name,$data){
    $directory=Join-Path $root 'diagnostics';[void][IO.Directory]::CreateDirectory($directory)
    [IO.File]::WriteAllText((Join-Path $directory $name),($data|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
}
function Test-AutomationActive {
    return ($fill.Checked -or $harvest.Checked) -and (-not $script:sourceRejected) -and ($null -eq $script:probe) -and (-not $script:manualClaim)
}
function Reset-TasksState([string]$reason){
    $script:pending=0L;$script:ack=$false;$script:inputProtection=''
    $script:session.Cancel();$script:state=New-SmartState;$status.Text=$reason
}
function Stop-OneShot([string]$reason){
    if($null -ne $script:probe){Write-UiaReport 'f24-test.json' @{CompletedAt=[DateTimeOffset]::Now.ToString('o');Verdict='stopped';Reason=$reason;Sent=$script:probe.Sent}}
    $script:manualClaim=$false;$script:probe=$null
    $fill.Enabled=$true;$harvest.Enabled=$true;$testKey.Enabled=$true;$testClaim.Enabled=$true
    Reset-TasksState $reason
}
function Stop-Uia([string]$reason){
    Write-UiaStartup ('state: '+$reason)
    $wasActive=$fill.Checked -or $harvest.Checked
    Write-UiaReport 'last-stop.json' @{At=[DateTimeOffset]::Now.ToString('o');Reason=$reason;WasRunning=$wasActive;Pending=$script:pending;Decision=$script:state;Snapshot=$script:session.Peek()}
    $fill.Checked=$false;$harvest.Checked=$false
    Stop-OneShot $reason
}
function Update-UiaTray {
    if($null -eq $script:trayHost -or $script:trayHost.IsExiting){return}
    $active=Test-AutomationActive
    $text=if($null -ne $script:probe){'F24验证中'}elseif($script:manualClaim){'验证单箱领取中'}elseif($active -and $script:state.InvalidReads -gt 0){'读取暂缓中'}elseif($active -and $script:inputProtection -ne ''){'输入避让中'}elseif($active){'常开运行中'}else{'只读待命'}
    $snapshot=$script:session.Peek()
    $script:dashboard.SetMode($active,($script:inputProtection -ne ''),($null -ne $script:probe),$script:manualClaim,($snapshot.Connected -and $snapshot.AgeSeconds -le 5),$script:keyVerified)
    if($snapshot.Connected -and $snapshot.BalanceValid){$text+=' · 余额 '+$snapshot.Balance}
    $script:trayHost.SetStatus($text)
}
function Get-UsableSnapshot {
    if($script:sourceRejected){throw '官方程序签名校验未通过，请检查来源后刷新。'}
    $s=$script:session.Peek()
    if(-not $s.Connected -or $s.AgeSeconds -gt 5 -or -not $s.BalanceValid){throw 'UIA尚未提供新鲜、精确的余额；请只读刷新并检查诊断。'}
    return $s
}
function Input-ProtectionText([string]$reason){
    switch($reason){
        'user_active' {'输入避让中：键盘输入停止1秒后再尝试自动动作；鼠标移动和滚轮不触发等待。'}
        'keys_held' {'输入避让中：有按键或鼠标按钮按住，自动动作暂缓。'}
        'editor_focus' {'输入避让中：焦点在文字编辑区域。点输入框外或结束编辑后恢复；无需切换讯飞或中文布局。'}
        'candidate_window' {'输入避让中：检测到候选窗口，选词结束并离开编辑区域后恢复。'}
        'focus_pending' {'正在确认新的输入焦点；没有固定10秒等待，鼠标移动和滚轮不重置等待。'}
        'observer_unavailable' {'输入避让中：输入观察器不可用，自动动作暂停。'}
        default {'输入避让中：无法确认前台输入环境，自动动作暂停。'}
    }
}
function Start-KeyProbe {
    try {
        Stop-OneShot '准备限量验证'
        $s=Get-UsableSnapshot
        if($null -eq $script:activity){throw '输入观察器不可用，不能排除自然输入干扰。'}
        $script:keyVerified=$false;$script:probe=@{Phase='settle';QuietSince=$script:clock.ElapsedMilliseconds;Activity=$script:activity.Count;Sent=0;Identity=$s.Identity}
        $testKey.Enabled=$false;$testClaim.Enabled=$false;$fill.Enabled=$false;$harvest.Enabled=$false
        $status.Text='F24验证：请停止键鼠操作。连续安静3秒后建立余额基线，再仅发送5次F24。'
    }catch{Stop-OneShot $_.Exception.Message}
}
function Finish-KeyProbe($snapshot){
    $p=$script:probe;$cursor=[TappoHelper.Native]::Cursor()
    $stable=$script:activity.Count -eq $p.Activity -and $cursor.X -eq $p.Cursor.X -and $cursor.Y -eq $p.Cursor.Y
    $focus=[TappoHelper.Native]::GetForegroundWindow() -eq $p.Foreground
    $delta=$snapshot.Balance-$p.Before
    $success=$stable -and $focus -and $p.Sent -eq 5 -and $delta -eq 5
    $verdict=if($success){'observed_plus_five'}elseif(-not $stable -or -not $focus){'inconclusive_other_input_or_focus'}elseif($delta -eq 0){'no_observed_effect'}else{'inconclusive_balance_change'}
    Write-UiaReport 'f24-test.json' @{CompletedAt=[DateTimeOffset]::Now.ToString('o');Verdict=$verdict;Before=$p.Before;After=$snapshot.Balance;Sent=$p.Sent;CursorStable=$stable;ForegroundStable=$focus;Identity=$snapshot.Identity}
    $script:probe=$null
    $script:keyVerified=$success
    Stop-OneShot $(if($success){if($fill.Checked){'F24验证通过：自动补充余额已立即生效。'}else{'F24验证通过：官方余额增加5，光标与前台窗口未改变。开启“自动补充余额”即可持续运行。'}}else{'F24未通过受控验证，自动补余额暂未激活。详见 diagnostics\f24-test.json。'})
}
function Request-UiaClaim([string]$side,[bool]$manual){
    $s=Get-UsableSnapshot;$slot=if($side -eq 'left'){$s.Left}else{$s.Right}
    if(-not $s.ChestCompatible -or -not $slot.Ready -or $s.Busy -or $s.Balance -lt 1000){throw '该侧未就绪、余额不足或正在播放奖励动画，未发起领取。'}
    if(-not $manual){
        $permission=[TappoHelper.InputGuard]::Assess($script:activity,[TappoHelper.InputGuard]::AutomaticQuietMilliseconds)
        $script:inputProtection=$permission.Reason
        if(-not $permission.Allowed){$script:state.Action='wait';$status.Text=Input-ProtectionText $permission.Reason;return}
    }
    $id=$script:session.RequestInvoke($side,$s.Identity,(-not $manual))
    if($id -eq 0){throw '已有领取请求，未重复发送。'}
    $script:pending=$id;$script:ack=$false;$script:manualClaim=$manual;$script:claimBaseline=$s.Balance
    $script:state.LastBalance=$s.Balance
    Start-SmartClaim $script:state $(if($side -eq 'left'){0}else{1}) $script:clock.ElapsedMilliseconds
    $status.Text='已排队一次 UIA Invoke；等待官方界面确认，期间不发 F24。'
}
function Start-ManualClaim {
    try {
        Stop-OneShot '准备验证单箱领取';$s=Get-UsableSnapshot
        $side=if($s.Left.Ready){'left'}elseif($s.Right.Ready){'right'}else{throw '没有可领取的UIA箱子按钮，未执行动作。'}
        $script:manualClaim=$true
        $fill.Enabled=$false;$harvest.Enabled=$false;$testKey.Enabled=$false;$testClaim.Enabled=$false
        Request-UiaClaim $side $true
    }catch{Stop-OneShot $_.Exception.Message}
}
function Slot-Description($slot){switch($slot.State){'ready'{'就绪（支持Invoke）'}'cooldown'{"冷却 $($slot.Text)"}'disabled'{'按钮禁用或Invoke不可用'}'ambiguous'{'控件不唯一，禁止领取'}default{'未显示 / 未暴露'}}}
$onTaskChanged={
    if($null -ne $script:probe -or $script:manualClaim){return}
    if(-not $fill.Checked -and -not $harvest.Checked){
        Reset-TasksState '已关闭全部自动任务，继续只读观察。'
    } else {
        $script:session.Refresh()
        if($fill.Checked -and -not $script:keyVerified -and -not $harvest.Checked){$status.Text='自动补充已开启：请先完成本轮“验证 F24 · 5次”后激活补充。'}
        elseif($script:state.Action -eq 'observe'){$status.Text='常开运行中：等待界面状态就绪。'}
    }
    Update-UiaTray
}
$fill.Add_CheckedChanged($onTaskChanged);$harvest.Add_CheckedChanged($onTaskChanged)
$refresh.Add_Click({if($script:sourceRejected){$script:identity=''};$script:savePending=$true;$script:session.Refresh()})
$testKey.Add_Click({Start-KeyProbe});$testClaim.Add_Click({Start-ManualClaim});$stop.Add_Click({Stop-Uia '已关闭全部自动任务，继续只读观察。'})
$form.add_StopRequested({Stop-Uia '快捷键已关闭全部自动任务。'})
$form.add_RestoreRequested({$script:trayHost.RestoreWindow()})
$script:trayHost.add_StopRequested({Stop-Uia '托盘已关闭全部自动任务，继续只读观察。';Update-UiaTray})
$script:trayHost.add_ExitRequested({Stop-Uia '用户从托盘退出助手。'})
$timer=[Windows.Forms.Timer]::new();$timer.Interval=50
$timer.Add_Tick({
    try {
        $s=$script:session.Peek();$now=$script:clock.ElapsedMilliseconds
        $new=$s.Sequence -ne $script:seen
        if($new -and ($script:savePending -or $script:autoDiagnostics -lt 5)){Write-UiaReport 'uia-latest.json' $s;$script:savePending=$false;$script:autoDiagnostics++}
        $active=Test-AutomationActive
        if($s.Connected -and $s.Identity -ne $script:identity){
            $hadIdentity=$script:identity -ne ''
            $script:identity=$s.Identity
            $sourceCheck=[TappoHelper.SetupWizard]::CheckExecutable($exePath)
            $script:sourceRejected=-not $sourceCheck.Compatible
            if($script:sourceRejected){$script:keyVerified=$false}
            if($hadIdentity){
                if($null -ne $script:probe -or $script:manualClaim){Stop-OneShot '官方窗口已改变，本次验证已停止。'}
                else{Reset-TasksState '官方窗口已更新，已重新同步状态并继续运行。'}
            }
            $active=Test-AutomationActive
        }
        if($script:sourceRejected){
            if($script:manualClaim -or $null -ne $script:probe){Stop-OneShot '官方程序签名校验未通过，验证已停止。'}
            elseif($script:pending -gt 0 -or $script:state.Pending -ge 0){Reset-TasksState '官方程序签名校验未通过，动作暂缓。'}
            $connection.Text='官方签名校验失败：检查来源、时间与证书状态后点击刷新重试。';return
        }
        if(-not $s.Connected -or $s.AgeSeconds -gt 5 -or -not $s.BalanceValid){
            $connection.Text='读取暂缓 · 等待同一官方窗口提供新鲜、精确的余额。'
            $balanceLabel.Text='—'
            if($script:manualClaim -or $null -ne $script:probe){Stop-OneShot '验证期间读数丢失，结果无法确认，本次验证已停止。'}
            elseif($active){
                if($script:pending -gt 0 -or $script:state.Pending -ge 0){$script:session.Cancel();$script:pending=0L;$script:ack=$false}
                if($new -or $script:state.InvalidReads -eq 0){Update-SmartState $script:state $now $null ([bool[]]@($false,$false))}
                $script:inputProtection='';$status.Text=$script:state.Reason
            }
            $script:seen=$s.Sequence
            return
        }
        if($new){
            $script:seen=$s.Sequence;$connection.Text=if($s.BalanceValid){$(if($s.ChestCompatible){'已连接官方桌宠 · 余额、箱子可识别'}else{'已连接 · 余额可读；箱子未显示或不兼容，领取暂缓'})}else{'已连接 · 余额暂不可读，可刷新并保存诊断。'}
            $balanceLabel.Text=if($s.BalanceValid){$s.Balance.ToString('N0')}else{'—'}
            $script:dashboard.SetSnapshot($s)
            if($null -ne $script:activity){
                $focus=$script:activity.FocusStatus
                $guardReport=@{Version='focus-v3';At=[DateTimeOffset]::Now.ToString('o');KeyboardIdleMilliseconds=[Math]::Round($script:activity.KeyboardIdleMilliseconds);Protection=$script:inputProtection;Focus=$focus}
                Write-UiaReport 'input-guard.json' $guardReport
                if($null -ne $focus){
                    $focusKey=$focus.Window.ToString()+':'+$focus.Focus+':'+$focus.Control+':'+$focus.Reason+':'+$focus.CaretVisible
                    if($focusKey -ne $script:lastFocusRecord){
                        $script:lastFocusRecord=$focusKey;$script:focusHistory.Enqueue($guardReport)
                        while($script:focusHistory.Count -gt 40){[void]$script:focusHistory.Dequeue()}
                        Write-UiaReport 'input-guard-history.json' @($script:focusHistory.ToArray())
                    }
                }
            }
        }
        if($null -ne $script:probe){
            $p=$script:probe
            if(-not $s.BalanceValid -or $s.Identity -ne $p.Identity){Stop-OneShot 'F24验证中余额不可用或目标改变，已停止。';return}
            if($p.Phase -eq 'settle'){
                if($script:activity.Count -ne $p.Activity){$p.Activity=$script:activity.Count;$p.QuietSince=$now}
                if($now-$p.QuietSince -ge 3000 -and -not [TappoHelper.Native]::ModifiersDown()){
                    $p.Before=$s.Balance;$p.Sequence=$s.Sequence;$p.Cursor=[TappoHelper.Native]::Cursor();$p.Foreground=[TappoHelper.Native]::GetForegroundWindow();$p.Phase='baseline';$p.Due=$now+1200
                }
                return
            }
            $cursor=[TappoHelper.Native]::Cursor()
            if($script:activity.Count -ne $p.Activity -or $cursor.X -ne $p.Cursor.X -or $cursor.Y -ne $p.Cursor.Y -or [TappoHelper.Native]::GetForegroundWindow() -ne $p.Foreground){Stop-OneShot '检测到其他输入、光标或前台变化，F24验证无法归因，已停止。';return}
            if($p.Phase -eq 'baseline' -and $new -and $s.Sequence -gt $p.Sequence -and $now -ge $p.Due){
                if($s.Balance -ne $p.Before){Stop-OneShot '发送F24前余额已经变化，已停止验证。';return}
                $p.Phase='send';$p.Due=$now
            }
            if($p.Phase -eq 'send' -and $now -ge $p.Due){
                if([TappoHelper.Native]::ModifiersDown()){Stop-OneShot '验证期间有修饰键按住，已停止。';return}
                $keyResult=[TappoHelper.Native]::TrySendF24($script:activity,3000)
                if($keyResult.State -eq 'deferred'){Stop-OneShot ('限量验证已停止：'+(Input-ProtectionText $keyResult.Reason));return}
                if($keyResult.State -ne 'sent'){Stop-OneShot 'F24注入失败，请核对权限。';return}
                $p.Sent++;$p.Due=$now+400;$status.Text="F24受控验证：已发送 $($p.Sent)/5。请勿操作键鼠。"
                if($p.Sent -ge 5){$p.Phase='after';$p.Due=$now+2500;$p.Sequence=$s.Sequence}
            }
            if($p.Phase -eq 'after' -and $new -and $s.Sequence -gt $p.Sequence -and $now -ge $p.Due){Finish-KeyProbe $s}
            return
        }
        if($new -and ($active -or $script:manualClaim -or $script:pending -gt 0)){
            if($active){$script:inputProtection=([TappoHelper.InputGuard]::Assess($script:activity,[TappoHelper.InputGuard]::AutomaticQuietMilliseconds)).Reason}
            $result=$script:session.LastResult()
            if($script:pending -gt 0 -and $null -ne $result -and $result.Id -eq $script:pending -and -not $script:ack){
                if($result.Status -eq 'deferred' -and $active -and -not $script:manualClaim){
                    $script:pending=0L;$script:ack=$false;$script:state.Pending=-1;$script:state.Action='wait'
                    $script:inputProtection=$result.Error
                    return
                }
                if($result.Status -ne 'invoked'){
                    if($script:manualClaim){Stop-OneShot ('UIA领取未执行：'+$result.Error);return}
                    $failedSlot=$script:state.Pending
                    if($failedSlot -ge 0){$script:state.RetryAfter[$failedSlot]=$now+3000}
                    $script:pending=0L;$script:ack=$false;$script:state.Pending=-1;$script:state.Action='wait'
                    $status.Text='UIA状态已变化，本次领取已跳过并继续观察'
                    return
                }
                $script:ack=$true;$script:state.BeforeClaim=$result.Before;$script:claimBaseline=$result.Before
            }
            $priorConfirmed=$script:state.Confirmed
            $allowClaims=$s.ChestCompatible -and ($harvest.Checked -or $script:manualClaim)
            $confirmationReady=[bool[]]@(($allowClaims -and $s.Left.Ready),($allowClaims -and $s.Right.Ready))
            if($script:state.Pending -ge 0){
                $pendingSlot=if($script:state.Pending -eq 0){$s.Left}else{$s.Right}
                if($pendingSlot.State -ne 'cooldown' -and -not $s.Busy){$confirmationReady[$script:state.Pending]=$true}
            }
            Update-SmartState $script:state $now $(if($s.BalanceValid){$s.Balance}else{$null}) $confirmationReady
            $status.Text=$script:state.Reason
            if($harvest.Checked -and -not $s.ChestCompatible -and $script:state.Pending -lt 0){$status.Text+='；箱子未显示或结构异常，领取暂缓。'}
            if($script:state.Confirmed -gt $priorConfirmed){
                Write-UiaReport 'uia-claim-test.json' @{CompletedAt=[DateTimeOffset]::Now.ToString('o');Verdict='observed_spend_and_slot_change';Before=$script:claimBaseline;After=$s.Balance;Invoke=$result;Left=$s.Left;Right=$s.Right}
                $script:pending=0L;$script:ack=$false
                if($script:manualClaim){Stop-OneShot '单箱领取验证完成：已确认余额扣除且箱子状态变化。';return}
            }
            if($script:pending -gt 0 -and $script:state.Pending -lt 0){$script:session.Cancel();$script:pending=0L;if($script:manualClaim){Stop-OneShot '单箱领取未得到界面确认，已停止验证。';return}}
            if($active -and $script:state.Action -match '^claim([01])$'){
                if($harvest.Checked -and -not $s.Busy){Request-UiaClaim $(if($Matches[1] -eq '0'){'left'}else{'right'}) $false}
                else{$script:state.Action='wait';$status.Text='箱子已就绪；自动领取未启用，或正在等待奖励动画。'}
            }
            if($script:state.Action -eq 'tap' -and (-not $fill.Checked -or -not $active)){$script:state.Action='wait';$status.Text='等待自然输入补充余额；自动F24未启用。'}
            elseif($script:state.Action -eq 'tap' -and $fill.Checked -and -not $script:keyVerified){$script:state.Action='wait';$status.Text='自动补充待激活：请先完成本轮“验证 F24 · 5次”。'}
        }
        if($active -and $script:state.Action -eq 'tap' -and $fill.Checked -and $script:keyVerified -and -not $s.Busy -and $s.BalanceValid -and
           $script:pending -eq 0 -and $now -ge $script:nextKey -and $script:state.SentSinceObservation -lt [Math]::Min(5,2000-$s.Balance)){
            $keyResult=[TappoHelper.Native]::TrySendF24($script:activity,[TappoHelper.InputGuard]::AutomaticQuietMilliseconds)
            $script:inputProtection=$keyResult.Reason;$script:nextKey=$now+250
            if($keyResult.State -eq 'deferred'){return}
            if($keyResult.State -ne 'sent'){$script:nextKey=$now+2000;$status.Text='F24发送失败，2秒后重试，请核对权限。';return}
            $script:state.SentSinceObservation++;$script:sent++;$script:nextKey=$now+200
        }
        $proof=if($script:keyVerified){'已通过'}else{'未通过'}
        $stats.Text="F24 $proof  ·  补充 $($script:sent) 次  ·  已确认领取 $($script:state.Confirmed) 次"
    }catch{
        Write-UiaStartup ('tick-error: '+$_.Exception.Message)
        if($null -ne $script:probe -or $script:manualClaim){Stop-OneShot ('验证异常已停止：'+$_.Exception.Message)}
        else{Reset-TasksState ('异常已恢复等待：'+$_.Exception.Message)}
    }finally{
        if((Test-AutomationActive) -and $script:inputProtection -ne ''){$status.Text=Input-ProtectionText $script:inputProtection}
        Update-UiaTray
    }
})
$form.Add_Shown({
    Write-UiaStartup 'form-shown'
    $script:trayHost.Start();Update-UiaTray
    try{$script:activity=[TappoHelper.ActivityMonitor]::new()}catch{$status.Text=$_.Exception.Message}
    $script:session.SetActivityMonitor($script:activity)
    $script:session.Start();$timer.Start()
    if(-not $form.HotkeysReady){$status.Text='全局组合键注册失败，请使用界面关闭按钮。'}
})
$form.Add_FormClosing({
    param($sender,$eventArgs)
    if($eventArgs.Cancel){Write-UiaStartup 'hidden-to-tray; current state preserved';return}
    $script:session.Cancel();$timer.Stop()
})
try{[Windows.Forms.Application]::Run($form)}finally{$timer.Dispose();$script:session.Dispose();if($null -ne $script:activity){$script:activity.Dispose()};$script:trayHost.Dispose();$script:trayHost=$null;$form.Dispose();$script:dashboard.Dispose();$instanceGuard.ReleaseMutex();$instanceGuard.Dispose()}
