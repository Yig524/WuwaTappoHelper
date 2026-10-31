namespace TappoHelper {
    using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.IO;using System.Runtime.InteropServices;using System.Windows.Forms;
    public static class Palette {
        public static readonly Color Background=Color.FromArgb(244,247,244),Ink=Color.FromArgb(29,48,42),Muted=Color.FromArgb(106,123,115),Green=Color.FromArgb(32,103,77),Mint=Color.FromArgb(231,244,235),Line=Color.FromArgb(225,233,226),Gold=Color.FromArgb(155,108,39);
        public static Color ParentColor(Control control){for(Control p=control.Parent;p!=null;p=p.Parent)if(p.BackColor.A==255)return p.BackColor;return Background;}
        public static GraphicsPath Shape(RectangleF r,float radius){float d=radius*2;GraphicsPath p=new GraphicsPath();p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    }
    public sealed class Surface:Panel {
        public Surface(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.White;Padding=new Padding(20);}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Palette.ParentColor(this));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float r=16*e.Graphics.DpiX/96;using(GraphicsPath p=Palette.Shape(new RectangleF(.5f,.5f,Width-1,Height-1),r)){using(SolidBrush b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);using(Pen pen=new Pen(Palette.Line))e.Graphics.DrawPath(pen,p);}}
    }
    public sealed class BufferedGrid:TableLayoutPanel { public BufferedGrid(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.White;} }
    public sealed class SoftButton:Button {
        bool hover,pressed;public bool Primary{get;set;}public bool Destructive{get;set;}
        public SoftButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;DoubleBuffered=true;UseVisualStyleBackColor=false;Height=40;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaint(PaintEventArgs e){
            Graphics g=e.Graphics;g.Clear(Palette.ParentColor(this));g.SmoothingMode=SmoothingMode.AntiAlias;
            Color bg=Primary?(pressed?Color.FromArgb(20,69,50):hover?Color.FromArgb(25,86,63):Palette.Green):pressed?Color.FromArgb(230,237,231):hover?Color.FromArgb(240,246,240):Color.White;
            Color fg=Primary?Color.White:Destructive?Color.FromArgb(171,67,56):Palette.Ink;if(!Enabled){bg=Color.FromArgb(240,243,239);fg=Color.FromArgb(164,177,166);}
            using(GraphicsPath p=Palette.Shape(new RectangleF(.5f,.5f,Width-1,Height-1),10*g.DpiX/96)){using(SolidBrush b=new SolidBrush(bg))g.FillPath(b,p);if(!Primary)using(Pen pen=new Pen(Destructive?Color.FromArgb(233,207,201):Palette.Line))g.DrawPath(pen,p);}
            TextRenderer.DrawText(g,Text,Font,ClientRectangle,fg,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-5,-5),fg,bg);
        }
    }
    public sealed class ToggleSwitch:CheckBox {
        public ToggleSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=false;Size=new Size(48,28);Cursor=Cursors.Hand;}
        protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaint(PaintEventArgs e){
            Graphics g=e.Graphics;g.Clear(Palette.ParentColor(this));g.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF track=new RectangleF(2,4,Width-4,Height-8);Color bg=Enabled?(Checked?Palette.Green:Color.FromArgb(211,221,213)):Color.FromArgb(230,235,230);
            using(GraphicsPath p=Palette.Shape(track,track.Height/2))using(SolidBrush b=new SolidBrush(bg))g.FillPath(b,p);
            float d=track.Height-6;using(SolidBrush b=new SolidBrush(Color.White))g.FillEllipse(b,Checked?track.Right-d-3:track.X+3,track.Y+3,d,d);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);
        }
    }
    public sealed class StatusPill:Control {
        Color tint=Palette.Mint,ink=Palette.Green;
        public StatusPill(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Size=new Size(112,30);TabStop=false;}
        public void SetState(string text,Color background,Color foreground){if(Text==text&&tint==background&&ink==foreground)return;Text=text;tint=background;ink=foreground;Invalidate();}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Palette.ParentColor(this));using(GraphicsPath p=Palette.Shape(new RectangleF(0,0,Width-1,Height-1),Height/2f-1))using(SolidBrush b=new SolidBrush(tint))e.Graphics.FillPath(b,p);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
    }
    public sealed class DashboardView:IDisposable {
        public readonly Label Connection,Balance,Slots,Status,Stats;public readonly SoftButton Refresh,TestKey,TestClaim,Start,Stop;public readonly ToggleSwitch Fill,Harvest;
        readonly Label leftValue,rightValue,leftDetail,rightDetail;readonly StatusPill pill;readonly Form owner;readonly List<Font> fonts=new List<Font>();readonly Image brandImage;readonly Icon brandIcon;
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
        Font Face(float size,FontStyle style=FontStyle.Regular){Font f=new Font("Microsoft YaHei UI",size,style,GraphicsUnit.Point);fonts.Add(f);return f;}
        Label Text(string text,float size,Color color,FontStyle style=FontStyle.Regular){return new Label{Text=text,AutoSize=false,ForeColor=color,BackColor=Color.White,Font=Face(size,style),TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty,Dock=DockStyle.Fill};}
        TableLayoutPanel Grid(int columns){TableLayoutPanel p=new BufferedGrid{ColumnCount=columns,RowCount=1,Dock=DockStyle.Fill,BackColor=Color.White,Margin=Padding.Empty,Padding=Padding.Empty};p.RowStyles.Add(new RowStyle(SizeType.Percent,100));return p;}
        SoftButton Button(string text,bool primary=false,bool destructive=false){return new SoftButton{Text=text,Primary=primary,Destructive=destructive,Font=Face(10,FontStyle.Bold),Dock=DockStyle.Fill,Margin=new Padding(0,0,8,0)};}
        Surface Card(){return new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty};}
        static void Row(TableLayoutPanel grid,Control control,int height,int gap=12){int row=grid.RowCount++;grid.RowStyles.Add(new RowStyle(SizeType.Absolute,height));grid.Controls.Add(control,0,row);if(gap>0){grid.RowCount++;grid.RowStyles.Add(new RowStyle(SizeType.Absolute,gap));}}
        public DashboardView(Form form,string root){
            owner=form;form.SuspendLayout();form.AutoScaleMode=AutoScaleMode.None;form.BackColor=Palette.Background;form.Font=Face(10);form.ClientSize=new Size(920,730);form.MinimumSize=new Size(760,540);form.FormBorderStyle=FormBorderStyle.Sizable;form.MaximizeBox=true;
            string ico=Path.Combine(root,"assets","app.ico"),png=Path.Combine(root,"assets","app-icon.png");
            if(File.Exists(ico)){brandIcon=new Icon(ico,32,32);form.Icon=brandIcon;}if(File.Exists(png)){using(Image source=Image.FromFile(png))brandImage=new Bitmap(source);}
            form.HandleCreated+=delegate{try{int round=2;DwmSetWindowAttribute(form.Handle,33,ref round,4);int caption=ColorTranslator.ToWin32(Palette.Background);DwmSetWindowAttribute(form.Handle,35,ref caption,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};
            Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Palette.Background};form.Controls.Add(scroll);
            TableLayoutPanel page=new TableLayoutPanel{ColumnCount=1,RowCount=0,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Padding=new Padding(24),Margin=Padding.Empty,BackColor=Palette.Background};page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(page);
            TableLayoutPanel header=Grid(3);header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,118));
            header.Controls.Add(new PictureBox{Image=brandImage,SizeMode=PictureBoxSizeMode.Zoom,Dock=DockStyle.Fill,Margin=new Padding(0,0,12,0)},0,0);
            TableLayoutPanel brand=Grid(1);brand.RowCount=2;brand.RowStyles.Clear();brand.RowStyles.Add(new RowStyle(SizeType.Absolute,34));brand.RowStyles.Add(new RowStyle(SizeType.Percent,100));brand.Controls.Add(Text("塔波小助手",19,Palette.Ink,FontStyle.Bold),0,0);brand.Controls.Add(Text("Wuwa Tappo  /  让收获轻松一点",9,Palette.Muted),0,1);header.Controls.Add(brand,1,0);
            pill=new StatusPill{Anchor=AnchorStyles.Right,Font=Face(9,FontStyle.Bold)};pill.SetState("只读待命",Palette.Mint,Palette.Green);header.Controls.Add(pill,2,0);Row(page,header,60,16);
            TableLayoutPanel overview=Grid(3);overview.BackColor=Palette.Background;overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,31));overview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,31));
            Surface balanceCard=Card();balanceCard.Margin=new Padding(0,0,12,0);TableLayoutPanel b=Grid(1);b.RowCount=3;b.RowStyles.Clear();b.RowStyles.Add(new RowStyle(SizeType.Absolute,22));b.RowStyles.Add(new RowStyle(SizeType.Percent,100));b.RowStyles.Add(new RowStyle(SizeType.Absolute,20));b.Controls.Add(Text("可用余额",9,Palette.Muted),0,0);Balance=Text("—",28,Palette.Ink,FontStyle.Bold);b.Controls.Add(Balance,0,1);b.Controls.Add(Text("目标储备  2,000",9,Palette.Muted),0,2);balanceCard.Controls.Add(b);overview.Controls.Add(balanceCard,0,0);
            Surface left=SlotCard("左侧箱子",out leftValue,out leftDetail);left.Margin=new Padding(0,0,12,0);overview.Controls.Add(left,1,0);overview.Controls.Add(SlotCard("右侧箱子",out rightValue,out rightDetail),2,0);Row(page,overview,138,8);
            Connection=Text("正在连接桌宠…",9,Palette.Muted);Row(page,Connection,24,12);
            TableLayoutPanel middle=Grid(2);middle.BackColor=Palette.Background;middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));
            Surface tasks=Card();tasks.Margin=new Padding(0,0,12,0);TableLayoutPanel taskRows=Grid(1);taskRows.RowCount=4;taskRows.RowStyles.Clear();taskRows.RowStyles.Add(new RowStyle(SizeType.Absolute,28));taskRows.RowStyles.Add(new RowStyle(SizeType.Absolute,59));taskRows.RowStyles.Add(new RowStyle(SizeType.Absolute,59));taskRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));taskRows.Controls.Add(Text("自动任务",11,Palette.Ink,FontStyle.Bold),0,0);
            ToggleSwitch fill,harvest;taskRows.Controls.Add(SwitchRow("自动补充余额","补至 2,000 后休息，输入时自动避让",out fill),0,1);taskRows.Controls.Add(SwitchRow("自动领取箱子","槽位就绪后领取，等待结果确认",out harvest),0,2);Fill=fill;Harvest=harvest;
            TableLayoutPanel actions=Grid(2);actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));Start=Button("开始运行",true);Stop=Button("停止",false,true);Stop.Margin=Padding.Empty;actions.Controls.Add(Start,0,0);actions.Controls.Add(Stop,1,0);actions.Padding=new Padding(0,8,0,0);taskRows.Controls.Add(actions,0,3);tasks.Controls.Add(taskRows);middle.Controls.Add(tasks,0,0);
            Surface activity=Card();TableLayoutPanel activityRows=Grid(1);activityRows.RowCount=4;activityRows.RowStyles.Clear();activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,28));activityRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,43));activityRows.RowStyles.Add(new RowStyle(SizeType.Absolute,24));activityRows.Controls.Add(Text("运行状态",11,Palette.Ink,FontStyle.Bold),0,0);Status=Text("准备好了。选择自动任务后，点击开始运行。",10,Palette.Ink);activityRows.Controls.Add(Status,0,1);Stats=Text("F24 未验证  ·  补充 0 次  ·  领取 0 次",9,Palette.Muted);activityRows.Controls.Add(Stats,0,2);activityRows.Controls.Add(Text("自然输入仍由官方桌宠正常计数",8.5f,Palette.Muted),0,3);activity.Controls.Add(activityRows);middle.Controls.Add(activity,1,0);Row(page,middle,240,12);
            Surface tools=Card();tools.Padding=new Padding(20,14,20,12);TableLayoutPanel toolRows=Grid(1);toolRows.RowCount=3;toolRows.RowStyles.Clear();toolRows.RowStyles.Add(new RowStyle(SizeType.Absolute,24));toolRows.RowStyles.Add(new RowStyle(SizeType.Absolute,38));toolRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));toolRows.Controls.Add(Text("检查与验证",10,Palette.Ink,FontStyle.Bold),0,0);TableLayoutPanel toolButtons=Grid(3);for(int i=0;i<3;i++)toolButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));Refresh=Button("刷新 / 保存诊断");TestKey=Button("验证 F24 · 5次");TestClaim=Button("验证领取 · 单箱");TestClaim.Margin=Padding.Empty;toolButtons.Controls.Add(Refresh,0,0);toolButtons.Controls.Add(TestKey,1,0);toolButtons.Controls.Add(TestClaim,2,0);toolRows.Controls.Add(toolButtons,0,1);toolRows.Controls.Add(Text("首次补充需通过验证；单箱验证会消耗约 1,000 余额。",8.5f,Palette.Muted),0,2);tools.Controls.Add(toolRows);Row(page,tools,118,12);
            Label footer=Text("关闭 / 最小化后驻留托盘     ·     Ctrl+Alt+F8 开始/暂停     ·     Ctrl+Alt+F9 停止",8.5f,Palette.Muted);Row(page,footer,24,0);
            Slots=new Label{Visible=false};
            // Build the complete logical layout before scaling. AutoScale during
            // construction scales controls but misses later absolute row styles.
            using(Graphics metrics=Graphics.FromHwnd(IntPtr.Zero)){float scale=metrics.DpiX/96f;if(Math.Abs(scale-1)>0.01f)form.Scale(new SizeF(scale,scale));}
            form.ResumeLayout(true);
            form.Load+=delegate{Rectangle work=Screen.FromControl(form).WorkingArea;form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,work.Width-32),Math.Min(form.MinimumSize.Height,work.Height-32));if(form.Height>work.Height-32)form.Height=work.Height-32;if(form.Width>work.Width-32)form.Width=work.Width-32;};
        }
        Surface SlotCard(string title,out Label value,out Label detail){Surface card=Card();TableLayoutPanel rows=Grid(1);rows.RowCount=3;rows.RowStyles.Clear();rows.RowStyles.Add(new RowStyle(SizeType.Absolute,22));rows.RowStyles.Add(new RowStyle(SizeType.Percent,100));rows.RowStyles.Add(new RowStyle(SizeType.Absolute,20));rows.Controls.Add(Text(title,9,Palette.Muted),0,0);value=Text("等待连接",17,Palette.Ink,FontStyle.Bold);detail=Text("独立冷却 30 分钟",9,Palette.Muted);rows.Controls.Add(value,0,1);rows.Controls.Add(detail,0,2);card.Controls.Add(rows);return card;}
        Control SwitchRow(string title,string detail,out ToggleSwitch toggle){TableLayoutPanel row=Grid(2);row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,52));TableLayoutPanel words=Grid(1);words.RowCount=2;words.RowStyles.Clear();words.RowStyles.Add(new RowStyle(SizeType.Absolute,27));words.RowStyles.Add(new RowStyle(SizeType.Percent,100));words.Controls.Add(Text(title,10,Palette.Ink,FontStyle.Bold),0,0);words.Controls.Add(Text(detail,8.5f,Palette.Muted),0,1);row.Controls.Add(words,0,0);toggle=new ToggleSwitch{Anchor=AnchorStyles.Right,AccessibleName=title,Margin=Padding.Empty};row.Controls.Add(toggle,1,0);return row;}
        void SlotState(Slot slot,Label value,Label detail){string text=slot.State=="ready"?"可以领取":slot.State=="cooldown"?(String.IsNullOrEmpty(slot.Text)?"冷却中":slot.Text):slot.State=="hidden"?"暂未显示":"暂不可领";if(value.Text!=text)value.Text=text;value.ForeColor=slot.State=="ready"?Palette.Green:slot.State=="cooldown"?Palette.Gold:Palette.Muted;detail.Text=slot.State=="ready"?"就绪后按需领取":slot.State=="cooldown"?"冷却结束后可再次领取":"独立冷却 30 分钟";}
        public void SetSnapshot(Snapshot s){SlotState(s.Left,leftValue,leftDetail);SlotState(s.Right,rightValue,rightDetail);}
        public void SetMode(bool running,bool protectedInput,bool probing,bool claiming,bool connected,bool verified){
            string text=probing?"验证中":claiming?"领取中":!connected?"等待连接":running?(protectedInput?"输入避让中":"正在运行"):"只读待命";
            Color bg=protectedInput&&running?Color.FromArgb(251,240,217):Palette.Mint,fg=protectedInput&&running?Palette.Gold:Palette.Green;if(!connected){bg=Color.FromArgb(235,239,234);fg=Palette.Muted;}
            pill.SetState(text,bg,fg);string action=running||probing||claiming?"暂停运行":"开始运行";if(Start.Text!=action)Start.Text=action;
        }
        public int CountControls(){return Count(owner);}static int Count(Control parent){int n=parent.Controls.Count;foreach(Control c in parent.Controls)n+=Count(c);return n;}
        public void Dispose(){Slots.Dispose();if(brandImage!=null)brandImage.Dispose();if(brandIcon!=null)brandIcon.Dispose();foreach(Font font in fonts)font.Dispose();}
    }
}
