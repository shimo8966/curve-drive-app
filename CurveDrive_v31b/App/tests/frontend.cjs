'use strict';
// Runs the delivered HTML's actual inline JavaScript. Minimal DOM/network mocks;
// this suite does not launch Windows or JW_CAD and does not establish CAD placement.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const assert=require('node:assert/strict'),{test}=require('node:test');
const legacyData={basics:{site:'サンプル現場',x0:'0',y0:'0',heading:'0',pitch:'0.5',z0:'0',grade:'0'},segs:[{type:'S',l:'10',r:''},{type:'L',l:'15',r:'100'},{type:'S',l:'8',r:''},{type:'R',l:'12',r:'80'},{type:'S',l:'5',r:''}],meas:[{s:'5',left:'15',up:''},{s:'10',left:'22',up:''},{s:'10.5',left:'18',up:''}]};
const html=fs.readFileSync(path.join(__dirname,'../app.html'),'utf8');
const scripts=[...html.matchAll(/<script\b[^>]*>([\s\S]*?)<\/script>/g)];
const startup='segRender();measRender();preview();if(recoveryNotice)';
assert.equal(scripts[1][1].split(startup).length,2,'Update the test adapter after intentional startup changes.');
const injected=scripts[1][1].replace(startup,`globalThis.app={model,advance,pointAt,samplePlan,dxf,managedDxf,parseCSV,snapshot,validateData,applyData,refresh,preview,planKey,fileName,bridgeSend,bridgeBaseline,bridgeCheck,bridgeRequest,autoAction,fieldStart,stationAlignment,updateAlignment,guideSet,guideDownload,guidePrimary,guideRead,guideRefresh,getGuide:()=>JSON.parse(JSON.stringify(guideState))};${startup}`);
function load(options={}){
 const nodes=new Map(),storage=new Map(Object.entries(options.storage||{})),requests=[],alerts=[],confirms=[],timers=[],downloads=[],blobs=new Map();
 if(!options.emptyDefault&&![...storage.keys()].some(k=>/^curve-drive-v(28|27|26|25|03|02)$/.test(k)))storage.set('curve-drive-v28',JSON.stringify(legacyData));
 let env;
 function element(id){if(!nodes.has(id)){
  const attrs=html.match(new RegExp('<input\\b[^>]*id="'+id+'"[^>]*>'))?.[0]||'';
  nodes.set(id,{value:attrs.match(/value="([^"]*)"/)?.[1]||'',style:{},disabled:false,innerHTML:'',textContent:'',className:'',listeners:{},
   addEventListener(k,fn){(this.listeners[k]??=[]).push(fn)},querySelector(){return{focus(){},scrollIntoView(){}}},click(){return this.onclick?.()}});
 }return nodes.get(id)}
 const context=vm.createContext({document:{getElementById:element,createElement:()=>({click(){downloads.push({name:this.download,href:this.href})}})},window:{addEventListener(){}},
  localStorage:{getItem:k=>storage.get(k)??null,setItem:(k,v)=>{if(options.storageFail)throw Error('quota');storage.set(k,v)}},
  location:{origin:'http://127.0.0.1:18718',href:'http://127.0.0.1:18718/'},
  setTimeout:(fn,ms)=>{const t={fn,ms,active:true};timers.push(t);return t},clearTimeout:t=>{if(t)t.active=false},
  confirm:message=>{confirms.push(message);return options.confirm!==false},alert:message=>alerts.push(message),
  fetch:async(url,opts)=>{const body=opts.body?JSON.parse(opts.body):undefined;const req={url,body,opts};requests.push(req);
   if(options.fetch)return options.fetch(req,env);
   let result=url.endsWith('health')?{ok:true,version:'3.1b'}:url.endsWith('latest')?env.latest:{ok:true,saved:'test'};
   return{ok:true,status:200,json:async()=>result};},
  AbortController,Blob,URL:class extends URL{static createObjectURL(blob){const url='blob:fixture-'+blobs.size;blobs.set(url,blob);return url}static revokeObjectURL(){}},TextDecoder,console,crypto:require('node:crypto').webcrypto});
 new vm.Script(scripts[0][1]).runInContext(context);new vm.Script(injected).runInContext(context);
 env={app:context.app,element,storage,requests,alerts,confirms,timers,downloads,blobs};
 env.latest={ok:true,hasBaseline:true,state:'ready',site:'サンプル現場',planKey:env.app.planKey(),session:'session-test',station:10,saved:'test',hasPoint:false};
 return env;
}
function clone(v){return JSON.parse(JSON.stringify(v))}
function close(a,b,t=1e-9){assert.ok(Math.abs(a-b)<=t,`${a} != ${b}`)}
function base(e){return JSON.stringify(e.app.snapshot())}
async function importJSON(e,obj){await e.element('jsonFile').onchange({target:{files:[{text:async()=>typeof obj==='string'?obj:JSON.stringify(obj)}],value:'sample'}})}
async function importCSV(e,text){await e.element('csvFile').onchange({target:{files:[{arrayBuffer:async()=>new TextEncoder().encode(text).buffer}],value:'sample'}})}
function keyEvent(e,stationRow=2,extra={}){
 const event={key:'Enter',ctrlKey:false,altKey:false,metaKey:false,isComposing:false,repeat:false,shiftKey:false,
  target:{dataset:{meas:String(stationRow),key:'up'},matches:()=>true},preventDefault(){this.prevented=true},...extra};
 return Promise.all(e.element('measBody').listeners.keydown.map(fn=>fn(event)));
}
function post(e,route){return e.requests.filter(r=>r.url.endsWith(route)&&r.body)}
function setMeasurements(e,mm){const d=clone(e.app.snapshot());d.meas=mm;e.app.applyData(e.app.validateData(d))}
function parseEntities(dxf){const lines=dxf.trim().split(/\r?\n/);let current,out=[];for(let i=0;i<lines.length;i+=2){const c=lines[i].trim(),v=lines[i+1];if(c==='0'){current={type:v};out.push(current)}else if(current)current[c]=v}return out}
test('both delivered inline scripts parse',()=>{assert.equal(scripts.length,2);for(const s of scripts)new vm.Script(s[1])});
test('delivered UI says v3.1b and has no obsolete macro instruction',()=>{assert.match(html,/<title>[^<]*v3\.1b/);assert.doesNotMatch(html,/v2\.4|01_AppendPoint_v13|Start_Bridge_NoPS|旧方式|Excel/)});
test('default geometry 10.5m and translated origin remain accurate',()=>{
 const e=load(),p=e.app.model().mm.find(p=>p.s===10.5);close(p.x,10.49990791704427);close(p.y,.019249772396300064);
 e.element('x0').value='123';e.element('y0').value='-456';const q=e.app.model().mm.find(p=>p.s===10.5);close(q.x,123+p.x);close(q.y,-456+p.y);
});
test('left and right R100 quarter circles preserve signs',()=>{const e=load(),a=e.app.advance(0,0,0,Math.PI*50,.01),b=e.app.advance(0,0,0,Math.PI*50,-.01);close(a.x,100);close(a.y,100);close(b.x,100);close(b.y,-100)});
test('default plan station40 matches independent circle-centre reference',()=>{
 const e=load();setMeasurements(e,[{s:'40',left:'20',up:''}]);const p=e.app.model().mm[0];
 close(p.x,39.81103785498893);close(p.y,3.0804728375231347);
 const points=parseEntities(e.app.dxf(e.app.model())).filter(q=>q.type==='CIRCLE'&&q[8]==='MEAS_PT');
 assert.equal(points.length,1);close(+points[0][10],p.x*1000,5.01e-8);close(+points[0][20],p.y*1000,5.01e-8);
});
test('DXF measured coordinates and red/green layers remain',()=>{const e=load(),m=e.app.model(),text=e.app.dxf(m),entities=parseEntities(text);const points=entities.filter(q=>q.type==='CIRCLE'&&q[8]==='MEAS_PT');assert.equal(points.length,3);close(+points[2][10],m.mm[2].x*1000,5.01e-8);close(+points[2][20],m.mm[2].y*1000,5.01e-8);assert.match(text,/PLAN\r\n 70\r\n0\r\n 62\r\n1/);assert.match(text,/MEAS_PT\r\n 70\r\n0\r\n 62\r\n3/)});
test('large plan is rejected before allocating its 200001 samples',()=>{const e=load(),d=clone(e.app.snapshot());d.segs=[{type:'S',l:'10000',r:''}];d.meas=[];d.basics.pitch='.05';e.app.applyData(e.app.validateData(d));assert.throws(()=>e.app.samplePlan(e.app.model()),/20000/);e.app.preview();assert.match(e.element('status').textContent,/20000/);assert.doesNotMatch(e.element('status').textContent,/stack/)});
test('sample budget counts each segment and origin',()=>{const e=load(),m=e.app.model();m.ss=[{l:10000}];m.pitch=.5;assert.throws(()=>e.app.samplePlan(m),/20000/)});
test('ASCII filenames stay short and independent from raw site identity',()=>{const e=load();e.element('site').value='日本語 現場/../../名';assert.match(e.app.fileName(),/^[A-Za-z0-9_]{1,40}$/);assert.ok(e.app.fileName().length<=40)});
test('plan key excludes pitch and measurements, normalizes numeric strings',()=>{const e=load(),key=e.app.planKey();e.element('pitch').value='1';const d=clone(e.app.snapshot());d.basics.x0='0.0';d.segs[0].l=10;d.meas=[];e.app.applyData(e.app.validateData(d));assert.equal(e.app.planKey(),key);e.element('grade').value='1';assert.notEqual(e.app.planKey(),key)});
for(const [name,mutate] of [
 ['null segment',d=>d.segs[0]=null],['unknown segment type',d=>d.segs[0].type='BAD'],['object measurement',d=>d.meas[0].left={}],['nonfinite number',d=>d.basics.x0='Infinity'],['duplicate station',d=>d.meas.push({...d.meas[0]})],['missing basics',d=>delete d.basics]
])test('JSON transaction rejects '+name+' without changing active/saved data',async()=>{const e=load();e.app.refresh();const before=base(e),saved=e.storage.get('curve-drive-v28'),d=clone(e.app.snapshot());mutate(d);await importJSON(e,d);assert.equal(base(e),before);assert.equal(e.storage.get('curve-drive-v28'),saved);assert.equal(e.alerts.length,1)});
test('valid JSON restoration commits and persists only the checked candidate',async()=>{const e=load(),d=clone(e.app.snapshot());d.basics.site=' restored ';d.meas=[{s:1,left:2,up:''}];await importJSON(e,d);assert.equal(e.alerts.length,0);assert.equal(e.app.snapshot().meas[0].s,'1');assert.equal(JSON.parse(e.storage.get('curve-drive-v28')).basics.site,' restored ')});
for(const text of ['10,abc,0','10,"18\n10.5,20,0','bad,2,0','10,2,0,extra'])test('CSV transaction preserves data for '+JSON.stringify(text),async()=>{const e=load();e.app.refresh();const before=base(e),saved=e.storage.get('curve-drive-v28');await importCSV(e,text);assert.equal(base(e),before);assert.equal(e.storage.get('curve-drive-v28'),saved);assert.equal(e.alerts.length,1)});
test('CSV normal quoting and optional vertical values parse',async()=>{const e=load();assert.equal(JSON.stringify(e.app.parseCSV('"a,b","a""b"\r\n"c\nd",e\r\n')),JSON.stringify([['a,b','a"b'],['c\nd','e']]));await importCSV(e,'推進延長_m,左右ズレ_mm_左正,上下ズレ_mm_上正\r\n1,2,\r\n2,3,4');assert.equal(e.alerts.length,0);assert.equal(e.app.model().mm[0].up,null)});
test('corrupt saved state is preserved and cannot break startup',()=>{const e=load({storage:{'curve-drive-v28':'{"segs":[null]}'}});assert.equal(e.app.model().mm.length,0);assert.match(e.element('saveStatus').textContent,/不正/);assert.equal(e.storage.get('curve-drive-v28'),'{"segs":[null]}')});
test('valid old storage recovers after corrupt new state',()=>{const data=clone(load().app.snapshot());data.basics.site='OldSite';const e=load({storage:{'curve-drive-v28':'bad','curve-drive-v03':JSON.stringify(data)}});assert.equal(e.element('site').value,'OldSite');assert.match(e.element('saveStatus').textContent,/以前の有効/)});
test('storage failure is visible',()=>{const e=load({storageFail:true});e.app.refresh();assert.match(e.element('saveStatus').textContent,/保存未完了/)});
test('baseline requires explicit confirmation; cancel sends nothing',async()=>{const e=load({confirm:false});assert.equal(await e.app.bridgeBaseline(),false);assert.equal(e.requests.length,0);assert.match(e.confirms[0],/テスト用コピー/);assert.match(e.confirms[0],/既知測点/)});
test('one-point baseline uses raw site, plan key and relative coordinate',async()=>{const e=load();setMeasurements(e,[{s:'5',left:'15',up:''}]);e.element('site').value=' 日本語現場 ';e.element('x0').value='123';e.element('y0').value='456';assert.equal(await e.app.bridgeBaseline(),true);const body=post(e,'baseline')[0].body;assert.equal(body.site,'日本語現場');assert.equal(body.station,5);close(body.p.x,5);close(body.p.y,.015);assert.equal(body.planKey,e.app.planKey())});
test('sending newest point uses registered session and previous endpoint',async()=>{const e=load();assert.equal(await e.app.bridgeSend(),true);const body=post(e,'point')[0].body;assert.equal(body.session,'session-test');assert.equal(body.station,10.5);assert.equal(body.previousStation,10);assert.equal(body.site,'サンプル現場');assert.equal(body.offsetMm,18);close(body.p.x,10.49990791704427);assert.match(e.element('bridgeStatus').textContent,/描画は未確認/)});
test('Enter on historical row does not silently send newest point',async()=>{const e=load(),before=base(e);await keyEvent(e,0);assert.equal(e.requests.length,0);assert.equal(base(e),before);assert.match(e.element('bridgeStatus').textContent,/最新の測点だけ/)});
for(const extra of [{ctrlKey:true},{altKey:true},{metaKey:true},{isComposing:true},{repeat:true}])test('Enter guard '+JSON.stringify(extra),async()=>{const e=load(),before=base(e);await keyEvent(e,2,extra);assert.equal(e.requests.length,0);assert.equal(base(e),before)});
test('Enter on latest row sends once then adds one blank row',async()=>{const e=load();await keyEvent(e);assert.equal(post(e,'point').length,1);assert.equal(e.app.snapshot().meas.length,4);assert.equal(e.app.snapshot().meas[3].s,'')});
test('Tab appends input only and does not send',async()=>{const e=load();await keyEvent(e,2,{key:'Tab',target:{dataset:{meas:'2',key:'depth'},matches:()=>true}});assert.equal(e.requests.length,0);assert.equal(e.app.snapshot().meas.length,4)});
test('one-point send asks for a point after baseline without posting',async()=>{const e=load();setMeasurements(e,[{s:'5',left:'15',up:''}]);e.latest.station=5;assert.equal(await e.app.bridgeSend(),false);assert.equal(post(e,'point').length,0);assert.match(e.element('bridgeStatus').textContent,/新しい測点/)});
test('busy guard prevents concurrent sends and baseline resets',async()=>{let release;const e=load({fetch:async(req,env)=>{if(req.url.endsWith('latest'))await new Promise(r=>release=r);return{ok:true,status:200,json:async()=>req.url.endsWith('latest')?env.latest:{ok:true}}}});const first=e.app.bridgeSend();assert.equal(await e.app.bridgeSend(),false);assert.equal(await e.app.bridgeBaseline(),false);assert.equal(e.requests.length,1);release();assert.equal(await first,true);assert.equal(post(e,'point').length,1);assert.equal(e.element('bridgeSend').disabled,false)});
test('identical pending point may be resent for server idempotency check',async()=>{const e=load();e.latest.state='pending';e.latest.station=10.5;assert.equal(await e.app.bridgeSend(),true);assert.equal(post(e,'point').length,1)});
test('pending point cannot be overwritten by a newer measurement',async()=>{const e=load();e.latest.state='pending';e.latest.station=10;assert.equal(await e.app.bridgeSend(),false);assert.equal(post(e,'point').length,0);assert.match(e.element('bridgeStatus').textContent,/未引渡し/)});
test('issued point cannot be sent again',async()=>{const e=load();e.latest.state='issued';e.latest.station=10.5;assert.equal(await e.app.bridgeSend(),false);assert.equal(post(e,'point').length,0)});
test('site and plan mismatch reject before POST',async()=>{for(const change of [e=>e.latest.site='Other',e=>e.latest.planKey='Other']){const e=load();change(e);assert.equal(await e.app.bridgeSend(),false);assert.equal(post(e,'point').length,0)}});
test('skipping an unsubmitted previous endpoint is rejected',async()=>{const e=load();e.latest.station=5;assert.equal(await e.app.bridgeSend(),false);assert.equal(post(e,'point').length,0);assert.match(e.element('bridgeStatus').textContent,/未送信/)});
test('POST network failure reports unknown acceptance, not definitely unsent',async()=>{const e=load({fetch:async(req,env)=>{if(req.body)throw Error('lost reply');return{ok:true,status:200,json:async()=>env.latest}}});assert.equal(await e.app.bridgeSend(),false);assert.match(e.element('bridgeStatus').textContent,/受信結果が不明/);assert.equal(e.element('bridgeSend').disabled,false)});
test('timeout aborts POST and carries uncertain result',async()=>{const e=load({fetch:async(req)=>new Promise((resolve,reject)=>req.opts.signal.addEventListener('abort',()=>reject(Object.assign(Error('aborted'),{name:'AbortError'}))))});const request=e.app.bridgeRequest('point',{station:1});const timer=e.timers.find(t=>t.ms===8000&&t.active);timer.fn();await assert.rejects(request,err=>err.uncertain===true&&/時間内/.test(err.message));assert.equal(timer.active,false)});
test('server 409 is a known refusal and restores controls',async()=>{const e=load({fetch:async(req,env)=>req.body?{ok:false,status:409,json:async()=>({error:'ALREADY_ISSUED'})}:{ok:true,status:200,json:async()=>env.latest}});assert.equal(await e.app.bridgeSend(),false);assert.match(e.element('bridgeStatus').textContent,/409/);assert.doesNotMatch(e.element('bridgeStatus').textContent,/受信結果が不明/);assert.equal(e.element('bridgeSend').disabled,false)});
test('issued status means handed off, CAD result remains unverified',async()=>{const e=load();e.latest.state='issued';await e.app.bridgeCheck();assert.match(e.element('bridgeStatus').textContent,/一回引渡し済み/);assert.match(e.element('bridgeStatus').textContent,/描画結果は未確認/)});

