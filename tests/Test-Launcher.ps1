$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ErrorActionPreference='Stop'
$automationRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=[IO.File]::ReadAllText((Join-Path $automationRoot 'Launch-UiaHelper.ps1'),[Text.Encoding]::UTF8)
$prefixEnd=$source.IndexOf('$identity=')
if($prefixEnd -lt 0){throw 'Cannot isolate launch-options builder'}
. ([scriptblock]::Create($source.Substring(0,$prefixEnd)))
function Assert($condition,[string]$message){if(-not $condition){throw $message}}
foreach($elevated in @($false,$true)){
    foreach($rootFixture in @($automationRoot,(Join-Path $env:TEMP "helper space\user's helper"))){
        $options=New-UiaLaunchOptions $rootFixture $elevated
        Assert ($options.WindowStyle -eq 'Normal') 'The interactive helper must remain visible after startup'
        Assert (-not $options.ContainsKey('Wait') -and -not $options.ContainsKey('NoNewWindow')) 'The launcher must return without keeping a shared console open'
        Assert ($options.WorkingDirectory -eq $rootFixture) 'Preserve helper working directory'
        Assert ($(if($elevated){-not $options.ContainsKey('Verb')}else{$options.Verb -eq 'RunAs'})) 'Request UAC only when not already elevated'
        Assert ($options.FilePath -eq (Join-Path $rootFixture 'WuwaTappoHelper.exe')) 'Launch the GUI executable directly even for paths containing spaces or apostrophes'
        Assert (-not $options.ContainsKey('ArgumentList')) 'Do not launch an encoded console PowerShell process'
    }
}
Write-Output 'PASS: GUI launch for both privilege states, UAC selection, visible GUI, and paths with spaces/apostrophes'

$hostBinary=Join-Path $automationRoot 'WuwaTappoHelper.exe'
$stream=[IO.File]::OpenRead($hostBinary);$reader=[IO.BinaryReader]::new($stream)
try{[void]$stream.Seek(0x3c,[IO.SeekOrigin]::Begin);$peOffset=$reader.ReadInt32();[void]$stream.Seek($peOffset+92,[IO.SeekOrigin]::Begin);Assert ($reader.ReadUInt16() -eq 2) 'Helper executable must use the Windows GUI subsystem, never the console subsystem'}finally{$reader.Dispose();$stream.Dispose()}
$options=New-UiaLaunchOptions $automationRoot $true
$options.WindowStyle='Hidden'
$options.PassThru=$true
$options.ArgumentList=@('--self-test')
$startedAt=[DateTime]::UtcNow
$child=Start-Process @options
try {
    Assert ($child.WaitForExit(10000)) 'Private GUI-host test must finish within ten seconds'
    $reportPath=Join-Path $automationRoot 'test-results\gui-host-smoke.json'
    Assert ((Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -ge $startedAt.AddSeconds(-2)) 'Console report must come from this run'
    $report=[IO.File]::ReadAllText($reportPath,[Text.Encoding]::UTF8)|ConvertFrom-Json
    Assert ($report.Completed -and $report.Error -eq '' -and -not $report.ConsolePresent -and $report.Apartment -eq 'STA' -and $report.TrayHidden -and $report.Restored -and $report.Exited) ('GUI host failed: '+($report|ConvertTo-Json -Compress))
} finally {$child.Dispose()}
Write-Output 'PASS: real GUI executable has no console; hosted PowerShell construction, timer callbacks, tray hiding, restoration and exit work'
