using System;
using System.IO;
using System.Net;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;

// C# 5 / .NET Framework 4.x. Local CAD bridge; no geometry deletion.
class Pt {
    public double x { get; set; }
    public double y { get; set; }
}
class State {
    public int schema { get; set; }
    public string session { get; set; }
    public string site { get; set; }
    public string planKey { get; set; }
    public string state { get; set; }
    public double station { get; set; }
    public double previousStation { get; set; }
    public Pt p { get; set; }
    public Pt q { get; set; }
    public double offsetMm { get; set; }
    public string saved { get; set; }
}
class Fault : Exception {
    public int Status { get; private set; }
    public Fault(int status, string code) : base(code) { Status = status; }
}
partial class Program {
    const string AppVersion="3.1";
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string StatePath = Path.Combine(Root, "State.json");
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer {MaxJsonLength=8388608};
    static readonly Encoding Utf8 = new UTF8Encoding(false);
    static string WorkId(){using(var hash=System.Security.Cryptography.SHA256.Create()){return BitConverter.ToString(hash.ComputeHash(Utf8.GetBytes(Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant()))).Replace("-","");}}
    static int Ready(){
        try{
            var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:18718/health");request.Timeout=800;request.ReadWriteTimeout=800;request.Proxy=null;
            using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Utf8)){
                var health=Json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                object version,id;
                if(health.TryGetValue("version",out version)&&health.TryGetValue("workId",out id)&&String.Equals(version as string,AppVersion,StringComparison.Ordinal)&&String.Equals(id as string,WorkId(),StringComparison.Ordinal))return 0;
                Console.Error.WriteLine("Another CurveDrive is running. Close its bridge window before switching folders.");return 2;
            }
        }catch(WebException){return 1;}catch{return 2;}
    }
    static string Now() { return DateTimeOffset.Now.ToString("o", Inv); }
    static string F(double n) { return n.ToString("0.0000000", Inv); }
    static object Field(Dictionary<string,object> d, string key) {
        object v;
        if (!d.TryGetValue(key, out v)) throw new Fault(400, "MISSING_" + key);
        return v;
    }
    static Dictionary<string,object> Map(object v) {
        var d = v as Dictionary<string,object>;
        if (d == null) throw new Fault(400, "INVALID_OBJECT");
        return d;
    }
    static string Text(object v, int max) {
        var s = v as string;
        if (String.IsNullOrWhiteSpace(s) || s.Length > max) throw new Fault(400, "INVALID_TEXT");
        return s;
    }
    static double Number(object v) {
        if (!(v is int || v is long || v is decimal || v is double || v is float))
            throw new Fault(400, "INVALID_NUMBER");
        double n = Convert.ToDouble(v, Inv);
        if (Double.IsNaN(n) || Double.IsInfinity(n) || Math.Abs(n) > 10000000)
            throw new Fault(400, "INVALID_NUMBER");
        return n;
    }
    static double Station(object v) {
        double n = Number(v);
        if (n < 0 || n > 10000) throw new Fault(400, "INVALID_STATION");
        return n;
    }
    static Pt Point(object v) {
        var d = Map(v);
        return new Pt { x = Number(Field(d,"x")), y = Number(Field(d,"y")) };
    }
    static bool Near(double a, double b) { return Math.Abs(a-b) <= 0.0000001; }
    static bool Same(Pt a, Pt b) { return a != null && b != null && Near(a.x,b.x) && Near(a.y,b.y); }
    static bool ValidNumber(double n, double limit) {
        return !Double.IsNaN(n) && !Double.IsInfinity(n) && Math.Abs(n) <= limit;
    }
    static bool ValidPoint(Pt p) { return p != null && ValidNumber(p.x,10000000) && ValidNumber(p.y,10000000); }
    static FileStream Lock() {
        DateTime deadline = DateTime.UtcNow.AddSeconds(4);
        while (true) {
            FileStream file = null;
            try {
                file = new FileStream(Path.Combine(Root,"State.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                // Explicit byte-range lock also coordinates Mono/Unix subprocesses.
                file.Lock(0,1);
                return file;
            }
            catch (IOException) {
                if (file != null) file.Dispose();
                if (DateTime.UtcNow >= deadline) throw new Fault(503,"STATE_BUSY");
                Thread.Sleep(40);
            }
        }
    }
    static State Load() {
        if (!File.Exists(StatePath)) return null;
        State s;
        try { s = Json.Deserialize<State>(File.ReadAllText(StatePath, Utf8)); }
        catch { throw new Fault(500,"STATE_INVALID_KEEP_BACKUP"); }
        Guid id;
        if (s == null || s.schema != 4 || !Guid.TryParseExact(s.session,"N",out id) || !ValidPoint(s.p) || !ValidPoint(s.q) ||
            (s.state != "ready" && s.state != "pending" && s.state != "issued") ||
            String.IsNullOrWhiteSpace(s.site) || s.site.Length > 120 || String.IsNullOrWhiteSpace(s.planKey) || s.planKey.Length > 16000 ||
            String.IsNullOrEmpty(s.saved) || !ValidNumber(s.offsetMm,10000000) ||
            !ValidNumber(s.station,10000) || s.station < 0 ||
            !ValidNumber(s.previousStation,10000) || s.previousStation < 0 ||
            (s.state == "ready" ? !Near(s.station,s.previousStation) || !Same(s.p,s.q) : s.station <= s.previousStation + 0.0000001))
            throw new Fault(500,"STATE_INVALID_KEEP_BACKUP");
        return s;
    }
    static void Save(State s) {
        string tmp = StatePath + ".tmp";
        using (var file = new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            byte[] bytes = Utf8.GetBytes(Json.Serialize(s));
            file.Write(bytes,0,bytes.Length); file.Flush(true);
        }
        if (File.Exists(StatePath)) File.Replace(tmp, StatePath, StatePath + ".bak");
        else File.Move(tmp, StatePath);
    }
    static void Log(string text) {
        File.AppendAllText(Path.Combine(Root,"Import.log"), Now()+" "+text+"\r\n", Utf8);
    }
    static bool OriginAllowed(string origin) {
        return String.IsNullOrEmpty(origin) || origin == "null" ||
            origin == "http://127.0.0.1:18718" || origin == "http://localhost:18718";
    }
    static void Reply(HttpListenerContext ctx, int status, object value) {
        byte[] data = Utf8.GetBytes(Json.Serialize(value));
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers.Add("Cache-Control","no-store");
        string origin = ctx.Request.Headers["Origin"];
        if (OriginAllowed(origin) && !String.IsNullOrEmpty(origin)) {
            ctx.Response.Headers.Add("Access-Control-Allow-Origin",origin);
            ctx.Response.Headers.Add("Vary","Origin");
            ctx.Response.Headers.Add("Access-Control-Allow-Methods","GET, POST, OPTIONS");
            ctx.Response.Headers.Add("Access-Control-Allow-Headers","Content-Type");
        }
        ctx.Response.ContentLength64 = data.Length;
        ctx.Response.OutputStream.Write(data,0,data.Length);
        ctx.Response.Close();
    }
    static Dictionary<string,object> Body(HttpListenerContext ctx,int limit=32768) {
        if (ctx.Request.ContentLength64 > limit*4L) throw new Fault(413,"TOO_LARGE");
        using (var reader = new StreamReader(ctx.Request.InputStream, Utf8)) {
            char[] chars = new char[limit+1];
            int count = reader.ReadBlock(chars,0,chars.Length);
            if (count > limit) throw new Fault(413,"TOO_LARGE");
            try { return Map(Json.DeserializeObject(new string(chars,0,count))); }
            catch (Fault) { throw; }
            catch { throw new Fault(400,"INVALID_JSON"); }
        }
    }
    static object Latest(State s) {
        if (s == null) return new { ok = true, hasBaseline = false, hasPoint = false };
        return new { ok = true, hasBaseline = true, hasPoint = s.state != "ready", state = s.state,
            site = s.site, planKey = s.planKey, session = s.session, station = s.station,
            previousStation = s.previousStation, saved = s.saved, cadVerified = ManagedVerified(s), automation = AutoSummary(s) };
    }
    static void Baseline(HttpListenerContext ctx, Dictionary<string,object> d) {
        string site = Text(Field(d,"site"),120), plan = Text(Field(d,"planKey"),16000);
        double station = Station(Field(d,"station")); Pt p = Point(Field(d,"p"));
        using (Lock()) {
            State old = Load();
            if (old != null) {
                string history = Path.Combine(Root,"History"); Directory.CreateDirectory(history);
                string file = old.session+"_"+Guid.NewGuid().ToString("N")+".json";
                File.Copy(StatePath,Path.Combine(history,file),false);
            }
            var s = new State { schema=4,session=Guid.NewGuid().ToString("N"),site=site,planKey=plan,
                state="ready",station=station,previousStation=station,p=p,q=p,saved=Now() };
            Save(s);
            Reply(ctx,200,Latest(s));
        }
    }
    static void Receive(HttpListenerContext ctx, Dictionary<string,object> d) {
        string site=Text(Field(d,"site"),120),plan=Text(Field(d,"planKey"),16000);
        string session=Text(Field(d,"session"),64);
        double station=Station(Field(d,"station")),previous=Station(Field(d,"previousStation"));
        double offset=Number(Field(d,"offsetMm"));
        Pt p=Point(Field(d,"p")),q=Point(Field(d,"q"));
        if (station <= previous + 0.0000001) throw new Fault(400,"STATION_MUST_INCREASE");
        using (Lock()) {
            State s=Load();
            if (s == null) throw new Fault(409,"BASELINE_REQUIRED");
            if (s.site != site || s.planKey != plan || s.session != session)
                throw new Fault(409,"BASELINE_MISMATCH");
            CadLink link=AutoLoad(s);
            if(link!=null&&link.fieldMode&&!link.fieldReady)throw new Fault(409,"FIELD_CAD_START_REQUIRED");
            if (s.state == "pending") {
                if (Near(s.station,station) && Near(s.previousStation,previous) && Same(s.p,p) && Same(s.q,q) && Near(s.offsetMm,offset)) {
                    Reply(ctx,200,new {ok=true,state="pending",station=s.station,timestamp=s.saved,idempotent=true});return;
                }
                throw new Fault(409,"PENDING_POINT_NOT_ISSUED");
            }
            if (station <= s.station + 0.0000001) throw new Fault(409,"POINT_ALREADY_ISSUED_OR_OLD");
            if (!Near(previous,s.station) || !Same(q,s.p)) throw new Fault(409,"PREVIOUS_POINT_MISMATCH");
            if(link!=null&&link.managed)ManagedSnapshot(link,d);
            s.state="pending";s.previousStation=previous;s.station=station;s.p=p;s.q=q;s.offsetMm=offset;s.saved=Now();
            Save(s);
            Reply(ctx,200,new {ok=true,state="pending",station=s.station,timestamp=s.saved,idempotent=false});
        }
    }
    static void Process(HttpListenerContext ctx) {
        if (!OriginAllowed(ctx.Request.Headers["Origin"])) throw new Fault(403,"ORIGIN_DENIED");
        string method=ctx.Request.HttpMethod,path=ctx.Request.Url.AbsolutePath;
        if (method=="OPTIONS") { Reply(ctx,200,new {ok=true}); return; }
        if (method=="GET" && (path=="/" || path=="/index.html" || path=="/app.html")) {
            string file=Path.Combine(Root,"app.html");
            if (!File.Exists(file)) throw new Fault(404,"HTML_MISSING");
            byte[] bytes=File.ReadAllBytes(file);
            ctx.Response.StatusCode=200;ctx.Response.ContentType="text/html; charset=utf-8";
            ctx.Response.Headers.Add("Cache-Control","no-store");ctx.Response.ContentLength64=bytes.Length;
            ctx.Response.OutputStream.Write(bytes,0,bytes.Length);ctx.Response.Close();return;
        }
        if (method=="GET" && path=="/health") { Reply(ctx,200,new {ok=true,version=AppVersion,workId=WorkId(),cadAutomation=false});return; }
        if (method=="GET" && path=="/latest") { using(Lock()) {Reply(ctx,200,Latest(Load()));}return; }
        if (GuideRoute(ctx,method,path)) return;
        if (ManagedRoute(ctx,method,path)) return;
        if (AutoRoute(ctx,method,path)) return;
        if (method=="POST" && path=="/baseline") { Baseline(ctx,Body(ctx));return; }
        if (method=="POST" && path=="/point") { Receive(ctx,Body(ctx));return; }
        throw new Fault(404,"NOT_FOUND");
    }
    static string Line(Pt a, Pt b) { return F(a.x*1000)+" "+F(a.y*1000)+" "+F(b.x*1000)+" "+F(b.y*1000); }
    static string Geometry(State s) {
        double r=.035,x=s.p.x,y=s.p.y;
        // b0 reads actual millimetres. bz would apply paper scale again.
        return String.Join("\r\n",new string[] {
            "b0", "lc3", Line(s.q,s.p),
            Line(new Pt{x=x-r,y=y},new Pt{x=x+r,y=y}),
            Line(new Pt{x=x,y=y-r},new Pt{x=x,y=y+r}),
            "ci "+F(x*1000)+" "+F(y*1000)+" "+F(r*1000), "" });
    }
    static int Import(string filename) {
        string expected=Path.GetFullPath(Path.Combine(Root,"JWC_TEMP.TXT"));
        string target=Path.GetFullPath(filename);
        if (!String.Equals(target,expected,StringComparison.OrdinalIgnoreCase)) {
            Console.Error.WriteLine("IMPORT_PATH_MISMATCH");return 2;
        }
        try {
            if (!File.Exists(target)) throw new Fault(400,"JWC_TEMP_MISSING_RUN_FROM_JWW");
            using (Lock()) {
                State s=Load();
                if (s==null || s.state!="pending") throw new Fault(409,"NO_PENDING_POINT_OR_ALREADY_ISSUED");
                string data=Geometry(s);
                // Commit 'issued' BEFORE handoff: an interrupted import must not auto-retry geometry.
                // There is no CAD acknowledgement in this protocol. This is NOT 'drawn'.
                s.state="issued";s.saved=Now();Save(s);
                File.WriteAllText(target,data,Encoding.ASCII);
                if (File.ReadAllText(target,Encoding.ASCII)!=data) throw new IOException("VERIFY_FAILED");
                Log("ISSUED session="+s.session+" station="+F(s.station)+" target="+target+" CAD_UNVERIFIED");
            }
            return 0;
        } catch (Exception e) {
            string code=e is Fault ? e.Message : "IMPORT_IO_FAILED_CHECK_LOG";
            try {File.WriteAllText(target,"heCurveDrive: "+code+". See Import.log.\r\n",Encoding.ASCII);} catch {}
            try {Log("ERROR "+code+" "+e.GetType().Name+" "+e.Message+" target="+target);} catch {}
            Console.Error.WriteLine(code);return 2;
        }
    }
    static int Main(string[] args) {
        if(args.Length==1&&args[0]=="--ready")return Ready();
        if (args.Length==3 && (args[0]=="--setup" || args[0]=="--auto" || args[0]=="--field" || args[0]=="--verify")) return AutoCli(args[0],args[1],args[2]);
        if (args.Length==2 && args[0]=="--import") return Import(args[1]);
        bool open = args.Length==1 && args[0]=="--open";
        if (args.Length!=0 && !open) {Console.Error.WriteLine("Usage: Bridge.exe [--open | --import JWC_TEMP.TXT]");return 2;}
        var listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:18718/");
        try {listener.Start();}
        catch(Exception e) {Console.Error.WriteLine("Cannot start bridge: "+e.Message);return 1;}
        Console.WriteLine("CurveDrive Bridge v3.1 http://127.0.0.1:18718/ (local only)");
        Console.WriteLine("Folder: "+Root+" | CAD drawing is not acknowledged.");
        if (open) {
            try {System.Diagnostics.Process.Start("microsoft-edge:http://127.0.0.1:18718/");}
            catch(Exception e) {Console.WriteLine("Open Edge at the URL above: "+e.Message);}
        }
        while (listener.IsListening) {
            try {
                var ctx=listener.GetContext();
                try {Process(ctx);}
                catch(Fault e) {Reply(ctx,e.Status,new {ok=false,error=e.Message});}
                catch(Exception e) {
                    Console.Error.WriteLine(e.GetType().Name+": "+e.Message);
                    try {Reply(ctx,500,new {ok=false,error="BRIDGE_ERROR_CHECK_CONSOLE"});} catch {}
                }
            } catch(HttpListenerException) {break;}
        }
        listener.Close();return 0;
    }
}
