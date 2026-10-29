$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase,System.Windows.Forms,System.Drawing
$refs=@([Windows.Automation.AutomationElement].Assembly.Location,[Windows.Automation.AutomationProperty].Assembly.Location,[Windows.Point].Assembly.Location,[Windows.Forms.Form].Assembly.Location,[Drawing.Bitmap].Assembly.Location,[Collections.Generic.HashSet[int]].Assembly.Location,[regex].Assembly.Location)|Select-Object -Unique
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $root 'UiaEngine.cs'),[Text.Encoding]::UTF8)) -ReferencedAssemblies $refs
Add-Type -TypeDefinition @'
using System.Windows.Forms;
public sealed class NonActivatingTrayFixture:Form {
 protected override bool ShowWithoutActivation {get{return true;}}
}
'@ -ReferencedAssemblies System.Windows.Forms,System.Drawing
$form=[NonActivatingTrayFixture]::new()
$form.StartPosition='Manual';$form.Location=[Drawing.Point]::new(-32000,-32000);$form.Size=[Drawing.Size]::new(200,100);$form.ShowInTaskbar=$false
# Suppress shell icon publication and focus activation in this private test fixture.
$tray=[TappoHelper.TrayHost]::new($form,$false)
$script:jobRunning=$true;$script:workTicks=0;$script:phase=0;$script:phaseTick=0;$script:failure=$null;$script:exitCallbacks=0
$watch=[Diagnostics.Stopwatch]::StartNew()
$timer=[Windows.Forms.Timer]::new();$timer.Interval=60
$tray.add_ExitRequested({$script:jobRunning=$false;$script:exitCallbacks++})
# Same event-order contract as the production form: a cancelled close must not stop work.
$form.Add_FormClosing({param($sender,$eventArgs);if($eventArgs.Cancel){return};$script:jobRunning=$false})
$timer.Add_Tick({
 try {
    if($watch.ElapsedMilliseconds -gt 5000){throw 'Lifecycle fixture timed out'}
    if($script:jobRunning){$script:workTicks++}
    switch($script:phase){
      0 {$script:phase=1;$script:phaseTick=$script:workTicks;$form.Close()}
      1 {
        if($form.Visible -or $form.IsDisposed -or -not $script:jobRunning){throw 'Close must hide and preserve running job'}
        if($script:workTicks-$script:phaseTick -ge 3){$tray.RestoreWindow();$script:phase=2;$script:phaseTick=$script:workTicks}
      }
      2 {
        if(-not $form.Visible -or -not $script:jobRunning){throw 'Restore must display without resetting running job'}
        if($script:workTicks-$script:phaseTick -ge 2){$form.WindowState='Minimized';$script:phase=3;$script:phaseTick=$script:workTicks}
      }
      3 {
        if($form.Visible -or -not $script:jobRunning){throw 'Minimize must hide and preserve running job'}
        if($script:workTicks-$script:phaseTick -ge 3){$tray.RestoreWindow();$script:phase=4;$script:phaseTick=$script:workTicks}
      }
      4 {
        if(-not $form.Visible -or -not $script:jobRunning){throw 'Restore from minimized tray state must keep working'}
        if($script:workTicks-$script:phaseTick -ge 2){$script:phase=5;$tray.Exit()}
      }
    }
 }catch{$script:failure=$_.Exception.Message;$tray.Exit()}
})
$form.Add_Shown({$tray.Start();$timer.Start()})
try{
    [Windows.Forms.Application]::Run($form)
    if($null -ne $script:failure){throw $script:failure}
    if($script:phase -ne 5 -or $script:jobRunning -or $script:exitCallbacks -ne 1){throw 'Explicit tray exit must stop work and end message loop exactly once'}
    Write-Output 'PASS: close-to-tray, hidden timer continuity, restore, minimize-to-tray, and explicit exit'
    Write-Output 'Private offscreen form only; no shell icon, focus activation, official UIA access, or keyboard input.'
}finally{$timer.Stop();$timer.Dispose();$tray.Dispose();$form.Dispose()}