for(const [button,suffix]of [['exportJson','_Data.json'],['exportCsv','_Meas.csv']]){
 test(button+' rejects incomplete rows before creating an unrestorable download',()=>{const e=load();e.app.snapshot().meas.push({s:'11',left:'',up:''});e.element(button).onclick();assert.equal(e.downloads.length,0);assert.equal(e.element('status').className,'error');assert.match(e.element('status').textContent,/未完成の行を完成するか削除/)});
 test(button+' downloads checked valid data with ASCII filename',()=>{const e=load();e.element(button).onclick();assert.equal(e.downloads.length,1);assert.match(e.downloads[0].name,/^[A-Za-z0-9_]+\.(json|csv)$/);assert.ok(e.downloads[0].name.endsWith(suffix));assert.equal(e.element('status').className,'ok')});
}

test('CAD output converts straight50m to50000mm, offset20mm remains20mm',()=>{const e=load(),d=clone(e.app.snapshot());d.segs=[{type:'S',l:'50',r:''}];d.meas=[{s:'10.5',left:'20',up:'20'}];e.app.applyData(e.app.validateData(d));const entities=parseEntities(e.app.dxf(e.app.model()));const plan=entities.filter(q=>q.type==='LINE'&&q[8]==='PLAN');close(+plan.at(-1)[11],50000);const point=entities.find(q=>q.type==='CIRCLE'&&q[8]==='MEAS_PT');close(+point[10],10500);close(+point[20],20);close(+point[40],35);const profile=entities.filter(q=>q.type==='LINE'&&q[8]==='PROFILE');close(+profile.at(-1)[11],50000);const up=entities.find(q=>q.type==='CIRCLE'&&q[8]==='MEAS_Z');close(+up[20],-29980)});
test('v2.5 valid saved inputs migrate without scaling field measurements',()=>{const data=clone(load().app.snapshot());data.basics.site='Previous';const e=load({storage:{'curve-drive-v25':JSON.stringify(data)}});assert.equal(e.element('site').value,'Previous');close(e.app.model().mm[2].s,10.5);close(e.app.model().mm[2].left,18)});
test('v2.6 inputs including an11m test point migrate as measurements, not CAD state',()=>{const data=clone(load().app.snapshot());data.meas.push({s:'11',left:'20',up:''});const e=load({storage:{'curve-drive-v26':JSON.stringify(data)}});close(e.app.model().mm.at(-1).s,11);close(e.app.model().mm.at(-1).left,20);assert.match(e.app.planKey(),/mm-real-v28-auto/)});

