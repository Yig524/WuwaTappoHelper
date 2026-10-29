$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Executed only by the GUI host's explicit --self-test switch. No official input.
$ErrorActionPreference='Stop'
[void][IO.Directory]::CreateDirectory((Join-Path $repoRoot 'test-results'))
$report=@{Completed=$false;Error='';ConsolePresent=$true;EventTicks=0;TrayHidden=$false;Restored=$false;Exited=$false;Apartment=[Threading.Thread]::CurrentThread.GetApartmentState().ToString()}
try {
    [IO.File]::AppendAllText((Join-Path $repoRoot 'diagnostics\gui-selftest.log'),'test-entry'+[Environment]::NewLine)
    Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;public static class GuiHostConsole { [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow(); }'
    $report.ConsolePresent=[GuiHostConsole]::GetConsoleWindow() -ne [IntPtr]::Zero
    if($report.ConsolePresent -or $report.Apartment -ne 'STA'){throw 'GUI host must be STA without a console'}
    & (Join-Path $repoRoot 'tests\Test-UiaGui.ps1')
    [IO.File]::AppendAllText((Join-Path $repoRoot 'diagnostics\gui-selftest.log'),'construction-passed'+[Environment]::NewLine)
    $script:hostTestReport=$report
    Add-Type -TypeDefinition 'using System.Windows.Forms;public sealed class HostNonActivatingForm:Form {protected override bool ShowWithoutActivation {get{return true;}}}' -ReferencedAssemblies System.Windows.Forms,System.Drawing
    $script:hostTestForm=[HostNonActivatingForm]::new()
    $script:hostTestForm.ShowInTaskbar=$false;$script:hostTestForm.StartPosition='Manual';$script:hostTestForm.Location=[Drawing.Point]::new(-30000,-30000)
    $script:hostTestTray=[TappoHelper.TrayHost]::new($script:hostTestForm,$false)
    $script:hostTestTimer=[Windows.Forms.Timer]::new();$script:hostTestTimer.Interval=40
    $script:hostTestTimer.Add_Tick({
        try {
            $script:hostTestReport.EventTicks++
            switch($script:hostTestReport.EventTicks){
                1 {$script:hostTestForm.Close()}
                2 {if($script:hostTestForm.Visible){throw 'Closing failed to hide'};$script:hostTestReport.TrayHidden=$true}
                3 {$script:hostTestTray.RestoreWindow();if(-not $script:hostTestForm.Visible){throw 'Restore failed'};$script:hostTestReport.Restored=$true}
                4 {$script:hostTestReport.Exited=$true;$script:hostTestTray.Exit()}
            }
        } catch {$script:hostTestReport.Error=$_.Exception.Message;$script:hostTestTray.Exit()}
    })
    $script:hostTestForm.Add_Shown({$script:hostTestTray.Start();$script:hostTestTimer.Start()})
    [IO.File]::AppendAllText((Join-Path $repoRoot 'diagnostics\gui-selftest.log'),'before-message-loop'+[Environment]::NewLine)
    [Windows.Forms.Application]::Run($script:hostTestForm)
    $report.Completed=$report.EventTicks -eq 4 -and $report.TrayHidden -and $report.Restored -and $report.Exited -and $report.Error -eq ''
} catch {$report.Error=$_.Exception.ToString()}
finally {
    if($null -ne $script:hostTestTimer){$script:hostTestTimer.Dispose()}
    if($null -ne $script:hostTestTray){$script:hostTestTray.Dispose()}
    if($null -ne $script:hostTestForm){$script:hostTestForm.Dispose()}
    [IO.File]::WriteAllText((Join-Path $repoRoot 'test-results\gui-host-smoke.json'),($report|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
}
if(-not $report.Completed){throw ('GUI host self-test failed: '+$report.Error)}
