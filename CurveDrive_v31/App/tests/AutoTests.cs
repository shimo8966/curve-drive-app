using System;using System.Globalization;using System.Reflection;
class AutoTests {
 static int passed;
 static void Check(bool b){if(!b)throw new Exception("Assertion failed");}
 static void Near(double a,double b){Check(Math.Abs(a-b)<1e-6);}
 static string N(double n){return n.ToString("R",CultureInfo.InvariantCulture);}
 static CadLink Link(double scale){return new CadLink{session="synthetic",token=new string('a',32),drawing="/synthetic/Cad.dxf",end=new Pt{x=50,y=0},x=-123456/scale,y=654321/scale,scale=scale,group=0,width=420,height=297};}
 static string Input(CadLink a,bool point){
  var previous=CadContext.Paper(a,new Pt{x=10.5,y=.018});var end=CadContext.Paper(a,a.end);
  return "hq\nfile=/synthetic/Cad.dxf\nhk 0\nhs "+String.Join(" ",new string[]{N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale),N(a.scale)})+"\nhzs 420 297\nlg0\nbz\n"+(point?"hp1 "+N(a.x)+" "+N(a.y)+"\n":"")+N(a.x)+" "+N(a.y)+" "+N(end.x)+" "+N(end.y)+"\nci "+N(previous.x)+" "+N(previous.y)+" "+N(35/a.scale)+"\n";
 }
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS "+name);}
 static void Rejected(Action body,string code){try{body();throw new Exception("Should reject");}catch(Fault f){Check(f.Message==code);}}
 static int Main(){
  try{
   foreach(double scale in new double[]{1,100,1000}){
    double value=scale;Test("Paper headers validate synthetic anchors at scale "+value,delegate(){var a=Link(value);var c=CadContext.Parse(Input(a,true));c.Validate(a,new Pt{x=10.5,y=.018});Near(c.picked.x,a.x);Near(c.picked.y,a.y);});
    Test("Real coordinates remain mm at scale "+value,delegate(){var a=Link(value);var p=CadContext.Real(a,new Pt{x=10.5,y=.018});Near(p.x,-112956);Near(p.y,654339);});
   }
   Test("Changed drawing rejected",delegate(){var a=Link(100);var c=CadContext.Parse(Input(a,true).Replace("Cad.dxf","Other.dxf"));Rejected(delegate(){c.Validate(a,new Pt{x=10.5,y=.018});},"CAD_DIFFERENT_DRAWING");});
   foreach(double scale in new[]{1.0,100,1000}){double value=scale;Test("Managed origin is recognized at scale "+value+" without a picked point",delegate(){var a=Link(value);var c=CadContext.Parse(Input(a,false));var end=CadContext.Paper(a,a.end);c.circles.Add(new[]{a.x,a.y,47/value});c.circles.Add(new[]{end.x,end.y,71/value});Pt origin=c.ManagedOrigin(a);Near(origin.x,a.x);Near(origin.y,a.y);});}
   Test("Missing origin marker refuses automatic registration",delegate(){var a=Link(100);var c=CadContext.Parse(Input(a,false));Rejected(delegate(){c.ManagedOrigin(a);},"MANAGED_ORIGIN_MARKERS_MISMATCH");});
   Test("Ambiguous origin markers refuse automatic registration",delegate(){var a=Link(100);var c=CadContext.Parse(Input(a,false));var end=CadContext.Paper(a,a.end);c.circles.Add(new[]{a.x,a.y,.47});c.circles.Add(new[]{a.x,a.y,.47});c.circles.Add(new[]{end.x,end.y,.71});Rejected(delegate(){c.ManagedOrigin(a);},"MANAGED_ORIGIN_MARKERS_MISMATCH");});
   Test("Readback requires a connecting measured line",delegate(){var a=Link(100);var c=CadContext.Parse(Input(a,false));Rejected(delegate(){c.RequireMeasuredLine(a,new Pt{x=10,y=0},new Pt{x=10.5,y=.018});},"MANAGED_MEASURED_LINE_NOT_FOUND");});
   Test("Changed paper size rejected",delegate(){var a=Link(100);var c=CadContext.Parse(Input(a,true).Replace("hzs 420 297","hzs 297 210"));Rejected(delegate(){c.Validate(a,new Pt{x=10.5,y=.018});},"CAD_SCALE_OR_PAPER_CHANGED");});
   Test("Missing previous point rejected",delegate(){var a=Link(100);var c=CadContext.Parse(Input(a,true));c.circles.Clear();Rejected(delegate(){c.Validate(a,new Pt{x=10.5,y=.018});},"CAD_PREVIOUS_POINT_NOT_FOUND");});
   Test("Moved plan anchors rejected",delegate(){var a=Link(100);var c=CadContext.Parse(Input(a,true));c.ends[0].x+=1;Rejected(delegate(){c.Validate(a,new Pt{x=10.5,y=.018});},"CAD_PLAN_ANCHORS_CHANGED");});
   Test("Nonzero CAD axis rejected",delegate(){var a=Link(100);Rejected(delegate(){CadContext.Parse(Input(a,true).Replace("hk 0","hk 15"));},"CAD_AXIS_MUST_BE_ZERO");});
   Test("Other layer group rejected",delegate(){var a=Link(100);Rejected(delegate(){CadContext.Parse(Input(a,true).Replace("lg0","lg1"));},"CAD_USE_GROUP0_ONLY");});
   Test("Missing paper-unit declaration rejected",delegate(){var a=Link(100);Rejected(delegate(){CadContext.Parse(Input(a,true).Replace("bz\n",""));},"CAD_HEADER_INCOMPLETE");});
   Test("Nonfinite coordinate rejected",delegate(){var a=Link(100);Rejected(delegate(){CadContext.Parse(Input(a,true)+"NaN 0 1 1\n");},"CAD_NUMBER_INVALID");});
   Test("Automatic geometry emits real-mm b0 plus calibration translation",delegate(){var a=Link(100);var s=new State{q=new Pt{x=10,y=.02},p=new Pt{x=10.5,y=.018}};var method=typeof(Program).GetMethod("AutoGeometry",BindingFlags.Static|BindingFlags.NonPublic);string data=(string)method.Invoke(null,new object[]{a,s});Check(data.StartsWith("b0\r\nlc3\r\n"));Check(data.Contains("ci -112956.0000000 654339.0000000 35.0000000"));Check(!data.Contains("hd"));Check(!data.Contains("bz"));});
   Console.WriteLine("RESULT "+passed+" passed; 0 failed. Native Windows UI is not executed.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
