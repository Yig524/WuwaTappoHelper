$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Production FocusInspector against private offscreen controls; no focus activation.
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
& (Join-Path $repoRoot 'tests\Test-Uia.ps1')
Add-Type -TypeDefinition 'using System.Windows.Forms;public sealed class NonActivatingFocusFixture:Form {protected override bool ShowWithoutActivation {get{return true;}}}' -ReferencedAssemblies System.Windows.Forms,System.Drawing
$form=[NonActivatingFocusFixture]::new();$form.StartPosition='Manual';$form.Location=[Drawing.Point]::new(-31000,-31000);$form.ShowInTaskbar=$false
$editor=[Windows.Forms.TextBox]::new();$editor.SetBounds(5,5,150,25);$form.Controls.Add($editor)
$button=[Windows.Forms.Button]::new();$button.SetBounds(5,40,150,25);$form.Controls.Add($button)
$rootHandle=$form.Handle.ToInt64();$editorHandle=$editor.Handle.ToInt64();$buttonHandle=$button.Handle.ToInt64()
$probeRunspace=[RunspaceFactory]::CreateRunspace();$probeRunspace.ApartmentState='MTA';$probeRunspace.ThreadOptions='ReuseThread';$probeRunspace.Open()
$probe=[PowerShell]::Create();$probe.Runspace=$probeRunspace
[void]$probe.AddScript(@'
param($rootHandle,$editorHandle,$buttonHandle)
$editorResult=[TappoHelper.FocusInspector]::Inspect([Windows.Automation.AutomationElement]::FromHandle([IntPtr]$editorHandle),$rootHandle)
$buttonResult=[TappoHelper.FocusInspector]::Inspect([Windows.Automation.AutomationElement]::FromHandle([IntPtr]$buttonHandle),$rootHandle)
[pscustomobject]@{Editor=$editorResult.Reason;Button=$buttonResult.Reason;EditorRole=$editorResult.Control;ButtonRole=$buttonResult.Control}
'@).AddArgument($rootHandle).AddArgument($editorHandle).AddArgument($buttonHandle)
$script:focusAsync=$null;$script:focusResult=$null;$script:focusFailure=$null
$watch=[Diagnostics.Stopwatch]::StartNew();$timer=[Windows.Forms.Timer]::new();$timer.Interval=40
$timer.Add_Tick({
    try {
        if($watch.ElapsedMilliseconds -gt 8000){throw 'Private UIA fixture timed out'}
        if($null -ne $script:focusAsync -and $script:focusAsync.IsCompleted){
            $script:focusResult=@($probe.EndInvoke($script:focusAsync))
            if($probe.HadErrors){throw ($probe.Streams.Error|Out-String)}
            $form.Close()
        }
    }catch{$script:focusFailure=$_.Exception.Message;$form.Close()}
})
$form.Add_Shown({$script:focusAsync=$probe.BeginInvoke();$timer.Start()})
try {
    [Windows.Forms.Application]::Run($form)
    if($null -ne $script:focusFailure){throw $script:focusFailure}
    if($script:focusResult.Count -ne 1 -or $script:focusResult[0].Editor -ne 'editor_focus' -or $script:focusResult[0].Button -ne ''){throw ('Incorrect real-control classification: '+($script:focusResult|ConvertTo-Json -Compress))}
    Write-Output 'PASS: production UIA inspector protects a real text editor and permits a real button in its owning window'
    Write-Output 'Own offscreen controls only; no foreground activation, outside UIA access, input hooks, or injected keys.'
} finally {$timer.Dispose();$form.Dispose();$probe.Dispose();$probeRunspace.Dispose()}
