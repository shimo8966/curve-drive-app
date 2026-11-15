using System;using System.IO;using System.Text;using System.Diagnostics;using System.Threading;using System.Runtime.InteropServices;using System.Drawing;using System.Drawing.Imaging;using System.Windows.Forms;using Accessibility;
class JwCheck {
 delegate bool EnumProc(IntPtr h,IntPtr unused);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetMenu(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetSubMenu(IntPtr h,int index);
 [DllImport("user32.dll")]static extern int GetMenuItemCount(IntPtr h);
 [DllImport("user32.dll")]static extern uint GetMenuItemID(IntPtr h,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetMenuString(IntPtr h,uint index,StringBuilder text,int length,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int length);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder text,int length);
 [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr h,int id);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("oleacc.dll")]static extern int AccessibleObjectFromWindow(IntPtr h,uint objectId,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IAccessible a);
 [DllImport("oleacc.dll",EntryPoint="AccessibleObjectFromWindow")]static extern int LegacyObject(IntPtr h,uint objectId,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out CadAccessible a);
 static StreamWriter log;static string output;static int pid;static IntPtr main;
 static void Write(string s){Console.WriteLine(s);log.WriteLine(s);}
 static string Title(IntPtr h){var s=new StringBuilder(1024);GetWindowText(h,s,s.Capacity);return s.ToString();}
 static string Kind(IntPtr h){var s=new StringBuilder(256);GetClassName(h,s,s.Capacity);return s.ToString();}
 static string Error(Exception e){return e.GetType().Name+" HRESULT=0x"+e.HResult.ToString("X8")+" "+e.Message;}
 static void Screenshot(string name){try{var r=Screen.PrimaryScreen.Bounds;using(var bitmap=new Bitmap(r.Width,r.Height)){using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(r.Left,r.Top,0,0,r.Size);bitmap.Save(Path.Combine(output,name+".png"),ImageFormat.Png);}}catch(Exception e){Write("SCREENSHOT "+Error(e));}}
 static IntPtr FindList(){IntPtr found=IntPtr.Zero;EnumWindows(delegate(IntPtr h,IntPtr p){uint owner;GetWindowThreadProcessId(h,out owner);if(owner==(uint)pid&&IsWindowVisible(h)){IntPtr list=GetDlgItem(h,1882);if(list!=IntPtr.Zero&&IsWindowVisible(list)){found=list;Write("DIALOG "+Kind(h)+" title="+Title(h));}}return true;},IntPtr.Zero);return found;}
 static uint Menu(IntPtr h){uint fallback=0;if(h==IntPtr.Zero)return 0;for(int i=0;i<GetMenuItemCount(h);i++){uint id=GetMenuItemID(h,i);var text=new StringBuilder(256);GetMenuString(h,(uint)i,text,256,0x400);string name=text.ToString();if(name.Contains("外部変形"))return id;if(id==32913)fallback=id;uint sub=Menu(GetSubMenu(h,i));if(sub!=0)fallback=sub;}return fallback;}
 static IntPtr Open(){IntPtr list=FindList();if(list!=IntPtr.Zero)return list;uint cmd=Menu(GetMenu(main));Write("EXTERNAL command="+cmd);if(cmd==0)throw new Exception("External menu was not found");PostMessage(main,0x111,new IntPtr(cmd),IntPtr.Zero);for(int i=0;i<70;i++){Thread.Sleep(100);list=FindList();if(list!=IntPtr.Zero)return list;}throw new Exception("File list did not open");}
 static bool WaitFile(string file){for(int i=0;i<40;i++){if(File.Exists(file))return true;Thread.Sleep(100);}return false;}
 static string Dxf(){return "0\r\nSECTION\r\n2\r\nHEADER\r\n9\r\n$ACADVER\r\n1\r\nAC1009\r\n0\r\nENDSEC\r\n0\r\nSECTION\r\n2\r\nENTITIES\r\n0\r\nLINE\r\n8\r\nPLAN\r\n62\r\n1\r\n10\r\n0\r\n20\r\n0\r\n11\r\n50000\r\n21\r\n0\r\n0\r\nCIRCLE\r\n8\r\nACTUAL\r\n62\r\n3\r\n10\r\n10000\r\n20\r\n0\r\n40\r\n35\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n";}
 [STAThread]static int Main(string[] args){
  output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);log=new StreamWriter(Path.Combine(output,"Windows.log"),false,new UTF8Encoding(false));log.AutoFlush=true;
  Process cad=null;
  try{
   Write("OS "+Environment.OSVersion+" interactive="+Environment.UserInteractive+" bits="+(IntPtr.Size*8));
   string folder=Path.GetDirectoryName(args[0]),input=Path.Combine(folder,"Fixture.dxf"),probe=Path.Combine(folder,"ProbeIn.txt");File.WriteAllText(input,Dxf(),Encoding.ASCII);
   File.WriteAllText(Path.Combine(folder,"Probe_Run.bat"),"REM #jww\r\nREM #cd\r\nREM #hf\r\nREM #h4\r\nREM #hp\r\nREM #bz\r\nREM #zs\r\nREM #e\r\n@echo off\r\ncopy /y \"%~dp0JWC_TEMP.TXT\" \"%~dp0ProbeIn.txt\" >nul\r\n> \"%~dp0JWC_TEMP.TXT\" echo h#Fixture read only.\r\nexit /b 0\r\n",Encoding.ASCII);
   cad=Process.Start(new ProcessStartInfo(args[0],"\""+input+"\""){WorkingDirectory=folder,UseShellExecute=false});pid=cad.Id;
   for(int i=0;i<150;i++){EnumWindows(delegate(IntPtr h,IntPtr p){uint owner;GetWindowThreadProcessId(h,out owner);if(owner==(uint)pid&&IsWindowVisible(h)&&GetMenu(h)!=IntPtr.Zero)main=h;return true;},IntPtr.Zero);if(main!=IntPtr.Zero)break;Thread.Sleep(100);}
   if(main==IntPtr.Zero)throw new Exception("CAD main window not found");Write("CAD title="+Title(main));Thread.Sleep(800);Screenshot("drawing");
   bool success=false;
   foreach(string mode in new[]{"standard","legacy","enter"}){
    if(File.Exists(probe))File.Delete(probe);IntPtr list=Open();Thread.Sleep(600);Screenshot("list-"+mode);Guid iid=typeof(IAccessible).GUID;IAccessible a=null;int hr=AccessibleObjectFromWindow(list,unchecked((uint)-4),ref iid,out a);Write("OBJECT hr=0x"+hr.ToString("X8")+" mode="+mode);if(hr!=0||a==null)throw new Exception("No accessible list");
    int match=0;for(int i=1;i<=a.accChildCount;i++){try{string name=a.get_accName(i);Write("ITEM "+i+" name="+name+" role="+a.get_accRole(i)+" state="+a.get_accState(i)+" action="+a.get_accDefaultAction(i));if(name=="Probe_Run.bat"||name=="Probe_Run")match=i;}catch(Exception e){Write("ITEM "+i+" "+Error(e));}}
    if(match==0)throw new Exception("Synthetic probe macro is not in the current file list");
    try{
     if(mode=="standard")a.accDoDefaultAction(match);
     else if(mode=="legacy"){CadAccessible old=null;int oldHr=LegacyObject(list,unchecked((uint)-4),ref iid,out old);Write("LEGACY hr=0x"+oldHr.ToString("X8"));try{old.Invoke(match);}finally{if(old!=null)Marshal.ReleaseComObject(old);}}
     else {a.accSelect(3,match);SetForegroundWindow(main);PostMessage(list,0x100,new IntPtr(13),IntPtr.Zero);PostMessage(list,0x101,new IntPtr(13),IntPtr.Zero);}
     Write("ACTION returned mode="+mode);
    }catch(Exception e){Write("ACTION error mode="+mode+" "+Error(e));}
    finally{Marshal.ReleaseComObject(a);}
    bool ran=WaitFile(probe);Write("RESULT mode="+mode+" macroRan="+ran);success=success||ran;if(ran)File.Copy(probe,Path.Combine(output,"Input-"+mode+".txt"),true);Screenshot("after-"+mode);
   }
   Write("FIXTURE_SUCCESS "+success);return success?0:1;
  }catch(Exception e){Write("FATAL "+e);Screenshot("failure");return 1;}
  finally{if(cad!=null){try{cad.Kill();cad.WaitForExit(5000);}catch{}cad.Dispose();}log.Dispose();}
 }
}