test('enabled automatic link posts the point then invokes guarded CAD handoff',async()=>{const e=load();e.latest.automation={prepared:true,calibrated:true,enabled:true};assert.equal(await e.app.bridgeSend(),true);assert.equal(post(e,'point').length,1);assert.equal(post(e,'auto/apply').length,1);assert.equal(post(e,'auto/apply')[0].body.session,'session-test');assert.match(e.element('bridgeStatus').textContent,/自動引渡ししました/)});
test('automatic handoff refusal preserves accepted-input status and does not claim drawing success',async()=>{const e=load({fetch:async(req,env)=>{if(req.url.endsWith('auto/apply'))return{ok:false,status:409,json:async()=>({ok:false,error:'CAD_PLAN_ANCHORS_CHANGED'})};return{ok:true,status:200,json:async()=>req.url.endsWith('latest')?env.latest:{ok:true}}}});e.latest.automation={prepared:true,enabled:true};assert.equal(await e.app.bridgeSend(),false);assert.equal(post(e,'point').length,1);assert.match(e.element('bridgeStatus').textContent,/PC受信後に自動連携を停止/);assert.doesNotMatch(e.element('bridgeStatus').textContent,/自動引渡ししました/)});
test('pending automatic failure does not instruct a manual import on status check',async()=>{const e=load();e.latest.state='pending';e.latest.automation={prepared:true,enabled:false,message:'AUTO_ITEM_ACTION_FAILED_CHECK_STATUS'};await e.app.bridgeCheck();assert.match(e.element('bridgeStatus').textContent,/未引渡し/);assert.match(e.element('bridgeStatus').textContent,/再送や手動取り込みはせず/);assert.doesNotMatch(e.element('bridgeStatus').textContent,/Curve_Latest/)});
test('pending point received with prepared disabled link does not suggest manual execution',async()=>{const e=load();e.latest.automation={prepared:true,enabled:false};assert.equal(await e.app.bridgeSend(),true);assert.equal(post(e,'point').length,1);assert.equal(post(e,'auto/apply').length,0);assert.match(e.element('bridgeStatus').textContent,/再送・手動取り込みをせず/);assert.doesNotMatch(e.element('bridgeStatus').textContent,/Curve_Latest/)});
test('first-time work start opens the setup guide without resetting the baseline',async()=>{const e=load();e.latest.hasBaseline=false;await e.app.fieldStart();assert.equal(e.element('setupDetails').open,true);assert.equal(e.element('projectSetup').open,true);assert.equal(e.requests.filter(r=>r.body).length,0);assert.match(e.element('fieldStatus').textContent,/初回/)});
test('work start reuses matching calibrated session and does not re-register the origin',async()=>{const e=load();e.latest.automation={prepared:true,calibrated:true,fieldMode:false,drawing:'Work.jww'};await e.app.fieldStart();assert.equal(post(e,'field/start').length,1);assert.equal(post(e,'baseline').length,0);assert.equal(post(e,'auto/prepare').length,0);assert.equal(post(e,'field/start')[0].body.session,'session-test');assert.match(e.element('fieldStatus').textContent,/Field_Run/)});
test('field Enter queues once and requests a CAD confirmation, not UI automation',async()=>{const e=load();e.latest.automation={prepared:true,calibrated:true,fieldMode:true,fieldReady:true,enabled:false};await keyEvent(e);assert.equal(post(e,'point').length,1);assert.equal(post(e,'auto/apply').length,0);assert.match(e.element('fieldStatus').textContent,/再実行/);assert.equal(e.app.snapshot().meas.length,4)});
test('field input waits for the once-per-start CAD preparation',async()=>{const e=load();e.latest.automation={prepared:true,calibrated:true,fieldMode:true,fieldReady:false};assert.equal(await e.app.bridgeSend(),false);assert.equal(post(e,'point').length,0);assert.match(e.element('fieldStatus').textContent,/Field_Run/)});
test('failed field validation shows the failure rather than suggesting repeated CAD execution',async()=>{const e=load();e.latest.state='pending';e.latest.automation={prepared:true,fieldMode:true,fieldReady:false,message:'CAD_PREVIOUS_POINT_NOT_FOUND'};await e.app.bridgeCheck();assert.match(e.element('fieldStatus').textContent,/停止/);assert.match(e.element('fieldStatus').textContent,/繰り返し実行せず/)});
test('main daily panel shows the automatic mode and Enter-only guidance',async()=>{const e=load();e.latest.automation={prepared:true,calibrated:true,fieldMode:false,enabled:true,drawing:'Work.jww'};await e.app.bridgeCheck();assert.match(e.element('fieldModeStatus').textContent,/自動方式：作業中/);assert.match(e.element('fieldGuide').textContent,/再実行.*押さず/)});
test('nonzero saved coordinates survive the collapsed advanced settings and keep DXF placement',()=>{const data=clone(load().app.snapshot());Object.assign(data.basics,{x0:'123456.7',y0:'-76543.2',heading:'30',pitch:'0.25',z0:'12.3',grade:'1.5'});const e=load({storage:{'curve-drive-v28':JSON.stringify(data)}});const before=base(e);e.element('advancedSettings').open=false;e.app.refresh();assert.equal(base(e),before);assert.deepEqual(JSON.parse(e.storage.get('curve-drive-v28')).basics,data.basics);assert.match(e.element('advancedStatus').textContent,/指定あり/);const plan=parseEntities(e.app.dxf(e.app.model())).filter(q=>q.type==='LINE'&&q[8]==='PLAN');close(+plan[0][10],123456700);close(+plan[0][20],-76543200)});
test('JSON recovery retains custom coordinates and direction while advanced panel remains closed',async()=>{const e=load(),d=clone(e.app.snapshot());Object.assign(d.basics,{x0:'54321',y0:'12345',heading:'90'});e.element('advancedSettings').open=false;await importJSON(e,d);assert.equal(e.alerts.length,0);assert.equal(e.element('advancedSettings').open,false);assert.equal(e.element('x0').value,'54321');assert.equal(e.element('heading').value,'90');assert.match(e.element('advancedStatus').textContent,/発進X/)});

