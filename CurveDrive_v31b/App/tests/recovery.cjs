'use strict';
// Real browser and delivered HTML; HTTP/CAD state is a controlled fixture.
// This verifies recovery UI and request semantics, not native CAD execution.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('playwright');
const html=fs.readFileSync(path.join(__dirname,'../app.html'),'utf8');
const origin='http://127.0.0.1:18718';
const data={basics:{site:'CI recovery',x0:'0',y0:'0',heading:'0',pitch:'0.5',z0:'0',grade:'0'},segs:[{type:'S',l:'50',r:''}],meas:[{s:'10',left:'0',up:''}]};
const key=JSON.stringify({cadUnits:'mm-real-v28-auto',x0:0,y0:0,heading:0,z0:0,grade:0,segs:[{type:'S',l:50,r:0}]});
let passed=0;
function pass(message){passed++;console.log('PASS '+message)}
(async()=>{
 let browser;
 try{
  browser=await chromium.launch({executablePath:process.env.CURVE_CHROMIUM_PATH||'/usr/bin/chromium',headless:true,args:['--no-sandbox','--disable-dev-shm-usage']});
  const context=await browser.newContext({viewport:{width:700,height:900}}),page=await context.newPage();
  const errors=[],requests=[];page.on('pageerror',e=>errors.push(e.message));
  let ready=false;
  function latest(){return{ok:true,hasBaseline:true,hasPoint:false,state:'ready',site:data.basics.site,planKey:key,session:ready?'current-ready':'current-waiting',station:10,previousStation:10,cadVerified:ready,automation:{prepared:true,managed:true,calibrated:ready,enabled:ready,verified:ready,drawing:ready?'Plan_fixture.dxf':null,message:ready?'作業図面と実測終点を確認済み':'AUTO_SESSION_CHANGED'}}}
  await context.addInitScript(({data})=>{
   localStorage.setItem('curve-drive-v28',JSON.stringify(data));
   localStorage.setItem('curve-drive-guide-v1',JSON.stringify({phase:'launch',workId:'recovery-fixture',requestId:'1'.repeat(32),snapshot:JSON.stringify(data)}));
  },{data});
  await page.route('**/*',async route=>{
   const request=route.request(),url=new URL(request.url());
   if(url.origin!==origin)return route.abort();
   if(url.pathname==='/')return route.fulfill({status:200,contentType:'text/html',body:html});
   requests.push({path:url.pathname,method:request.method(),body:request.postData()?JSON.parse(request.postData()):null});
   if(url.pathname==='/guide/info')return route.fulfill({json:{ok:true,version:'3.1b',workId:'recovery-fixture',folder:'C:\\Synthetic\\App\\',cadReady:ready,latest:latest()}});
   if(url.pathname==='/managed/start'){
    ready=true;return route.fulfill({json:latest()});
   }
   if(url.pathname==='/latest')return route.fulfill({json:latest()});
   return route.fulfill({status:404,json:{ok:false,error:'UNEXPECTED_FIXTURE_REQUEST'}});
  });
  await page.goto(origin);
  await page.waitForFunction(()=>document.querySelector('#guideConnection').textContent==='PCに接続済み');
  assert.match(await page.locator('#guideTitle').innerText(),/準備を完了できません/);
  assert.doesNotMatch(await page.locator('#guideDescription').innerText(),/お待ちください/);
  assert.match(await page.locator('#guideNotice').innerText(),/作業の登録.*一致しません/);
  assert.equal(await page.locator('#guideSecondary').isVisible(),true);
  pass('Restored launch failure shows a stopped state and a clear preserve-input recovery action');
  const saved=await page.evaluate(()=>localStorage.getItem('curve-drive-v28'));
  for(let attempt=0;attempt<2;attempt++){
   const response=page.waitForResponse(r=>new URL(r.url()).pathname==='/guide/info');
   await page.locator('#guidePrimary').click();await response;
   await page.waitForFunction(()=>!document.querySelector('#guidePrimary').disabled);
  }
  assert.equal(requests.filter(r=>r.method==='POST').length,0);
  assert.equal(await page.evaluate(()=>localStorage.getItem('curve-drive-v28')),saved);
  pass('Repeated result checks only read status and retain all entered data');
  ready=true;
  await page.locator('#guidePrimary').click();
  await page.waitForFunction(()=>document.body.dataset.guide==='working');
  assert.match(await page.locator('#guideTitle').innerText(),/準備できました/);
  assert.doesNotMatch(await page.locator('#guideNotice').innerText(),/一致しません|確認できません/);
  assert.equal(requests.filter(r=>r.method==='POST').length,0);
  assert.equal(await page.locator('[data-meas="0"][data-key="s"]').inputValue(),'10');
  pass('A later successful status moves directly to input without starting CAD again');
  ready=false;await page.reload();
  await page.waitForFunction(()=>document.querySelector('#guideConnection').textContent==='PCに接続済み');
  await page.locator('#guideSecondary').click();
  await page.waitForFunction(()=>document.body.dataset.guide==='working');
  const posts=requests.filter(r=>r.method==='POST');assert.equal(posts.length,1);assert.equal(posts[0].path,'/managed/start');
  assert.notEqual(posts[0].body.requestId,'1'.repeat(32));assert.deepEqual(posts[0].body.project,data);assert.equal(posts[0].body.station,10);assert.match(posts[0].body.dxf,/CD_ORIGIN/);assert.match(posts[0].body.dxf,/CD_END/);
  assert.equal(await page.evaluate(()=>localStorage.getItem('curve-drive-v28')),saved);
  pass('Only the explicit recovery button starts a fresh job with the preserved input');
  assert.deepEqual(errors,[]);pass('No uncaught browser errors during failure, status checks and recovery');
  console.log('RESULT '+passed+' checks passed; 0 failed. CAD readiness is mocked; native execution is tested separately.');
 }catch(e){console.error(e.stack);process.exitCode=1}
 finally{if(browser)await browser.close()}
})();
