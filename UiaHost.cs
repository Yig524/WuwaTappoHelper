// GUI-subsystem host for the local PowerShell/WinForms helper. No console process.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

[assembly:AssemblyTitle("Wuwa Tappo Helper")]
[assembly:AssemblyProduct("Wuwa Tappo Helper")]
[assembly:AssemblyDescription("Desktop pet companion helper")]
[assembly:AssemblyVersion("1.2.0.0")]

internal static class UiaHost {
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    static readonly string Root=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    static void Log(string text){
        Directory.CreateDirectory(Path.Combine(Root,"diagnostics"));
        File.AppendAllText(Path.Combine(Root,"diagnostics","uia-host.log"),DateTimeOffset.Now.ToString("o")+" pid="+Process.GetCurrentProcess().Id+" "+text+Environment.NewLine,new UTF8Encoding(false));
    }
    [STAThread] static int Main(string[] args){
        bool selfTest=args.Length==1&&args[0]=="--self-test";
        bool forceSetup=Array.IndexOf(args,"--setup")>=0;
        try {
            bool elevated;using(WindowsIdentity identity=WindowsIdentity.GetCurrent())elevated=new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            Log("gui-host-v2 elevated="+elevated+" console="+GetConsoleWindow().ToInt64()+" selfTest="+selfTest);
            if(!selfTest&&!elevated){
                Process.Start(new ProcessStartInfo{FileName=Assembly.GetExecutingAssembly().Location,WorkingDirectory=Root,UseShellExecute=true,Verb="runas",Arguments=forceSetup?"--setup":""});
                return 0;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Directory.SetCurrentDirectory(Root);
            string systemModules=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","Modules");
            Environment.SetEnvironmentVariable("PSModulePath",systemModules+Path.PathSeparator+Environment.GetEnvironmentVariable("PSModulePath"));
            InitialSessionState initial=InitialSessionState.CreateDefault();
            initial.ExecutionPolicy=Microsoft.PowerShell.ExecutionPolicy.Bypass;
            initial.ImportPSModule(new string[]{Path.Combine(systemModules,"Microsoft.PowerShell.Utility","Microsoft.PowerShell.Utility.psd1")});
            using(Runspace runspace=RunspaceFactory.CreateRunspace(initial)){
                runspace.ApartmentState=System.Threading.ApartmentState.STA;
                runspace.ThreadOptions=PSThreadOptions.UseCurrentThread;
                Log("opening-runspace");runspace.Open();Runspace.DefaultRunspace=runspace;Log("runspace-ready");
                runspace.SessionStateProxy.SetVariable("ForceSetup",forceSetup);
                using(PowerShell shell=PowerShell.Create()){
                    shell.Runspace=runspace;
                    string entry=Path.Combine(Root,selfTest?"tests\\Test-GuiHost.ps1":"Run-UiaHelper.ps1");
                    Log("script-begin "+Path.GetFileName(entry));shell.AddCommand(entry);shell.Invoke();Log("script-return");
                    if(shell.HadErrors){foreach(ErrorRecord error in shell.Streams.Error)Log("script-error "+error);return 1;}
                }
            }
            Log("clean-exit");return 0;
        }catch(Win32Exception error){if(error.NativeErrorCode==1223)return 0;return Fail(error,selfTest);}
        catch(Exception error){return Fail(error,selfTest);}
    }
    static int Fail(Exception error,bool selfTest){
        try{Log(error.ToString());}catch{}
        if(!selfTest)MessageBox.Show(error.Message,"Wuwa Tappo helper startup error",MessageBoxButtons.OK,MessageBoxIcon.Error);
        return 1;
    }
}