for(const [s,kind,label] of [[0,'S','直線'],[5,'S','直線'],[11,'L','左カーブ・R100m'],[40,'R','右カーブ・R80m'],[49,'S','直線']])test('station '+s+' has planned alignment '+label,()=>{const e=load(),a=e.app.stationAlignment(s);assert.equal(a.kind,kind);assert.equal(a.title,label)});
for(const [s,before,after]of [[10,'直線','左カーブ・R100m'],[25,'左カーブ・R100m','直線'],[33,'直線','右カーブ・R80m'],[45,'右カーブ・R80m','直線']])test('boundary '+s+' identifies both adjoining segments',()=>{const e=load(),a=e.app.stationAlignment(s);assert.equal(a.kind,'boundary');assert.equal(a.title,before+' → '+after)});
for(const s of [-.1,50.1,'bad','Infinity'])test('invalid station '+s+' is not labelled as a straight or curve',()=>{const a=load().app.stationAlignment(s);assert.equal(a.kind,'invalid')});
test('empty station asks for input and arrival station is identified',()=>{const e=load();assert.equal(e.app.stationAlignment('').kind,'empty');assert.equal(e.app.stationAlignment('  ').kind,'empty');assert.match(e.app.stationAlignment(50).detail,/到達点/)});
test('alignment classification does not require a completed measurement offset',()=>{const e=load(),d=clone(e.app.snapshot());d.meas=[{s:'11',left:'',up:''}];e.app.applyData(d);assert.equal(e.app.stationAlignment(11).title,'左カーブ・R100m');assert.throws(()=>e.app.model(),/左右ズレ/)});
test('changed radius immediately changes the displayed planned alignment',()=>{const e=load(),d=clone(e.app.snapshot());d.segs[1].r='125';e.app.applyData(e.app.validateData(d));assert.equal(e.app.stationAlignment(11).title,'左カーブ・R125m')});
test('invalid plan does not leave a stale curve label',()=>{const e=load(),d=clone(e.app.snapshot());d.segs[1].r='0';e.app.applyData(d);assert.equal(e.app.stationAlignment(11).kind,'invalid')});
test('translation and heading do not change left/right relative to forward direction',()=>{const e=load();e.element('heading').value='210';e.element('x0').value='12345';e.element('y0').value='-9876';assert.equal(e.app.stationAlignment(11).title,'左カーブ・R100m');assert.equal(e.app.stationAlignment(40).title,'右カーブ・R80m')});
test('review Enter advances input without posting a measurement or starting CAD',async()=>{const e=load();e.app.guideSet('review');await keyEvent(e);assert.equal(post(e,'point').length,0);assert.equal(post(e,'auto/apply').length,0);assert.equal(e.app.snapshot().meas.length,4);assert.match(e.element('measHint').textContent,/CADには送信しません/)});
test('guided download does not reset an existing server baseline',()=>{const e=load();e.app.guideSet('review');e.app.guideDownload();assert.equal(e.downloads.length,1);assert.match(e.downloads[0].name,/^Plan_\d{8}_\d{6}_[A-F0-9]{4}\.dxf$/);assert.equal(e.app.getGuide().phase,'open');assert.equal(e.requests.length,0)});
test('changed data invalidates the downloaded guide drawing without deleting inputs',()=>{const e=load();e.app.guideDownload();e.element('x0').value='123';e.app.refresh();assert.equal(e.app.getGuide().phase,'review');assert.equal(e.app.snapshot().basics.x0,'123');assert.equal(e.requests.length,0);assert.match(e.element('guideNotice').textContent,/入力が変わりました/)});
test('guide state from another work folder cannot continue an old setup',async()=>{const e=load({storage:{'curve-drive-guide-v1':JSON.stringify({phase:'origin',workId:'old',session:'old'})},fetch:async(req,env)=>({ok:true,status:200,json:async()=>({ok:true,version:'3.1b',workId:'new',cadReady:false,latest:{...env.latest,automation:{prepared:false}}})})});await e.app.guideRead();assert.equal(e.app.getGuide().phase,'welcome');assert.equal(e.app.getGuide().workId,'new');assert.equal(e.requests.filter(r=>r.body).length,0)});
test('calibration is required before the guide can enable automatic CAD updates',async()=>{const e=load({fetch:async(req,env)=>({ok:true,status:200,json:async()=>({ok:true,version:'3.1b',workId:'current',cadReady:false,latest:{...env.latest,automation:{prepared:true,calibrated:false}}})})});e.app.guideSet('pick');await e.app.guidePrimary();assert.match(e.element('guideNotice').textContent,/まだ発進点/);assert.equal(post(e,'auto/enable').length,0);assert.equal(e.app.getGuide().phase,'pick')});

