namespace TappoHelper {
    using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.IO;using System.Runtime.InteropServices;using System.Windows.Forms;
    public static class Palette {
        public static readonly Color Background=Color.FromArgb(242,246,243),Ink=Color.FromArgb(26,45,38),Muted=Color.FromArgb(92,111,102),Green=Color.FromArgb(29,104,76),Mint=Color.FromArgb(230,244,236),MintBorder=Color.FromArgb(188,224,202),Line=Color.FromArgb(218,229,220),Soft=Color.FromArgb(247,250,248),Gold=Color.FromArgb(148,91,16),GoldBg=Color.FromArgb(253,241,214),GoldBorder=Color.FromArgb(232,201,142),Danger=Color.FromArgb(174,62,50),DangerBg=Color.FromArgb(253,244,243),DangerBorder=Color.FromArgb(235,200,195);
        public static Color ParentColor(Control control){for(Control p=control.Parent;p!=null;p=p.Parent)if(p.BackColor.A==255)return p.BackColor;return Background;}
        public static GraphicsPath Shape(RectangleF r,float radius){float d=radius*2;GraphicsPath p=new GraphicsPath();p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    }
    public sealed class Surface:Panel {
        public Color BorderColor{get;set;}
        public float CornerRadius{get;set;}
        public Surface(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.White;BorderColor=Palette.Line;CornerRadius=14;Padding=new Padding(20,18,20,18);}
        protected override void OnPaintBackground(PaintEventArgs e){
            e.Graphics.Clear(Palette.ParentColor(this));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float r=CornerRadius*e.Graphics.DpiX/96f;
            using(GraphicsPath p=Palette.Shape(new RectangleF(.5f,.5f,Width-1.5f,Height-1.5f),r)){
                using(SolidBrush b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);
                using(Pen pen=new Pen(BorderColor))e.Graphics.DrawPath(pen,p);
            }
        }
    }
    public sealed class CalloutBox:Panel {
        Color accent=Palette.Green;
        public CalloutBox(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Palette.Soft;Padding=new Padding(18,12,14,12);}
        public void SetAccent(Color color){if(accent==color)return;accent=color;Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e){
            e.Graphics.Clear(Palette.ParentColor(this));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float r=10*e.Graphics.DpiX/96f;
            using(GraphicsPath p=Palette.Shape(new RectangleF(.5f,.5f,Width-1.5f,Height-1.5f),r)){
                using(SolidBrush b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);
                using(Pen pen=new Pen(Palette.Line))e.Graphics.DrawPath(pen,p);
            }
            float pad=10*e.Graphics.DpiY/96f,barW=3.5f*e.Graphics.DpiX/96f,barX=8*e.Graphics.DpiX/96f;
            if(Height>pad*2+4){
                using(GraphicsPath bar=Palette.Shape(new RectangleF(barX,pad,barW,Height-pad*2),barW/2f))
                using(SolidBrush sb=new SolidBrush(accent))e.Graphics.FillPath(sb,bar);
            }
        }
    }
    public sealed class DotIndicator:Control {
        Color dot=Palette.Muted;
        public DotIndicator(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Size=new Size(14,14);TabStop=false;}
        public void SetColor(Color color){if(dot==color)return;dot=color;Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(Palette.ParentColor(this));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float d=8*e.Graphics.DpiX/96f;float x=(Width-d)/2f,y=(Height-d)/2f;
            using(SolidBrush b=new SolidBrush(dot))e.Graphics.FillEllipse(b,x,y,d,d);
        }
    }
    public sealed class ReserveBar:Control {
        double ratio;
        public ReserveBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Height=6;TabStop=false;}
        public void SetProgress(long balance,long target){double next=target<=0?0:Math.Max(0,Math.Min(1,(double)balance/target));if(Math.Abs(next-ratio)<0.005)return;ratio=next;Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(Palette.ParentColor(this));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF track=new RectangleF(0,1,Width-1,Math.Max(4,Height-2));
            using(GraphicsPath p=Palette.Shape(track,track.Height/2f))using(SolidBrush b=new SolidBrush(Color.FromArgb(232,239,234)))e.Graphics.FillPath(b,p);
            if(ratio>0.01){float w=Math.Max(track.Height,(float)(track.Width*ratio));using(GraphicsPath p=Palette.Shape(new RectangleF(track.X,track.Y,w,track.Height),track.Height/2f))using(SolidBrush b=new SolidBrush(Palette.Green))e.Graphics.FillPath(b,p);}
        }
    }
    public sealed class BufferedGrid:TableLayoutPanel { public BufferedGrid(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.White;} }
    public sealed class SoftButton:Button {
        bool hover,pressed;public bool Primary{get;set;}public bool Destructive{get;set;}public bool Tinted{get;set;}
        public SoftButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;DoubleBuffered=true;UseVisualStyleBackColor=false;Height=40;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaint(PaintEventArgs e){
            Graphics g=e.Graphics;g.Clear(Palette.ParentColor(this));g.SmoothingMode=SmoothingMode.AntiAlias;
            Color bg,fg,border;
            if(Primary){
                bg=pressed?Color.FromArgb(20,69,50):hover?Color.FromArgb(24,88,64):Palette.Green;
                fg=Color.White;border=bg;
            }else if(Destructive){
                bg=pressed?Color.FromArgb(248,225,222):hover?Color.FromArgb(250,235,233):Palette.DangerBg;
                fg=Palette.Danger;border=Palette.DangerBorder;
            }else if(Tinted){
                bg=pressed?Color.FromArgb(215,237,224):hover?Color.FromArgb(227,243,234):Color.FromArgb(239,247,242);
                fg=Palette.Green;border=Palette.MintBorder;
            }else{
                bg=pressed?Color.FromArgb(228,236,230):hover?Color.FromArgb(238,244,240):Palette.Soft;
                fg=Palette.Ink;border=Color.FromArgb(206,219,209);
            }
            if(!Enabled){bg=Color.FromArgb(238,242,239);fg=Color.FromArgb(156,169,161);border=Color.FromArgb(224,231,226);}
            using(GraphicsPath p=Palette.Shape(new RectangleF(.5f,.5f,Width-1.5f,Height-1.5f),10*g.DpiX/96f)){
                using(SolidBrush b=new SolidBrush(bg))g.FillPath(b,p);
                if(!Primary)using(Pen pen=new Pen(border))g.DrawPath(pen,p);
            }
            TextRenderer.DrawText(g,Text,Font,ClientRectangle,fg,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-5,-5),fg,bg);
        }
    }
    public sealed class ToggleSwitch:CheckBox {
        public ToggleSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=false;Size=new Size(48,28);Cursor=Cursors.Hand;}
        protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaint(PaintEventArgs e){
            Graphics g=e.Graphics;g.Clear(Palette.ParentColor(this));g.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF track=new RectangleF(2,4,Width-4,Height-8);Color bg=Enabled?(Checked?Palette.Green:Color.FromArgb(204,216,207)):Color.FromArgb(228,234,229);
            using(GraphicsPath p=Palette.Shape(track,track.Height/2))using(SolidBrush b=new SolidBrush(bg))g.FillPath(b,p);
            float d=track.Height-6;using(SolidBrush b=new SolidBrush(Color.White))g.FillEllipse(b,Checked?track.Right-d-3:track.X+3,track.Y+3,d,d);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);
        }
    }
    public sealed class StatusPill:Control {
        Color tint=Palette.Mint,ink=Palette.Green,border=Palette.MintBorder,dot=Palette.Green;
        public StatusPill(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Size=new Size(124,32);TabStop=false;}
        public void SetState(string text,Color background,Color foreground,Color borderColor,Color dotColor){
            if(Text==text&&tint==background&&ink==foreground&&border==borderColor&&dot==dotColor)return;
            Text=text;tint=background;ink=foreground;border=borderColor;dot=dotColor;Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e){
            Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Palette.ParentColor(this));
            using(GraphicsPath p=Palette.Shape(new RectangleF(.5f,.5f,Width-1.5f,Height-1.5f),(Height-1)/2f)){
                using(SolidBrush b=new SolidBrush(tint))g.FillPath(b,p);
                using(Pen pen=new Pen(border))g.DrawPath(pen,p);
            }
            float d=7*g.DpiX/96f;float dotX=13*g.DpiX/96f,dotY=(Height-d)/2f;
            using(SolidBrush b=new SolidBrush(dot))g.FillEllipse(b,dotX,dotY,d,d);
            Rectangle textRect=new Rectangle((int)(dotX+d+4),0,Width-(int)(dotX+d+10),Height);
            TextRenderer.DrawText(g,Text,Font,textRect,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
    }
    public sealed class DashboardView:IDisposable {
        public readonly Label Connection,Balance,Slots,Status,Stats;public readonly SoftButton Refresh,TestKey,TestClaim,Stop;public readonly ToggleSwitch Fill,Harvest;
        readonly Label leftValue,rightValue,leftDetail,rightDetail;readonly DotIndicator leftDot,rightDot,connDot;readonly ReserveBar reserveBar;readonly CalloutBox statusCallout;readonly StatusPill pill;readonly Form owner;readonly List<Font> fonts=new List<Font>();readonly Image brandImage;readonly Icon brandIcon;
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
        Font Face(float size,FontStyle style=FontStyle.Regular){Font f=new Font("Microsoft YaHei UI",size,style,GraphicsUnit.Point);fonts.Add(f);return f;}
        Label Text(string text,float size,Color color,FontStyle style=FontStyle.Regular,Color? back=null){return new Label{Text=text,AutoSize=false,ForeColor=color,BackColor=back??Color.White,Font=Face(size,style),TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty,Dock=DockStyle.Fill};}
        TableLayoutPanel Grid(int columns,Color? back=null){TableLayoutPanel p=new BufferedGrid{ColumnCount=columns,RowCount=1,Dock=DockStyle.Fill,BackColor=back??Color.White,Margin=Padding.Empty,Padding=Padding.Empty};p.RowStyles.Add(new RowStyle(SizeType.Percent,100));return p;}
        SoftButton Button(string text,bool primary=false,bool destructive=false,bool tinted=false){return new SoftButton{Text=text,Primary=primary,Destructive=destructive,Tinted=tinted,Font=Face(9.5f,FontStyle.Bold),Dock=DockStyle.Fill,Margin=new Padding(0,0,10,0)};}
        Surface Card(){return new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty};}
        static void Row(TableLayoutPanel grid,Control control,int height,int gap=12){int row=grid.RowCount++;grid.RowStyles.Add(new RowStyle(SizeType.Absolute,height));grid.Controls.Add(control,0,row);if(gap>0){grid.RowCount++;grid.RowStyles.Add(new RowStyle(SizeType.Absolute,gap));}}
        public DashboardView(Form form,string root){
            owner=form;form.SuspendLayout();form.AutoScaleMode=AutoScaleMode.None;form.BackColor=Palette.Background;form.Font=Face(10);form.ClientSize=new Size(920,740);form.MinimumSize=new Size(760,540);form.FormBorderStyle=FormBorderStyle.Sizable;form.MaximizeBox=true;
            string ico=Path.Combine(root,"assets","app.ico"),png=Path.Combine(root,"assets","app-icon.png");
            if(File.Exists(ico)){brandIcon=new Icon(ico,32,32);form.Icon=brandIcon;}if(File.Exists(png)){using(Image source=Image.FromFile(png))brandImage=new Bitmap(source);}
            form.HandleCreated+=delegate{try{int round=2;DwmSetWindowAttribute(form.Handle,33,ref round,4);int caption=ColorTranslator.ToWin32(Palette.Background);DwmSetWindowAttribute(form.Handle,35,ref caption,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};
            Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Palette.Background};form.Controls.Add(scroll);
            TableLayoutPanel page=new TableLayoutPanel{ColumnCount=1,RowCount=0,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Padding=new Padding(24,20,24,18),Margin=Padding.Empty,BackColor=Palette.Background};page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(page);

            TableLayoutPanel header=Grid(3,Palette.Background);header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
            header.Controls.Add(new PictureBox{Image=brandImage,SizeMode=PictureBoxSizeMode.Zoom,Dock=DockStyle.Fill,BackColor=Palette.Background,Margin=new Padding(0,2,12,2)},0,0);
            TableLayoutPanel brand=Grid(1,Palette.Background);brand.RowCount=2;brand.RowStyles.Clear();brand.RowStyles.Add(new RowStyle(SizeType.Absolute,34));brand.RowStyles.Add(new RowStyle(SizeType.Percent,100));brand.Controls.Add(Text("塔波小助手",19,Palette.Ink,FontStyle.Bold,Palette.Background),0,0);brand.Controls.Add(Text("Wuwa Tappo  ·  常开自动化与输入避让",9,Palette.Muted,FontStyle.Regular,Palette.Background),0,1);header.Controls.Add(brand,1,0);
            pill=new StatusPill{Anchor=AnchorStyles.Right,Font=Face(9,FontStyle.Bold)};pill.SetState("只读待命",Palette.Mint,Palette.Green,Palette.MintBorder,Palette.Muted);header.Controls.Add(pill,2,0);Row(page,header,58,14);

            TableLayoutPanel overview=Grid(3,Palette.Background);overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,31));overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,31));
            Surface balanceCard=Card();balanceCard.Padding=new Padding(20,16,20,14);balanceCard.Margin=new Padding(0,0,12,0);
            TableLayoutPanel b=Grid(1);b.RowCount=3;b.RowStyles.Clear();b.RowStyles.Add(new RowStyle(SizeType.Absolute,22));b.RowStyles.Add(new RowStyle(SizeType.Percent,100));b.RowStyles.Add(new RowStyle(SizeType.Absolute,20));
            b.Controls.Add(Text("可用余额",9,Palette.Muted,FontStyle.Bold),0,0);
            Balance=Text("—",25,Palette.Ink,FontStyle.Bold);b.Controls.Add(Balance,0,1);
            TableLayoutPanel bFoot=Grid(2);bFoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));bFoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            bFoot.Controls.Add(Text("目标储备  2,000",8.5f,Palette.Muted),0,0);
            reserveBar=new ReserveBar{Dock=DockStyle.Fill,Margin=new Padding(0,7,0,6)};bFoot.Controls.Add(reserveBar,1,0);
            b.Controls.Add(bFoot,0,2);balanceCard.Controls.Add(b);overview.Controls.Add(balanceCard,0,0);
            Surface left=SlotCard("左侧箱子",out leftValue,out leftDetail,out leftDot);left.Margin=new Padding(0,0,12,0);overview.Controls.Add(left,1,0);
            overview.Controls.Add(SlotCard("右侧箱子",out rightValue,out rightDetail,out rightDot),2,0);Row(page,overview,124,10);

            Surface connBanner=new Surface{Dock=DockStyle.Fill,BackColor=Color.FromArgb(236,242,238),BorderColor=Color.FromArgb(212,223,215),CornerRadius=10,Padding=new Padding(12,0,12,0),Margin=Padding.Empty};
            TableLayoutPanel connRow=Grid(2,connBanner.BackColor);connRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,18));connRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            connDot=new DotIndicator{Dock=DockStyle.Fill};connDot.SetColor(Palette.Muted);connRow.Controls.Add(connDot,0,0);
            Connection=Text("正在连接桌宠…",9,Palette.Muted,FontStyle.Regular,connBanner.BackColor);connRow.Controls.Add(Connection,1,0);
            connBanner.Controls.Add(connRow);Row(page,connBanner,34,12);

            TableLayoutPanel middle=Grid(2,Palette.Background);middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));
            Surface tasks=Card();tasks.Margin=new Padding(0,0,12,0);TableLayoutPanel taskRows=Grid(1);taskRows.RowCount=4;taskRows.RowStyles.Clear();taskRows.RowStyles.Add(new RowStyle(SizeType.Absolute,28));taskRows.RowStyles.Add(new RowStyle(SizeType.Absolute,66));taskRows.RowStyles.Add(new RowStyle(SizeType.Absolute,66));taskRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            taskRows.Controls.Add(Text("自动任务",11,Palette.Ink,FontStyle.Bold),0,0);
            ToggleSwitch fill,harvest;taskRows.Controls.Add(SwitchRow("自动补充余额","补至 2,000 后休息，输入时自动避让",out fill),0,1);taskRows.Controls.Add(SwitchRow("自动领取箱子","槽位就绪后领取，等待结果确认",out harvest),0,2);Fill=fill;Harvest=harvest;
            TableLayoutPanel actions=Grid(1);actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Stop=Button("关闭全部任务",false,true);Stop.Margin=Padding.Empty;actions.Controls.Add(Stop,0,0);actions.Padding=new Padding(0,10,0,0);taskRows.Controls.Add(actions,0,3);tasks.Controls.Add(taskRows);middle.Controls.Add(tasks,0,0);

            Surface activity=Card();TableLayoutPanel activityRows=Grid(1);activityRows.RowCount=6;activityRows.RowStyles.Clear();activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,28));activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,88));activityRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,12));activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,26));activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,22));
            activityRows.Controls.Add(Text("运行状态",11,Palette.Ink,FontStyle.Bold),0,0);
            statusCallout=new CalloutBox{Dock=DockStyle.Fill,Margin=new Padding(0,6,0,0)};
            Status=Text("常开模式：开启上方自动开关后即持续运行。",9.5f,Palette.Ink,FontStyle.Regular,Palette.Soft);Status.TextAlign=ContentAlignment.TopLeft;statusCallout.Controls.Add(Status);activityRows.Controls.Add(statusCallout,0,1);
            activityRows.Controls.Add(new Panel{Dock=DockStyle.Fill,BackColor=Color.White},0,2);
            Panel divider=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(0,5,0,6)};divider.Controls.Add(new Panel{Dock=DockStyle.Top,Height=1,BackColor=Palette.Line});activityRows.Controls.Add(divider,0,3);
            Stats=Text("F24 未验证  ·  补充 0 次  ·  领取 0 次",9,Palette.Ink);activityRows.Controls.Add(Stats,0,4);
            activityRows.Controls.Add(Text("自然输入仍由官方桌宠正常计数",8.5f,Palette.Muted),0,5);activity.Controls.Add(activityRows);middle.Controls.Add(activity,1,0);Row(page,middle,256,12);

            Surface tools=Card();tools.Padding=new Padding(20,14,20,12);TableLayoutPanel toolRows=Grid(1);toolRows.RowCount=3;toolRows.RowStyles.Clear();toolRows.RowStyles.Add(new RowStyle(SizeType.Absolute,26));toolRows.RowStyles.Add(new RowStyle(SizeType.Absolute,38));toolRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            toolRows.Controls.Add(Text("检查与验证",10.5f,Palette.Ink,FontStyle.Bold),0,0);
            TableLayoutPanel toolButtons=Grid(3);for(int i=0;i<3;i++)toolButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
            Refresh=Button("刷新 / 保存诊断");TestKey=Button("验证 F24 · 5次",false,false,true);TestClaim=Button("验证领取 · 单箱");TestClaim.Margin=Padding.Empty;
            toolButtons.Controls.Add(Refresh,0,0);toolButtons.Controls.Add(TestKey,1,0);toolButtons.Controls.Add(TestClaim,2,0);toolRows.Controls.Add(toolButtons,0,1);
            toolRows.Controls.Add(Text("首次补充需通过验证；单箱验证会消耗约 1,000 余额。",8.5f,Palette.Muted),0,2);tools.Controls.Add(toolRows);Row(page,tools,120,10);

            Label footer=Text("关闭 / 最小化后驻留托盘     ·     Ctrl+Alt+F9 关闭全部任务",8.5f,Palette.Muted,FontStyle.Regular,Palette.Background);footer.TextAlign=ContentAlignment.MiddleCenter;Row(page,footer,22,0);
            Slots=new Label{Visible=false};
            using(Graphics metrics=Graphics.FromHwnd(IntPtr.Zero)){float scale=metrics.DpiX/96f;if(Math.Abs(scale-1)>0.01f)form.Scale(new SizeF(scale,scale));}
            form.ResumeLayout(true);
            form.Load+=delegate{Rectangle work=Screen.FromControl(form).WorkingArea;form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,work.Width-32),Math.Min(form.MinimumSize.Height,work.Height-32));if(form.Height>work.Height-32)form.Height=work.Height-32;if(form.Width>work.Width-32)form.Width=work.Width-32;};
        }
        Surface SlotCard(string title,out Label value,out Label detail,out DotIndicator dot){
            Surface card=Card();card.Padding=new Padding(20,16,20,14);TableLayoutPanel rows=Grid(1);rows.RowCount=3;rows.RowStyles.Clear();rows.RowStyles.Add(new RowStyle(SizeType.Absolute,22));rows.RowStyles.Add(new RowStyle(SizeType.Percent,100));rows.RowStyles.Add(new RowStyle(SizeType.Absolute,20));
            rows.Controls.Add(Text(title,9,Palette.Muted,FontStyle.Bold),0,0);
            TableLayoutPanel stateRow=Grid(2);stateRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,18));stateRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            dot=new DotIndicator{Dock=DockStyle.Fill};stateRow.Controls.Add(dot,0,0);
            value=Text("等待连接",18,Palette.Ink,FontStyle.Bold);stateRow.Controls.Add(value,1,0);rows.Controls.Add(stateRow,0,1);
            detail=Text("独立冷却 30 分钟",8.5f,Palette.Muted);rows.Controls.Add(detail,0,2);card.Controls.Add(rows);return card;
        }
        Control SwitchRow(string title,string detail,out ToggleSwitch toggle){
            Surface box=new Surface{Dock=DockStyle.Fill,BackColor=Palette.Soft,BorderColor=Color.FromArgb(228,236,229),CornerRadius=10,Padding=new Padding(14,8,12,8),Margin=new Padding(0,4,0,4)};
            TableLayoutPanel row=Grid(2,Palette.Soft);row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,52));
            TableLayoutPanel words=Grid(1,Palette.Soft);words.RowCount=2;words.RowStyles.Clear();words.RowStyles.Add(new RowStyle(SizeType.Absolute,23));words.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            words.Controls.Add(Text(title,10,Palette.Ink,FontStyle.Bold,Palette.Soft),0,0);words.Controls.Add(Text(detail,8.5f,Palette.Muted,FontStyle.Regular,Palette.Soft),0,1);
            row.Controls.Add(words,0,0);toggle=new ToggleSwitch{Anchor=AnchorStyles.Right,AccessibleName=title,Margin=Padding.Empty};row.Controls.Add(toggle,1,0);
            box.Controls.Add(row);return box;
        }
        void SlotState(Slot slot,Label value,Label detail,DotIndicator dot){
            string text=slot.State=="ready"?"可以领取":slot.State=="cooldown"?(String.IsNullOrEmpty(slot.Text)?"冷却中":slot.Text):slot.State=="hidden"?"暂未显示":"暂不可领";
            if(value.Text!=text)value.Text=text;
            Color color=slot.State=="ready"?Palette.Green:slot.State=="cooldown"?Palette.Gold:Palette.Muted;
            value.ForeColor=color;dot.SetColor(color);
            detail.Text=slot.State=="ready"?"就绪后按需领取":slot.State=="cooldown"?"冷却结束后可再次领取":"独立冷却 30 分钟";
        }
        public void SetSnapshot(Snapshot s){
            if(s.BalanceValid)reserveBar.SetProgress(s.Balance,2000);
            SlotState(s.Left,leftValue,leftDetail,leftDot);SlotState(s.Right,rightValue,rightDetail,rightDot);
        }
        public void SetMode(bool active,bool protectedInput,bool probing,bool claiming,bool connected,bool verified){
            string text=probing?"验证中":claiming?"领取中":!connected?"等待连接":active?(protectedInput?"输入避让中":"常开运行"):"只读待命";
            Color bg=protectedInput&&active?Palette.GoldBg:Palette.Mint,fg=protectedInput&&active?Palette.Gold:Palette.Green,border=protectedInput&&active?Palette.GoldBorder:Palette.MintBorder,dot=protectedInput&&active?Palette.Gold:active||probing||claiming?Palette.Green:Palette.Muted;
            if(!connected){bg=Color.FromArgb(234,239,235);fg=Palette.Muted;border=Palette.Line;dot=Palette.Muted;}
            pill.SetState(text,bg,fg,border,dot);
            connDot.SetColor(connected?Palette.Green:Palette.Gold);
            statusCallout.SetAccent(!connected?Palette.Muted:protectedInput&&active?Palette.Gold:active||probing||claiming?Palette.Green:Palette.Line);
        }
        public int CountControls(){return Count(owner);}static int Count(Control parent){int n=parent.Controls.Count;foreach(Control c in parent.Controls)n+=Count(c);return n;}
        public void Dispose(){Slots.Dispose();if(brandImage!=null)brandImage.Dispose();if(brandIcon!=null)brandIcon.Dispose();foreach(Font font in fonts)font.Dispose();}
    }
}
