using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;

partial class Program {
 static bool GuideRoute(HttpListenerContext ctx,string method,string path){
  if(path!="/guide/info"&&path!="/guide/folder")return false;
  string origin=ctx.Request.Headers["Origin"];
  if(method=="GET"&&path=="/guide/info"){
   if(!OriginAllowed(origin))throw new Fault(403,"ORIGIN_NOT_ALLOWED");
   using(Lock()){
    State s=Load();CadLink a=AutoLoad(s);bool ready=false;string code=null;
    // This is an identity check for the guide. A BAT may temporarily disable the main window.
    // Actual enable/execute/callback paths retain their existing operation and geometry guards.
    if(a!=null&&a.calibrated){try{CadNative.Find(a,true);ready=true;}catch(Fault e){code=e.Message;}}
    Reply(ctx,200,new{ok=true,version=AppVersion,workId=WorkId(),folder=Root,cadReady=ready,cadCode=code,latest=Latest(s)});
   }return true;
  }
  if(method=="POST"&&path=="/guide/folder"){
   if(origin!="http://127.0.0.1:18718"&&origin!="http://localhost:18718")throw new Fault(403,"AUTO_USE_LOCAL_PAGE");
   var body=Body(ctx);string file=Text(Field(body,"file"),16);string name;
   if(file=="setup")name="Setup_Link.bat";else if(file=="run")name="Field_Run.bat";else throw new Fault(400,"GUIDE_FILE_INVALID");
   string target=Path.Combine(Root,name);if(!File.Exists(target))throw new Fault(409,"GUIDE_FILE_NOT_READY");
   if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new Fault(501,"AUTO_WINDOWS_ONLY");
   System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","/select,\""+target+"\""){UseShellExecute=true});
   Reply(ctx,200,new{ok=true,file=name});return true;
  }
  throw new Fault(404,"NOT_FOUND");
 }
 static void AutoTrace(string text){try{Log(text);}catch{}}
 static readonly string AutoPath=Path.Combine(Root,"Auto.json");
 static CadLink AutoLoad(State s){
  if(!File.Exists(AutoPath)||s==null)return null;
  CadLink a;
  try{a=Json.Deserialize<CadLink>(File.ReadAllText(AutoPath,Utf8));}catch{throw new Fault(409,"AUTO_CONFIG_INVALID");}
  Guid id;
  if(a==null||a.schema!=1||!Guid.TryParseExact(a.token,"N",out id)||!ValidPoint(a.end))throw new Fault(409,"AUTO_CONFIG_INVALID");
  if(a.session!=s.session)return null;
  if(a.fieldMode&&a.enabled)throw new Fault(409,"AUTO_CONFIG_INVALID");
  if(a.calibrated&&(a.pid<=0||a.started<=0||String.IsNullOrEmpty(a.drawing)||a.group!=0||!ValidNumber(a.scale,1e6)||a.scale<=0||!ValidNumber(a.x,1e12)||!ValidNumber(a.y,1e12)||!ValidNumber(a.width,1e7)||!ValidNumber(a.height,1e7)||a.width<=0||a.height<=0))throw new Fault(409,"AUTO_CONFIG_INVALID");
  return a;
 }
 static void AutoSave(CadLink a){
  string tmp=AutoPath+".tmp";
  using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] bytes=Utf8.GetBytes(Json.Serialize(a));f.Write(bytes,0,bytes.Length);f.Flush(true);}
  if(File.Exists(AutoPath))File.Replace(tmp,AutoPath,AutoPath+".bak");else File.Move(tmp,AutoPath);
 }
 static object AutoSummary(State s){
  try{CadLink a=AutoLoad(s);return new {prepared=a!=null,calibrated=a!=null&&a.calibrated,enabled=a!=null&&a.enabled,fieldMode=a!=null&&a.fieldMode,fieldReady=a!=null&&a.fieldReady,drawing=a!=null&&a.calibrated?Path.GetFileName(a.drawing):null,message=a==null?"未設定":a.message};}
  catch(Fault){return new {prepared=false,calibrated=false,enabled=false,message="自動設定を読み込めません。新しいDXFで再設定してください。"};}
 }
 static string AutoMacro(CadLink a,bool setup){return LinkMacro(a,setup?"--setup":"--auto",setup);}
 static string LinkMacro(CadLink a,string action,bool setup){
  var rows=new System.Collections.Generic.List<string>(new string[]{"REM CurveDrive v3.0 - CAD LINK - NO DELETE","REM #jww","REM #cd","REM #hf","REM #h4","REM #hp","REM #bz","REM #zs"});
  if(setup)rows.Add("REM #1 Select PLAN START once (R)Read");
  rows.AddRange(new string[]{"REM #e","@echo off","setlocal EnableExtensions DisableDelayedExpansion","set \"TARGET=%~dp0JWC_TEMP.TXT\"","if not exist \"%TARGET%\" exit /b 1","copy /y \"%TARGET%\" \"%~dp0AutoInput.txt\" >nul","\"%~dp0Bridge.exe\" "+action+" \"%TARGET%\" "+a.token+" >> \"%~dp0AutoBatch.log\" 2>&1","if errorlevel 1 goto failed","exit /b 0",":failed","findstr /b /c:\"heCurveDrive:\" \"%TARGET%\" >nul 2>&1","if not errorlevel 1 exit /b 1","> \"%TARGET%\" echo heCurveDrive: link stopped. See Import.log.","exit /b 1"});
  return String.Join("\r\n",rows.ToArray())+"\r\n";
 }
 static bool AutoRoute(HttpListenerContext ctx,string method,string path){
  if(method!="POST"||(!path.StartsWith("/auto/")&&path!="/field/start"))return false;
  string origin=ctx.Request.Headers["Origin"];
  if(origin!="http://127.0.0.1:18718"&&origin!="http://localhost:18718")throw new Fault(403,"AUTO_USE_LOCAL_PAGE");
  var d=Body(ctx);
  if(path=="/field/start"){
   using(Lock()){
    State s=Load();CadLink a=AutoLoad(s);
    if(s==null||a==null||!a.calibrated)throw new Fault(409,"FIELD_FIRST_SETUP_REQUIRED");
    if(!String.Equals(Path.GetExtension(a.drawing),".jww",StringComparison.OrdinalIgnoreCase))throw new Fault(409,"FIELD_SAVE_JWW_FIRST");
    if(Text(Field(d,"session"),64)!=s.session||Text(Field(d,"site"),120)!=s.site||Text(Field(d,"planKey"),16000)!=s.planKey)throw new Fault(409,"AUTO_REQUEST_MISMATCH");
    File.WriteAllText(Path.Combine(Root,"Field_Run.bat"),LinkMacro(a,"--field",false),Encoding.ASCII);
    a.enabled=false;a.fieldMode=true;a.fieldReady=false;a.message="確認方式：CADでField_Run.batを一回選ぶと開始します。";AutoSave(a);
    Reply(ctx,200,new {ok=true,automation=AutoSummary(s),pending=s.state=="pending",station=s.station});
   }return true;
  }
  if(path=="/auto/prepare"){
   Pt end=Point(Field(d,"end"));string session=Text(Field(d,"session"),64);
   using(Lock()){
    State s=Load();if(s==null||s.session!=session||s.state=="pending")throw new Fault(409,"AUTO_BASELINE_REQUIRED");
    var a=new CadLink{schema=1,session=s.session,token=Guid.NewGuid().ToString("N"),end=end,message="発進点の登録待ち"};
    File.WriteAllText(Path.Combine(Root,"Setup_Link.bat"),AutoMacro(a,true),Encoding.ASCII);
    File.WriteAllText(Path.Combine(Root,a.Macro),AutoMacro(a,false),Encoding.ASCII);AutoSave(a);
    Reply(ctx,200,new {ok=true,automation=AutoSummary(s)});
   }return true;
  }
  if(path=="/auto/enable"){
   using(Lock()){
    State s=Load();CadLink a=AutoLoad(s);
    if(s==null||s.state=="pending"||a==null||!a.calibrated)throw new Fault(409,"AUTO_CALIBRATION_REQUIRED");
    CadNative.Find(a,false);a.fieldMode=false;a.fieldReady=false;a.enabled=true;a.message="自動連携ON";AutoSave(a);Reply(ctx,200,new {ok=true,automation=AutoSummary(s)});
   }return true;
  }
  if(path=="/auto/disable"){
   using(Lock()){State s=Load();CadLink a=AutoLoad(s);if(a!=null){a.enabled=false;a.message="自動連携OFF";AutoSave(a);}Reply(ctx,200,new {ok=true});}return true;
  }
  if(path=="/auto/apply"){
   CadLink a;State initial;
   using(Lock()){
    initial=Load();a=AutoLoad(initial);
    if(initial==null||initial.state!="pending"||a==null||!a.calibrated||!a.enabled)throw new Fault(409,"AUTO_NOT_ENABLED_OR_NO_PENDING");
    if(Text(Field(d,"session"),64)!=initial.session||!Near(Station(Field(d,"station")),initial.station))throw new Fault(409,"AUTO_REQUEST_MISMATCH");
   }
   Exception failure=null;
   var worker=new Thread(delegate(){
    try{CadNative.Execute(a,delegate(){using(Lock()){
     State current=Load();CadLink link=AutoLoad(current);
     if(current==null||current.session!=initial.session||link==null||link.token!=a.token||!link.enabled)throw new Fault(409,"AUTO_STOPPED_CHECK_STATUS");
     return Near(current.station,initial.station)&&current.state=="issued";
    }},AutoTrace);}catch(Exception e){AutoTrace("AUTO_UI_ERROR step=execute type="+e.GetType().FullName+" HRESULT=0x"+e.HResult.ToString("X8"));failure=e;}
   });
   if(Environment.OSVersion.Platform==PlatformID.Win32NT)worker.SetApartmentState(ApartmentState.STA);
   worker.IsBackground=true;worker.Start();
   if(!worker.Join(30000))failure=new Fault(409,"AUTO_RESULT_UNKNOWN_NEW_DXF_REQUIRED");
   if(failure!=null){
    AutoTrace("AUTO_APPLY_STOP "+(failure is Fault?failure.Message:"AUTO_WINDOWS_OPERATION_FAILED"));
    using(Lock()){State s=Load();CadLink current=AutoLoad(s);if(current!=null&&current.token==a.token){current.enabled=false;current.message=failure is Fault?failure.Message:"AUTO_WINDOWS_OPERATION_FAILED";AutoSave(current);}}
    throw new Fault(409,failure is Fault?failure.Message:"AUTO_WINDOWS_OPERATION_FAILED");
   }
   Reply(ctx,200,new {ok=true,state="issued",cadVerified=false});return true;
  }
  throw new Fault(404,"NOT_FOUND");
 }
 static string AutoGeometry(CadLink a,State s){
  Pt q=CadContext.Real(a,s.q),p=CadContext.Real(a,s.p);
  string cx=F(p.x),cy=F(p.y);
  return String.Join("\r\n",new string[]{"b0","lc3",F(q.x)+" "+F(q.y)+" "+cx+" "+cy,F(p.x-35)+" "+cy+" "+F(p.x+35)+" "+cy,cx+" "+F(p.y-35)+" "+cx+" "+F(p.y+35),"ci "+cx+" "+cy+" 35.0000000",""});
 }
 static int AutoCli(string action,string filename,string token){
  string target=Path.GetFullPath(filename);
  if(!String.Equals(target,Path.GetFullPath(Path.Combine(Root,"JWC_TEMP.TXT")),StringComparison.OrdinalIgnoreCase))return 2;
  try{
   if(!File.Exists(target)||new FileInfo(target).Length>16777216)throw new Fault(400,"AUTO_INPUT_MISSING_OR_LARGE");
   string input=File.ReadAllText(target,Encoding.GetEncoding(932));
   if(input.TrimStart('\ufeff','\r','\n',' ').Split('\n')[0].Trim()!="hq")throw new Fault(409,"AUTO_FRESH_CAD_INPUT_REQUIRED");
   CadContext context=CadContext.Parse(input);
   using(Lock()){
    State s=Load();CadLink a=AutoLoad(s);
    if(s==null||a==null||a.token!=token)throw new Fault(409,"AUTO_SESSION_CHANGED");
    if(action=="--setup"){
     if(s.state=="pending"||context.picked==null)throw new Fault(409,"AUTO_SETUP_NEEDS_BASELINE_AND_ORIGIN");
     var window=CadNative.FindSetup(context.drawing);
     a.drawing=context.drawing;a.x=context.picked.x;a.y=context.picked.y;a.group=context.group;a.scale=context.scales[a.group];a.width=context.width;a.height=context.height;a.pid=window.pid;a.started=window.started;
     context.Validate(a,s.p);a.calibrated=true;a.enabled=false;a.fieldMode=false;a.fieldReady=false;a.message="発進点登録済み。入力画面で作業を開始できます。";AutoSave(a);
     File.WriteAllText(target,"h#CurveDrive: origin registered. Return to HTML and start work.\r\n",Encoding.ASCII);
    }else{
     bool field=action=="--field";
     if(!a.calibrated||(field?!a.fieldMode:!a.enabled))throw new Fault(409,"AUTO_NO_PENDING_OR_DISABLED");
     if(field){
      var window=CadNative.FindSetup(context.drawing);
      context.Validate(a,s.state=="pending"?s.q:s.p);
      // Rebind only after a manual CAD invocation and full geometry/file checks.
      a.pid=window.pid;a.started=window.started;a.fieldReady=true;
      if(s.state!="pending"){
       a.message="作業中：入力してEnter → CADの再実行を1回。";AutoSave(a);
       File.WriteAllText(target,"h#CurveDrive: ready. Enter in HTML, then click Re-run in CAD.\r\n",Encoding.ASCII);return 0;
      }
     }else{
      if(s.state!="pending")throw new Fault(409,"AUTO_NO_PENDING_OR_DISABLED");
      CadNative.FindCallback(a);context.Validate(a,s.q);
     }
     string geometry=AutoGeometry(a,s);s.state="issued";s.saved=Now();Save(s);
     File.WriteAllText(target,geometry,Encoding.ASCII);
     if(File.ReadAllText(target,Encoding.ASCII)!=geometry)throw new IOException("VERIFY_FAILED");
     a.message=field?"確認方式：引渡し済み。CADの点と線を確認してください。":"自動引渡し済み。CAD描画結果は未確認。";AutoSave(a);
     Log((field?"FIELD_ISSUED station=":"AUTO_ISSUED station=")+F(s.station)+" CAD_UNVERIFIED");
    }
   }return 0;
  }catch(Exception e){
   string code=e is Fault?e.Message:"AUTO_IO_OR_NATIVE_ERROR";
   try{File.WriteAllText(target,"heCurveDrive: "+code+". See Import.log.\r\n",Encoding.ASCII);}catch{}
   try{using(Lock()){State s=Load();CadLink a=AutoLoad(s);if(a!=null){a.enabled=false;a.fieldReady=false;a.message=code;AutoSave(a);}}}catch{}
   try{Log("AUTO_ERROR "+code+" "+e.GetType().Name+" "+e.Message);}catch{}Console.Error.WriteLine(code);return 2;
  }
 }
}