test('working guide stops keyboard submission when the CAD binding is unavailable',async()=>{const e=load();e.app.guideSet('working');await keyEvent(e);assert.equal(post(e,'point').length,0);assert.match(e.element('guideNotice').textContent,/連携が停止/)});
test('guided download requires the site name before creating a file',()=>{const e=load();e.element('site').value='';assert.throws(()=>e.app.guideDownload(),/現場名/);assert.equal(e.downloads.length,0);assert.equal(e.requests.length,0)});

test('guide displays the actual CAD setup failure instead of claiming the point was never clicked',async()=>{const e=load({fetch:async(req,env)=>({ok:true,status:200,json:async()=>({ok:true,version:'3.1b',workId:'current',cadReady:false,latest:{...env.latest,automation:{prepared:true,calibrated:false,message:'CAD_PLAN_ANCHORS_CHANGED'}}})})});e.app.guideSet('pick');const before=base(e);await e.app.guidePrimary();assert.doesNotMatch(e.element('guideNotice').textContent,/まだ発進点|CAD_PLAN_ANCHORS_CHANGED/);assert.equal(e.element('guideSecondary').hidden,false);e.element('guideSecondary').onclick();assert.equal(e.app.getGuide().phase,'review');assert.equal(base(e),before);assert.equal(e.requests.filter(r=>r.body).length,0)});

