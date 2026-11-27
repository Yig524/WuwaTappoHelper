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
        RetryAfter = @(0L,0L); LastObservation = -1L; InvalidReads = 0; RecoveryReads = 0
        Action = 'observe'; Reason = '等待识别官方余额'; LastReady = @($false,$false)
    }
}
function Update-SmartState($s, [long]$now, $balance, [bool[]]$ready, [long]$reserve = 2000) {
    $s.Action = 'wait'
    if ($null -eq $balance) {
        $s.InvalidReads++
        $s.RecoveryReads = 0; $s.ReadyRuns = @(0,0)
        if ($s.Pending -ge 0) {
            $slot = $s.Pending
            $s.Pending = -1
            $s.RetryAfter[$slot] = $now + 30000
            $s.LastBalance = $null
            $s.SentSinceObservation = 0
            $s.NoProgress = 0
            $s.Reason = '领取期间读数丢失，已暂缓30秒防重复领取，读取恢复后自动继续'
            return
        }
        $s.Reason = '暂时无法读取余额，暂缓动作，读取恢复后自动继续'
        return
    }
    if ($s.InvalidReads -gt 0) {
        $s.RecoveryReads++
        if ($s.RecoveryReads -lt 2) { $s.Reason = '读取已恢复，等待第二次有效读数确认后继续'; return }
    }
    $s.InvalidReads = 0
    $s.RecoveryReads = 0
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
            $s.Pending = -1
            $s.RetryAfter[$slot] = $now + 30000
            $s.NoProgress = 0
            $s.Reason = '领取期间余额出现意外变化，已重设基线并等待30秒后继续'
        } elseif ($now - $s.PendingSince -ge 15000) {
            $s.FailedClaims[$slot]++
            $s.Pending = -1
            $s.RetryAfter[$slot] = $now + 30000
            $s.Reason = '未观察到领取结果，等待30秒后重新识别'
            if ($s.FailedClaims[$slot] -ge 2) {
                $s.FailedClaims[$slot] = 0
                $s.RetryAfter[$slot] = $now + 60000
                $s.Reason = '同一槽位连续两次领取未获确认，暂缓60秒后重试'
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
            if ($gain -le -900) { $s.HoldUntil = [Math]::Max($s.HoldUntil, $now + 8000) }
            $s.LastBalance = $balance
            $s.SentSinceObservation = 0
            $s.NoProgress = 0
            $s.LastReady = $ready
            $s.Reason = '检测到余额下降，已自动同步新基线并继续'
            return
        }
        if ($s.SentSinceObservation -gt 0) {
            if ($gain -eq 0) { $s.NoProgress++ } else { $s.NoProgress = 0 }
            if ($s.NoProgress -ge 3) {
                $s.NoProgress = 0
                $s.HoldUntil = [Math]::Max($s.HoldUntil, $now + 5000)
                $s.LastBalance = $balance
                $s.SentSinceObservation = 0
                $s.LastReady = $ready
                $s.Reason = '连续多轮补充未见余额增加，暂缓5秒后继续观察'
                return
            }
            if ($gain -gt $s.SentSinceObservation * 3 + 20) {
                $s.NoProgress = 0
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
