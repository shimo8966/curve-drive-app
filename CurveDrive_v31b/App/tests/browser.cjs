'use strict';
// Linux integration QA: actual Chromium + the compiled .NET bridge under Mono.
// Simulates only the JWC_TEMP file contract; does not run Windows CMD or JW_CAD.
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {spawn,spawnSync}=require('node:child_process');
const assert=require('node:assert/strict');
const {chromium}=require('playwright');
const legacyData={basics:{site:'サンプル現場',x0:'0',y0:'0',heading:'0',pitch:'0.5',z0:'0',grade:'0'},segs:[{type:'S',l:'10',r:''},{type:'L',l:'15',r:'100'},{type:'S',l:'8',r:''},{type:'R',l:'12',r:'80'},{type:'S',l:'5',r:''}],meas:[{s:'5',left:'15',up:''},{s:'10',left:'22',up:''},{s:'10.5',left:'18',up:''}]};
const project=path.resolve(__dirname,'..');
const mono=process.env.CURVE_MONO_PATH||'';
const chrome=process.env.CURVE_CHROMIUM_PATH||'';
const origin='http://127.0.0.1:18718';
const lines=[];let passed=0;
function log(text){lines.push(text);console.log(text)}
function pass(text){passed++;log('PASS '+text)}
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function latest(){const r=await fetch(origin+'/latest');assert.equal(r.status,200);return r.json()}
async function until(predicate,label,timeout=7000){const end=Date.now()+timeout;while(Date.now()<end){if(await predicate())return;await delay(50)}throw Error('Timed out: '+label)}
async function fields(page){return page.locator('input[data-meas]').evaluateAll(nodes=>nodes.map(n=>({row:n.dataset.meas,key:n.dataset.key,value:n.value})))}
(async()=>{
 let temp,server,browser,serverOutput='';
 try{
  assert.ok(fs.existsSync(mono),'Set CURVE_MONO_PATH to a usable Mono launcher');
  assert.ok(fs.existsSync(chrome),'Set CURVE_CHROMIUM_PATH to Chromium');
  let occupied=false;try{const r=await fetch(origin+'/health',{signal:AbortSignal.timeout(500)});occupied=r.ok}catch{}
  assert.equal(occupied,false,'Port 18718 is already in use; leave the existing bridge untouched and rerun later.');
  temp=fs.mkdtempSync(path.join(os.tmpdir(),'CurveBrowser_'));
  for(const name of ['app.html','Bridge.exe'])fs.copyFileSync(path.join(project,name),path.join(temp,name));
  server=spawn(mono,[path.join(temp,'Bridge.exe')],{stdio:['ignore','pipe','pipe']});
  server.stdout.on('data',data=>serverOutput+=data);server.stderr.on('data',data=>serverOutput+=data);
  server.on('error',error=>serverOutput+=error.stack);
  await until(async()=>{if(server.exitCode!==null)throw Error('Bridge exited: '+serverOutput);try{return (await fetch(origin+'/health')).ok}catch{return false}},'bridge startup');
  pass('Actual compiled bridge starts from isolated temporary directory');
  browser=await chromium.launch({executablePath:chrome,headless:true,args:['--no-sandbox','--disable-dev-shm-usage']});
  const context=await browser.newContext({acceptDownloads:true,viewport:{width:1120,height:1000}});
  await context.addInitScript(data=>{if(!localStorage.getItem('curve-drive-v28'))localStorage.setItem('curve-drive-v28',JSON.stringify(data))},legacyData);
  const page=await context.newPage(),pageErrors=[];
  page.on('pageerror',err=>pageErrors.push(err.message));
  await page.goto(origin+'/',{waitUntil:'load'});
  await page.waitForFunction(()=>document.getElementById('bridgeStatus').textContent.includes('未登録'));
  assert.match(await page.title(),/v3\.1b/);assert.equal(await page.locator('input[data-meas][data-key="s"]').count(),3);
  assert.equal(pageErrors.length,0);pass('Visible v3.1b startup, auto connection check and initial rows have no JavaScript errors');
  await page.locator('#guidePrimary').click();
  await page.locator('#tools > summary').click();
  await page.locator('#fieldStart').click();
  await page.waitForFunction(()=>document.getElementById('setupDetails').open===true);
  assert.equal(await page.locator('#setupDetails').evaluate(n=>n.open),true);
  assert.equal((await latest()).hasBaseline,false);
  await page.locator('#setupDetails > details > summary').click();
  const downloaded=await Promise.all([page.waitForEvent('download'),page.locator('#exportDxf').click()]);
  const download=downloaded[0],name=download.suggestedFilename();
  assert.match(name,/^[A-Za-z0-9_]+\.dxf$/);assert.ok(name.length<=49);
  const dxf=fs.readFileSync(await download.path(),'utf8');assert.match(dxf,/AC1009/);assert.match(dxf,/MEAS_PT/);assert.match(dxf,/PLAN/);
  pass('DXF download has short ASCII filename and real R12 plan/measurement content');
  let baselineConfirmation='';
  page.once('dialog',async dialog=>{baselineConfirmation=dialog.message();await dialog.accept()});
  await page.locator('#bridgeBaseline').click();
  await until(async()=>{const s=await latest();return s.hasBaseline&&s.state==='ready'},'baseline registration');
  assert.match(baselineConfirmation,/テスト用コピー/);assert.match(baselineConfirmation,/10\.5m/);assert.match(baselineConfirmation,/既知測点/);
  const baseline=await latest();assert.equal(baseline.station,10.5);assert.ok(baseline.session);
  pass('Baseline UI confirmation registers station 10.5m with real server session');
  await page.locator('#addMeas').click();
  await page.locator('[data-meas="3"][data-key="s"]').fill('11');
  await page.locator('[data-meas="3"][data-key="left"]').fill('20');
  await page.locator('#bridgeSend').click();
  await page.locator('#addMeas').click();
  await until(async()=>{const s=await latest();return s.state==='pending'&&s.station===11},'Enter POST pending11');
  await page.waitForFunction(()=>document.querySelectorAll('input[data-meas][data-key="s"]').length===5);
  const pending=await latest();assert.equal(pending.previousStation,10.5);assert.equal(pending.session,baseline.session);
  assert.match(await page.locator('#bridgeStatus').textContent(),/PC受信済み/);
  assert.match(await page.locator('#bridgeStatus').textContent(),/描画は未確認/);
  pass('Advanced manual send on new 11m/+20mm row posts once and creates pending point');
  const target=path.join(temp,'JWC_TEMP.TXT');
  fs.writeFileSync(target,'hq\r\nhg0\r\nht0\r\n');
  const imported=spawnSync(mono,[path.join(temp,'Bridge.exe'),'--import',target],{encoding:'utf8'});
  assert.equal(imported.status,0,imported.stderr);const geometry=fs.readFileSync(target,'ascii');
  assert.match(geometry,/^b0\r\nlc3\r\n/);assert.match(geometry,/\r\nci /);assert.doesNotMatch(geometry,/^he|\r\ne\r\n/);
  assert.equal((await latest()).state,'issued');
  await page.locator('#fieldCheck').click();
  await page.waitForFunction(()=>document.getElementById('bridgeStatus').textContent.includes('一回引渡し済み'));
  assert.match(await page.locator('#bridgeStatus').textContent(),/描画結果は未確認/);
  pass('Actual Mono --import changes pending to issued and emits b0/line/circle handoff without deletion');
  fs.writeFileSync(target,'hq\r\nhg0\r\nht0\r\n');
  const repeated=spawnSync(mono,[path.join(temp,'Bridge.exe'),'--import',target],{encoding:'utf8'});
  assert.equal(repeated.status,2);const repeatText=fs.readFileSync(target,'ascii');
  assert.match(repeatText,/^heCurveDrive: NO_PENDING_POINT_OR_ALREADY_ISSUED/);assert.doesNotMatch(repeatText,/\r\nci /);
  pass('Repeated import is blocked with he response and emits no duplicate geometry');
  await page.locator('[data-meas="4"][data-key="s"]').fill('11.5');
  await page.locator('[data-meas="4"][data-key="left"]').fill('25');
  await page.locator('#bridgeSend').click();
  await page.locator('#addMeas').click();
  await until(async()=>{const s=await latest();return s.state==='pending'&&s.station===11.5},'next Enter pending11.5');
  await page.waitForFunction(()=>document.querySelectorAll('input[data-meas][data-key="s"]').length===6);
  pass('Next 11.5m measurement continues from the issued 11m endpoint');
  await page.reload({waitUntil:'load'});
  await page.waitForFunction(()=>document.getElementById('bridgeStatus').textContent.includes('未引渡し'));
  assert.equal(await page.locator('[data-meas="4"][data-key="s"]').inputValue(),'11.5');
  assert.equal(await page.locator('[data-meas="4"][data-key="left"]').inputValue(),'25');
  assert.equal((await latest()).state,'pending');pass('Browser reload restores measurements and real server pending state');
  const beforeFields=JSON.stringify(await fields(page)),beforeStorage=await page.evaluate(()=>localStorage.getItem('curve-drive-v28'));
  let restoreAlert='';page.once('dialog',async dialog=>{restoreAlert=dialog.message();await dialog.accept()});
  await page.locator('#jsonFile').setInputFiles({name:'Bad.json',mimeType:'application/json',buffer:Buffer.from('{"basics":{},"segs":[null],"meas":[]}')});
  await until(()=>!!restoreAlert,'invalid JSON error');
  assert.match(restoreAlert,/現在のデータは変更していません/);
  assert.equal(JSON.stringify(await fields(page)),beforeFields);assert.equal(await page.evaluate(()=>localStorage.getItem('curve-drive-v28')),beforeStorage);
  pass('Actual file-input JSON rejection leaves visible measurements and saved data unchanged');
  const advancedContext=await browser.newContext({acceptDownloads:true});
  const advancedPage=await advancedContext.newPage();
  advancedPage.on('pageerror',err=>pageErrors.push(err.message));
  await advancedPage.goto(origin+'/',{waitUntil:'load'});
  const custom=JSON.parse(beforeStorage);custom.basics.x0='123.25';custom.basics.y0='-456.75';
  custom.meas=custom.meas.filter(row=>String(row.s).trim()!=='');
  await advancedPage.evaluate(data=>localStorage.setItem('curve-drive-v28',JSON.stringify(data)),custom);
  await advancedPage.reload({waitUntil:'load'});
  await advancedPage.locator('#guidePrimary').click();
  assert.equal(await advancedPage.locator('#advancedSettings').evaluate(node=>node.open),false);
  assert.equal(await advancedPage.locator('#x0').inputValue(),'123.25');
  assert.equal(await advancedPage.locator('#y0').inputValue(),'-456.75');
  assert.match(await advancedPage.locator('#advancedStatus').textContent(),/詳細設定の指定あり/);
  pass('Closed advanced settings preserve existing nonzero X/Y after a real browser reload');
  const customDownload=await Promise.all([advancedPage.waitForEvent('download'),advancedPage.locator('#exportDxf').click()]);
  const customDxf=fs.readFileSync(await customDownload[0].path(),'utf8');
  assert.match(customDxf,/LINE\r\n\s*8\r\nPLAN\r\n\s*10\r\n123250\.0000000\r\n\s*20\r\n-456750\.0000000/);
  const backupDownload=await Promise.all([advancedPage.waitForEvent('download'),advancedPage.locator('#exportJson').click()]);
  const backup=JSON.parse(fs.readFileSync(await backupDownload[0].path(),'utf8'));
  assert.equal(backup.basics.x0,'123.25');assert.equal(backup.basics.y0,'-456.75');
  await advancedPage.locator('#jsonFile').setInputFiles({name:'Saved.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(backup))});
  await advancedPage.waitForFunction(()=>document.querySelector('#jsonFile').value==='');
  assert.equal(await advancedPage.locator('#x0').inputValue(),'123.25');
  assert.equal(await advancedPage.locator('#advancedSettings').evaluate(node=>node.open),false);
  pass('DXF and JSON preserve hidden X/Y values in metres, including actual file-input restoration');
  await advancedContext.close();
  assert.equal(pageErrors.length,0,pageErrors.join('\n'));pass('No uncaught browser JavaScript errors across integration flow');
  const screenshot=process.env.CURVE_SCREENSHOT_PATH;
  if(screenshot){await page.screenshot({path:screenshot,fullPage:true});log('Optional test screenshot created.');}
  log('Environment: '+process.platform+'; Chromium '+browser.version()+'; compiled .NET executable under Mono');
  log('LIMIT: JWC_TEMP protocol simulated. Windows CMD, Edge and JW_CAD GUI/coordinate placement were not executed.');
  log('RESULT: '+passed+' checks passed; 0 failed');
 }catch(error){log('FAIL '+error.stack);process.exitCode=1}
 finally{
  if(browser)await browser.close().catch(()=>{});
  if(server){server.kill('SIGTERM');await Promise.race([new Promise(resolve=>server.once('exit',resolve)),delay(1500)]);if(server.exitCode===null)server.kill('SIGKILL')}
  if(temp)fs.rmSync(temp,{recursive:true,force:true});
  fs.writeFileSync(path.join(__dirname,'Browser.log'),lines.join('\n')+'\n'+(process.exitCode?'BRIDGE OUTPUT:\n'+serverOutput:''));
 }
})()