test('managed drawings include distinct origin and end markers without changing the original DXF export',()=>{const e=load(),m=e.app.model(),before=e.app.dxf(m),entities=parseEntities(e.app.managedDxf(m));const origin=entities.filter(x=>x.type==='CIRCLE'&&x['8']==='CD_ORIGIN'),end=entities.filter(x=>x.type==='CIRCLE'&&x['8']==='CD_END');assert.equal(origin.length,1);assert.equal(end.length,1);assert.equal(Number(origin[0]['40']),47);assert.equal(Number(end[0]['40']),71);const expected=e.app.pointAt(m.total,m.ss);close(Number(end[0]['10']),expected.x*1000,1e-6);close(Number(end[0]['20']),expected.y*1000,1e-6);assert.equal(e.app.dxf(m),before)});
test('review presents automatic preparation as the main action',()=>{const e=load();e.app.guideSet('review');assert.match(e.element('guidePrimary').textContent,/JW_CADを自動で準備/);assert.match(e.element('guideDescription').textContent,/発進点の登録・外部変形の選択を自動/);assert.equal(e.downloads.length,0)});

function launchFixture(automation,cadReady=false){
 return load({storage:{'curve-drive-v28':JSON.stringify(load().app.snapshot())},fetch:async(req,env)=>{
  assert.ok(req.url.endsWith('guide/info'),'A result check must only read guide/info: '+req.url);
  return{ok:true,status:200,json:async()=>({ok:true,version:'3.1b',workId:'current',cadReady,latest:{...env.latest,automation}})};
 }});
}
test('launch result checks never resubmit a preparation or reset the entered measurements',async()=>{
 const e=launchFixture({prepared:true,managed:true,calibrated:false,enabled:false,message:'発進点の登録待ち'});e.app.guideSet('launch');const before=base(e);
 await e.app.guidePrimary();await e.app.guidePrimary();
 assert.equal(e.requests.length,2);assert.ok(e.requests.every(r=>!r.body&&r.opts.method==='GET'));assert.equal(base(e),before);assert.equal(e.downloads.length,0);
 assert.equal(e.app.getGuide().phase,'launch');assert.match(e.element('guideTitle').textContent,/準備を完了できません/);assert.doesNotMatch(e.element('guideDescription').textContent,/お待ちください/);assert.equal(e.element('guideSecondary').hidden,false);assert.match(e.element('guideSecondary').textContent,/入力を保って/);
});
test('launch result check reports a registration mismatch as stopped and preserves input',async()=>{
 const e=launchFixture({prepared:true,managed:true,calibrated:false,enabled:false,message:'AUTO_SESSION_CHANGED'});e.app.guideSet('launch');const before=base(e);
 await e.app.guidePrimary();assert.equal(e.requests.length,1);assert.equal(e.requests[0].body,undefined);assert.equal(base(e),before);assert.match(e.element('guideTitle').textContent,/準備を完了できません/);assert.match(e.element('guideNotice').textContent,/作業の登録.*一致しません/);assert.doesNotMatch(e.element('guideNotice').textContent,/AUTO_SESSION_CHANGED/);assert.equal(e.element('guideSecondary').hidden,false);
});
test('a completed preparation leaves launch and clears the old error without starting CAD again',async()=>{
 const a={prepared:true,managed:true,calibrated:false,enabled:false,message:'AUTO_SESSION_CHANGED'},e=launchFixture(a,true);e.app.guideSet('launch');const before=base(e);
 await e.app.guidePrimary();assert.match(e.element('guideNotice').textContent,/作業の登録/);Object.assign(a,{calibrated:true,enabled:true,verified:true,drawing:'Plan_fixture.dxf',message:'確認済み'});
 await e.app.guidePrimary();assert.equal(e.app.getGuide().phase,'working');assert.equal(base(e),before);assert.equal(e.requests.length,2);assert.ok(e.requests.every(r=>!r.body));assert.match(e.element('guideTitle').textContent,/準備できました/);assert.doesNotMatch(e.element('guideNotice').textContent,/一致しません|確認できません|AUTO_SESSION_CHANGED/);assert.match(e.element('guidePrimary').textContent,/次の測点/);
});
test('a fresh project has a valid plan and zero measurements, including its DXF and JSON backup',async()=>{
 const e=load({emptyDefault:true}),m=e.app.model();assert.equal(m.total,50);assert.equal(m.mm.length,0);assert.equal(e.app.snapshot().meas.length,0);
 const entities=parseEntities(e.app.dxf(m));assert.equal(entities.filter(q=>q.type==='LINE'&&q['8']==='PLAN').length,100);assert.equal(entities.filter(q=>q.type==='CIRCLE'||q['8']==='MEAS_LN').length,0);
 e.element('exportJson').onclick();assert.equal(e.downloads.length,1);const saved=JSON.parse(await e.blobs.get(e.downloads[0].href).text());assert.deepEqual(saved.meas,[]);
 const restored=load({storage:{'curve-drive-v28':JSON.stringify(saved)}});assert.equal(restored.app.model().mm.length,0);assert.equal(restored.app.dxf(restored.app.model()),e.app.dxf(m));
});
test('plan-only DXF save from review keeps the plan editable and does not register any fabricated point',()=>{
 const e=load({emptyDefault:true});e.app.guideSet('review');const before=base(e);e.element('guideSecondary').onclick();assert.equal(e.app.getGuide().phase,'review');assert.equal(base(e),before);assert.equal(e.downloads.length,1);assert.match(e.downloads[0].name,/_Plan\.dxf$/);assert.equal(e.requests.length,0);assert.match(e.element('guideNotice').textContent,/実測0件/);
});
test('plan-only automatic start sends no measurements and shows the first-measurement action',async()=>{
 const e=load({emptyDefault:true,fetch:async(req,env)=>{
  if(req.url.endsWith('managed/start')){env.latest.automation={prepared:true,managed:true,calibrated:true,enabled:true,drawing:'Plan_fixture.dxf'};return{ok:true,status:200,json:async()=>env.latest}}
  if(req.url.endsWith('managed/project'))return{ok:true,status:200,json:async()=>({ok:true,project:clone(env.app.snapshot())})};
  assert.ok(req.url.endsWith('guide/info'));return{ok:true,status:200,json:async()=>({ok:true,version:'3.1b',workId:'current',cadReady:true,latest:env.latest})};
 }});Object.assign(e.latest,{station:0,previousStation:0,hasMeasurements:false});e.app.guideSet('review');await e.app.guidePrimary();
 const starts=post(e,'managed/start');assert.equal(starts.length,1);assert.equal(starts[0].body.hasMeasurements,false);assert.equal(starts[0].body.station,0);assert.equal(starts[0].body.p.x,0);assert.equal(starts[0].body.p.y,0);assert.equal(starts[0].body.project.meas.length,0);assert.equal(e.app.snapshot().meas.length,0);assert.equal(e.app.getGuide().phase,'working');assert.match(e.element('guidePrimary').textContent,/最初の実測/);
});
test('first0.5m and second1m sends preserve the plan and never insert a fake zero measurement',async()=>{
 const e=load({emptyDefault:true,fetch:async(req,env)=>{
  if(req.url.endsWith('managed/point')){Object.assign(env.latest,{station:req.body.station,hasMeasurements:true,state:'pending'});return{ok:true,status:200,json:async()=>({ok:true})}}
  if(req.url.endsWith('auto/apply')){env.latest.state='issued';return{ok:true,status:200,json:async()=>({ok:true,cadVerified:true})}}
  assert.ok(req.url.endsWith('latest'));return{ok:true,status:200,json:async()=>clone(env.latest)};
 }});Object.assign(e.latest,{station:0,previousStation:0,hasMeasurements:false,automation:{prepared:true,managed:true,calibrated:true,enabled:true}});const plan=e.app.planKey();
 setMeasurements(e,[{s:'0.5',left:'20',up:'',depth:'2.5'}]);assert.equal(await e.app.bridgeSend(.5),true);let sent=post(e,'managed/point')[0].body;
 assert.equal(sent.previousStation,0);assert.equal(sent.station,.5);close(sent.q.x,0);close(sent.q.y,0);close(sent.p.x,.5);close(sent.p.y,.02);assert.equal(sent.project.meas.length,1);assert.equal(e.app.snapshot().meas.some(q=>Number(q.s)===0),false);
 setMeasurements(e,[{s:'0.5',left:'20',up:'',depth:'2.5'},{s:'1',left:'-10',up:'',depth:'2.6'}]);assert.equal(await e.app.bridgeSend(1),true);sent=post(e,'managed/point')[1].body;
 assert.equal(sent.previousStation,.5);assert.equal(sent.station,1);close(sent.q.x,.5);close(sent.q.y,.02);close(sent.p.x,1);close(sent.p.y,-.01);assert.equal(sent.project.meas.length,2);assert.equal(e.app.planKey(),plan);assert.equal(post(e,'auto/apply').length,2);assert.equal(e.app.model().mm.length,2);
});
test('depth in metres survives JSON and CSV without becoming a vertical deviation',async()=>{
 const e=load({emptyDefault:true});setMeasurements(e,[{s:'0.5',left:'20',up:'',depth:'2.5'}]);const point=e.app.model().mm[0];assert.equal(point.depth,2.5);assert.equal(point.up,null);
 const entities=parseEntities(e.app.dxf(e.app.model()));assert.equal(entities.filter(q=>q.type==='CIRCLE'&&q['8']==='MEAS_Z').length,0);
 e.element('exportJson').onclick();let saved=JSON.parse(await e.blobs.get(e.downloads.at(-1).href).text());assert.equal(saved.meas[0].depth,'2.5');assert.equal(saved.meas[0].up,'');
 e.element('exportCsv').onclick();const csv=await e.blobs.get(e.downloads.at(-1).href).text();assert.match(csv,/深さ_m/);await importCSV(e,csv);assert.equal(e.app.model().mm[0].depth,2.5);assert.equal(e.app.model().mm[0].up,null);
 const wrong=clone(saved);wrong.meas[0].depth='-1';assert.throws(()=>e.app.validateData(wrong),/深さ/);
});
test('an explicitly measured first station0 is accepted once and becomes the real prior point for0.5m',async()=>{
 const e=load({emptyDefault:true,fetch:async(req,env)=>{
  if(req.url.endsWith('managed/point')){Object.assign(env.latest,{station:req.body.station,hasMeasurements:true,state:'pending'});return{ok:true,status:200,json:async()=>({ok:true})}}
  if(req.url.endsWith('auto/apply')){env.latest.state='issued';return{ok:true,status:200,json:async()=>({ok:true,cadVerified:true})}}
  assert.ok(req.url.endsWith('latest'));return{ok:true,status:200,json:async()=>clone(env.latest)};
 }});Object.assign(e.latest,{station:0,previousStation:0,hasMeasurements:false,automation:{prepared:true,managed:true,calibrated:true,enabled:true}});
 setMeasurements(e,[{s:'0',left:'7',up:'',depth:'2.4'}]);assert.equal(await e.app.bridgeSend(0),true);let body=post(e,'managed/point')[0].body;
 assert.equal(body.station,0);assert.equal(body.previousStation,0);close(body.p.x,0);close(body.p.y,.007);assert.equal(body.project.meas.length,1);assert.equal(body.project.meas[0].left,'7');
 assert.equal(await e.app.bridgeSend(0),false);assert.equal(post(e,'managed/point').length,1);
 setMeasurements(e,[{s:'0',left:'7',up:'',depth:'2.4'},{s:'0.5',left:'8',up:'',depth:'2.5'}]);assert.equal(await e.app.bridgeSend(.5),true);body=post(e,'managed/point')[1].body;
 assert.equal(body.previousStation,0);assert.equal(body.station,.5);close(body.q.x,0);close(body.q.y,.007);close(body.p.x,.5);close(body.p.y,.008);assert.equal(body.project.meas.length,2);
});
test('an empty browser restores a PC-saved plan-only project without inventing measurements or re-preparing CAD',async()=>{
 const saved=clone(legacyData);saved.basics.site='Saved before work';saved.segs=[{type:'S',l:'65',r:''}];saved.meas=[];
 const e=load({emptyDefault:true,fetch:async(req,env)=>{
  assert.equal(req.body,undefined);
  if(req.url.endsWith('managed/project'))return{ok:true,status:200,json:async()=>({ok:true,project:clone(saved)})};
  assert.ok(req.url.endsWith('guide/info'));return{ok:true,status:200,json:async()=>({ok:true,version:'3.1b',workId:'current',cadReady:true,latest:env.latest})};
 }});Object.assign(e.latest,{site:saved.basics.site,planKey:JSON.stringify({cadUnits:'mm-real-v28-auto',x0:0,y0:0,heading:0,z0:0,grade:0,segs:[{type:'S',l:65,r:0}]}),station:0,hasMeasurements:false,automation:{prepared:true,managed:true,calibrated:true,enabled:true,drawing:'Plan_fixture.dxf'}});
 await e.app.guideRead();assert.equal(e.app.model().total,65);assert.equal(e.app.model().mm.length,0);assert.deepEqual(clone(e.app.snapshot()),saved);assert.deepEqual(JSON.parse(e.storage.get('curve-drive-v28')),saved);assert.equal(e.app.getGuide().phase,'working');assert.match(e.element('guideTitle').textContent,/実測は未登録/);assert.match(e.element('guidePrimary').textContent,/最初の実測/);assert.equal(e.requests.length,2);assert.equal(e.requests.filter(r=>r.body).length,0);
 await e.app.guidePrimary();assert.equal(e.app.snapshot().meas.length,1);assert.equal(e.app.snapshot().meas[0].s,'');assert.equal(e.app.model().mm.length,0);assert.equal(e.requests.length,2);
});
test('restoring a same-plan empty-measurement backup during work returns to review without changing CAD registration',async()=>{
 const e=launchFixture({prepared:true,managed:true,calibrated:true,enabled:true,drawing:'Plan_fixture.dxf',message:'確認済み'},true);await e.app.guideRead();assert.equal(e.app.getGuide().phase,'working');const data=clone(e.app.snapshot());data.meas=[];const session=e.latest.session;
 await importJSON(e,data);assert.equal(e.app.getGuide().phase,'review');assert.equal(e.app.model().mm.length,0);assert.equal(e.latest.session,session);assert.equal(e.requests.filter(r=>r.body).length,0);assert.match(e.element('guideNotice').textContent,/復元.*新しい作業図面/);
});
test('CSV replacement during work returns to review and keeps depth separate from vertical deviation',async()=>{
 const e=launchFixture({prepared:true,managed:true,calibrated:true,enabled:true,drawing:'Plan_fixture.dxf',message:'確認済み'},true);await e.app.guideRead();assert.equal(e.app.getGuide().phase,'working');const session=e.latest.session;
 await importCSV(e,'0.5,20,,2.4\n1,-10,,2.5');assert.equal(e.app.getGuide().phase,'review');assert.equal(e.app.model().mm.length,2);assert.equal(e.app.model().mm[0].depth,2.4);assert.equal(e.app.model().mm[0].up,null);assert.equal(e.latest.session,session);assert.equal(e.requests.filter(r=>r.body).length,0);assert.match(e.element('guideNotice').textContent,/置き換え.*新しい作業図面/);
});
test('an invalid imported backup leaves working state and saved measurements intact',async()=>{
 const e=launchFixture({prepared:true,managed:true,calibrated:true,enabled:true,drawing:'Plan_fixture.dxf',message:'確認済み'},true);await e.app.guideRead();const before=base(e),saved=e.storage.get('curve-drive-v28'),bad=clone(e.app.snapshot());bad.meas=[null];
 await importJSON(e,bad);assert.equal(e.app.getGuide().phase,'working');assert.equal(base(e),before);assert.equal(e.storage.get('curve-drive-v28'),saved);assert.equal(e.alerts.length,1);assert.equal(e.requests.filter(r=>r.body).length,0);
});
