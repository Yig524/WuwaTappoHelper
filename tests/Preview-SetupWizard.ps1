$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Test-UiaGui.ps1')
Add-Type -TypeDefinition 'using System.Windows.Forms;public sealed class SetupPreviewHost:Form {protected override bool ShowWithoutActivation {get{return true;}}}' -ReferencedAssemblies System.Windows.Forms,System.Drawing
$repo=Split-Path $PSScriptRoot;$output=Join-Path $repo 'test-results';[void][IO.Directory]::CreateDirectory($output)
$wizard=[TappoHelper.SetupWizard]::new('D:\Games\WuwaTappo\WuwaTappo.exe','')
$preview=[SetupPreviewHost]::new();$preview.FormBorderStyle='None';$preview.AutoScaleMode='None';$preview.Font=$wizard.Font;$preview.BackColor=$wizard.BackColor;$preview.ClientSize=$wizard.ClientSize;$preview.ShowInTaskbar=$false;$preview.StartPosition='Manual';$preview.Location=[Drawing.Point]::new(-32000,-32000)
$controls=@($wizard.Controls);foreach($control in $controls){$preview.Controls.Add($control)}
try{
    $preview.Show();[Windows.Forms.Application]::DoEvents()
    for($stage=0;$stage -le 2;$stage++){
        $image=[Drawing.Bitmap]::new($preview.ClientSize.Width,$preview.ClientSize.Height)
        try{$preview.DrawToBitmap($image,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$image.Size));$image.Save((Join-Path $output ('setup-'+$stage+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
        if($stage -eq 0){$check=[TappoHelper.ExecutableCheck]::new();$check.Compatible=$true;$wizard.SetValidation($check);$wizard.Advance()}
        elseif($stage -eq 1){$wizard.SetConnection($true,'示例：已连接，可用余额 1,680');$wizard.Advance()}
    }
}finally{$preview.Dispose();$wizard.Dispose()}
'Rendered sample wizard pages only; no outside UIA, input or configuration writes.'
