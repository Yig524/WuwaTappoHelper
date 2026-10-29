param([switch]$At96,[switch]$Compact)
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[void][IO.Directory]::CreateDirectory((Join-Path $root 'test-results'))
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase,System.Windows.Forms,System.Drawing
$refs=@([Windows.Automation.AutomationElement].Assembly.Location,[Windows.Automation.AutomationProperty].Assembly.Location,[Windows.Point].Assembly.Location,[Windows.Forms.Form].Assembly.Location,[Drawing.Bitmap].Assembly.Location,[Collections.Generic.HashSet[int]].Assembly.Location,[regex].Assembly.Location)|Select-Object -Unique
$source=[IO.File]::ReadAllText((Join-Path $root 'UiaEngine.cs'),[Text.Encoding]::UTF8)+[Environment]::NewLine+[IO.File]::ReadAllText((Join-Path $root 'Dashboard.cs'),[Text.Encoding]::UTF8)
Add-Type -TypeDefinition $source -ReferencedAssemblies $refs
[TappoHelper.Native]::InitDpi()
if($At96){Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;public static class PreviewDpi { [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value); }';[void][PreviewDpi]::SetThreadDpiAwarenessContext([IntPtr]::new(-1))}
$suffix=if($At96){'-96'}else{''};if($Compact){$suffix+='-compact'}
# Plain Form: never creates HelperForm's global hotkeys or real input observers.
Add-Type -TypeDefinition 'using System.Windows.Forms;public sealed class DashboardPreviewForm:Form {protected override bool ShowWithoutActivation {get{return true;}}}' -ReferencedAssemblies System.Windows.Forms,System.Drawing
$form=[DashboardPreviewForm]::new();$dashboard=[TappoHelper.DashboardView]::new($form,$root)
$form.StartPosition='Manual';$form.Location=[Drawing.Point]::new(-32000,-32000);$form.ShowInTaskbar=$false
try {
    $dashboard.Balance.Text='1,680';$dashboard.Connection.Text='已连接官方桌宠 · 数据实时更新'
    $snapshot=[TappoHelper.Snapshot]::new();$snapshot.Left.State='ready';$snapshot.Right.State='cooldown';$snapshot.Right.Text='12:34';$dashboard.SetSnapshot($snapshot)
    $dashboard.Status.Text='准备好了。选择自动任务后，点击开始运行。'
    $dashboard.Stats.Text='F24 已通过  ·  补充 120 次  ·  已确认领取 2 次'
    $dashboard.Fill.Checked=$true;$dashboard.Harvest.Checked=$true;$dashboard.SetMode($false,$false,$false,$false,$true,$true)
    $form.Show();$form.PerformLayout();[Windows.Forms.Application]::DoEvents()
    if($Compact){$factor=$form.ClientSize.Width/920.0;$form.ClientSize=[Drawing.Size]::new([int](760*$factor),[int](540*$factor));$form.PerformLayout();[Windows.Forms.Application]::DoEvents();if(-not $form.Controls[0].VerticalScroll.Visible){throw 'Compact window must allow scrolling to verification tools'}}
    $bitmap=[Drawing.Bitmap]::new($form.ClientSize.Width,$form.ClientSize.Height)
    try{$form.Controls[0].DrawToBitmap($bitmap,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$bitmap.Size));$bitmap.Save((Join-Path $root ('test-results\dashboard-preview'+$suffix+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
    $dashboard.Status.Text='输入避让中：焦点在文字编辑区域。点输入框外或结束编辑后恢复；无需切换讯飞或中文布局。';$dashboard.SetMode($true,$true,$false,$false,$true,$true)
    $bitmap=[Drawing.Bitmap]::new($form.ClientSize.Width,$form.ClientSize.Height)
    try{$form.Controls[0].DrawToBitmap($bitmap,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$bitmap.Size));$bitmap.Save((Join-Path $root ('test-results\dashboard-protected'+$suffix+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
    'Rendered private dashboard with sample data; no real UIA, input hooks, hotkeys, or injected input.'
} finally {$form.Dispose();$dashboard.Dispose()}
