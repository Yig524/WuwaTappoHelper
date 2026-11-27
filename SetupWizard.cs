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
            face=new Font("Microsoft YaHei UI",10);titleFace=new Font("Microsoft YaHei UI",18.5f,FontStyle.Bold);Font=face;
            Surface card=new Surface{Bounds=new Rectangle(20,18,610,384),Padding=new Padding(24,22,24,20)};Controls.Add(card);
            heading=new Label{Bounds=new Rectangle(24,22,562,42),Font=titleFace,ForeColor=Palette.Ink,BackColor=Color.White};card.Controls.Add(heading);
            description=new Label{Bounds=new Rectangle(24,74,562,100),ForeColor=Palette.Ink,BackColor=Color.White};card.Controls.Add(description);
            picker=new Panel{Bounds=new Rectangle(24,182,562,38),BackColor=Color.White};card.Controls.Add(picker);
            Surface pathBox=new Surface{Bounds=new Rectangle(0,0,434,38),BackColor=Palette.Soft,BorderColor=Color.FromArgb(206,219,209),CornerRadius=10,Padding=new Padding(12,8,12,8)};picker.Controls.Add(pathBox);
            path=new TextBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=Palette.Soft,ForeColor=Palette.Ink,Font=face,Text=initialPath??"",AccessibleName="官方桌宠程序路径"};pathBox.Controls.Add(path);
            browse=new SoftButton{Bounds=new Rectangle(446,0,116,38),Text="选择程序…",Font=face,Tinted=true};picker.Controls.Add(browse);
            browse.Click+=delegate{using(OpenFileDialog dialog=new OpenFileDialog{Title="选择官方 WuwaTappo.exe",Filter="官方桌宠程序|WuwaTappo.exe|应用程序|*.exe",CheckFileExists=true}){if(dialog.ShowDialog(this)==DialogResult.OK)path.Text=dialog.FileName;}};
            path.TextChanged+=delegate{PathValidated=false;feedback.Text="路径已更改，继续时将重新校验。";};
            feedback=new Label{Bounds=new Rectangle(24,232,562,76),ForeColor=Palette.Muted,BackColor=Color.White};card.Controls.Add(feedback);
            back=new SoftButton{Bounds=new Rectangle(24,322,106,42),Text="上一步",Font=face};back.Click+=delegate{if(Stage>0)ShowStage(Stage-1);else{DialogResult=DialogResult.Cancel;Close();}};card.Controls.Add(back);
            later=new LinkLabel{Bounds=new Rectangle(148,326,200,34),Text="稍后连接，先看使用步骤",LinkColor=Palette.Green,BackColor=Color.White,TextAlign=ContentAlignment.MiddleLeft};later.Click+=delegate{if(Stage==1)ShowStage(2);};card.Controls.Add(later);
            next=new SoftButton{Bounds=new Rectangle(398,322,188,42),Text="校验并继续",Primary=true,Font=face};next.Click+=delegate{Advance();};card.Controls.Add(next);
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
            else{heading.Text="3 / 3   准备开始";description.Text="进入主界面后：\r\n① 如需自动补余额，点击“验证 F24 · 5次”并保持键鼠静止约12秒。\r\n② 开启“自动补充余额”和/或“自动领取箱子”开关即可常开运行。\r\n③ 随时可关闭开关或按 Ctrl+Alt+F9 关闭全部任务。";next.Text="保存并进入主界面";feedback.Text="本向导不会自动发键或启用任务。单箱领取验证会真实消耗约 1,000 余额（可选操作）。\r\n关闭窗口“×”仅隐藏到托盘，彻底退出请使用托盘右键菜单。";}
        }
        protected override void WndProc(ref Message message){if(message.Msg==Native.RestoreMessage){WindowState=FormWindowState.Normal;Show();Activate();return;}base.WndProc(ref message);}
        protected override bool ProcessDialogKey(Keys keyData){if(keyData==Keys.Escape){DialogResult=DialogResult.Cancel;Close();return true;}return base.ProcessDialogKey(keyData);}
        protected override void Dispose(bool disposing){if(disposing){face.Dispose();titleFace.Dispose();if(brandIcon!=null)brandIcon.Dispose();}base.Dispose(disposing);}
    }
}
