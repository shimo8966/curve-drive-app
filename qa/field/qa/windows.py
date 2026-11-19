from pathlib import Path
import sys,os,time,json,subprocess,tempfile,hashlib,shutil,urllib.request
from urllib.parse import urlsplit,parse_qs
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1];data=Path(tempfile.mkdtemp(prefix='CurveFieldData_'));out=Path(os.environ.get('CURVE_QA_OUT',str(root/'qa-out')));out.mkdir(exist_ok=True);server=None;count=0
legacy=Path(tempfile.mkdtemp(prefix="CurveFieldLegacy_"));legacy_record={"data":{"basics":{"site":"引継ぎ現場","x0":"0","y0":"0","heading":"0","pitch":"0.5","z0":"0","grade":"0"},"segs":[{"type":"S","l":"10","r":""},{"type":"L","l":"20","r":"100"},{"type":"S","l":"10","r":""},{"type":"R","l":"20","r":"50"},{"type":"S","l":"10","r":""}],"meas":[]},"savedAt":1}
(legacy/"Project.json").write_text(json.dumps(legacy_record),encoding="utf-8")

def check(message):
 global count
 count+=1;print('PASS '+message,flush=True)
def start():
 global server
 server=subprocess.Popen([str(root/'CurveDrive.exe'),'--server-only'],env=dict(os.environ,CURVEDRIVE_FIELD_DATA=str(data),CURVEDRIVE_FIELD_LEGACY=str(legacy)))
 file=data/('runtime-'+str(server.pid)+'.json')
 for _ in range(100):
  if file.exists():return json.loads(file.read_text(encoding='utf-8'))['url']
  if server.poll() is not None:raise AssertionError('Windows app process exited')
  time.sleep(.1)
 raise AssertionError('Windows app not ready')
def stop():
 global server
 if server is not None:
  server.kill();server.wait(timeout=5);server=None
def saved(predicate):
 for _ in range(100):
  try:
   record=json.loads((data/'Project.json').read_text(encoding='utf-8'))
   if predicate(record):return record
  except (OSError,ValueError,KeyError,IndexError):pass
  time.sleep(.1)
 raise AssertionError('Expected PC file save not observed')
