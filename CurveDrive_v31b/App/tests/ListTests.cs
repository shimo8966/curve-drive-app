using System;using System.Collections.Generic;using System.Runtime.InteropServices;
class ListTests {
 const string FileName="Auto_0123456789ab.bat";
 class List:CadListReader {
  public int count=4,role=34,state,calls,child,nameReads;
  public string action="Open";
  public bool changed;
  public Exception countError,nameError,actionError;
  public Dictionary<int,string> names=new Dictionary<int,string>{{1,FileName},{2,"Curve_Latest.bat"},{3,"Setup_Link.bat"}};
  public int Count(){if(countError!=null)throw countError;return count;}
  public string Name(int id){if(nameError!=null)throw nameError;if(id==1&&++nameReads>1&&changed)return "Other.bat";string s;if(!names.TryGetValue(id,out s))throw new ArgumentException("Invalid child");return s;}
  public int Role(int id){return role;}
  public int State(int id){return state;}
  public string DefaultAction(int id){return action;}
  public void Invoke(int id){calls++;child=id;if(actionError!=null)throw actionError;}
 }
 static int passed;
 static void Check(bool b){if(!b)throw new Exception("Assertion failed");}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS "+name);}
 static void Reject(List list,string code,Action guard,string file){try{CadListChoice.Run(list,file,guard,delegate(string s){});throw new Exception("Expected rejection");}catch(Fault f){Check(f.Message==code);}}
 static void Run(List list){CadListChoice.Run(list,FileName,delegate(){},delegate(string s){});}
 static int Main(){try{
  Test("A narrow file pane still chooses a point inside the visible target row",delegate(){int x,y;Check(CadClickGeometry.TryVisiblePoint(0,20,1000,14,180,100,out x,out y)&&x==20&&y==26);});
  Test("A horizontally clipped target row uses its visible part",delegate(){int x,y;Check(CadClickGeometry.TryVisiblePoint(-60,20,400,14,180,100,out x,out y)&&x==20&&y==26);});
  Test("A one-pixel visible target remains inside that target",delegate(){int x,y;Check(CadClickGeometry.TryVisiblePoint(-60,20,61,14,180,100,out x,out y)&&x==0&&y==26);});
  Test("Rows clipped at the top or bottom choose only visible pixels",delegate(){int x,y;Check(CadClickGeometry.TryVisiblePoint(0,-8,400,14,180,100,out x,out y)&&x==20&&y==2);Check(CadClickGeometry.TryVisiblePoint(0,98,400,14,180,100,out x,out y)&&x==20&&y==98);});
  Test("A row clipped at the right edge cannot click the adjacent control",delegate(){int x,y;Check(CadClickGeometry.TryVisiblePoint(175,20,400,14,180,100,out x,out y)&&x==177&&y==26);});
  Test("Rows with no visible intersection are refused",delegate(){int x,y;foreach(var r in new[]{new[]{180,0,20,14},new[]{0,100,20,14},new[]{-500,0,400,14},new[]{0,-20,20,14}})Check(!CadClickGeometry.TryVisiblePoint(r[0],r[1],r[2],r[3],180,100,out x,out y));});
  Test("Empty rows and empty clients are refused",delegate(){int x,y;foreach(var r in new[]{new[]{0,14,180,100},new[]{20,0,180,100},new[]{20,14,0,100},new[]{20,14,180,0},new[]{-20,14,180,100}})Check(!CadClickGeometry.TryVisiblePoint(0,0,r[0],r[1],r[2],r[3],out x,out y));});
  Test("Overflow and coordinates beyond packed mouse message limits are refused",delegate(){int x,y;Check(!CadClickGeometry.TryVisiblePoint(Int32.MinValue,0,Int32.MaxValue,14,180,100,out x,out y));Check(!CadClickGeometry.TryVisiblePoint(Int32.MaxValue-10,0,100,14,Int32.MaxValue,100,out x,out y));Check(!CadClickGeometry.TryVisiblePoint(33000,0,100,14,40000,100,out x,out y));});
  Test("Synthetic scaled row bounds retain a visible point without hardcoded screen positions",delegate(){foreach(double scale in new[]{1.25,1.5,2.0}){int x,y,left=(int)(-60*scale),top=(int)(20*scale),width=(int)(400*scale),height=(int)(14*scale),cw=(int)(180*scale),ch=(int)(100*scale);Check(CadClickGeometry.TryVisiblePoint(left,top,width,height,cw,ch,out x,out y)&&x>=0&&x<cw&&y>=top&&y<top+height);}});
  Test("Selection waits for delayed native state without repeating an activation",delegate(){int waits=0;CadListChoice.WaitSelected(delegate(){return FileName;},delegate(){return waits>=2?2:0;},FileName,delegate(){waits++;},4);Check(waits==2);});
  Test("Selection that never appears is refused after a bounded wait",delegate(){int waits=0;try{CadListChoice.WaitSelected(delegate(){return FileName;},delegate(){return 0;},FileName,delegate(){waits++;},3);throw new Exception("Expected refusal");}catch(Fault f){Check(f.Message=="AUTO_ITEM_SELECTION_NOT_CONFIRMED"&&waits==3);}});
  Test("A stale row appearing while selection settles is refused",delegate(){int waits=0;try{CadListChoice.WaitSelected(delegate(){return waits==0?FileName:"Auto_abcdefabcdef.bat";},delegate(){return 0;},FileName,delegate(){waits++;},3);throw new Exception("Expected refusal");}catch(Fault f){Check(f.Message=="AUTO_ITEM_CHANGED"&&waits==1);}});
  Test("A row swapped during a selected-state read is refused",delegate(){bool changed=false;try{CadListChoice.WaitSelected(delegate(){return changed?"Other.bat":FileName;},delegate(){changed=true;return 2;},FileName,delegate(){},3);throw new Exception("Expected refusal");}catch(Fault f){Check(f.Message=="AUTO_ITEM_CHANGED");}});
  Test("A hidden row is refused while waiting for selection",delegate(){try{CadListChoice.WaitSelected(delegate(){return FileName;},delegate(){return 0x8000;},FileName,delegate(){throw new Exception("Must not wait");},3);throw new Exception("Expected refusal");}catch(Fault f){Check(f.Message=="AUTO_ITEM_NOT_VISIBLE");}});
  Test("Selection confirmation accepts the exact hidden-extension stem",delegate(){CadListChoice.WaitSelected(delegate(){return "AUTO_0123456789AB";},delegate(){return 2;},FileName,delegate(){throw new Exception("Must not wait");},3);});
  Test("Count includes an invalid child: unique session macro still invokes once",delegate(){var l=new List();Run(l);Check(l.calls==1&&l.child==1);});
  Test("No separate select operation is required by the list adapter",delegate(){Check(typeof(CadListReader).GetMethod("Select")==null);var l=new List();Run(l);Check(l.calls==1);});
  Test("Hidden file extension matches exact session stem",delegate(){var l=new List();l.names[1]="Auto_0123456789ab";Run(l);Check(l.calls==1&&l.child==1);});
  Test("Case-insensitive exact name matches",delegate(){var l=new List();l.names[1]="AUTO_0123456789AB.BAT";Run(l);Check(l.calls==1);});
  Test("Another session macro cannot be used",delegate(){var l=new List();l.names[1]="Auto_abcdefabcdef.bat";Reject(l,"AUTO_MACRO_NOT_IN_CURRENT_FOLDER",delegate(){},FileName);Check(l.calls==0);});
  Test("Ambiguous matches stop without a default action",delegate(){var l=new List();l.names[2]=FileName;Reject(l,"AUTO_MACRO_NOT_IN_CURRENT_FOLDER",delegate(){},FileName);Check(l.calls==0);});
  Test("Unexpected item role stops",delegate(){var l=new List();l.role=33;Reject(l,"AUTO_ITEM_NOT_FILE",delegate(){},FileName);Check(l.calls==0);});
  Test("Unavailable default action stops",delegate(){var l=new List();l.action="";Reject(l,"AUTO_ITEM_ACTION_UNAVAILABLE",delegate(){},FileName);Check(l.calls==0);});
  Test("Off-screen or invisible item stops before action",delegate(){foreach(int state in new[]{0x8000,0x10000,0x18000}){var l=new List();l.state=state;Reject(l,"AUTO_ITEM_NOT_VISIBLE",delegate(){},FileName);Check(l.calls==0);}});
  Test("Empty and oversized lists stop",delegate(){foreach(int n in new[]{0,2001}){var l=new List();l.count=n;Reject(l,"AUTO_LIST_TOO_LARGE_OR_EMPTY",delegate(){},FileName);Check(l.calls==0);}});
  Test("Bad macro names and paths stop before reading or acting",delegate(){foreach(string name in new[]{"Curve_Latest.bat","Auto_*.bat","../Auto_0123456789ab.bat","Auto_zzzzzzzzzzzz.bat"}){var l=new List();Reject(l,"AUTO_MACRO_NAME_INVALID",delegate(){},name);Check(l.calls==0);}});
  Test("Count failure is preserved as a read refusal",delegate(){var l=new List();l.countError=new COMException("Unavailable",unchecked((int)0x80004005));Reject(l,"AUTO_LIST_READ_FAILED",delegate(){},FileName);Check(l.calls==0);});
  Test("Non-child-related name errors cannot be silently skipped",delegate(){var l=new List();l.nameError=new COMException("Denied",unchecked((int)0x80070005));Reject(l,"AUTO_LIST_READ_FAILED",delegate(){},FileName);Check(l.calls==0);});
  Test("Changed CAD/dialog guard stops before invoking",delegate(){var l=new List();Reject(l,"AUTO_FILE_DIALOG_CHANGED",delegate(){throw new Fault(409,"AUTO_FILE_DIALOG_CHANGED");},FileName);Check(l.calls==0);});
  Test("Changed row name stops after the guard",delegate(){var l=new List();l.changed=true;Reject(l,"AUTO_ITEM_CHANGED",delegate(){},FileName);Check(l.calls==0);});
  Test("Native default action failure is recorded and never retried",delegate(){var l=new List();l.actionError=new COMException("Failed",unchecked((int)0x80070057));var trace=new System.Collections.Generic.List<string>();try{CadListChoice.Run(l,FileName,delegate(){},trace.Add);throw new Exception("Expected rejection");}catch(Fault f){Check(f.Message=="AUTO_ITEM_ACTION_FAILED_CHECK_STATUS");}Check(l.calls==1);Check(trace.Exists(s=>s.Contains("step=default-action")&&s.Contains("HRESULT=0x80070057")));Check(!trace.Contains("AUTO_UI_DEFAULT_ACTION_RETURNED"));});
  Console.WriteLine("RESULT "+passed+" passed; 0 failed. Windows APIs are not executed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
