from pathlib import Path
import sys,os,time,json,subprocess,tempfile,hashlib,shutil,urllib.request
from urllib.parse import urlsplit,parse_qs
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1];data=Path(tempfile.mkdtemp(prefix='CurveFieldData_'));out=Path(os.environ.get('CURVE_QA_OUT',str(root/'qa-out')));out.mkdir(exist_ok=True);server=None;count=0

def check(message):
 global count
 count+=1;print('PASS '+message,flush=True)
def start():
 global server
 server=subprocess.Popen([str(root/'CurveDrive.exe'),'--server-only'],env=dict(os.environ,CURVEDRIVE_FIELD_DATA=str(data)))
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
  assert page.locator('[data-meas][data-key="s"]').count()==0;assert page.locator('#latestView').count()==0;check('Plan-only app starts without JW and without latest-point button')
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
  page.close();context.close()
  before=(data/'Project.json').read_bytes();loading=browser.new_context();pending=loading.new_page();pending.route('**/api/project',lambda route:route.abort());pending.goto(url);pending.close();loading.close();time.sleep(.3);assert (data/'Project.json').read_bytes()==before;check('Closing while initial saved data is unavailable cannot overwrite PC files with defaults')
  # A separate browser-only session uses the same distributed app without PC APIs.
  web=browser.new_context(viewport={'width':390,'height':844},accept_downloads=True);page=web.new_page();page.on('pageerror',lambda e:errors.append(str(e)));origin=urlsplit(url).scheme+'://'+urlsplit(url).netloc
  calls=[];page.on('request',lambda r:calls.append(r.url) if '/api/' in r.url else None);page.goto(origin+'/');page.locator('#addMeas').click();page.locator('[data-meas="0"][data-key="s"]').fill('2');page.reload();assert page.locator('[data-meas="0"][data-key="s"]').input_value()=='2';check('Web mode restores incomplete drafts without sending project data to any server')
  page.locator('[data-meas="0"][data-key="left"]').fill('3');page.locator('[data-section="0"][data-key="type"]').select_option('L');page.locator('[data-section="0"][data-key="r"]').fill('100');page.locator('#preview g[data-station="2"]').click();assert '左曲線' in page.locator('#selectedPoint').inner_text();check('Curve selection reports the planned curved segment and radius')
  page.wait_for_function('document.querySelector("#deviceBackupStatus").textContent.includes("保存控え")');page.add_init_script("if(sessionStorage.getItem('qaClear')==='1'){localStorage.clear();sessionStorage.removeItem('qaClear')}");page.evaluate("sessionStorage.setItem('qaClear','1')");page.reload();page.wait_for_function("document.querySelector('[data-meas][data-key=s]')?.value==='2'");check('Independent IndexedDB copy restores data when primary browser storage is removed')
  page.wait_for_function('navigator.serviceWorker.controller!==null',timeout=15000);web.set_offline(True);page.reload();assert page.locator('[data-meas="0"][data-key="left"]').input_value()=='3';web.set_offline(False);check('Installed shell and saved web data reopen offline after initial cache is ready')
  assert not calls,calls;assert not errors,errors;check('No uncaught JavaScript error or project-data network request in browser-only mode')
  browser.close()
 stop();shutil.copy2(root/'CurveDrive.exe',out/'CurveDrive.exe');(out/'NativeSource.json').write_text(json.dumps({n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in ['CurveDrive.cs','dist/index.html','dist/app.js','dist/sw.js','dist/manifest.webmanifest']},indent=2));print('RESULT '+str(count)+' Windows app checks passed',flush=True)
finally:
 stop();shutil.rmtree(data,ignore_errors=True)