try:
 with sync_playwright() as p:
  browser=p.chromium.launch(channel='msedge',headless=False)
  page=browser.new_page(viewport={'width':1180,'height':900},accept_downloads=True);errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
  url=start();page.goto(url);page.wait_for_function('!document.querySelector("#beginEntry").disabled')
  assert page.locator('[data-meas][data-key="s"]').count()==0;assert page.locator('#latestView').count()==0;assert page.locator('#site').input_value()=='引継ぎ現場';assert json.loads((legacy/'Project.json').read_text(encoding='utf-8'))==legacy_record;check('Legacy PC data is copied without modifying original files')
  check('Plan-only app starts without JW and without latest-point button')
  page.locator('#openChartSettings').click()
  with page.expect_download() as download:page.locator('#exportJson').click()
  assert json.loads(Path(download.value.path()).read_text(encoding='utf-8'))['meas']==[];check('Plan-only backup contains no fabricated actual measurement')
  page.locator('#closeChartSettings').click();page.locator('#modeEntry').click()
  page.locator('#addMeas').click()
  for k,v in [('s','0.5'),('left','20'),('up','-5'),('depth','2.3')]:page.locator(f'[data-meas="0"][data-key="{k}"]').fill(v)
  page.locator('[data-meas="0"][data-key="depth"]').press('Enter');page.locator('[data-meas="1"][data-key="s"]').fill('1')
  saved(lambda r:r['data']['meas'][1]['s']=='1');assert page.locator('#preview g[data-station="0.5"]').count()==1;check('PC automatically saves unfinished measurement rows as well as complete values')
  page.locator('[data-meas="1"][data-key="s"]').fill('1.5');page.close();saved(lambda r:r['data']['meas'][1]['s']=='1.5');check('Closing the app retains the latest typed value in a PC file')
  stop();url=start();context=browser.new_context(viewport={'width':1180,'height':900});page=context.new_page();page.on('pageerror',lambda e:errors.append(str(e)));page.goto(url);page.wait_for_function('!document.querySelector("#beginEntry").disabled')
  assert page.locator('[data-meas="1"][data-key="s"]').input_value()=='1.5';assert page.locator('[data-meas="0"][data-key="depth"]').input_value()=='2.3';check('New app process, new port and fresh browser context restore latest PC data without browser storage')
  page.locator('#modeEntry').click()
  for k,v in [('left','-10'),('up','6'),('depth','2.4')]:page.locator(f'[data-meas="1"][data-key="{k}"]').fill(v)
  saved(lambda r:r['data']['meas'][1]['depth']=='2.4')
  page.locator('#modeReview').click()
  page.locator('#preview g[data-station="0.5"]').click();text=page.locator('#selectedPoint').inner_text();assert all(v in text for v in ['0.5m','直線','左 20mm','下 5mm','2.3m']);check('Clicking the actual plan point shows station, line type and signed deviations')
  page.locator('#deviation [data-station="1.5"]').first.click();text=page.locator('#selectedPoint').inner_text();assert '右 10mm' in text and '上 6mm' in text;check('Deviation graph selection identifies the selected measurement, rather than the latest point')
  assert page.locator('#preview g[data-station="1.5"]').get_attribute('aria-pressed')=='true';assert page.locator('#deviation circle[data-station="1.5"].selectedMeasurement').count()==2
  page.locator('#preview g[data-station="0.5"]').click();assert page.locator('#deviation circle[data-station="0.5"].selectedMeasurement').count()==2;assert page.locator('#deviation circle[data-station="1.5"].selectedMeasurement').count()==0;check('Selection synchronizes both graphs in both directions without leaving the previous point highlighted')
  assert page.locator('#deviation .toleranceLine[data-limit="50"]').count()==1 and page.locator('#deviation .toleranceLine[data-limit="-50"]').count()==1
  page.locator('#toleranceMm').fill('30');page.locator('#toleranceMm').press('Tab');saved(lambda r:r['data'].get('toleranceMm')=='30');assert page.locator('#deviation .toleranceLine[data-limit="30"]').count()==1;assert float(page.locator('#deviation .toleranceLine[data-limit="30"]').get_attribute('y1'))>20;check('Editable positive and negative tolerance lines remain visible and save with the current project')
  page.locator('#preview g[data-station="0.5"]').focus();page.keyboard.press('Enter');assert '0.5m' in page.locator('#selectedPoint').inner_text();check('Measurement selection supports keyboard Enter')
  page.locator('#zoomIn').click();assert page.locator('#preview').get_attribute('viewBox')!='0 0 800 310';page.locator('#fit').click();check('Plan zoom and whole-plan reset work')
  box=page.locator('#preview').bounding_box();wx=box['x']+box['width']*.35;wy=box['y']+box['height']*.5
  coord_js='arg=>{const s=document.querySelector("#preview"),p=s.createSVGPoint();p.x=arg.x;p.y=arg.y;const q=p.matrixTransform(s.getScreenCTM().inverse());return [q.x,q.y]}'
  before_anchor=page.evaluate(coord_js,{'x':wx,'y':wy});before_view=list(map(float,page.locator('#preview').get_attribute('viewBox').split()));page.mouse.move(wx,wy);page.mouse.wheel(0,-160);page.wait_for_timeout(120)
  zoomed_view=list(map(float,page.locator('#preview').get_attribute('viewBox').split()));after_anchor=page.evaluate(coord_js,{'x':wx,'y':wy});assert zoomed_view[2]<before_view[2];screen_drift=page.evaluate('a=>{const s=document.querySelector("#preview"),p=s.createSVGPoint();p.x=a.anchor[0];p.y=a.anchor[1];const q=p.matrixTransform(s.getScreenCTM());return Math.hypot(q.x-a.x,q.y-a.y)}',{'anchor':before_anchor,'x':wx,'y':wy});assert screen_drift<1,{'screen_drift':screen_drift,'before':before_anchor,'after':after_anchor};page.mouse.wheel(0,160);page.wait_for_timeout(120);assert float(page.locator('#preview').get_attribute('viewBox').split()[2])>zoomed_view[2];assert page.evaluate('scrollY===0');check('Mouse wheel zooms both directions around the cursor within one screen pixel without page scrolling')
  page.mouse.move(wx,wy);page.mouse.down();page.mouse.move(wx+35,wy+12,steps=6);page.mouse.up();panned=list(map(float,page.locator('#preview').get_attribute('viewBox').split()));assert abs(panned[0]-before_view[0])>1;page.locator('#fit').click();check('Panning remains available after wheel zoom and whole-plan reset is still explicit')
  assert json.loads((data/'Project.previous.json').read_text(encoding='utf-8'))['data']['meas'];check('Automatic previous PC backup is present and readable')
  page.locator('#modeEntry').click();page.set_viewport_size({'width':390,'height':844});assert page.locator('#measTable').bounding_box()['width']<=390;page.screenshot(path=str(out/'Windows-mobile-layout.png'));check('Narrow-screen measurement cards fit a phone-width viewport')
  # Workspace: curve ranges, boundaries, shared colors, new/return and failure protection.
  assert page.locator('#preview polyline[data-kind="S"]').first.get_attribute('stroke')=='#2563eb'
  assert page.locator('#preview polyline[data-kind="L"]').get_attribute('stroke')=='#d97706'
  assert page.locator('#preview polyline[data-kind="R"]').get_attribute('stroke')=='#8b5cf6'
  assert '10〜30m' in page.locator('#segmentBands').inner_text();check('Plan colors and explicit curve station ranges distinguish all three segment types')
  page.locator('#addMeas').click()
  for k,v in [('s','10'),('left','1')]:page.locator(f'[data-meas="2"][data-key="{k}"]').fill(v)
  boundary=page.locator('#preview g[data-station="10"]');assert boundary.get_attribute('data-kind')=='boundary';boundary.click();assert '直線 → 左曲線' in page.locator('#selectedPoint').inner_text();check('Exact-boundary measured point reports the segment transition');page.locator('#modeReview').click()
  assert page.locator('#preview text').count()==0
  assert page.locator('#preview').evaluate("el=>{const all=[...el.children];return Math.max(...all.filter(x=>x.classList.contains('boundaryAnnotation')).map(x=>all.indexOf(x)))<Math.min(...all.filter(x=>x.matches('g[data-station]')).map(x=>all.indexOf(x)))}")
  page.locator('#zoomIn').click();page.locator('#preview g[data-station="10"]').focus();page.keyboard.press('Enter');assert '直線 → 左曲線' in page.locator('#selectedPoint').inner_text();page.locator('#fit').click();page.screenshot(path=str(out/'Windows-clear-actual-points.png'),full_page=True);check('Curve labels stay outside the plot and actual points remain above all plan and boundary graphics after zoom')
  for w,h in [(390,844),(360,640),(320,568),(844,390),(1024,600),(1366,768)]:
   page.set_viewport_size({'width':w,'height':h});page.wait_for_timeout(120)
   for selector in ['#preview','#deviation','#selectedPoint','#segmentBands','#toleranceMm','#quickAdd','#forecastToggle','#chartSplitter','#resetSplit']:
    box=page.locator(selector).bounding_box();assert box is not None and box['y']>=0 and box['y']+box['height']<=h+1,(w,h,selector,box)
   assert page.locator('#segmentBands').evaluate('el=>el.scrollHeight<=el.clientHeight+1 && el.scrollWidth<=el.clientWidth+1'),(w,h)
   assert page.locator('#segmentBands .segmentBand').count()==5
   assert page.locator('#preview').bounding_box()['y']<page.locator('#segmentBands').bounding_box()['y']<page.locator('#toleranceMm').bounding_box()['y']<page.locator('#deviation').bounding_box()['y']
   assert page.evaluate('document.documentElement.scrollHeight<=innerHeight+1 && document.documentElement.scrollWidth<=innerWidth+1')
   if w==360:page.screenshot(path=str(out/'Windows-dashboard-small.png'))
   if w==1366:page.screenshot(path=str(out/'Windows-dashboard-desktop.png'))
  check('Both stacked charts, five alignment bars, editable tolerance and selected-point values fit without page scrolling at six viewport sizes')
  before_data=saved(lambda r:len(r['data']['meas'])==3)['data'];before_plan=page.locator('#planPanel').bounding_box()['height'];before_dev=page.locator('#deviationPanel').bounding_box()['height'];handle=page.locator('#chartSplitter').bounding_box();hx=handle['x']+handle['width']/2;hy=handle['y']+handle['height']/2
  page.mouse.move(hx,hy);page.mouse.down();page.mouse.move(hx,hy+70,steps=10);page.mouse.up();page.wait_for_timeout(150)
  assert page.locator('#planPanel').bounding_box()['height']>before_plan+45;assert page.locator('#deviationPanel').bounding_box()['height']<before_dev-45
  after_data=saved(lambda r:r['data']['workspace'].get('planRatio',0)>.6)['data'];assert after_data['segs']==before_data['segs'] and after_data['meas']==before_data['meas'];assert page.locator('#preview g[data-station="10"]').get_attribute('aria-pressed')=='true';page.screenshot(path=str(out/'Windows-frame-resize.png'));check('Mouse dragging enlarges the plan frame and shrinks the deviation frame while preserving data and synchronized selection')
  handle=page.locator('#chartSplitter').bounding_box();page.mouse.move(handle['x']+10,handle['y']+handle['height']/2);page.mouse.down();page.mouse.move(handle['x']+10,760,steps=10);page.mouse.up();page.wait_for_timeout(100);assert page.locator('#preview').bounding_box()['height']>=50 and page.locator('#deviation').bounding_box()['height']>=50;assert page.evaluate('document.documentElement.scrollHeight<=innerHeight+1');check('Resize limits keep both diagrams visible even when dragged toward the screen edge')
  page.locator('#resetSplit').click();saved(lambda r:abs(r['data']['workspace'].get('planRatio',0)-11/21)<.001);assert abs(page.locator('#planPanel').bounding_box()['height']-before_plan)<3
  page.locator('#chartSplitter').focus();page.keyboard.press('ArrowDown');saved_ratio=saved(lambda r:r['data']['workspace'].get('planRatio',0)>.55)['data']['workspace']['planRatio'];check('Standard reset and keyboard resizing work and persist to the PC save file')
  page.set_viewport_size({'width':390,'height':844});page.locator('#modeEntry').click();page.locator('#modeReview').click();assert page.locator('#preview g[data-station="10"]').get_attribute('aria-pressed')=='true';check('Input and management switching preserves synchronized selection and current data')
  page.locator('#openChartSettings').click()
  page.locator('#colorL').evaluate("el=>{el.value='#aa4400';el.dispatchEvent(new Event('input',{bubbles:true}))}")
  first=saved(lambda r:r['data'].get('workspace',{}).get('colors',{}).get('L')=='#aa4400')['data']['workspace']['activeId']
  page.locator('#closeChartSettings').click();page.locator('#modeEntry').click()
  page.once('dialog',lambda d:d.accept('第２現場'));page.locator('#newProject').click();page.wait_for_function("document.querySelector('#currentProjectName').textContent==='第２現場'")
  assert page.locator('#toleranceMm').input_value()=='50';assert page.locator('#segBody tr').count()==0 and page.locator('#measBody tr').count()==0;assert page.locator('#projectList option').count()==2;check('New project is truly blank and previous plan/actuals remain in saved list')
  page.locator('#site').fill('第２現場・改名');saved(lambda r:r['data']['basics']['site']=='第２現場・改名');assert page.locator('#projectList option').count()==2;check('Renaming changes the current project, without creating or deleting a project')
  page.locator('#projectList').select_option(first);page.locator('#openProject').click();page.wait_for_function("document.querySelector('#currentProjectName').textContent==='引継ぎ現場'")
  assert page.locator('#toleranceMm').input_value()=='30';assert page.locator('#measBody tr').count()==3;assert page.locator('#colorL').input_value()=='#aa4400';check('Switching back restores plan, measurements and shared palette')
  page.locator('#openChartSettings').click()
  with page.expect_download() as download:page.locator('#exportAll').click()
  archive=Path(download.value.path()).read_bytes();assert len(json.loads(archive)['workspace']['projects'])==2
  page.locator('#jsonFile').set_input_files({'name':'projects.json','mimeType':'application/json','buffer':archive});page.wait_for_function("document.querySelectorAll('#projectList option').length===4");check('Whole-workspace import adds restored copies and retains existing projects');page.locator('#closeChartSettings').click()
  page.locator('#projectList').select_option(first)
  page.route('**/api/project',lambda route:route.fulfill(status=500,body='{}') if route.request.method=='POST' else route.continue_())
  before_name=page.locator('#currentProjectName').inner_text();page.locator('#openProject').click();page.wait_for_function("document.querySelector('#projectMessage').textContent.includes('中止')");assert page.locator('#currentProjectName').inner_text()==before_name;page.unroute('**/api/project');check('Failed checkpoint keeps the current project visible instead of losing its draft')
  page.locator('#openProject').click();page.wait_for_function("document.querySelector('#currentProjectName').textContent==='引継ぎ現場'");page.screenshot(path=str(out/'Windows-projects-colors.png'),full_page=True)
  page.close();context.close()
  stop();url=start();restart=browser.new_context();again=restart.new_page();again.goto(url);again.wait_for_function('!document.querySelector("#beginEntry").disabled');assert again.locator('#toleranceMm').input_value()=='30';assert again.locator('#projectList option').count()==4 and again.locator('#site').input_value()=='引継ぎ現場';check('Restart restores the selected project and the complete project catalogue');assert abs(saved(lambda r:True)['data']['workspace']['planRatio']-saved_ratio)<.001;check('Frame-size preference survives a new app process and fresh browser storage')
  again.locator('#quickAdd').click();again.locator('#quickS').fill('11');saved(lambda r:r['data']['meas'][-1]['s']=='11' and r['data']['meas'][-1]['left']=='');again.reload();again.wait_for_function('!document.querySelector("#quickAdd").disabled');again.locator('#zoomIn').click();b=again.locator('#preview').bounding_box();again.mouse.move(b['x']+b['width']*.45,b['y']+b['height']*.5);again.mouse.down();again.mouse.move(b['x']+b['width']*.45+25,b['y']+b['height']*.5+10,steps=5);again.mouse.up();quick_view=list(map(float,again.locator('#preview').get_attribute('viewBox').split()));again.locator('#quickAdd').click();assert again.locator('#quickS').input_value()=='11';assert again.locator('#quickLeft').evaluate('el=>el===document.activeElement');check('Quick-entry partial measurement survives PC save and reload, then resumes at the missing field')
  for k,v in [('quickLeft','17'),('quickUp','-2'),('quickDepth','2.5')]:again.locator('#'+k).fill(v)
  again.locator('#quickDepth').press('Enter');assert not again.locator('#quickDialog').evaluate('el=>el.open');assert again.locator('body').get_attribute('data-app-mode')=='review';assert again.evaluate('scrollY===0');assert again.locator('#preview g[data-station="11"]').get_attribute('aria-pressed')=='true';assert again.locator('#deviation circle[data-station="11"].selectedMeasurement').count()==2;saved(lambda r:r['data']['meas'][-1]['depth']=='2.5');check('Quick entry returns directly to both graphs with the added point selected and saved on PC');assert all(abs(a-b)<1e-8 for a,b in zip(quick_view,map(float,again.locator('#preview').get_attribute('viewBox').split())));check('Adding the next measurement preserves the existing plan zoom and pan position')
  count_before=again.locator('#measBody tr').count();again.locator('#quickAdd').click();again.locator('#quickClose').click();assert again.locator('#measBody tr').count()==count_before;check('Opening and closing an empty quick-entry dialog adds no blank measurement row')
  again.close();restart.close()
  stop();url=start();before=(data/'Project.json').read_bytes();loading=browser.new_context();pending=loading.new_page();pending.route('**/api/project',lambda route:route.abort());pending.goto(url);pending.wait_for_function('!document.querySelector("#boardAlert").hidden');assert '保護' in pending.locator('#boardAlert').inner_text();pending.close();loading.close();time.sleep(.3);assert (data/'Project.json').read_bytes()==before;check('Closing while initial saved data is unavailable cannot overwrite PC files with defaults')
  # A separate browser-only session uses the same distributed app without PC APIs.
  web=browser.new_context(viewport={'width':390,'height':844},accept_downloads=True);page=web.new_page();page.on('pageerror',lambda e:errors.append(str(e)));origin=urlsplit(url).scheme+'://'+urlsplit(url).netloc
  calls=[];page.on('request',lambda r:calls.append(r.url) if '/api/' in r.url else None);page.goto(origin+'/');page.wait_for_function('!document.querySelector("#beginEntry").disabled');assert page.locator('#segBody tr').count()==0;page.locator('#addSeg').click();page.locator('[data-section="0"][data-key="l"]').fill('20');page.locator('#addMeas').click();page.locator('[data-meas="0"][data-key="s"]').fill('2');page.reload();page.wait_for_function('!document.querySelector("#beginEntry").disabled');assert page.locator('[data-meas="0"][data-key="s"]').input_value()=='2';check('Web mode restores incomplete drafts without sending project data to any server')
  page.locator('#modeEntry').click();page.locator('[data-meas="0"][data-key="left"]').fill('3');page.locator('[data-section="0"][data-key="type"]').select_option('L');page.locator('[data-section="0"][data-key="r"]').fill('100');page.locator('#preview g[data-station="2"]').click();assert '左曲線' in page.locator('#selectedPoint').inner_text();check('Curve selection reports the planned curved segment and radius')
  page.locator('#modeReview').click();page.locator('#quickAdd').click();assert page.locator('#quickDialog').evaluate('el=>el.scrollWidth<=el.clientWidth+1');page.screenshot(path=str(out/'Windows-quick-entry-dialog.png'));page.locator('#quickSave').click();assert page.locator('#quickDialog').evaluate('el=>el.open') and page.locator('#measBody tr').count()==1
  page.locator('#quickS').fill('2');page.locator('#quickLeft').fill('9');page.locator('#quickSave').click();assert '重複' in page.locator('#quickMessage').inner_text();assert page.locator('#quickDialog').evaluate('el=>el.open')
  page.locator('#quickClose').click();page.locator('#quickAdd').click();assert page.locator('#quickS').input_value()=='2' and page.locator('#quickLeft').input_value()=='9';page.locator('#quickS').fill('2.5');page.locator('#quickSave').click();assert not page.locator('#quickDialog').evaluate('el=>el.open');assert page.locator('#preview g[data-station="2.5"]').count()==1;check('Quick entry validates required fields and duplicate stations before returning to the graph')
  page.locator('#quickAdd').click();page.locator('#quickS').fill('999');page.locator('#quickLeft').fill('1');page.locator('#quickSave').click();assert '範囲外' in page.locator('#quickMessage').inner_text() or '区間外' in page.locator('#quickMessage').inner_text();page.locator('#quickS').fill('3');page.locator('#quickSave').click();assert not page.locator('#quickDialog').evaluate('el=>el.open');assert page.locator('#deviation circle[data-station="3"]').count()==1;page.screenshot(path=str(out/'Windows-quick-entry-result.png'));check('Quick entry rejects out-of-plan stations and accepts blank optional up/depth fields')
  b=page.locator('#preview').bounding_box();page.mouse.move(b['x']+b['width']*.4,b['y']+b['height']*.5);page.mouse.wheel(0,-120);page.wait_for_timeout(100);view_before=list(map(float,page.locator('#preview').get_attribute('viewBox').split()));plan_before=page.locator('#preview polyline[data-kind="L"]').get_attribute('points');point_before=page.locator('#preview g[data-station="2"] circle').nth(1).evaluate('el=>[el.getAttribute("cx"),el.getAttribute("cy")]')
  page.locator('#quickAdd').click();page.locator('#quickS').fill('0');page.locator('#quickLeft').fill('-45');page.locator('#quickSave').click();assert not page.locator('#quickDialog').evaluate('el=>el.open');assert all(abs(a-b)<1e-8 for a,b in zip(view_before,map(float,page.locator('#preview').get_attribute('viewBox').split())));assert page.locator('#preview polyline[data-kind="L"]').get_attribute('points')==plan_before;assert page.locator('#preview g[data-station="2"] circle').nth(1).evaluate('el=>[el.getAttribute("cx"),el.getAttribute("cy")]')==point_before;page.screenshot(path=str(out/'Windows-preserved-view.png'));check('A new outlying measurement cannot shift the plan projection or existing measurement positions')
  page.locator('#fit').click();origin_marker=page.locator('#preview > circle');assert origin_marker.evaluate('el=>{const m=el.getScreenCTM();return el.r.baseVal.value*Math.hypot(m.a,m.b)}')<6;check('The plan origin marker remains a readable fixed screen size when zooming')
  page.wait_for_function('document.querySelector("#deviceBackupStatus").textContent.includes("保存控え")');page.add_init_script("if(sessionStorage.getItem('qaClear')==='1'){localStorage.clear();sessionStorage.removeItem('qaClear')}");page.evaluate("sessionStorage.setItem('qaClear','1')");page.reload();page.wait_for_function("document.querySelector('[data-meas][data-key=s]')?.value==='2'");check('Independent IndexedDB copy restores data when primary browser storage is removed')
  page.wait_for_function('navigator.serviceWorker.controller!==null',timeout=15000);web.set_offline(True);page.reload();assert page.locator('[data-meas="0"][data-key="left"]').input_value()=='3';web.set_offline(False);check('Installed shell and saved web data reopen offline after initial cache is ready')
  # Separate synthetic workspace: the curve ends at 3m; actual trend continues curving.
  predctx=browser.new_context(viewport={'width':1180,'height':900},accept_downloads=True);pred=predctx.new_page();pred.on('pageerror',lambda e:errors.append(str(e)));pred.on('request',lambda r:calls.append(r.url) if '/api/' in r.url else None);pred.goto(origin+'/');pred.wait_for_function('!document.querySelector("#quickAdd").disabled')
  fixture={'basics':dict(legacy_record['data']['basics'],site='予測の架空検証'),'segs':[{'type':'L','l':'3','r':'45'},{'type':'S','l':'10','r':''}],'meas':[{'s':str(s),'left':'0','up':'','depth':''} for s in [1,1.5,2,2.5,3]]}
  pred.locator('#jsonFile').set_input_files({'name':'forecast.json','mimeType':'application/json','buffer':json.dumps(fixture).encode()});pred.wait_for_function("document.querySelector('#boardSite').textContent.includes('架空検証')");pred.locator('#modeReview').click();assert pred.locator('#forecastToggle').get_attribute('aria-pressed')=='false';pred.locator('#forecastToggle').click();pred.wait_for_selector('#preview [data-forecast=plan]');assert pred.locator('#deviation [data-forecast=deviation]').count()==1;assert all(t in pred.locator('#forecastReadout').inner_text() for t in ['1m先','2m先','約44mm','終了点']);assert pred.locator('#preview g[data-station]').count()==5;check('Reference forecast continues actual curvature beyond the planned curve end and shows predicted normal offsets separately')
  pred.locator('#openChartSettings').click()
  with pred.expect_download() as download:pred.locator('#exportJson').click()
  exported=json.loads(Path(download.value.path()).read_text(encoding='utf-8'));assert len(exported['meas'])==5 and exported['forecastEnabled'] is True
  with pred.expect_download() as download:pred.locator('#exportCsv').click()
  assert len(Path(download.value.path()).read_text(encoding='utf-8-sig').splitlines())==6;pred.locator('#closeChartSettings').click();check('Forecast samples never become measured rows in saved data or CSV output')
  pred.set_viewport_size({'width':360,'height':640});pred.wait_for_timeout(120)
  for selector in ['#preview','#deviation','#forecastReadout','#segmentBands','#toleranceMm']:
   b=pred.locator(selector).bounding_box();assert b and b['y']>=0 and b['y']+b['height']<=640+1,(selector,b)
  assert pred.evaluate('document.documentElement.scrollHeight<=innerHeight+1');pred.screenshot(path=str(out/'Windows-forecast-phone.png'));pred.set_viewport_size({'width':1366,'height':768});pred.screenshot(path=str(out/'Windows-forecast-desktop.png'));check('Forecast readout, tolerance, alignment bars and both graphs remain visible at phone width')
  b=pred.locator('#preview').bounding_box();pred.mouse.move(b['x']+b['width']*.4,b['y']+b['height']*.5);pred.mouse.wheel(0,-120);pred.wait_for_timeout(100);camera=pred.locator('#preview').get_attribute('viewBox');pred.locator('#forecastToggle').click();assert pred.locator('#preview [data-forecast]').count()==0;assert pred.locator('#forecastReadout').is_hidden();assert pred.locator('#preview').get_attribute('viewBox')==camera;pred.locator('#forecastToggle').click();assert pred.locator('#preview').get_attribute('viewBox')==camera;pred.reload();pred.wait_for_function('!document.querySelector("#forecastToggle").disabled');assert pred.locator('#forecastToggle').get_attribute('aria-pressed')=='true';assert pred.locator('#preview [data-forecast=plan]').count()==1;check('Forecast toggle preserves manual zoom and its per-project preference survives reload')
  unstable=dict(fixture,basics=dict(fixture['basics'],site='不安定な架空検証'),segs=[{'type':'S','l':'20','r':''}],meas=[{'s':str(i/2),'left':str(30 if i%2 else -30),'up':'','depth':''} for i in range(7)],forecastEnabled=True)
  pred.locator('#jsonFile').set_input_files({'name':'unstable.json','mimeType':'application/json','buffer':json.dumps(unstable).encode()});pred.wait_for_function("document.querySelector('#boardSite').textContent.includes('不安定')");assert '安定' in pred.locator('#forecastReadout').inner_text();assert pred.locator('#preview [data-forecast]').count()==0 and pred.locator('#preview g[data-station]').count()==7;check('Unstable recent measurements suppress the reference forecast without hiding actual measurements')
  short=dict(unstable,basics=dict(fixture['basics'],site='少ない測点の架空検証'),meas=fixture['meas'][:4]);pred.locator('#jsonFile').set_input_files({'name':'short.json','mimeType':'application/json','buffer':json.dumps(short).encode()});pred.wait_for_function("document.querySelector('#boardSite').textContent.includes('少ない')");assert '5点' in pred.locator('#forecastReadout').inner_text();assert pred.locator('#preview [data-forecast]').count()==0;predctx.close();check('Insufficient measurement count displays a clear requirement instead of a forecast')
  assert not calls,calls;assert not errors,errors;check('No uncaught JavaScript error or project-data network request in browser-only mode')
  browser.close()
 stop();shutil.copy2(root/'CurveDrive.exe',out/'CurveDrive.exe');(out/'NativeSource.json').write_text(json.dumps({n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in ['CurveDrive.cs','dist/index.html','dist/app.js','dist/sw.js','dist/manifest.webmanifest']},indent=2));print('RESULT '+str(count)+' Windows app checks passed',flush=True)
finally:
 stop();shutil.rmtree(data,ignore_errors=True);shutil.rmtree(legacy,ignore_errors=True)
