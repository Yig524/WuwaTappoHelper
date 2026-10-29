# Pure decision logic. All balances come from the official visible UI.
function Get-SmartPermittedAction([string]$action,[bool]$daily,[double]$idleSeconds) {
    if ($daily -and ($action -eq 'tap' -or ($action -match '^claim[01]$' -and $idleSeconds -lt 10))) {return 'wait'}
    return $action
}
function New-SmartState {
    return @{
        LastBalance = $null; ReadyRuns = @(0,0); Pending = -1; PendingSince = 0L
        BeforeClaim = 0L; HoldUntil = 0L; CooldownUntil = @(0L,0L)
        SentSinceObservation = 0; NoProgress = 0; Confirmed = 0; FailedClaims = @(0,0)
        RetryAfter = @(0L,0L); LastObservation = -1L; InvalidReads = 0
        Action = 'observe'; Reason = '等待识别官方余额'; LastReady = @($false,$false)
    }
}
function Update-SmartState($s, [long]$now, $balance, [bool[]]$ready, [long]$reserve = 2000) {
    $s.Action = 'wait'
    if ($null -eq $balance) {
        $s.InvalidReads++
        $s.Reason = '无法可靠识别余额，暂停发送输入'
        if ($s.InvalidReads -ge 3) { $s.Action = 'stop' }
        return
    }
    $s.InvalidReads = 0
    $balance = [long]$balance
    $s.LastObservation = $now
    for ($i=0; $i -lt 2; $i++) {
        if ($ready[$i]) { $s.ReadyRuns[$i]++ } else { $s.ReadyRuns[$i] = 0 }
    }
    if ($s.Pending -ge 0) {
        $slot = $s.Pending
        $spent = $s.BeforeClaim - $balance
        if ($spent -ge 900 -and $spent -le 1100 -and -not $ready[$slot]) {
            $s.Confirmed++
            $s.CooldownUntil[$slot] = $now + 1800000
            $s.Pending = -1
            $s.FailedClaims[$slot] = 0
            $s.HoldUntil = $now + 8000
            $s.NoProgress = 0
            $s.Reason = '已观察到余额扣除及箱子消失，等待奖励动画结束'
        } elseif ($spent -gt 1100 -or $balance -gt $s.BeforeClaim + 20) {
            $s.Action = 'stop'; $s.Reason = '领取期间余额出现意外变化，请核对官方界面'
        } elseif ($now - $s.PendingSince -ge 15000) {
            $s.FailedClaims[$slot]++
            $s.Pending = -1
            $s.RetryAfter[$slot] = $now + 30000
            $s.Reason = '未观察到领取结果，等待30秒后重新识别'
            if ($s.FailedClaims[$slot] -ge 2) {
                $s.Action = 'stop'; $s.Reason = '同一槽位连续两次领取未得到界面确认，请检查登录和网络'
            }
        } else { $s.Reason = '等待官方界面确认领取；此时不发送其他点击' }
        $s.LastBalance = $balance
        $s.SentSinceObservation = 0
        $s.LastReady = $ready
        return
    }
    if ($null -ne $s.LastBalance) {
        $gain = $balance - $s.LastBalance
        if ($gain -lt 0) {
            $s.Action = 'stop'; $s.Reason = '未发起领取但余额下降，请重新核对界面读数'; return
        }
        if ($s.SentSinceObservation -gt 0) {
            if ($gain -eq 0) { $s.NoProgress++ } else { $s.NoProgress = 0 }
            if ($s.NoProgress -ge 3) {
                $s.Action = 'stop'; $s.Reason = '连续三轮点击未使官方余额增加，已停止'; return
            }
            if ($gain -gt $s.SentSinceObservation * 3 + 20) {
                $s.Action = 'stop'; $s.Reason = '余额增量异常，可能识别错误，请重新标定'; return
            }
        }
    }
    $s.LastBalance = $balance
    $s.SentSinceObservation = 0
    $s.LastReady = $ready
    if ($now -lt $s.HoldUntil) { $s.Reason = '等待官方奖励动画及忙碌状态结束'; return }
    if ($balance -ge 1000) {
        for ($i=0; $i -lt 2; $i++) {
            if ($s.ReadyRuns[$i] -ge 2 -and $now -ge $s.RetryAfter[$i]) {
                $s.Action = "claim$i"; $s.Reason = '箱子连续两次识别为就绪，准备单独领取'; return
            }
        }
    }
    if ($balance -lt $reserve) {
        $s.Action = 'tap'; $s.Reason = "补充点击余额至 $reserve，随后等待箱子就绪"
    } else { $s.Reason = '余额已足够领取两个箱子，停止补点击并等待就绪' }
}
function Start-SmartClaim($s, [int]$slot, [long]$now) {
    $s.Pending = $slot; $s.BeforeClaim = $s.LastBalance; $s.PendingSince = $now
    $s.Action = 'wait'; $s.Reason = '已发送一次领取，等待余额与槽位变化'
}
