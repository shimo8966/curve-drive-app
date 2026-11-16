using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using WinProcess=System.Diagnostics.Process;

// Full standard IAccessible vtable order (oleacc.idl). Dual calls avoid late-bound property invocation.
[ComImport,Guid("618736E0-3C3D-11CF-810C-00AA00389B71"),InterfaceType(ComInterfaceType.InterfaceIsDual)]
interface CadAccessible {
 [return:MarshalAs(UnmanagedType.IDispatch)] object Parent();
 int Count();
 [return:MarshalAs(UnmanagedType.IDispatch)] object Child([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Name([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Value([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Description([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object Role([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object State([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Help([MarshalAs(UnmanagedType.Struct)]object child);
 int HelpTopic([MarshalAs(UnmanagedType.BStr)]out string file,[MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Shortcut([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object Focus();
 [return:MarshalAs(UnmanagedType.Struct)] object Selection();
 [return:MarshalAs(UnmanagedType.BStr)] string DefaultAction([MarshalAs(UnmanagedType.Struct)]object child);
 void Select(int flags,[MarshalAs(UnmanagedType.Struct)]object child);
 void Location(out int left,out int top,out int width,out int height,[MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object Navigate(int direction,[MarshalAs(UnmanagedType.Struct)]object start);
 [return:MarshalAs(UnmanagedType.Struct)] object HitTest(int x,int y);
 void Invoke([MarshalAs(UnmanagedType.Struct)]object child);
 void SetName([MarshalAs(UnmanagedType.Struct)]object child,[MarshalAs(UnmanagedType.BStr)]string name);
 void SetValue([MarshalAs(UnmanagedType.Struct)]object child,[MarshalAs(UnmanagedType.BStr)]string value);
}

partial class CadNative {
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
 public static bool ManagedPath(CadLink a){
  Guid session;if(a==null||!a.managed||String.IsNullOrEmpty(a.drawing)||!Guid.TryParseExact(a.session,"N",out session))return false;
  string expected=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Work",a.session,"Plan_"+a.session.Substring(0,12)+".dxf");return String.Equals(Path.GetFullPath(a.drawing),Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase);
 }
 static bool CadExecutable(string path){return !String.IsNullOrEmpty(path)&&String.Equals(Path.GetFileName(path),"Jw_win.exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(path);}
 static string CadExe(){
  string cache=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CadPath.txt"),found=null;
  foreach(var process in WinProcess.GetProcessesByName("Jw_win"))using(process){try{string path=process.MainModule.FileName;if(CadExecutable(path)){if(found!=null&&!String.Equals(path,found,StringComparison.OrdinalIgnoreCase))throw new Fault(409,"MANAGED_JWCAD_VERSION_AMBIGUOUS");found=path;}}catch(Fault){throw;}catch{}}
  if(found==null&&File.Exists(cache)){string path=File.ReadAllText(cache).Trim();if(CadExecutable(path))found=path;}
  if(found==null)foreach(string path in new[]{@"C:\JWW\Jw_win.exe",@"C:\Jw_cad\Jw_win.exe",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Jw_cad","Jw_win.exe")})if(CadExecutable(path)){found=path;break;}
  if(found==null)try{using(var extension=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(".jww")){string type=extension==null?null:extension.GetValue(null) as string;if(type!=null)using(var command=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(type+@"\shell\open\command")){string text=command==null?null:command.GetValue(null) as string;if(!String.IsNullOrWhiteSpace(text)){text=text.Trim();string path=text.StartsWith("\"",StringComparison.Ordinal)?text.Split('"')[1]:text.Split(' ')[0];if(CadExecutable(path))found=path;}}}}catch{}
  if(found==null)throw new Fault(409,"MANAGED_JWCAD_NOT_FOUND");File.WriteAllText(cache,found,new UTF8Encoding(false));return found;
 }
 public static CadWindow StartManaged(string drawing){
  var start=new System.Diagnostics.ProcessStartInfo(CadExe(),"\""+drawing+"\""){UseShellExecute=false,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};
  using(var process=WinProcess.Start(start)){
   long started=process.StartTime.ToUniversalTime().Ticks;DateTime until=DateTime.UtcNow.AddSeconds(20);
   while(DateTime.UtcNow<until){if(process.HasExited)throw new Fault(409,"MANAGED_JWCAD_LAUNCH_FAILED");try{var window=Window(process.Id,drawing,false);if(window.started==started)return window;}catch(Fault e){if(e.Message!="AUTO_CAD_WINDOW_NOT_FOUND"&&e.Message!="AUTO_MENU_NOT_AVAILABLE")throw;}Thread.Sleep(100);}
  }throw new Fault(409,"MANAGED_JWCAD_LAUNCH_FAILED");
 }
 static void ManagedFolder(IntPtr list,Action<string> trace){
  IntPtr dialog=GetAncestor(list,2),tree=GetDlgItem(dialog,1775);var kind=new StringBuilder(100);GetClassName(tree,kind,kind.Capacity);
  string folder=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar),drive=Path.GetPathRoot(folder).TrimEnd(Path.DirectorySeparatorChar);
  if(tree==IntPtr.Zero||kind.ToString()!="SysTreeView32"||!IsWindowVisible(tree))throw new Fault(409,"MANAGED_FOLDER_CONTROL_NOT_FOUND");
  Guid iid=typeof(CadAccessible).GUID;CadAccessible accessible=null;
  try{
   int hr=AccessibleObjectFromWindow(tree,unchecked((uint)-4),ref iid,out accessible);if(hr!=0||accessible==null)throw new Fault(409,"MANAGED_FOLDER_CONTROL_NOT_FOUND");
   trace("MANAGED_TREE_OBJECT children="+accessible.Count());
   for(int n=1;n<=Math.Min(30,accessible.Count());n++)try{trace("MANAGED_TREE_CHILD "+n+" "+accessible.Name(n));}catch{}
   IntPtr root=SendMessage(tree,0x110A,IntPtr.Zero,IntPtr.Zero),item=TreeDrive(tree,accessible,root,drive,0);
   trace("MANAGED_TREE_DRIVE root="+root+" rootName="+TreeName(tree,accessible,root)+" found="+item);
   if(item==IntPtr.Zero)throw new Fault(409,"MANAGED_FOLDER_CHANGE_FAILED");
   string tail=folder.Substring(Path.GetPathRoot(folder).Length);foreach(string part in tail.Split(new char[]{Path.DirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries)){
    SendMessage(tree,0x1102,new IntPtr(2),item);IntPtr child=IntPtr.Zero;
    for(int attempt=0;attempt<20;attempt++){child=TreeSibling(tree,accessible,SendMessage(tree,0x110A,new IntPtr(4),item),part);if(child!=IntPtr.Zero)break;Thread.Sleep(50);}
    trace("MANAGED_TREE_PART "+part+" found="+child);
    if(child==IntPtr.Zero)throw new Fault(409,"MANAGED_FOLDER_CHANGE_FAILED");item=child;
   }
   trace("MANAGED_FOLDER_SELECT");SendMessage(tree,0x110B,new IntPtr(9),item);
   if(SendMessage(tree,0x110A,new IntPtr(9),IntPtr.Zero)!=item)throw new Fault(409,"MANAGED_FOLDER_CHANGE_FAILED");Thread.Sleep(200);
  }finally{if(accessible!=null&&Marshal.IsComObject(accessible))Marshal.ReleaseComObject(accessible);}
 }
 static string TreeName(IntPtr tree,CadAccessible accessible,IntPtr item){
  IntPtr id=SendMessage(tree,0x112B,item,IntPtr.Zero);
  // Older common-control tree providers use the HTREEITEM as their child ID.
  if(id!=IntPtr.Zero)try{string text=accessible.Name(id.ToInt32());if(!String.IsNullOrEmpty(text))return text;}catch{}
  try{return accessible.Name(item.ToInt32())??"";}catch{return "";}
 }
 static IntPtr TreeSibling(IntPtr tree,CadAccessible accessible,IntPtr first,string name){
  IntPtr found=IntPtr.Zero;int matches=0,count=0;
  for(IntPtr item=first;item!=IntPtr.Zero&&count++<5000;item=SendMessage(tree,0x110A,new IntPtr(1),item))if(String.Equals(TreeName(tree,accessible,item).TrimEnd(Path.DirectorySeparatorChar),name,StringComparison.OrdinalIgnoreCase)){found=item;matches++;}
  return matches==1?found:IntPtr.Zero;
 }
 static IntPtr TreeDrive(IntPtr tree,CadAccessible accessible,IntPtr first,string drive,int depth){
  IntPtr found=TreeSibling(tree,accessible,first,drive);if(found!=IntPtr.Zero||depth>=2)return found;int count=0;
  for(IntPtr item=first;item!=IntPtr.Zero&&count++<100;item=SendMessage(tree,0x110A,new IntPtr(1),item)){
   SendMessage(tree,0x1102,new IntPtr(2),item);found=TreeDrive(tree,accessible,SendMessage(tree,0x110A,new IntPtr(4),item),drive,depth+1);if(found!=IntPtr.Zero)return found;
  }return IntPtr.Zero;
 }
}
class CadWindow {public IntPtr handle;public int pid;public long started;}
class CadProcessInfo {public int pid,parent;public string name;}
class CadWindowInfo {
 public IntPtr handle;public int pid;public bool owned,visible,enabled;public string title,kind;
}
// Pure identity rules, also exercised without a Windows desktop.
class CadIdentity {
 public static int Launcher(Dictionary<int,CadProcessInfo> rows,int current){
  var seen=new HashSet<int>();
  for(int depth=0;depth<32;depth++){
   CadProcessInfo row;
   if(current<=0||!seen.Add(current)||!rows.TryGetValue(current,out row))break;
   if(depth>0&&String.Equals(Path.GetFileNameWithoutExtension(row.name),"Jw_win",StringComparison.OrdinalIgnoreCase))return current;
   current=row.parent;
  }
  throw new Fault(409,"AUTO_CAD_LAUNCHER_NOT_FOUND");
 }
 public static void RequireLauncher(Dictionary<int,CadProcessInfo> rows,int current,int expected){
  if(Launcher(rows,current)!=expected)throw new Fault(409,"AUTO_CAD_LAUNCHER_CHANGED");
 }
 public static CadWindowInfo SelectWindow(List<CadWindowInfo> windows,int pid,string drawing,bool busy){
  string expected=Path.GetFileName(drawing)+" - jw_win";
  // Native JWW files use the stem in Jw_cad's title; DXF files retain their extension.
  string native=String.Equals(Path.GetExtension(drawing),".jww",StringComparison.OrdinalIgnoreCase)?Path.GetFileNameWithoutExtension(drawing)+" - jw_win":expected;
  CadWindowInfo found=null;int count=0;
  foreach(var w in windows){
   if(w.pid!=pid||w.owned||w.kind==null||!w.kind.StartsWith("Afx:",StringComparison.Ordinal)||(!String.Equals(w.title,expected,StringComparison.OrdinalIgnoreCase)&&!String.Equals(w.title,native,StringComparison.OrdinalIgnoreCase)))continue;
   if(!busy&&!w.visible)continue;
   found=w;count++;
  }
  if(count!=1)throw new Fault(409,count==0?"AUTO_CAD_WINDOW_NOT_FOUND":"AUTO_CAD_WINDOW_AMBIGUOUS");
  if(!busy&&!found.enabled)throw new Fault(409,"CAD_DIALOG_OPEN");
  return found;
 }
}
interface CadListReader {
 int Count();string Name(int child);int Role(int child);int State(int child);string DefaultAction(int child);void Invoke(int child);
}
class CadListChoice {
 public static string FailureDetail(string step,Exception e){return "AUTO_UI_ERROR step="+step+" type="+e.GetType().FullName+" HRESULT=0x"+e.HResult.ToString("X8");}
 static T Read<T>(string step,Func<T> read,Action<string> trace){
  try{return read();}catch(Fault){throw;}catch(Exception e){trace(FailureDetail(step,e));throw new Fault(409,"AUTO_LIST_READ_FAILED");}
 }
 static bool Matches(string name,string filename){return String.Equals((name??"").Trim(),filename,StringComparison.OrdinalIgnoreCase)||String.Equals((name??"").Trim(),Path.GetFileNameWithoutExtension(filename),StringComparison.OrdinalIgnoreCase);}
 public static void Run(CadListReader list,string filename,Action guard,Action<string> trace){
  // The exact session filename is required; never fall back to the selected row or a wildcard.
  Guid token;
  if(filename==null||filename.Length!=21||!filename.StartsWith("Auto_",StringComparison.Ordinal)||!filename.EndsWith(".bat",StringComparison.Ordinal)||!Guid.TryParseExact(filename.Substring(5,12)+new string('0',20),"N",out token))throw new Fault(409,"AUTO_MACRO_NAME_INVALID");
  int count=Read("count",list.Count,trace);trace("AUTO_UI_COUNT "+count);
  if(count<1||count>2000)throw new Fault(409,"AUTO_LIST_TOO_LARGE_OR_EMPTY");
  int match=0,hits=0;
  for(int i=1;i<=count;i++){
   string name;
   try{name=list.Name(i);}catch(Exception e){
    // Some standard list providers include a scrollbar in accChildCount: its ID is not i.
    // Only their documented invalid-child errors are skipped; other failures stop the action.
    if((e is ArgumentException||e is COMException)&&(e.HResult==unchecked((int)0x80070057)||e.HResult==unchecked((int)0x8002000B))){trace("AUTO_UI_SKIP_INVALID_CHILD child="+i+" HRESULT=0x"+e.HResult.ToString("X8"));continue;}
    trace(FailureDetail("name",e));throw new Fault(409,"AUTO_LIST_READ_FAILED");
   }
   if(Matches(name,filename)){match=i;hits++;}
  }
  trace("AUTO_UI_MATCHES "+hits);
  if(hits!=1)throw new Fault(409,"AUTO_MACRO_NOT_IN_CURRENT_FOLDER");
  if(Read("role",delegate(){return list.Role(match);},trace)!=34)throw new Fault(409,"AUTO_ITEM_NOT_FILE");
  if(String.IsNullOrWhiteSpace(Read("default-action",delegate(){return list.DefaultAction(match);},trace)))throw new Fault(409,"AUTO_ITEM_ACTION_UNAVAILABLE");
  guard();
  if(!Matches(Read("verify-name",delegate(){return list.Name(match);},trace),filename))throw new Fault(409,"AUTO_ITEM_CHANGED");
  if((Read("verify-visibility",delegate(){return list.State(match);},trace)&0x18000)!=0)throw new Fault(409,"AUTO_ITEM_NOT_VISIBLE");
  trace("AUTO_UI_DEFAULT_ACTION_START child="+match);
  try{list.Invoke(match);}catch(Fault){throw;}catch(Exception e){trace(FailureDetail("default-action",e));throw new Fault(409,"AUTO_ITEM_ACTION_FAILED_CHECK_STATUS");}
  trace("AUTO_UI_DEFAULT_ACTION_RETURNED");
 }
}
partial class CadNative {
 sealed class ListReader:CadListReader {
  readonly CadAccessible accessible;
  readonly IntPtr window;readonly string filename;
  public ListReader(CadAccessible value,IntPtr handle,string expected){accessible=value;window=handle;filename=expected;}
  public int Count(){return accessible.Count();}
  public string Name(int child){return accessible.Name(child);}
  public int Role(int child){object value=accessible.Role(child);return value is int?(int)value:-1;}
  public int State(int child){object value=accessible.State(child);if(!(value is int))throw new Fault(409,"AUTO_LIST_READ_FAILED");return (int)value;}
  public string DefaultAction(int child){return accessible.DefaultAction(child);}
  public void Invoke(int child){
   // Control-directed activation avoids the unsupported MSAA default action. No global mouse input.
   var kind=new StringBuilder(256);GetClassName(window,kind,kind.Capacity);
   if(kind.ToString()!="SysListView32"||!IsWindowVisible(window)||!IsWindowEnabled(window))throw new Fault(409,"AUTO_ITEM_NOT_CLICKABLE");
   accessible.Select(2,child);
   if((State(child)&2)==0)throw new Fault(409,"AUTO_ITEM_SELECTION_NOT_CONFIRMED");
   string name=Name(child);if(!String.Equals(name,filename,StringComparison.OrdinalIgnoreCase)&&!String.Equals(name,Path.GetFileNameWithoutExtension(filename),StringComparison.OrdinalIgnoreCase))throw new Fault(409,"AUTO_ITEM_CHANGED");
   int left,top,width,height;accessible.Location(out left,out top,out width,out height,child);
   if(width<=0||height<=0)throw new Fault(409,"AUTO_ITEM_NOT_CLICKABLE");
   var point=new ClickPoint{x=left+Math.Min(20,width/2),y=top+height/2};object hit=accessible.HitTest(point.x,point.y);
   if(!(hit is int)||(int)hit!=child)throw new Fault(409,"AUTO_ITEM_NOT_CLICKABLE");
   ClickRect rect;if(!ScreenToClient(window,ref point)||!GetClientRect(window,out rect)||point.x<0||point.y<0||point.x>=rect.right||point.y>=rect.bottom||point.x>32767||point.y>32767)throw new Fault(409,"AUTO_ITEM_NOT_CLICKABLE");
   IntPtr position=new IntPtr((point.y<<16)|point.x);
   if(!PostMessage(window,0x203,new IntPtr(1),position))throw new Fault(409,"AUTO_CONTROL_MESSAGE_FAILED");
   PostMessage(window,0x202,IntPtr.Zero,position);
  }
 }
 [StructLayout(LayoutKind.Sequential)]struct ClickPoint{public int x,y;}
 [StructLayout(LayoutKind.Sequential)]struct ClickRect{public int left,top,right,bottom;}
 [DllImport("user32.dll")]static extern bool ScreenToClient(IntPtr h,ref ClickPoint point);
 [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr h,out ClickRect rect);
 delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr h,int command);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint command);
 [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr h);
 [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr menu);
 [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr menu,int index);
 [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr menu,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetMenuString(IntPtr menu,uint index,StringBuilder s,int n,uint flags);
 [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h,int id);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr h,uint objectId,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out CadAccessible accessible);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ProcessEntry {
  public uint size,usage,pid;public UIntPtr heap;public uint module,threads,parent;public int priority;public uint flags;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string executable;
 }
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32First(IntPtr snapshot,ref ProcessEntry row);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32Next(IntPtr snapshot,ref ProcessEntry row);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 static Dictionary<int,CadProcessInfo> Processes(){
  IntPtr snapshot=CreateToolhelp32Snapshot(2,0);
  if(snapshot==new IntPtr(-1))throw new Fault(409,"AUTO_PROCESS_SNAPSHOT_FAILED");
  try{
   var rows=new Dictionary<int,CadProcessInfo>();var entry=new ProcessEntry();entry.size=(uint)Marshal.SizeOf(typeof(ProcessEntry));
   if(!Process32First(snapshot,ref entry))throw new Fault(409,"AUTO_PROCESS_SNAPSHOT_FAILED");
   do{rows[(int)entry.pid]=new CadProcessInfo{pid=(int)entry.pid,parent=(int)entry.parent,name=entry.executable};}while(Process32Next(snapshot,ref entry));
   return rows;
  }finally{CloseHandle(snapshot);}
 }
 static string Title(IntPtr h){var s=new StringBuilder(1024);GetWindowText(h,s,s.Capacity);return s.ToString();}
 static uint External(IntPtr menu,int depth){
  if(menu==IntPtr.Zero||depth>8)return 0;
  for(int i=0;i<GetMenuItemCount(menu);i++){
   var s=new StringBuilder(200);GetMenuString(menu,(uint)i,s,s.Capacity,0x400);
   string label=s.ToString().Replace("(&G)","").Replace("&","").Trim();
   uint id=GetMenuItemID(menu,i);if(label=="外部変形"&&id!=0&&id!=UInt32.MaxValue)return id;
   uint nested=External(GetSubMenu(menu,i),depth+1);if(nested!=0)return nested;
  }return 0;
 }
 static CadWindow Window(int pid,string drawing,bool busy){
  long started;
  try{using(var p=WinProcess.GetProcessById(pid)){
   if(!String.Equals(p.ProcessName,"Jw_win",StringComparison.OrdinalIgnoreCase))throw new Fault(409,"CAD_PROCESS_CHANGED");
   started=p.StartTime.ToUniversalTime().Ticks;
  }}catch(Fault){throw;}catch{throw new Fault(409,"AUTO_CAD_PROCESS_UNREADABLE");}
  var windows=new List<CadWindowInfo>();
  EnumWindows(delegate(IntPtr h,IntPtr ignored){
   uint windowPid;GetWindowThreadProcessId(h,out windowPid);if(windowPid!=(uint)pid)return true;
   var kind=new StringBuilder(256);GetClassName(h,kind,kind.Capacity);
   windows.Add(new CadWindowInfo{handle=h,pid=pid,owned=GetWindow(h,4)!=IntPtr.Zero,visible=IsWindowVisible(h),enabled=IsWindowEnabled(h),title=Title(h),kind=kind.ToString()});return true;
  },IntPtr.Zero);
  var found=CadIdentity.SelectWindow(windows,pid,drawing,busy);
  // Jw_cad may hide/disable its main window or suspend menus while running a BAT.
  // A callback is identified by its launcher/binding, never by current menu visibility.
  if(!busy&&(GetMenu(found.handle)==IntPtr.Zero||External(GetMenu(found.handle),0)==0))throw new Fault(409,"AUTO_MENU_NOT_AVAILABLE");
  return new CadWindow{handle=found.handle,pid=pid,started=started};
 }
 public static CadWindow FindSetup(string drawing){
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new Fault(501,"AUTO_WINDOWS_ONLY");
  int pid=CadIdentity.Launcher(Processes(),WinProcess.GetCurrentProcess().Id);
  return Window(pid,drawing,true);
 }
 public static CadWindow FindCallback(CadLink binding){
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new Fault(501,"AUTO_WINDOWS_ONLY");
  CadIdentity.RequireLauncher(Processes(),WinProcess.GetCurrentProcess().Id,binding.pid);
  return Find(binding,true);
 }
 public static CadWindow Find(CadLink binding,bool busy){
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new Fault(501,"AUTO_WINDOWS_ONLY");
  if(binding==null)throw new Fault(409,"AUTO_CALIBRATION_REQUIRED");
  if(!busy&&!ManagedPath(binding)){
   int count=0;foreach(var row in Processes().Values)if(String.Equals(Path.GetFileNameWithoutExtension(row.name),"Jw_win",StringComparison.OrdinalIgnoreCase))count++;
   if(count==0)throw new Fault(409,"AUTO_JWCAD_NOT_FOUND");
   if(count!=1)throw new Fault(409,"AUTO_MULTIPLE_JWCAD");
  }
  var found=Window(binding.pid,binding.drawing,busy);
  if(found.started!=binding.started)throw new Fault(409,"CAD_PROCESS_CHANGED");
  return found;
 }
 static IntPtr FileList(CadWindow main){
  IntPtr result=IntPtr.Zero;int count=0;
  EnumWindows(delegate(IntPtr h,IntPtr ignored){
   uint pid;GetWindowThreadProcessId(h,out pid);
   if(pid!=(uint)main.pid||!IsWindowVisible(h)||GetAncestor(h,3)!=main.handle)return true;
   IntPtr list=GetDlgItem(h,1882);if(list!=IntPtr.Zero&&IsWindowVisible(list)){result=list;count++;}return true;
  },IntPtr.Zero);
  return count==1?result:IntPtr.Zero;
 }
 static void Restore(IntPtr window,string step,Action<string> trace){
  if(!IsIconic(window))return;
  trace("AUTO_UI_RESTORE "+step);ShowWindowAsync(window,9);
  for(int i=0;i<40;i++){if(!IsIconic(window)){Thread.Sleep(80);return;}Thread.Sleep(40);}
  throw new Fault(409,"AUTO_CAD_RESTORE_FAILED");
 }
 static void Choose(IntPtr list,string filename,Action guard,Action<string> trace){
  Guid iid=typeof(CadAccessible).GUID;CadAccessible accessible=null;
  try{
   int hr=AccessibleObjectFromWindow(list,unchecked((uint)-4),ref iid,out accessible);
   trace("AUTO_UI_LIST_OBJECT HRESULT=0x"+hr.ToString("X8"));
   if(hr!=0||accessible==null)throw new Fault(409,"AUTO_LIST_NOT_ACCESSIBLE");
   CadListChoice.Run(new ListReader(accessible,list,filename),filename,guard,trace);
  }catch(Fault){throw;}catch(Exception e){trace(CadListChoice.FailureDetail("list-object",e));throw new Fault(409,"AUTO_LIST_READ_FAILED");
  }finally{if(accessible!=null&&Marshal.IsComObject(accessible))Marshal.ReleaseComObject(accessible);}
 }
 public static void Execute(CadLink link,Func<bool> completed,Action<string> trace){ExecuteFile(link,link.Macro,completed,trace);}
 public static void ExecuteFile(CadLink link,string filename,Func<bool> completed,Action<string> trace){
  CadWindow main=Find(link,true);IntPtr previous=GetForegroundWindow();
  IntPtr existing=FileList(main);
  if(!IsWindowEnabled(main.handle)&&existing==IntPtr.Zero)throw new Fault(409,"CAD_DIALOG_OPEN");
  Restore(main.handle,"main",trace);
  if(completed())throw new Fault(409,"AUTO_ALREADY_ISSUED_CHECK_STATUS");
  uint command=External(GetMenu(main.handle),0);
  if(existing==IntPtr.Zero){trace("AUTO_UI_MENU_START");if(command==0||!PostMessage(main.handle,0x111,new IntPtr((long)command),IntPtr.Zero))throw new Fault(409,"AUTO_MENU_FAILED");}
  try{
   IntPtr list=existing;DateTime until=DateTime.UtcNow.AddSeconds(5);
   while(DateTime.UtcNow<until){Find(link,true);list=FileList(main);if(list!=IntPtr.Zero)break;Thread.Sleep(80);}
   if(list==IntPtr.Zero)throw new Fault(409,"AUTO_FILE_DIALOG_NOT_FOUND");
   Restore(GetAncestor(list,2),"file-dialog",trace);
   if(link.managed)ManagedFolder(list,trace);
   trace("AUTO_UI_LIST_FOUND");
   for(int attempt=0;attempt<2;attempt++)try{
    Choose(list,filename,delegate(){
    Find(link,true);
    if(FileList(main)!=list)throw new Fault(409,"AUTO_FILE_DIALOG_CHANGED");
    if(completed())throw new Fault(409,"AUTO_ALREADY_ISSUED_CHECK_STATUS");
    },trace);break;
   }catch(Fault e){
    if((e.Message!="AUTO_ITEM_NOT_VISIBLE"&&e.Message!="AUTO_ITEM_NOT_CLICKABLE")||attempt!=0)throw;
    // No control activation was posted: safely rebuild only this verified BAT picker.
    trace("AUTO_UI_REOPEN_PICKER_BEFORE_ACTION");
    if(FileList(main)!=list)throw new Fault(409,"AUTO_FILE_DIALOG_CHANGED");
    PostMessage(GetAncestor(list,2),0x111,new IntPtr(2),IntPtr.Zero);
    until=DateTime.UtcNow.AddSeconds(3);
    while(DateTime.UtcNow<until&&FileList(main)!=IntPtr.Zero)Thread.Sleep(50);
    if(FileList(main)!=IntPtr.Zero||!IsWindowEnabled(main.handle))throw new Fault(409,"AUTO_FILE_DIALOG_CHANGED");
    Restore(main.handle,"main-before-reopen",trace);
    if(!PostMessage(main.handle,0x111,new IntPtr((long)command),IntPtr.Zero))throw new Fault(409,"AUTO_MENU_FAILED");
    list=IntPtr.Zero;until=DateTime.UtcNow.AddSeconds(5);
    while(DateTime.UtcNow<until){list=FileList(main);if(list!=IntPtr.Zero)break;Thread.Sleep(80);}
    if(list==IntPtr.Zero)throw new Fault(409,"AUTO_FILE_DIALOG_NOT_FOUND");
    Restore(GetAncestor(list,2),"reopened-dialog",trace);
    if(link.managed)ManagedFolder(list,trace);
   }
   until=DateTime.UtcNow.AddSeconds(12);
   while(DateTime.UtcNow<until){if(completed()){Thread.Sleep(250);return;}Thread.Sleep(80);}
   throw new Fault(409,"AUTO_RESULT_UNKNOWN_NEW_DXF_REQUIRED");
  }finally{
   uint currentPid;GetWindowThreadProcessId(GetForegroundWindow(),out currentPid);
   if(currentPid==(uint)main.pid&&previous!=IntPtr.Zero)SetForegroundWindow(previous);
  }
 }
}
