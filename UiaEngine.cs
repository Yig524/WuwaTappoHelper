// UIA/F24 design inspired by rnpfdl/WuwaTappoHelper (MIT).
// See third-party/WuwaTappoHelper-LICENSE.txt. No upstream artwork is used.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace TappoHelper {
public static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct MouseInput {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] struct KeyInput {public ushort Key,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Explicit)] struct InputUnion {[FieldOffset(0)]public MouseInput Mouse;[FieldOffset(0)]public KeyInput Key;}
    [StructLayout(LayoutKind.Sequential)] struct Input {public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Sequential)] internal struct HookKey {public uint Key,Scan,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] internal struct HookMouse {public Point Point;public uint Data,Flags,Time;public UIntPtr Extra;}
    internal delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    delegate bool EnumProc(IntPtr h,IntPtr arg);
    public static readonly UIntPtr InputTag=new UIntPtr(0x57555448);
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] input,int size);
    [DllImport("user32.dll")] static extern bool GetPhysicalCursorPos(out Point point);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [StructLayout(LayoutKind.Sequential)]struct GuiThread {public uint Size,Flags;public IntPtr Active,Focus,Capture,Menu,Move,Caret;public Rect CaretRect;}
    [DllImport("user32.dll")]static extern bool GetGUIThreadInfo(uint thread,ref GuiThread info);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int how);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    [DllImport("kernel32.dll")]static extern bool FreeConsole();
    public static readonly uint RestoreMessage=RegisterWindowMessage("WuwaTappoHelper.ShowMain.v1");
    [DllImport("user32.dll",EntryPoint="PostMessageW",SetLastError=true)]static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr arg);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h,EnumProc callback,IntPtr arg);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int size);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder text,int size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder name,ref uint size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr SetWindowsHookEx(int kind,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    public static void InitDpi(){if(SetThreadDpiAwarenessContext(new IntPtr(-4))==IntPtr.Zero)throw new InvalidOperationException("Cannot establish physical DPI context");}
    public static Point Cursor(){Point p;if(!GetPhysicalCursorPos(out p))throw new InvalidOperationException("Cannot read cursor");return p;}
    public static uint Pid(IntPtr h){uint pid;GetWindowThreadProcessId(h,out pid);return pid;}
    public static string ProcessPath(uint pid){
        IntPtr h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)return null;
        try{StringBuilder b=new StringBuilder(32768);uint size=(uint)b.Capacity;return QueryFullProcessImageName(h,0,b,ref size)?b.ToString():null;}finally{CloseHandle(h);}
    }
    static string Text(IntPtr h,bool name){StringBuilder b=new StringBuilder(512);if(name)GetWindowText(h,b,b.Capacity);else GetClassName(h,b,b.Capacity);return b.ToString();}
    public static IntPtr MainWindow(string expectedPath){
        HashSet<uint> owners=new HashSet<uint>();
        foreach(Process p in Process.GetProcessesByName("WuwaTappo")){try{if(String.Equals(ProcessPath((uint)p.Id),expectedPath,StringComparison.OrdinalIgnoreCase))owners.Add((uint)p.Id);}finally{p.Dispose();}}
        IntPtr result=IntPtr.Zero;long best=0;uint owner=0;bool ambiguous=false;
        EnumWindows(delegate(IntPtr h,IntPtr arg){
            uint pid=Pid(h);if(!owners.Contains(pid)||!IsWindowVisible(h)||Text(h,true)!="Wuwa Tappo"||Text(h,false)!="Tauri Window")return true;
            Rect r;if(!GetWindowRect(h,out r))return true;long area=(long)(r.Right-r.Left)*(r.Bottom-r.Top);
            if(owner!=0&&owner!=pid)ambiguous=true;owner=pid;
            if(area>best){best=area;result=h;}return true;
        },IntPtr.Zero);
        if(ambiguous)throw new InvalidOperationException("Multiple official pet processes; cannot select safely");
        return result;
    }
    public static IntPtr Webview(IntPtr main){
        IntPtr found=IntPtr.Zero;
        EnumChildWindows(main,delegate(IntPtr h,IntPtr arg){if(IsWindowVisible(h)&&Text(h,false)=="Chrome_WidgetWin_1"&&Text(h,true)=="Wuwa Tappo"){found=h;return false;}return true;},IntPtr.Zero);
        return found==IntPtr.Zero?main:found;
    }
    public static void ShowOwnWindow(IntPtr h){if(Pid(h)==(uint)Process.GetCurrentProcess().Id)ShowWindow(h,5);}
    public static bool RequestShowExistingHelper(){
        bool sent=false;uint self=(uint)Process.GetCurrentProcess().Id;
        EnumWindows(delegate(IntPtr h,IntPtr arg){
            if(Pid(h)==self)return true;
            string title=Text(h,true);
            if(title!="Wuwa Tappo · 首次使用"&&(!title.StartsWith("Wuwa Tappo",StringComparison.Ordinal)||!title.EndsWith("F24 + UIA 助手",StringComparison.Ordinal)))return true;
            string image=ProcessPath(Pid(h));
            if(image!=null&&(String.Equals(System.IO.Path.GetFileName(image),"powershell.exe",StringComparison.OrdinalIgnoreCase)||String.Equals(System.IO.Path.GetFileName(image),"WuwaTappoHelper.exe",StringComparison.OrdinalIgnoreCase))){
                if(RestoreMessage!=0&&PostMessage(h,RestoreMessage,IntPtr.Zero,IntPtr.Zero))sent=true;
                ShowWindow(h,9); // Also surface a pre-tray version for a one-time update.
            }
            return true;
        },IntPtr.Zero);
        return sent;
    }
    // Detach only this process. Other processes sharing a console are unaffected.
    public static bool DetachOwnConsole(){return FreeConsole();}
    public static int InputSize(){return Marshal.SizeOf(typeof(Input));}
    public static bool ModifiersDown(){foreach(int key in new int[]{0x10,0x11,0x12,0x5b,0x5c})if((GetAsyncKeyState(key)&0x8000)!=0)return true;return false;}
    internal static bool OtherKeysDown(){for(int key=1;key<255;key++)if(key!=0x87&&(GetAsyncKeyState(key)&0x8000)!=0)return true;return false;}
    internal static ForegroundInfo ReadForeground(bool scanCandidates){
        IntPtr h=GetForegroundWindow();if(h==IntPtr.Zero)throw new InvalidOperationException("No foreground window");
        uint pid;uint thread=GetWindowThreadProcessId(h,out pid);
        GuiThread info=new GuiThread();info.Size=(uint)Marshal.SizeOf(typeof(GuiThread));
        if(!GetGUIThreadInfo(thread,ref info))throw new InvalidOperationException("Cannot inspect foreground focus");
        string cls=Text(h,false),focus=info.Focus==IntPtr.Zero?cls:Text(info.Focus,false);
        bool desktop=(cls=="Progman"||cls=="WorkerW"||cls=="Shell_TrayWnd")&&(focus=="Progman"||focus=="WorkerW"||focus=="Shell_TrayWnd"||focus=="SHELLDLL_DefView"||focus=="SysListView32");
        bool candidate=FocusSemantics.IsCandidateClass(cls)||FocusSemantics.IsCandidateClass(focus);
        if(scanCandidates&&!candidate)EnumWindows(delegate(IntPtr w,IntPtr arg){
            if(IsWindowVisible(w)&&FocusSemantics.IsCandidateClass(Text(w,false))){Rect r;if(GetWindowRect(w,out r)&&r.Right-r.Left>20&&r.Bottom-r.Top>10){candidate=true;return false;}}
            return true;
        },IntPtr.Zero);
        bool caretVisible=info.Caret!=IntPtr.Zero&&(info.Flags&1)!=0;
        return new ForegroundInfo{Window=h,Focus=info.Focus,WindowClass=cls,FocusClass=focus,SafeSurface=desktop||pid==(uint)Process.GetCurrentProcess().Id,Editor=caretVisible||FocusSemantics.IsEditorClass(focus),CaretVisible=caretVisible,Candidate=candidate};
    }
    public static KeySendResult TrySendF24(ActivityMonitor observer,int quietMilliseconds){
        InputPermission p=InputGuard.Assess(observer,quietMilliseconds);
        if(!p.Allowed)return new KeySendResult{State="deferred",Reason=p.Reason};
        return new KeySendResult{State=SendF24Core()?"sent":"failed",Reason=""};
    }
    static bool SendF24Core(){
        Input down=new Input{Type=1,Data=new InputUnion{Key=new KeyInput{Key=0x87,Extra=InputTag}}};
        Input up=new Input{Type=1,Data=new InputUnion{Key=new KeyInput{Key=0x87,Flags=2,Extra=InputTag}}};
        bool pressed=false,released=false;
        try{pressed=SendInput(1,new Input[]{down},InputSize())==1;if(!pressed)return false;Thread.Sleep(8);released=SendInput(1,new Input[]{up},InputSize())==1;return released;}
        finally{if(pressed&&!released)SendInput(1,new Input[]{up},InputSize());}
    }
}

