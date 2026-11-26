namespace TappoHelper {
    using System;using System.Drawing;using System.IO;using System.Security.Cryptography;using System.Windows.Forms;
    public sealed class ExecutableCheck {
        public bool Compatible;public string Message;
    }
    public sealed class SetupWizard:Form {
        public const int CurrentVersion=1;
        public event Action ValidateRequested,ConnectRequested;
        public string SelectedPath{get{return path.Text.Trim();}}
        public int Stage{get;private set;}
        public bool PathValidated{get;private set;}
        public bool ConnectionReady{get;private set;}
        readonly Label heading,description,feedback;readonly TextBox path;readonly SoftButton browse,next,back;readonly LinkLabel later;readonly Panel picker;
        readonly Font face,titleFace;
        readonly Icon brandIcon;
        public SetupWizard(string initialPath,string configError){
            Text="Wuwa Tappo · 首次使用";StartPosition=FormStartPosition.CenterScreen;ClientSize=new Size(650,420);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Palette.Background;AutoScaleMode=AutoScaleMode.None;
            try{brandIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Icon=brandIcon;}catch{}
            face=new Font("Microsoft YaHei UI",10);titleFace=new Font("Microsoft YaHei UI",19,FontStyle.Bold);Font=face;
            heading=new Label{Bounds=new Rectangle(28,24,594,48),Font=titleFace,ForeColor=Palette.Ink};Controls.Add(heading);
            description=new Label{Bounds=new Rectangle(30,84,590,106),ForeColor=Palette.Ink};Controls.Add(description);
            picker=new Panel{Bounds=new Rectangle(30,199,590,38)};Controls.Add(picker);
            path=new TextBox{Bounds=new Rectangle(0,6,460,29),Text=initialPath??"",AccessibleName="官方桌宠程序路径"};picker.Controls.Add(path);
            browse=new SoftButton{Bounds=new Rectangle(474,0,116,36),Text="选择程序…",Font=face};picker.Controls.Add(browse);
            browse.Click+=delegate{using(OpenFileDialog dialog=new OpenFileDialog{Title="选择官方 WuwaTappo.exe",Filter="官方桌宠程序|WuwaTappo.exe|应用程序|*.exe",CheckFileExists=true}){if(dialog.ShowDialog(this)==DialogResult.OK)path.Text=dialog.FileName;}};
            path.TextChanged+=delegate{PathValidated=false;feedback.Text="路径已更改，继续时将重新校验。";};
            feedback=new Label{Bounds=new Rectangle(30,250,590,83),ForeColor=Palette.Muted};Controls.Add(feedback);
            back=new SoftButton{Bounds=new Rectangle(30,350,106,42),Text="上一步",Font=face};back.Click+=delegate{if(Stage>0)ShowStage(Stage-1);else{DialogResult=DialogResult.Cancel;Close();}};Controls.Add(back);
            later=new LinkLabel{Bounds=new Rectangle(158,354,202,34),Text="稍后连接，先看使用步骤",LinkColor=Palette.Green,TextAlign=ContentAlignment.MiddleLeft};later.Click+=delegate{if(Stage==1)ShowStage(2);};Controls.Add(later);
            next=new SoftButton{Bounds=new Rectangle(432,350,188,42),Text="校验并继续",Primary=true,Font=face};next.Click+=delegate{Advance();};Controls.Add(next);
            ShowStage(0);if(!String.IsNullOrEmpty(configError))feedback.Text=configError;
            using(Graphics g=Graphics.FromHwnd(IntPtr.Zero)){float scale=g.DpiX/96f;if(Math.Abs(scale-1)>0.01f)Scale(new SizeF(scale,scale));}
            AutoScroll=true;AutoScrollMinSize=ClientSize;
            Load+=delegate{Rectangle work=Screen.FromControl(this).WorkingArea;if(Height>work.Height-32)Height=work.Height-32;if(Width>work.Width-32)Width=work.Width-32;};
        }
        public static ExecutableCheck CheckExecutable(string selectedPath){
            try{
                if(String.IsNullOrWhiteSpace(selectedPath)||!File.Exists(selectedPath))return new ExecutableCheck{Message="找不到文件。请选择已经安装的官方 WuwaTappo.exe。"};
                if(!String.Equals(Path.GetFileName(selectedPath),"WuwaTappo.exe",StringComparison.OrdinalIgnoreCase))return new ExecutableCheck{Message="请选择官方 WuwaTappo.exe，不要选择助手、卸载程序或快捷方式。"};
                // Let Windows validate Authenticode, including timestamp and certificate trust.
                // Encode the path as data; never concatenate it into executable script syntax.
                string encodedPath=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(selectedPath)));
                string script="$ErrorActionPreference='Stop';$s=Get-AuthenticodeSignature -LiteralPath ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+encodedPath+"')));if($s.Status -eq 'Valid' -and $null -ne $s.SignerCertificate -and $s.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName,$false) -ceq '广州库洛数界科技有限公司'){[Console]::Write('official-valid')}else{[Console]::Write('signature-rejected')}";
                string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
                var info=new System.Diagnostics.ProcessStartInfo(powershell,"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var process=System.Diagnostics.Process.Start(info)){
                    var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                    if(!process.WaitForExit(20000)){try{process.Kill();}catch{}return new ExecutableCheck{Message="官方签名校验超时，请检查系统证书或网络后重试。"};}
                    bool trusted=process.ExitCode==0 && output.Result=="official-valid";
                    string version=System.Diagnostics.FileVersionInfo.GetVersionInfo(selectedPath).FileVersion;
                    return new ExecutableCheck{Compatible=trusted,Message=trusted?"官方签名与发布者校验通过（版本 "+version+"）；界面能力将在连接后检查。":"无法确认有效的库洛官方签名。请检查文件来源、系统时间与证书状态。"};
                }
            }catch(Exception error){return new ExecutableCheck{Message="无法校验所选文件："+error.Message};}
        }
        public void SetValidation(ExecutableCheck check){PathValidated=check.Compatible;feedback.Text=check.Message;feedback.ForeColor=check.Compatible?Palette.Green:Color.FromArgb(171,67,56);}
        public void SetConnection(bool ready,string message){ConnectionReady=ready;if(Stage==1){feedback.Text=message;feedback.ForeColor=ready?Palette.Green:Palette.Muted;next.Enabled=ready;}}
        public void Advance(){
            if(Stage==0){Action validate=ValidateRequested;if(validate!=null)validate();if(PathValidated)ShowStage(1);}
            else if(Stage==1){if(ConnectionReady)ShowStage(2);}
            else{DialogResult=DialogResult.OK;Close();}
        }
        void ShowStage(int stage){
            Stage=stage;picker.Visible=stage==0;back.Visible=true;back.Text=stage==0?"取消":"上一步";later.Visible=stage==1;next.Enabled=true;feedback.ForeColor=Palette.Muted;
            if(stage==0){heading.Text="1 / 3   选择官方桌宠";description.Text="先告诉助手官方桌宠装在哪里。\r\n请选择官方 WuwaTappo.exe；助手会只读检查文件兼容性。\r\n设置完成后会保存路径，下次不必重复填写。";next.Text="校验并继续";feedback.Text="不修改官方文件，不发送按键，不领取箱子。";}
            else if(stage==1){ConnectionReady=false;heading.Text="2 / 3   检查连接";description.Text="请手动启动官方桌宠并登录，保持桌宠可见。\r\n这里仅检查窗口和余额，连接就绪后才能继续。\r\n如果暂时不方便登录，可选择“稍后连接”。";next.Text="继续";next.Enabled=false;feedback.Text="等待官方桌宠连接…";Action connect=ConnectRequested;if(connect!=null)connect();}
            else{heading.Text="3 / 3   准备开始";description.Text="进入主界面后：\r\n① 点击“验证 F24 · 5次”，保持键鼠静止约12秒。\r\n② 验证通过后，按需选择自动补充与自动领取。\r\n③ 点击“开始运行”，随时可按 Ctrl+Alt+F9 停止。";next.Text="保存并进入主界面";feedback.Text="本向导不会自动发键或启用任务。单箱领取验证会真实消耗约1,000余额；它是可选操作。窗口“×”会驻留托盘，退出需使用托盘菜单。";}
        }
        protected override void WndProc(ref Message message){if(message.Msg==Native.RestoreMessage){WindowState=FormWindowState.Normal;Show();Activate();return;}base.WndProc(ref message);}
        protected override bool ProcessDialogKey(Keys keyData){if(keyData==Keys.Escape){DialogResult=DialogResult.Cancel;Close();return true;}return base.ProcessDialogKey(keyData);}
        protected override void Dispose(bool disposing){if(disposing){face.Dispose();titleFace.Dispose();if(brandIcon!=null)brandIcon.Dispose();}base.Dispose(disposing);}
    }
}
