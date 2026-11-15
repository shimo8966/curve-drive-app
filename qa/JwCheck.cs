using System;using System.IO;using System.Text;using System.Diagnostics;using System.Threading;using System.Runtime.InteropServices;using System.Drawing;using System.Drawing.Imaging;using System.Windows.Forms;using Accessibility;using System.Collections.Generic;using System.Net;using System.Globalization;using System.Web.Script.Serialization;
class JwCheck {
 delegate bool EnumProc(IntPtr h,IntPtr unused);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetMenu(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetSubMenu(IntPtr h,int index);
 [DllImport("user32.dll")]static extern int GetMenuItemCount(IntPtr h);
 [DllImport("user32.dll")]static extern uint GetMenuItemID(IntPtr h,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetMenuString(IntPtr h,uint index,StringBuilder text,int length,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int length);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder text,int length);
 [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr h,int id);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern int GetDlgCtrlID(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,StringBuilder text);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool SetWindowText(IntPtr h,string text);
 [StructLayout(LayoutKind.Sequential)]struct Point{public int x,y;}
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int left,top,right,bottom;}
 [DllImport("user32.dll")]static extern bool ScreenToClient(IntPtr h,ref Point point);
 [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
 [DllImport("user32.dll")]static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr h,uint kind);
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
 static readonly JavaScriptSerializer json=new JavaScriptSerializer();
 static string Num(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
 static string Call(string path,object body){
  var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:18718/"+path);request.Proxy=null;request.Timeout=45000;request.ContentType="text/plain;charset=utf-8";request.Headers["Origin"]="http://127.0.0.1:18718";
  if(body!=null){request.Method="POST";byte[] bytes=Encoding.UTF8.GetBytes(json.Serialize(body));request.ContentLength=bytes.Length;using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);}
  using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))return reader.ReadToEnd();
 }
 static void Activate(string filename,Func<bool> done,int attempt=0){
  ShowWindow(main,9);IntPtr list=Open();IntPtr dialog=GetAncestor(list,2);ShowWindow(dialog,9);Application.DoEvents();Thread.Sleep(300);Write("ACTIVATE "+filename+" mainIconic="+IsIconic(main)+" dialogIconic="+IsIconic(dialog)+" parentKind="+Kind(GetParent(list))+" rootKind="+Kind(dialog));Guid iid=typeof(IAccessible).GUID;IAccessible a=null;
  try{int hr=AccessibleObjectFromWindow(list,unchecked((uint)-4),ref iid,out a);if(hr!=0)throw new Exception("Cannot read macro list");int match=0,hits=0;for(int i=1;i<=a.accChildCount;i++){string name;try{name=a.get_accName(i);}catch(ArgumentException){continue;}if(String.Equals(name,filename,StringComparison.OrdinalIgnoreCase)){match=i;hits++;}}
   if(hits!=1)throw new Exception("Fixture macro match is not unique: "+filename);
   int state=(int)a.get_accState(match);Write("ACTIVATE itemState="+state);
   if((state&0x18000)!=0){
    if(attempt!=0)throw new Exception("Fixture file list is still not visible");Marshal.ReleaseComObject(a);a=null;PostMessage(dialog,0x111,new IntPtr(2),IntPtr.Zero);
    for(int i=0;i<40&&FindList()!=IntPtr.Zero;i++)Thread.Sleep(50);Activate(filename,done,attempt+1);return;
   }
   a.accSelect(2,match);Write("ACTIVATE selectedState="+a.get_accState(match));
   if(((int)a.get_accState(match)&2)==0)throw new Exception("Selection not confirmed");
   int left,top,width,height;a.accLocation(out left,out top,out width,out height,match);var point=new Point{x=left+Math.Min(20,width/2),y=top+height/2};Write("ACTIVATE bounds="+left+","+top+","+width+","+height);
   object hit=a.accHitTest(point.x,point.y);if(!(hit is int)||(int)hit!=match)throw new Exception("Hit-test disagrees with exact filename");Rect rect;
   if(!ScreenToClient(list,ref point)||!GetClientRect(list,out rect)||point.x<0||point.y<0||point.x>=rect.right||point.y>=rect.bottom)throw new Exception("Item is outside the list client area");
   IntPtr position=new IntPtr((point.y<<16)|point.x);if(!PostMessage(list,0x203,new IntPtr(1),position))throw new Exception("Cannot post control click");PostMessage(list,0x202,IntPtr.Zero,position);
  }finally{if(a!=null)Marshal.ReleaseComObject(a);}
  for(int i=0;i<100;i++){if(done()){Thread.Sleep(250);for(int n=0;n<60&&!IsWindowEnabled(main);n++)Thread.Sleep(50);return;}Thread.Sleep(80);}throw new Exception("Fixture macro did not finish: "+filename);
 }
 static CadContext ReadDrawing(string probe){if(File.Exists(probe))File.Delete(probe);Activate("Probe_Run.bat",delegate(){return File.Exists(probe);});return CadContext.Parse(File.ReadAllText(probe,Encoding.GetEncoding(932)));}
 static void CheckDrawing(CadContext c,CadLink link,Pt point,int expectedCircles){
  c.Validate(link,point);if(c.circles.Count!=expectedCircles)throw new Exception("Unexpected circle count: "+c.circles.Count);
  Pt end=CadContext.Paper(link,new Pt{x=50,y=0});int plans=0;
  // The application-generated fixture has a 0.5m segment pitch: retain all 100 plan segments.
  for(int k=0;k<100;k++){Pt p=CadContext.Paper(link,new Pt{x=k*.5,y=0}),q=CadContext.Paper(link,new Pt{x=(k+1)*.5,y=0});bool found=false;
   for(int i=0;i+1<c.ends.Count;i+=2)if(Math.Abs(c.ends[i].x-p.x)<.05/link.scale&&Math.Abs(c.ends[i].y-p.y)<.05/link.scale&&Math.Abs(c.ends[i+1].x-q.x)<.05/link.scale&&Math.Abs(c.ends[i+1].y-q.y)<.05/link.scale)found=true;
   if(!found)throw new Exception("A planned segment is missing: "+k);plans++;
  }
  Write("NATIVE_GEOMETRY_OK planSegments="+plans+" circles="+c.circles.Count+" scale="+link.scale+" expectedPointM="+Num(point.x)+","+Num(point.y)+" planEndPaper="+Num(end.x)+","+Num(end.y));
 }
 static void NativeApplication(string folder,string probe){
  Process bridge=Process.Start(new ProcessStartInfo(Path.Combine(folder,"Bridge.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder});
  try{
   for(int i=0;i<60;i++){try{Call("health",null);break;}catch(WebException){Thread.Sleep(100);}}
   var baseline=json.Deserialize<Dictionary<string,object>>(Call("baseline",new{site="CI fixture",planKey="straight-50m",station=10,p=new{x=10,y=0}}));string session=(string)baseline["session"];
   Call("auto/prepare",new{session=session,end=new{x=50,y=0}});CadLink link=json.Deserialize<CadLink>(File.ReadAllText(Path.Combine(folder,"Auto.json")));
   CadContext input=ReadDrawing(probe);Pt origin=input.ends[0];string tag=Path.Combine(folder,"SetupDone.tag");if(File.Exists(tag))File.Delete(tag);
   string text="REM Fixture setup - known picked point from real CAD geometry\r\nREM #jww\r\nREM #cd\r\nREM #hf\r\nREM #h4\r\nREM #hp\r\nREM #bz\r\nREM #zs\r\nREM #e\r\n@echo off\r\n>> \"%~dp0JWC_TEMP.TXT\" echo hp1 "+Num(origin.x)+" "+Num(origin.y)+"\r\n\"%~dp0Bridge.exe\" --setup \"%~dp0JWC_TEMP.TXT\" "+link.token+" >> \"%~dp0Setup.log\" 2>&1\r\ntype nul > \"%~dp0SetupDone.tag\"\r\nexit /b 0\r\n";
   File.WriteAllText(Path.Combine(folder,"Setup_CI.bat"),text,Encoding.ASCII);Activate("Setup_CI.bat",delegate(){return File.Exists(tag);});
   link=json.Deserialize<CadLink>(File.ReadAllText(Path.Combine(folder,"Auto.json")));if(!link.calibrated)throw new Exception("Native calibration failed: "+File.ReadAllText(Path.Combine(folder,"JWC_TEMP.TXT")));
   Write("NATIVE_CALIBRATED pid="+link.pid+" actualCAD="+pid+" scale="+link.scale);if(link.pid!=pid)throw new Exception("Calibration bound another process");
   Call("auto/enable",new{});
   foreach(double station in new[]{10.5,11}){
    double offset=station==10.5?.02:-.02;State previous=json.Deserialize<State>(File.ReadAllText(Path.Combine(folder,"State.json")));
    Call("point",new{site="CI fixture",planKey="straight-50m",session=session,station=station,previousStation=previous.station,p=new{x=station,y=offset},q=previous.p,offsetMm=offset*1000});
    if(station==11){IntPtr pendingList=Open();ShowWindow(GetParent(pendingList),6);Write("NATIVE_PREOPEN_MINIMIZED_PICKER");}
    string result=Call("auto/apply",new{session=session,station=station});Write("NATIVE_AUTO_APPLY "+result);Thread.Sleep(350);
    CheckDrawing(ReadDrawing(probe),link,new Pt{x=station,y=offset},station==10.5?2:3);
   }
   bool refused=false;try{Call("auto/apply",new{session=session,station=11});}catch(WebException){refused=true;}
   if(!refused)throw new Exception("Duplicate apply was not refused");CheckDrawing(ReadDrawing(probe),link,new Pt{x=11,y=-.02},3);Screenshot("app-after-points");
   Write("NATIVE_APPLICATION_OK two automatic point updates, 20mm left/right, all planned segments retained, duplicate refused");
  }finally{if(!bridge.HasExited)bridge.Kill();bridge.WaitForExit(5000);bridge.Dispose();foreach(string file in new[]{"Import.log","State.json","Auto.json","Setup.log"}){string path=Path.Combine(folder,file);if(File.Exists(path))File.Copy(path,Path.Combine(output,"Fixture-"+file),true);}}
 }
 static bool SaveJww(string folder){
  IntPtr fileMenu=GetSubMenu(GetMenu(main),0);for(int m=0;m<GetMenuItemCount(fileMenu);m++){var label=new StringBuilder(256);GetMenuString(fileMenu,(uint)m,label,256,0x400);Write("FILE MENU id="+GetMenuItemID(fileMenu,m)+" text="+label);}
  string file=Path.Combine(folder,"Work.jww");IntPtr oldList=FindList();if(oldList!=IntPtr.Zero){PostMessage(GetAncestor(oldList,2),0x111,new IntPtr(2),IntPtr.Zero);Thread.Sleep(300);}ShowWindow(main,9);SetForegroundWindow(main);Thread.Sleep(250);PostMessage(main,0x111,new IntPtr(57604),IntPtr.Zero);Thread.Sleep(1500);Screenshot("save-dialog");IntPtr dialog=IntPtr.Zero,edit=IntPtr.Zero;
  EnumWindows(delegate(IntPtr h,IntPtr unused){uint owner;GetWindowThreadProcessId(h,out owner);if(owner==(uint)pid&&IsWindowVisible(h)&&Kind(h)=="#32770")dialog=h;return true;},IntPtr.Zero);
  if(dialog==IntPtr.Zero){Write("SAVE no dialog");return false;}
  EnumChildWindows(dialog,delegate(IntPtr h,IntPtr unused){if(!IsWindowVisible(h))return true;var text=new StringBuilder(2048);SendMessage(h,13,new IntPtr(text.Capacity),text);Write("SAVE CONTROL id="+GetDlgCtrlID(h)+" class="+Kind(h)+" text="+text.ToString());if(Kind(h)=="Edit"&&(GetDlgCtrlID(h)==1152||text.ToString().IndexOf("Fixture",StringComparison.OrdinalIgnoreCase)>=0))edit=h;return true;},IntPtr.Zero);
  if(edit==IntPtr.Zero){
   IntPtr create=GetDlgItem(dialog,2408);
   if(create!=IntPtr.Zero&&IsWindowVisible(create)){
    PostMessage(create,0xF5,IntPtr.Zero,IntPtr.Zero);Thread.Sleep(450);Screenshot("save-new-name");
    EnumWindows(delegate(IntPtr h,IntPtr unused){uint owner;GetWindowThreadProcessId(h,out owner);if(owner!=(uint)pid||!IsWindowVisible(h)||Kind(h)!="#32770")return true;
     EnumChildWindows(h,delegate(IntPtr c,IntPtr nothing){if(IsWindowVisible(c)){var text=new StringBuilder(1024);SendMessage(c,13,new IntPtr(text.Capacity),text);Write("NEW CONTROL id="+GetDlgCtrlID(c)+" class="+Kind(c)+" text="+text);if(Kind(c)=="Edit"&&GetDlgCtrlID(c)==1491){edit=c;dialog=h;}}return true;},IntPtr.Zero);return true;
    },IntPtr.Zero);
   }
  }
  if(edit==IntPtr.Zero){Write("SAVE no filename edit");return false;}
  SetWindowText(edit,file);PostMessage(dialog,0x111,new IntPtr(1),IntPtr.Zero);bool saved=WaitFile(file);Write("SAVE result="+saved+" title="+Title(main));Screenshot("saved");return saved;
 }
 static string Dxf(){return "0\r\nSECTION\r\n2\r\nHEADER\r\n9\r\n$ACADVER\r\n1\r\nAC1009\r\n0\r\nENDSEC\r\n0\r\nSECTION\r\n2\r\nENTITIES\r\n0\r\nLINE\r\n8\r\nPLAN\r\n62\r\n1\r\n10\r\n0\r\n20\r\n0\r\n11\r\n50000\r\n21\r\n0\r\n0\r\nCIRCLE\r\n8\r\nACTUAL\r\n62\r\n3\r\n10\r\n10000\r\n20\r\n0\r\n40\r\n35\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n";}
 [STAThread]static int Main(string[] args){
  output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);log=new StreamWriter(Path.Combine(output,"Windows.log"),false,new UTF8Encoding(false));log.AutoFlush=true;
  Process cad=null;
  try{
   Write("OS "+Environment.OSVersion+" interactive="+Environment.UserInteractive+" bits="+(IntPtr.Size*8));
   string folder=Path.GetDirectoryName(args[0]),input=Path.Combine(folder,"Fixture.dxf"),probe=Path.Combine(folder,"ProbeIn.txt");if(!File.Exists(input))File.WriteAllText(input,Dxf(),Encoding.ASCII);
   File.WriteAllText(Path.Combine(folder,"Probe_Run.bat"),"REM Read-only fixture probe\r\nREM #jww\r\nREM #cd\r\nREM #hf\r\nREM #h4\r\nREM #hp\r\nREM #bz\r\nREM #zs\r\nREM #e\r\n@echo off\r\ncopy /y \"%~dp0JWC_TEMP.TXT\" \"%~dp0ProbeIn.txt\" >nul\r\n> \"%~dp0JWC_TEMP.TXT\" echo h#Fixture read only.\r\nexit /b 0\r\n",Encoding.ASCII);
   cad=Process.Start(new ProcessStartInfo(args[0],"\""+input+"\""){WorkingDirectory=folder,UseShellExecute=false});pid=cad.Id;
   for(int i=0;i<150;i++){EnumWindows(delegate(IntPtr h,IntPtr p){uint owner;GetWindowThreadProcessId(h,out owner);if(owner==(uint)pid&&IsWindowVisible(h)&&GetMenu(h)!=IntPtr.Zero)main=h;return true;},IntPtr.Zero);if(main!=IntPtr.Zero)break;Thread.Sleep(100);}
   if(main==IntPtr.Zero)throw new Exception("CAD main window not found");Thread.Sleep(1800);Write("CAD title="+Title(main));Thread.Sleep(200);Screenshot("drawing");
   bool success=false;
   foreach(string mode in new[]{"standard","legacy","minimized"}){
    if(File.Exists(probe))File.Delete(probe);IntPtr list=Open();Thread.Sleep(600);Screenshot("list-"+mode);if(mode=="minimized"){ShowWindow(GetParent(list),6);Thread.Sleep(200);}Guid iid=typeof(IAccessible).GUID;IAccessible a=null;int hr=AccessibleObjectFromWindow(list,unchecked((uint)-4),ref iid,out a);Write("OBJECT hr=0x"+hr.ToString("X8")+" mode="+mode);if(hr!=0||a==null)throw new Exception("No accessible list");
    int match=0;for(int i=1;i<=a.accChildCount;i++){try{string name=a.get_accName(i);Write("ITEM "+i+" name="+name+" role="+a.get_accRole(i)+" state="+a.get_accState(i)+" action="+a.get_accDefaultAction(i));if(name=="Probe_Run.bat"||name=="Probe_Run")match=i;}catch(Exception e){Write("ITEM "+i+" "+Error(e));}}
    if(match==0)throw new Exception("Synthetic probe macro is not in the current file list");
    try{
     if(mode=="standard"||mode=="minimized")a.accDoDefaultAction(match);
     else if(mode=="legacy"){CadAccessible old=null;int oldHr=LegacyObject(list,unchecked((uint)-4),ref iid,out old);Write("LEGACY hr=0x"+oldHr.ToString("X8"));try{old.Invoke(match);}finally{if(old!=null)Marshal.ReleaseComObject(old);}}
     else {a.accSelect(3,match);SetForegroundWindow(main);PostMessage(list,0x100,new IntPtr(13),IntPtr.Zero);PostMessage(list,0x101,new IntPtr(13),IntPtr.Zero);}
     Write("ACTION returned mode="+mode);
    }catch(Exception e){Write("ACTION error mode="+mode+" "+Error(e));}
    finally{Marshal.ReleaseComObject(a);}
    bool ran=WaitFile(probe);Write("RESULT mode="+mode+" macroRan="+ran);success=success||ran;if(ran)File.Copy(probe,Path.Combine(output,"Input-"+mode+".txt"),true);Screenshot("after-"+mode);if(mode=="minimized"){ShowWindow(GetParent(list),9);Thread.Sleep(200);}
   }
   bool saved=SaveJww(folder);Write("SAVE_JWW "+saved);if(!saved)throw new Exception("Saving the synthetic JWW fixture failed");NativeApplication(folder,probe);Write("FIXTURE_SUCCESS "+success);return success?0:1;
  }catch(Exception e){Write("FATAL "+e);Screenshot("failure");return 1;}
  finally{if(cad!=null){try{cad.Kill();cad.WaitForExit(5000);}catch{}cad.Dispose();}log.Dispose();}
 }
}
