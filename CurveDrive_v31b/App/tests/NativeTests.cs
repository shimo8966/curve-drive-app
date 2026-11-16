using System;using System.Collections.Generic;
class NativeTests {
 static int passed;
 static void Check(bool yes){if(!yes)throw new Exception("Assertion failed");}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS "+name);}
 static void Reject(Action body,string code){try{body();throw new Exception("Expected rejection");}catch(Fault f){Check(f.Message==code);}}
 static CadWindowInfo Window(int pid,bool visible,bool enabled){return new CadWindowInfo{handle=new IntPtr(pid),pid=pid,title="Trial.dxf - jw_win",kind="Afx:main",visible=visible,enabled=enabled};}
 static List<CadWindowInfo> One(CadWindowInfo w){return new List<CadWindowInfo>{w};}
 static Dictionary<int,CadProcessInfo> Chain(){return new Dictionary<int,CadProcessInfo>{
  {10,new CadProcessInfo{pid=10,parent=20,name="Bridge.exe"}},
  {20,new CadProcessInfo{pid=20,parent=30,name="cmd.exe"}},
  {30,new CadProcessInfo{pid=30,parent=40,name="Jw_win.exe"}},
  {40,new CadProcessInfo{pid=40,parent=0,name="explorer.exe"}}};}
 static int Main(){try{
  Test("BAT callback finds launching CAD through cmd",delegate(){Check(CadIdentity.Launcher(Chain(),10)==30);});
  Test("Callback launcher agrees with the registered CAD",delegate(){CadIdentity.RequireLauncher(Chain(),10,30);});
  Test("Another CAD launcher is rejected even with the same drawing",delegate(){Reject(delegate(){CadIdentity.RequireLauncher(Chain(),10,31);},"AUTO_CAD_LAUNCHER_CHANGED");});
  Test("Unrelated CAD process cannot be launcher",delegate(){var p=Chain();p[20].parent=40;Reject(delegate(){CadIdentity.Launcher(p,10);},"AUTO_CAD_LAUNCHER_NOT_FOUND");});
  Test("Broken process chain rejects",delegate(){var p=Chain();p.Remove(20);Reject(delegate(){CadIdentity.Launcher(p,10);},"AUTO_CAD_LAUNCHER_NOT_FOUND");});
  Test("Cyclic process chain rejects",delegate(){var p=Chain();p[20].parent=10;Reject(delegate(){CadIdentity.Launcher(p,10);},"AUTO_CAD_LAUNCHER_NOT_FOUND");});
  Test("Callback recognizes hidden disabled main with no menu requirement",delegate(){var w=Window(30,false,false);Check(Object.ReferenceEquals(w,CadIdentity.SelectWindow(One(w),30,"Trial.dxf",true)));});
  Test("Normal operation rejects hidden main",delegate(){Reject(delegate(){CadIdentity.SelectWindow(One(Window(30,false,true)),30,"Trial.dxf",false);},"AUTO_CAD_WINDOW_NOT_FOUND");});
  Test("Normal operation refuses disabled main",delegate(){Reject(delegate(){CadIdentity.SelectWindow(One(Window(30,true,false)),30,"Trial.dxf",false);},"CAD_DIALOG_OPEN");});
  Test("Another process with same title is excluded",delegate(){Reject(delegate(){CadIdentity.SelectWindow(One(Window(31,true,true)),30,"Trial.dxf",true);},"AUTO_CAD_WINDOW_NOT_FOUND");});
  Test("Different drawing title is excluded",delegate(){Reject(delegate(){CadIdentity.SelectWindow(One(Window(30,true,true)),30,"Other.dxf",true);},"AUTO_CAD_WINDOW_NOT_FOUND");});
  Test("Native JWW title without extension is recognized",delegate(){var w=Window(30,true,true);w.title="Work - jw_win";Check(Object.ReferenceEquals(w,CadIdentity.SelectWindow(One(w),30,"Work.jww",true)));});
  Test("Native JWW title retaining extension is recognized",delegate(){var w=Window(30,true,true);w.title="Work.jww - jw_win";Check(Object.ReferenceEquals(w,CadIdentity.SelectWindow(One(w),30,"Work.jww",false)));});
  Test("DXF cannot use a stem-only native JWW title",delegate(){var w=Window(30,true,true);w.title="Work - jw_win";Reject(delegate(){CadIdentity.SelectWindow(One(w),30,"Work.dxf",true);},"AUTO_CAD_WINDOW_NOT_FOUND");});
  Test("Matching name within another filename is excluded",delegate(){var w=Window(30,true,true);w.title="OtherTrial.dxf - jw_win";Reject(delegate(){CadIdentity.SelectWindow(One(w),30,"Trial.dxf",true);},"AUTO_CAD_WINDOW_NOT_FOUND");});
  Test("Owned helper and file dialog cannot become main",delegate(){var w=Window(30,true,true);var helper=Window(30,true,true);helper.owned=true;var dialog=Window(30,true,true);dialog.kind="#32770";Check(Object.ReferenceEquals(w,CadIdentity.SelectWindow(new List<CadWindowInfo>{helper,dialog,w},30,"Trial.dxf",true)));});
  Test("Ambiguous main windows stop",delegate(){Reject(delegate(){CadIdentity.SelectWindow(new List<CadWindowInfo>{Window(30,true,true),Window(30,true,true)},30,"Trial.dxf",true);},"AUTO_CAD_WINDOW_AMBIGUOUS");});
  Console.WriteLine("RESULT "+passed+" passed; 0 failed. Native Windows API calls are not executed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