// Separate all-input attribution (probe) from keyboard quiet time (automation).
public sealed class ActivityTracker {
    long count,lastKeyboardTimestamp,lastPointerTimestamp;
    public ActivityTracker(long now){lastKeyboardTimestamp=now;}
    public long Count{get{return Interlocked.Read(ref count);}}
    public long LastKeyboardTimestamp{get{return Interlocked.Read(ref lastKeyboardTimestamp);}}
    public long LastPointerTimestamp{get{return Interlocked.Read(ref lastPointerTimestamp);}}
    public void Record(bool keyboard,long now){if(keyboard)Interlocked.Exchange(ref lastKeyboardTimestamp,now);Interlocked.Increment(ref count);}
    public void PointerFocusChanged(long now){Interlocked.Exchange(ref lastPointerTimestamp,now);}
    public static bool IsPointerFocusMessage(int message){return (message>=0x201&&message<=0x209)||(message>=0x20b&&message<=0x20d);}
}
// Observer only: stores counts/timing, no keystrokes; never swallows events.
public sealed class ActivityMonitor:IDisposable {
    Native.HookProc keyCallback,mouseCallback;IntPtr keyHook,mouseHook;
    readonly ActivityTracker activity=new ActivityTracker(Stopwatch.GetTimestamp());
    ForegroundFocusObserver focusObserver;
    public long Count{get{return activity.Count;}}
    public double KeyboardIdleMilliseconds{get{return (Stopwatch.GetTimestamp()-activity.LastKeyboardTimestamp)*1000.0/Stopwatch.Frequency;}}
    public string ForegroundBlock(){return focusObserver==null?"input_context_unknown":focusObserver.GetBlock(Math.Max(activity.LastPointerTimestamp,activity.LastKeyboardTimestamp));}
    public FocusObservation FocusStatus{get{return focusObserver==null?null:focusObserver.Peek();}}
    public static bool IsOwnKeyboardEvent(bool injected,UIntPtr extra){return injected&&extra==Native.InputTag;}
    public ActivityMonitor(){
        keyCallback=Key;mouseCallback=Mouse;IntPtr module=Native.GetModuleHandle(null);
        keyHook=Native.SetWindowsHookEx(13,keyCallback,module,0);mouseHook=Native.SetWindowsHookEx(14,mouseCallback,module,0);
        if(keyHook==IntPtr.Zero||mouseHook==IntPtr.Zero){Dispose();throw new InvalidOperationException("Input observer unavailable; bounded test cannot exclude natural input");}
        focusObserver=new ForegroundFocusObserver();
    }
    IntPtr Key(int code,IntPtr message,IntPtr data){
        if(code>=0){Native.HookKey k=(Native.HookKey)Marshal.PtrToStructure(data,typeof(Native.HookKey));if(!IsOwnKeyboardEvent((k.Flags&0x10)!=0,k.Extra))activity.Record(true,Stopwatch.GetTimestamp());}
        return Native.CallNextHookEx(keyHook,code,message,data);
    }
    IntPtr Mouse(int code,IntPtr message,IntPtr data){
        if(code>=0){long now=Stopwatch.GetTimestamp();activity.Record(false,now);if(ActivityTracker.IsPointerFocusMessage(message.ToInt32())){activity.PointerFocusChanged(now);if(focusObserver!=null)focusObserver.Refresh();}}
        return Native.CallNextHookEx(mouseHook,code,message,data);
    }
    public void Dispose(){if(keyHook!=IntPtr.Zero){Native.UnhookWindowsHookEx(keyHook);keyHook=IntPtr.Zero;}if(mouseHook!=IntPtr.Zero){Native.UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;}if(focusObserver!=null)focusObserver.Dispose();}
}
public sealed class KeySendResult {public string State,Reason;}
public sealed class InputPermission {public bool Allowed;public string Reason;}
public static class InputGuard {
    public const int AutomaticQuietMilliseconds=1000;
    public static string Decide(bool observerAvailable,double keyboardIdleMilliseconds,int quietMilliseconds,bool held,string contextBlock){
        if(!observerAvailable)return "observer_unavailable";
        if(keyboardIdleMilliseconds<quietMilliseconds)return "user_active";
        if(held)return "keys_held";
        return contextBlock??"input_context_unknown";
    }
    public static InputPermission Assess(ActivityMonitor observer,int quietMilliseconds){
        double idle=observer==null?0:observer.KeyboardIdleMilliseconds;
        string block=Decide(observer!=null,idle,quietMilliseconds,false,"");
        if(block==""){
            try{block=Decide(true,idle,quietMilliseconds,Native.OtherKeysDown(),observer.ForegroundBlock());}
            catch{block="input_context_unknown";}
        }
        return new InputPermission{Allowed=block=="",Reason=block};
    }
}
internal sealed class ForegroundInfo {public IntPtr Window,Focus;public string WindowClass,FocusClass;public bool SafeSurface,Editor,Candidate,CaretVisible;}
public sealed class FocusObservation {
    public long Window,Focus,StartedAt,CompletedAt;public string WindowClass="",FocusClass="",Control="",Framework="",Reason="input_context_unknown";
    public bool CaretVisible;
    public double AgeMilliseconds{get{return (Stopwatch.GetTimestamp()-CompletedAt)*1000.0/Stopwatch.Frequency;}}
}
public static class FocusSemantics {
    public static bool IsCandidateClass(string cls){return Regex.IsMatch(cls??"","candidate|candwnd|candui|mscandui|ifly.*comp",RegexOptions.IgnoreCase);}
    public static bool IsEditorClass(string cls){return Regex.IsMatch(cls??"","(^Edit$|RichEdit|Scintilla|WindowsForms.*EDIT|^_WwG$)",RegexOptions.IgnoreCase);}
    public static bool IsTextEntry(string control,string framework,string cls,bool password,bool writableValue,bool writableDocument){
        if(password||IsEditorClass(cls)||control=="Edit")return true;
        // Explorer items support SetValue for rename even when not editing.
        // Writable Value alone is an editor signal only on text-like roles.
        if(writableValue&&(control=="Custom"||control=="ComboBox"||control=="Document"))return true;
        // Chromium's page document can report a writable text pattern even while
        // browsing. Actual web editors expose Edit/value/caret or an edit ancestor.
        return control=="Document"&&!String.Equals(framework,"Chrome",StringComparison.OrdinalIgnoreCase)&&writableDocument;
    }
    public static string CacheDecision(long window,long focus,long inputStamp,long now,long observedWindow,long observedFocus,long observedAt,string reason,long completedAt=0){
        long freshAt=completedAt==0?observedAt:completedAt;
        if(window!=observedWindow||focus!=observedFocus||observedAt<=inputStamp||(now-freshAt)*1000.0/Stopwatch.Frequency>1500)return "focus_pending";
        return reason??"input_context_unknown";
    }
}
public static class FocusInspector {
    public static FocusObservation Inspect(AutomationElement element,long foregroundWindow){
        FocusObservation result=new FocusObservation{Window=foregroundWindow,StartedAt=Stopwatch.GetTimestamp()};
        bool belongs=false,editable=false;int depth=0;
        while(element!=null&&depth++<24){
            AutomationElement.AutomationElementInformation info=element.Current;
            string control=info.ControlType.ProgrammaticName.Replace("ControlType.",""),framework=info.FrameworkId,cls=info.ClassName;
            if(depth==1){result.Control=control;result.Framework=framework;}
            if(new IntPtr(info.NativeWindowHandle).ToInt64()==foregroundWindow)belongs=true;
            object value;bool writable=false,documentWritable=false;
            if(element.TryGetCurrentPattern(ValuePattern.Pattern,out value))writable=!((ValuePattern)value).Current.IsReadOnly;
            if(control=="Document"&&!String.Equals(framework,"Chrome",StringComparison.OrdinalIgnoreCase)&&element.TryGetCurrentPattern(TextPattern.Pattern,out value)){
                object readOnly=((TextPattern)value).DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute);documentWritable=readOnly is bool&&!(bool)readOnly;
            }
            if(FocusSemantics.IsTextEntry(control,framework,cls,info.IsPassword,writable,documentWritable)){editable=true;break;}
            if(belongs)break;
            element=TreeWalker.ControlViewWalker.GetParent(element);
        }
        result.Reason=editable?"editor_focus":belongs?"":"input_context_unknown";
        return result;
    }
}
// Cross-process UIA stays on a background MTA thread. Only role/state metadata is
// read: never Name, Value, text contents, candidate words, or passwords.
sealed class ForegroundFocusObserver:IDisposable {
    readonly object gate=new object();readonly AutoResetEvent wake=new AutoResetEvent(false);readonly Thread worker;
    volatile bool stopping;FocusObservation latest=new FocusObservation();
    public ForegroundFocusObserver(){worker=new Thread(Loop);worker.IsBackground=true;worker.SetApartmentState(ApartmentState.MTA);worker.Start();}
    public void Refresh(){wake.Set();}
    public FocusObservation Peek(){lock(gate)return latest;}
    public string GetBlock(long pointerStamp){
        ForegroundInfo current=Native.ReadForeground(false);
        if(current.Candidate)return "candidate_window";
        if(current.Editor)return "editor_focus";
        if(current.SafeSurface)return "";
        FocusObservation snapshot=Peek();
        string reason=FocusSemantics.CacheDecision(current.Window.ToInt64(),current.Focus.ToInt64(),pointerStamp,Stopwatch.GetTimestamp(),snapshot.Window,snapshot.Focus,snapshot.StartedAt,snapshot.Reason,snapshot.CompletedAt);
        if(reason=="focus_pending")Refresh();return reason;
    }
    void Loop(){
        while(!stopping){
            FocusObservation observation=new FocusObservation{StartedAt=Stopwatch.GetTimestamp()};
            try{
                ForegroundInfo before=Native.ReadForeground(true);
                observation.Window=before.Window.ToInt64();observation.Focus=before.Focus.ToInt64();observation.WindowClass=before.WindowClass;observation.FocusClass=before.FocusClass;observation.CaretVisible=before.CaretVisible;
                if(before.Candidate)observation.Reason="candidate_window";
                else if(before.Editor)observation.Reason="editor_focus";
                else if(before.SafeSurface)observation.Reason="";
                else{
                    FocusObservation inspected=FocusInspector.Inspect(AutomationElement.FocusedElement,before.Window.ToInt64());
                    observation.Control=inspected.Control;observation.Framework=inspected.Framework;observation.Reason=inspected.Reason;
                    ForegroundInfo after=Native.ReadForeground(false);
                    if(after.Window!=before.Window||after.Focus!=before.Focus)observation.Reason="focus_pending";
                }
            }catch{observation.Reason="input_context_unknown";}
            observation.CompletedAt=Stopwatch.GetTimestamp();lock(gate)latest=observation;
            wake.WaitOne(250);
        }
    }
    public void Dispose(){stopping=true;wake.Set();worker.Join(200);}
}
public sealed class HelperForm:Form {
    [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr h,int id);
    public event Action ToggleRequested,StopRequested,RestoreRequested;
    public bool HotkeysReady{get;private set;}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);bool a=RegisterHotKey(Handle,1,0x4003,0x77),b=RegisterHotKey(Handle,2,0x4003,0x78);HotkeysReady=a&&b;}
    protected override void OnHandleDestroyed(EventArgs e){UnregisterHotKey(Handle,1);UnregisterHotKey(Handle,2);base.OnHandleDestroyed(e);}
    protected override void OnShown(EventArgs e){base.OnShown(e);Native.ShowOwnWindow(Handle);}
    protected override void WndProc(ref Message m){
        if(Native.RestoreMessage!=0&&m.Msg==Native.RestoreMessage){Action restore=RestoreRequested;if(restore!=null)restore();}
        if(m.Msg==0x0312){Action a=m.WParam.ToInt32()==1?ToggleRequested:StopRequested;if(a!=null)a();}base.WndProc(ref m);
    }
}
sealed class TrayColors:ProfessionalColorTable {
    public override System.Drawing.Color ToolStripDropDownBackground{get{return System.Drawing.Color.White;}}
    public override System.Drawing.Color ImageMarginGradientBegin{get{return System.Drawing.Color.White;}}
    public override System.Drawing.Color ImageMarginGradientMiddle{get{return System.Drawing.Color.White;}}
    public override System.Drawing.Color ImageMarginGradientEnd{get{return System.Drawing.Color.White;}}
    public override System.Drawing.Color MenuItemSelected{get{return System.Drawing.Color.FromArgb(231,244,235);}}
    public override System.Drawing.Color MenuItemBorder{get{return System.Drawing.Color.FromArgb(231,244,235);}}
    public override System.Drawing.Color MenuBorder{get{return System.Drawing.Color.FromArgb(225,233,226);}}
}
public sealed class TrayHost:IDisposable {
    readonly Form form;readonly bool publish;readonly NotifyIcon icon;readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem status;readonly System.Drawing.Icon image;
    readonly System.Drawing.Font menuFont;
    bool started,disposed,notified,transitioning;
    public bool IsExiting{get;private set;}
    public event Action ToggleRequested,StopRequested,ExitRequested;
    public bool IsHidden{get{return !form.Visible;}}
    public TrayHost(Form owner):this(owner,true){}
    public TrayHost(Form owner,bool publishToShell){
        form=owner;publish=publishToShell;
        image=(System.Drawing.Icon)(form.Icon??System.Drawing.SystemIcons.Application).Clone();form.Icon=image;
        menuFont=new System.Drawing.Font("Microsoft YaHei UI",9.5f);
        menu=new ContextMenuStrip{ShowImageMargin=false,Font=menuFont,Renderer=new ToolStripProfessionalRenderer(new TrayColors()),ForeColor=System.Drawing.Color.FromArgb(29,48,42)};status=new ToolStripMenuItem("状态：只读观察");status.Enabled=false;menu.Items.Add(status);
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem show=new ToolStripMenuItem("显示窗口");show.Click+=delegate{RestoreWindow();};menu.Items.Add(show);
        ToolStripMenuItem toggle=new ToolStripMenuItem("开始 / 暂停");toggle.Click+=delegate{Action a=ToggleRequested;if(a!=null)a();};menu.Items.Add(toggle);
        ToolStripMenuItem stop=new ToolStripMenuItem("停止全部动作");stop.Click+=delegate{Action a=StopRequested;if(a!=null)a();};menu.Items.Add(stop);
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem exit=new ToolStripMenuItem("退出程序");exit.Click+=delegate{Exit();};menu.Items.Add(exit);
        icon=new NotifyIcon{Icon=image,Text="Wuwa Tappo 助手 · 只读观察",ContextMenuStrip=menu};
        icon.DoubleClick+=delegate{RestoreWindow();};
        form.FormClosing+=Closing;form.FormClosed+=Closed;form.Resize+=Resized;
    }
    public void Start(){if(disposed)return;started=true;icon.Visible=publish;}
    public void SetStatus(string text){
        if(disposed)return;
        string tip="Wuwa Tappo 助手 · "+text;if(tip.Length>63)tip=tip.Substring(0,63);
        if(icon.Text!=tip)icon.Text=tip;
        string label="状态："+text;if(status.Text!=label)status.Text=label;
    }
    public void HideToTray(){
        if(IsExiting||disposed||transitioning)return;if(!started)Start();
        transitioning=true;
        try{form.Hide();form.ShowInTaskbar=false;}finally{transitioning=false;}
        if(publish&&!notified){notified=true;icon.ShowBalloonTip(2500,"Wuwa Tappo 助手","助手仍在后台运行。双击托盘图标恢复，右键可停止或退出。",ToolTipIcon.Info);}
    }
    public void RestoreWindow(){
        if(disposed||IsExiting||transitioning)return;
        transitioning=true;
        try{form.WindowState=FormWindowState.Normal;form.ShowInTaskbar=publish;form.Show();if(publish){form.BringToFront();form.Activate();}}
        finally{transitioning=false;}
    }
    public void Exit(){
        if(disposed||IsExiting)return;IsExiting=true;
        Action a=ExitRequested;if(a!=null)a();form.Close();
    }
    void Closing(object sender,FormClosingEventArgs e){
        if(e.CloseReason==CloseReason.UserClosing&&!IsExiting){e.Cancel=true;HideToTray();return;}
        IsExiting=true;
    }
    void Closed(object sender,FormClosedEventArgs e){IsExiting=true;icon.Visible=false;}
    void Resized(object sender,EventArgs e){if(form.WindowState==FormWindowState.Minimized&&!IsExiting&&!transitioning)HideToTray();}
    public void Dispose(){
        if(disposed)return;disposed=true;
        form.FormClosing-=Closing;form.FormClosed-=Closed;form.Resize-=Resized;
        icon.Visible=false;icon.Dispose();menu.Dispose();image.Dispose();menuFont.Dispose();
    }
}
public sealed class Node {
    public string Classes="",Name="",Ancestors="";public bool Enabled,Offscreen,CanInvoke;public int Index;
}
public sealed class Slot {
    public string Side,State="hidden",Text="";public bool Ready,Present,CanInvoke;public int Index=-1;
}
public sealed class Snapshot {
    public long Sequence,UtcTicks;public string Identity="",Error="";public bool Connected,BalanceValid,Busy,Capped;
    public long Balance;public int NodeCount,UnmappedSlots;public Slot Left=new Slot{Side="left"},Right=new Slot{Side="right"};
    public string[] Diagnostics=new string[0];
    public double AgeSeconds{get{return (DateTime.UtcNow.Ticks-UtcTicks)/(double)TimeSpan.TicksPerSecond;}}
}
public static class Semantics {
    public static bool Token(string classes,string token){return Array.IndexOf((classes??"").Split(new char[]{' ',';','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries),token)>=0;}
    public static bool PermitsInvoke(Snapshot s,string side,string identity){Slot slot=side=="left"?s.Left:side=="right"?s.Right:null;return slot!=null&&s.Connected&&s.Identity==identity&&s.AgeSeconds<=5&&s.BalanceValid&&s.Balance>=1000&&!s.Busy&&slot.Ready&&slot.CanInvoke&&slot.Index>=0;}
    public static Snapshot Evaluate(IEnumerable<Node> rows){
        Snapshot s=new Snapshot();HashSet<long> balances=new HashSet<long>();List<string> debug=new List<string>();bool capped=false;
        foreach(Node n in rows){
            s.NodeCount++;if(n.Offscreen)continue;
            string path=n.Classes+";"+n.Ancestors;
            if((Token(path,"pet-info")||Token(path,"pet-info-container"))&&Regex.IsMatch(n.Name.Trim(),@"^\d[\d,]*\+?$")){
                long value;if(Int64.TryParse(n.Name.Trim().TrimEnd('+').Replace(",",""),out value)){balances.Add(value);capped|=n.Name.Trim().EndsWith("+");if(debug.Count<20)debug.Add("balance: "+n.Classes+" / "+n.Ancestors);}
            }
            else if(Regex.IsMatch(n.Name.Trim(),@"^\d[\d,]*\+?$")&&debug.Count<20)debug.Add("numeric outside balance container: "+n.Classes+" / "+n.Ancestors);
            if((Token(n.Classes,"chest-reward-popup")||Token(n.Classes,"chest-drop-motion"))&&(Token(n.Classes,"active")||Token(n.Classes,"playing")||Token(n.Classes,"shown")))s.Busy=true;
            bool button=Token(n.Classes,"reward-btn"),cooling=Token(n.Classes,"reward-countdown");
            if(!button&&!cooling)continue;
            bool left=Token(path,"reward-anchor--left"),right=Token(path,"reward-anchor--right");
            if(left==right){s.UnmappedSlots++;if(debug.Count<20)debug.Add("unmapped slot: "+n.Classes+" / "+n.Ancestors);continue;}
            Slot slot=left?s.Left:s.Right;
            if(slot.Present){slot.Ready=false;slot.State="ambiguous";slot.Index=-1;continue;}
            slot.Present=true;slot.Text=cooling?n.Name:"";slot.CanInvoke=n.CanInvoke;slot.Index=n.Index;
            slot.Ready=button&&n.Enabled&&n.CanInvoke&&!Token(n.Classes,"reward-btn--loading");
            slot.State=cooling?"cooldown":slot.Ready?"ready":"disabled";
        }
        s.Capped=capped;
        if(balances.Count==1){foreach(long b in balances)s.Balance=b;s.BalanceValid=!capped;}
        if(!s.BalanceValid)s.Error=capped?"Displayed balance is capped; exact spend cannot be verified":"UIA did not expose one exact balance under pet-info";
        s.Diagnostics=debug.ToArray();return s;
    }
}
public sealed class InvokeResult {public long Id,Before;public string Side,Status,Error;}
sealed class InvokeCommand {public long Id,Generation,Expires;public string Side,Identity;public bool RequireQuiet;}
public sealed class Session:IDisposable {
    readonly string expectedPath;readonly object gate=new object();readonly AutoResetEvent wake=new AutoResetEvent(false);
    Thread worker;volatile bool stopping;Snapshot latest=new Snapshot{UtcTicks=DateTime.UtcNow.Ticks};InvokeResult result;
    InvokeCommand pending;long generation,commandId,sequence;AutomationElement[] elements=new AutomationElement[0];
    volatile ActivityMonitor activityObserver;
    public Session(string path){expectedPath=path;}
    public void Start(){worker=new Thread(Loop);worker.IsBackground=true;worker.SetApartmentState(ApartmentState.MTA);worker.Start();}
    public Snapshot Peek(){lock(gate)return latest;}
    public InvokeResult LastResult(){lock(gate)return result;}
    public void Refresh(){wake.Set();}
    public void SetActivityMonitor(ActivityMonitor observer){activityObserver=observer;}
    public void Cancel(){lock(gate){generation++;pending=null;}wake.Set();}
    public long RequestInvoke(string side,string identity){
        return RequestInvoke(side,identity,false);
    }
    public long RequestInvoke(string side,string identity,bool requireQuiet){
        if(side!="left"&&side!="right")throw new ArgumentException("side");
        lock(gate){if(pending!=null)return 0;long id=++commandId;pending=new InvokeCommand{Id=id,Generation=generation,Expires=DateTime.UtcNow.AddSeconds(5).Ticks,Side=side,Identity=identity,RequireQuiet=requireQuiet};wake.Set();return id;}
    }
    string Ancestors(AutomationElement e){
        List<string> path=new List<string>();AutomationElement parent=e;
        for(int depth=0;depth<24;depth++){try{parent=TreeWalker.RawViewWalker.GetParent(parent);if(parent==null)break;string cls=parent.Current.ClassName;if(!String.IsNullOrEmpty(cls))path.Add(cls);if(TokenAnchor(cls))break;}catch{break;}}
        return String.Join(";",path.ToArray());
    }
    static bool TokenAnchor(string cls){return Semantics.Token(cls,"reward-anchor--left")||Semantics.Token(cls,"reward-anchor--right")||Semantics.Token(cls,"pet-info")||Semantics.Token(cls,"pet-info-container");}
    Snapshot Read(){
        IntPtr main=Native.MainWindow(expectedPath);if(main==IntPtr.Zero)throw new InvalidOperationException("Official WuwaTappo window not found");
        IntPtr view=Native.Webview(main);AutomationElement root=AutomationElement.FromHandle(view);
        AutomationElementCollection all=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
        List<Node> nodes=new List<Node>();elements=new AutomationElement[all.Count];
        for(int i=0;i<all.Count;i++){
            AutomationElement e=all[i];elements[i]=e;
            try{
                string cls=e.Current.ClassName??"",name=e.Current.Name??"";
                bool relevant=Regex.IsMatch(name.Trim(),@"^\d[\d,]*\+?$")||cls.Contains("reward-")||cls.Contains("chest-drop-motion");
                if(!relevant)continue;
                object pattern=null;bool button=Semantics.Token(cls,"reward-btn");
                nodes.Add(new Node{Classes=cls,Name=name,Ancestors=(button||Semantics.Token(cls,"reward-countdown")||Regex.IsMatch(name.Trim(),@"^\d[\d,]*\+?$"))?Ancestors(e):"",Enabled=e.Current.IsEnabled,Offscreen=e.Current.IsOffscreen,CanInvoke=button&&e.TryGetCurrentPattern(InvokePattern.Pattern,out pattern),Index=i});
            }catch(ElementNotAvailableException){}catch(InvalidOperationException){}
        }
        Snapshot s=Semantics.Evaluate(nodes);s.Connected=true;s.Identity=Native.Pid(main)+":"+main.ToInt64()+":"+view.ToInt64();s.Sequence=Interlocked.Increment(ref sequence);s.UtcTicks=DateTime.UtcNow.Ticks;
        lock(gate)latest=s;return s;
    }
    void Execute(InvokeCommand command){
        InvokeResult r=new InvokeResult{Id=command.Id,Side=command.Side,Status="rejected",Error=""};
        try{
            Snapshot s=Read();Slot slot=command.Side=="left"?s.Left:s.Right;
            lock(gate){if(stopping||command.Generation!=generation||DateTime.UtcNow.Ticks>command.Expires)throw new InvalidOperationException("Cancelled or expired request");}
            if(!Semantics.PermitsInvoke(s,command.Side,command.Identity))throw new InvalidOperationException("Fresh UIA state does not permit this claim");
            object value;if(!elements[slot.Index].TryGetCurrentPattern(InvokePattern.Pattern,out value))throw new InvalidOperationException("InvokePattern unavailable");
            lock(gate){if(stopping||command.Generation!=generation||DateTime.UtcNow.Ticks>command.Expires)throw new InvalidOperationException("Cancelled or expired request");}
            if(command.RequireQuiet){
                InputPermission permission=InputGuard.Assess(activityObserver,InputGuard.AutomaticQuietMilliseconds);
                if(!permission.Allowed){r.Status="deferred";r.Error=permission.Reason;lock(gate)result=r;return;}
            }
            r.Before=s.Balance;((InvokePattern)value).Invoke();r.Status="invoked";
        }catch(Exception ex){r.Error=ex.Message;}
        lock(gate)result=r;
    }
    void Loop(){
        while(!stopping){
            try{InvokeCommand c;lock(gate){c=pending;pending=null;}if(c!=null)Execute(c);if(!stopping)Read();}
            catch(Exception ex){lock(gate)latest=new Snapshot{Sequence=Interlocked.Increment(ref sequence),UtcTicks=DateTime.UtcNow.Ticks,Error=ex.Message};}
            wake.WaitOne(1000);
        }
    }
    public void Dispose(){stopping=true;Cancel();if(worker!=null)worker.Join(200);}
}
}
