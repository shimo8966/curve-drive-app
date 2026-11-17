from pathlib import Path
import sys,os,time,json,subprocess,tempfile,hashlib,shutil,urllib.request
from urllib.parse import urlsplit,parse_qs
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1];data=Path(tempfile.mkdtemp(prefix='CurveFieldData_'));out=Path(os.environ.get('CURVE_QA_OUT',str(root/'qa-out')));out.mkdir(exist_ok=True);server=None;count=0
legacy=Path(tempfile.mkdtemp(prefix="CurveFieldLegacy_"));legacy_record={"data":{"basics":{"site":"引継ぎ現場","x0":"0","y0":"0","heading":"0","pitch":"0.5","z0":"0","grade":"0"},"segs":[{"type":"S","l":"10","r":""},{"type":"L","l":"15","r":"100"},{"type":"R","l":"10","r":"80"}],"meas":[]},"savedAt":1}
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
  with page.expect_download() as download:page.locator('#exportJson').click()
  assert json.loads(Path(download.value.path()).read_text(encoding='utf-8'))['meas']==[];check('Plan-only backup contains no fabricated actual measurement')
  page.locator('#addMeas').click()
  for k,v in [('s','0.5'),('left','20'),('up','-5'),('depth','2.3')]:page.locator(f'[data-meas="0"][data-key="{k}"]').fill(v)
  page.locator('[data-meas="0"][data-key="depth"]').press('Enter');page.locator('[data-meas="1"][data-key="s"]').fill('1')
  saved(lambda r:r['data']['meas'][1]['s']=='1');assert page.locator('#preview g[data-station="0.5"]').count()==1;check('PC automatically saves unfinished measurement rows as well as complete values')
  page.locator('[data-meas="1"][data-key="s"]').fill('1.5');page.close();saved(lambda r:r['data']['meas'][1]['s']=='1.5');check('Closing the app retains the latest typed value in a PC file')
  stop();url=start();context=browser.new_context(viewport={'width':1180,'height':900});page=context.new_page();page.on('pageerror',lambda e:errors.append(str(e)));page.goto(url);page.wait_for_function('!document.querySelector("#beginEntry").disabled')
  assert page.locator('[data-meas="1"][data-key="s"]').input_value()=='1.5';assert page.locator('[data-meas="0"][data-key="depth"]').input_value()=='2.3';check('New app process, new port and fresh browser context restore latest PC data without browser storage')
  for k,v in [('left','-10'),('up','6'),('depth','2.4')]:page.locator(f'[data-meas="1"][data-key="{k}"]').fill(v)
  saved(lambda r:r['data']['meas'][1]['depth']=='2.4')
  page.locator('#preview g[data-station="0.5"]').click();text=page.locator('#selectedPoint').inner_text();assert all(v in text for v in ['0.5m','直線','左 20mm','下 5mm','2.3m']);check('Clicking the actual plan point shows station, line type and signed deviations')
  page.locator('#deviation [data-station="1.5"]').first.click();text=page.locator('#selectedPoint').inner_text();assert '右 10mm' in text and '上 6mm' in text;check('Deviation graph selection identifies the selected measurement, rather than the latest point')
  page.locator('#preview g[data-station="0.5"]').focus();page.keyboard.press('Enter');assert '0.5m' in page.locator('#selectedPoint').inner_text();check('Measurement selection supports keyboard Enter')
  page.locator('#zoomIn').click();assert page.locator('#preview').get_attribute('viewBox')!='0 0 800 310';page.locator('#fit').click();check('Plan zoom and whole-plan reset work')
  assert json.loads((data/'Project.previous.json').read_text(encoding='utf-8'))['data']['meas'];check('Automatic previous PC backup is present and readable')
  page.set_viewport_size({'width':390,'height':844});assert page.locator('#measTable').bounding_box()['width']<=390;page.screenshot(path=str(out/'Windows-mobile-layout.png'));check('Narrow-screen measurement cards fit a phone-width viewport')
  # Workspace: curve ranges, boundaries, shared colors, new/return and failure protection.
  assert page.locator('#preview polyline[data-kind="S"]').get_attribute('stroke')=='#2563eb'
  assert page.locator('#preview polyline[data-kind="L"]').get_attribute('stroke')=='#d97706'
  assert page.locator('#preview polyline[data-kind="R"]').get_attribute('stroke')=='#8b5cf6'
  assert '10〜25m' in page.locator('#segmentBands').inner_text();check('Plan colors and explicit curve station ranges distinguish all three segment types')
  page.locator('#addMeas').click()
  for k,v in [('s','10'),('left','1')]:page.locator(f'[data-meas="2"][data-key="{k}"]').fill(v)
  boundary=page.locator('#preview g[data-station="10"]');assert boundary.get_attribute('data-kind')=='boundary';boundary.click();assert '直線 → 左曲線' in page.locator('#selectedPoint').inner_text();check('Exact-boundary measured point reports the segment transition')
  assert page.locator('#preview .boundaryAnnotation text').count()==0
  assert page.locator('#preview').evaluate("el=>{const all=[...el.children];return Math.max(...all.filter(x=>x.classList.contains('boundaryAnnotation')).map(x=>all.indexOf(x)))<Math.min(...all.filter(x=>x.matches('g[data-station]')).map(x=>all.indexOf(x)))}")
  page.locator('#zoomIn').click();page.locator('#preview g[data-station="10"]').focus();page.keyboard.press('Enter');assert '直線 → 左曲線' in page.locator('#selectedPoint').inner_text();page.locator('#fit').click();page.screenshot(path=str(out/'Windows-clear-actual-points.png'),full_page=True);check('Curve labels stay outside the plot and actual points remain above all plan and boundary graphics after zoom')
  page.locator('#colorL').evaluate("el=>{el.value='#aa4400';el.dispatchEvent(new Event('input',{bubbles:true}))}")
  first=saved(lambda r:r['data'].get('workspace',{}).get('colors',{}).get('L')=='#aa4400')['data']['workspace']['activeId']
  page.once('dialog',lambda d:d.accept('第２現場'));page.locator('#newProject').click();page.wait_for_function("document.querySelector('#currentProjectName').textContent==='第２現場'")
  assert page.locator('#segBody tr').count()==0 and page.locator('#measBody tr').count()==0;assert page.locator('#projectList option').count()==2;check('New project is truly blank and previous plan/actuals remain in saved list')
  page.locator('#site').fill('第２現場・改名');saved(lambda r:r['data']['basics']['site']=='第２現場・改名');assert page.locator('#projectList option').count()==2;check('Renaming changes the current project, without creating or deleting a project')
  page.locator('#projectList').select_option(first);page.locator('#openProject').click();page.wait_for_function("document.querySelector('#currentProjectName').textContent==='引継ぎ現場'")
  assert page.locator('#measBody tr').count()==3;assert page.locator('#colorL').input_value()=='#aa4400';check('Switching back restores plan, measurements and shared palette')
  with page.expect_download() as download:page.locator('#exportAll').click()
  archive=Path(download.value.path()).read_bytes();assert len(json.loads(archive)['workspace']['projects'])==2
  page.locator('#jsonFile').set_input_files({'name':'projects.json','mimeType':'application/json','buffer':archive});page.wait_for_function("document.querySelectorAll('#projectList option').length===4");check('Whole-workspace import adds restored copies and retains existing projects')
  page.locator('#projectList').select_option(first)
  page.route('**/api/project',lambda route:route.fulfill(status=500,body='{}') if route.request.method=='POST' else route.continue_())
  before_name=page.locator('#currentProjectName').inner_text();page.locator('#openProject').click();page.wait_for_function("document.querySelector('#projectMessage').textContent.includes('中止')");assert page.locator('#currentProjectName').inner_text()==before_name;page.unroute('**/api/project');check('Failed checkpoint keeps the current project visible instead of losing its draft')
  page.locator('#openProject').click();page.wait_for_function("document.querySelector('#currentProjectName').textContent==='引継ぎ現場'");page.screenshot(path=str(out/'Windows-projects-colors.png'),full_page=True)
  page.close();context.close()
  stop();url=start();restart=browser.new_context();again=restart.new_page();again.goto(url);again.wait_for_function('!document.querySelector("#beginEntry").disabled');assert again.locator('#projectList option').count()==4 and again.locator('#site').input_value()=='引継ぎ現場';again.close();restart.close();check('Restart restores the selected project and the complete project catalogue')
  stop();url=start();before=(data/'Project.json').read_bytes();loading=browser.new_context();pending=loading.new_page();pending.route('**/api/project',lambda route:route.abort());pending.goto(url);pending.close();loading.close();time.sleep(.3);assert (data/'Project.json').read_bytes()==before;check('Closing while initial saved data is unavailable cannot overwrite PC files with defaults')
  # A separate browser-only session uses the same distributed app without PC APIs.
  web=browser.new_context(viewport={'width':390,'height':844},accept_downloads=True);page=web.new_page();page.on('pageerror',lambda e:errors.append(str(e)));origin=urlsplit(url).scheme+'://'+urlsplit(url).netloc
  calls=[];page.on('request',lambda r:calls.append(r.url) if '/api/' in r.url else None);page.goto(origin+'/');page.wait_for_function('!document.querySelector("#beginEntry").disabled');assert page.locator('#segBody tr').count()==0;page.locator('#addSeg').click();page.locator('[data-section="0"][data-key="l"]').fill('20');page.locator('#addMeas').click();page.locator('[data-meas="0"][data-key="s"]').fill('2');page.reload();page.wait_for_function('!document.querySelector("#beginEntry").disabled');assert page.locator('[data-meas="0"][data-key="s"]').input_value()=='2';check('Web mode restores incomplete drafts without sending project data to any server')
  page.locator('[data-meas="0"][data-key="left"]').fill('3');page.locator('[data-section="0"][data-key="type"]').select_option('L');page.locator('[data-section="0"][data-key="r"]').fill('100');page.locator('#preview g[data-station="2"]').click();assert '左曲線' in page.locator('#selectedPoint').inner_text();check('Curve selection reports the planned curved segment and radius')
  page.wait_for_function('document.querySelector("#deviceBackupStatus").textContent.includes("保存控え")');page.add_init_script("if(sessionStorage.getItem('qaClear')==='1'){localStorage.clear();sessionStorage.removeItem('qaClear')}");page.evaluate("sessionStorage.setItem('qaClear','1')");page.reload();page.wait_for_function("document.querySelector('[data-meas][data-key=s]')?.value==='2'");check('Independent IndexedDB copy restores data when primary browser storage is removed')
  page.wait_for_function('navigator.serviceWorker.controller!==null',timeout=15000);web.set_offline(True);page.reload();assert page.locator('[data-meas="0"][data-key="left"]').input_value()=='3';web.set_offline(False);check('Installed shell and saved web data reopen offline after initial cache is ready')
  assert not calls,calls;assert not errors,errors;check('No uncaught JavaScript error or project-data network request in browser-only mode')
  browser.close()
 stop();shutil.copy2(root/'CurveDrive.exe',out/'CurveDrive.exe');(out/'NativeSource.json').write_text(json.dumps({n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in ['CurveDrive.cs','dist/index.html','dist/app.js','dist/sw.js','dist/manifest.webmanifest']},indent=2));print('RESULT '+str(count)+' Windows app checks passed',flush=True)
finally:
 stop();shutil.rmtree(data,ignore_errors=True);shutil.rmtree(legacy,ignore_errors=True)
