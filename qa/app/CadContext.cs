using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

class CadLink {
 public int schema {get;set;}
 public string session {get;set;}
 public string token {get;set;}
 public Pt end {get;set;}
 public bool calibrated {get;set;}
 public bool managed {get;set;}
 public string checkNonce {get;set;}
 public string checkedNonce {get;set;}
 public double? verifiedStation {get;set;}
 public bool enabled {get;set;}
 public bool fieldMode {get;set;}
 public bool fieldReady {get;set;}
 public string drawing {get;set;}
 public int pid {get;set;}
 public long started {get;set;}
 public double x {get;set;}
 public double y {get;set;}
 public int group {get;set;}
 public double scale {get;set;}
 public double width {get;set;}
 public double height {get;set;}
 public string message {get;set;}
 public string Macro {get{return "Auto_"+token.Substring(0,12)+".bat";}}
 public string CheckMacro {get{return "Auto_"+token.Substring(12,12)+".bat";}}
}
class CadContext {
 public string drawing;
 public double axis,width,height;
 public double[] scales;
 public int group=-1;
 public Pt picked;
 public bool paper;
 public readonly List<Pt> ends=new List<Pt>();
 public readonly List<double[]> circles=new List<double[]>();
 static double N(string s){double n;if(!Double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||Double.IsNaN(n)||Double.IsInfinity(n)||Math.Abs(n)>1e12)throw new Fault(400,"CAD_NUMBER_INVALID");return n;}
 public static CadContext Parse(string text){
  var c=new CadContext();bool axis=false,size=false;int scaleRows=0;
  foreach(string raw in text.Split('\n')){
   string s=raw.Trim();if(s.Length==0)continue;
   string[] p=s.Split(new char[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
   if(s.StartsWith("file=")){if(c.drawing!=null)throw new Fault(400,"CAD_HEADER_DUPLICATE");c.drawing=s.Substring(5).Trim();}
   else if(p[0]=="hk"&&p.Length==2){if(axis)throw new Fault(400,"CAD_HEADER_DUPLICATE");c.axis=N(p[1]);axis=true;}
   else if(p[0]=="hs"&&p.Length==17){if(++scaleRows>1)throw new Fault(400,"CAD_HEADER_DUPLICATE");c.scales=new double[16];for(int i=0;i<16;i++){c.scales[i]=N(p[i+1]);if(c.scales[i]<=0||c.scales[i]>1e6)throw new Fault(400,"CAD_SCALE_INVALID");}}
   else if(p[0]=="hzs"&&p.Length==3){if(size)throw new Fault(400,"CAD_HEADER_DUPLICATE");c.width=N(p[1]);c.height=N(p[2]);size=true;}
   else if(p[0]=="bz")c.paper=true;
   else if(p[0]=="b0")c.paper=false;
   else if(p[0].Length>=3&&p[0].StartsWith("lg")){int g;if(!Int32.TryParse(p[0].Substring(2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out g)||g!=0)throw new Fault(409,"CAD_USE_GROUP0_ONLY");c.group=g;}
   else if(p[0]=="hp1"&&p.Length==3){if(c.picked!=null)throw new Fault(400,"CAD_PICK_DUPLICATE");c.picked=new Pt{x=N(p[1]),y=N(p[2])};}
   else if(p[0]=="ci"&&p.Length==4)c.circles.Add(new double[]{N(p[1]),N(p[2]),N(p[3])});
   else if(p.Length==4){double first;if(Double.TryParse(p[0],NumberStyles.Float,CultureInfo.InvariantCulture,out first)){c.ends.Add(new Pt{x=N(p[0]),y=N(p[1])});c.ends.Add(new Pt{x=N(p[2]),y=N(p[3])});}}
  }
  if(String.IsNullOrWhiteSpace(c.drawing)||!axis||c.scales==null||!size||c.group!=0||!c.paper||c.width<=0||c.height<=0)throw new Fault(409,"CAD_HEADER_INCOMPLETE");
  if(Math.Abs(c.axis)>1e-9)throw new Fault(409,"CAD_AXIS_MUST_BE_ZERO");
  return c;
 }
 public static Pt Paper(CadLink link,Pt relative){return new Pt{x=link.x+relative.x*1000/link.scale,y=link.y+relative.y*1000/link.scale};}
 public static Pt Real(CadLink link,Pt relative){return new Pt{x=link.x*link.scale+relative.x*1000,y=link.y*link.scale+relative.y*1000};}
 public Pt ManagedOrigin(CadLink link){
  Pt found=null;int matches=0;double scale=scales[0];
  foreach(var first in circles)if(Math.Abs(first[2]-47/scale)<.005/scale)
   foreach(var last in circles)if(Math.Abs(last[2]-71/scale)<.005/scale&&Math.Abs(last[0]-first[0]-link.end.x*1000/scale)<.05/scale&&Math.Abs(last[1]-first[1]-link.end.y*1000/scale)<.05/scale){found=new Pt{x=first[0],y=first[1]};matches++;}
  if(matches!=1)throw new Fault(409,"MANAGED_ORIGIN_MARKERS_MISMATCH");return found;
 }
 public void RequireMeasuredLine(CadLink link,Pt first,Pt last){
  Pt p=Paper(link,first),q=Paper(link,last);
  for(int i=0;i+1<ends.Count;i+=2)if(Same(ends[i].x,p.x,link)&&Same(ends[i].y,p.y,link)&&Same(ends[i+1].x,q.x,link)&&Same(ends[i+1].y,q.y,link))return;
  throw new Fault(409,"MANAGED_MEASURED_LINE_NOT_FOUND");
 }
 bool Same(double a,double b,CadLink link){return Math.Abs(a-b)<=.05/link.scale;}
 bool HasEnd(Pt p,CadLink link){foreach(var q in ends)if(Same(q.x,p.x,link)&&Same(q.y,p.y,link))return true;return false;}
 public void Validate(CadLink link,Pt previous){
  if(!String.Equals(Path.GetFullPath(drawing),Path.GetFullPath(link.drawing),StringComparison.OrdinalIgnoreCase))throw new Fault(409,"CAD_DIFFERENT_DRAWING");
  if(group!=link.group||Math.Abs(scales[group]-link.scale)>1e-9||Math.Abs(width-link.width)>1e-6||Math.Abs(height-link.height)>1e-6)throw new Fault(409,"CAD_SCALE_OR_PAPER_CHANGED");
  if(!HasEnd(new Pt{x=link.x,y=link.y},link)||!HasEnd(Paper(link,link.end),link))throw new Fault(409,"CAD_PLAN_ANCHORS_CHANGED");
  Pt target=Paper(link,previous);bool found=false;
  foreach(var circle in circles)if(Same(circle[0],target.x,link)&&Same(circle[1],target.y,link)&&Same(circle[2],35/link.scale,link))found=true;
  if(!found)throw new Fault(409,"CAD_PREVIOUS_POINT_NOT_FOUND");
 }
}
