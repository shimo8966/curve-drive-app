"""Plan-only preparation and real first measurements, exclusively through the UI."""
import sys,json,time
from pathlib import Path
from playwright.sync_api import sync_playwright

folder=Path.cwd();mode=sys.argv[1]
data={'basics':{'site':'CI plan only','x0':'0','y0':'0','heading':'0','pitch':'0.5','z0':'0','grade':'0'},'segs':[{'type':'S','l':'50','r':''}],'meas':[]}
with sync_playwright() as p:
    browser=p.chromium.launch(channel='msedge',headless=False)
    page=browser.new_page(viewport={'width':800,'height':750},accept_downloads=True)
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto('http://127.0.0.1:18718/')
    if mode=='new':
        page.evaluate('data=>{localStorage.setItem("curve-drive-v28",JSON.stringify(data));localStorage.removeItem("curve-drive-guide-v1")}',data);page.reload()
    page.wait_for_function('document.querySelector("#guideConnection").textContent === "PCに接続済み"')
    if mode=='resume':
        restored=json.loads(page.evaluate('localStorage.getItem("curve-drive-v28")'))
        assert restored['meas']==[] and restored['basics']['site']=='CI plan only'
    page.locator('#guidePrimary').click()
    if mode=='new':page.locator('#guidePrimary').click()
    try:
        page.wait_for_function('document.body.dataset.guide==="working" && document.querySelector("#guideTitle").textContent.includes("計画線を保存しました")',timeout=70000)
    except Exception:
        diagnostic=page.evaluate('({phase:document.body.dataset.guide,title:document.querySelector("#guideTitle").textContent,notice:document.querySelector("#guideNotice").textContent,status:document.querySelector("#bridgeStatus").textContent,button:document.querySelector("#guidePrimary").textContent,disabled:document.querySelector("#guidePrimary").disabled})')
        print('PLAN_READY_FAILURE '+json.dumps(diagnostic,ensure_ascii=True),flush=True)
        page.screenshot(path=str(folder/('Plan-failure-'+mode+'.png')),full_page=True)
        (folder/('Plan-failure-'+mode+'.html')).write_text(page.content(),encoding='utf-8')
        raise
    assert '実測は未登録' in page.locator('#guideTitle').inner_text()
    assert page.locator('[data-meas][data-key="s"]').count()==0
    with page.expect_download() as event:page.locator('#exportDxf').click()
    exported=Path(event.value.path()).read_text();lines=[x.strip() for x in exported.splitlines()]
    assert not any(code=='0' and value=='CIRCLE' for code,value in zip(lines[::2],lines[1::2]))
    (folder/('Plan-'+mode+'.dxf')).write_text(exported)
    if mode=='new':
        page.reload();page.wait_for_function('document.body.dataset.guide==="working"')
        assert page.locator('[data-meas][data-key="s"]').count()==0
        print('NATIVE_PLAN_ONLY_SAVE_RELOAD_OK zero measured rows; exported DXF contains no measured circle',flush=True)
    else:
        page.locator('#guidePrimary').click()
        for row,station,left,up,depth in [(0,.5,15,10,2.3),(1,1.0,20,5,2.4)]:
            for key,value in [('s',station),('left',left),('up',up),('depth',depth)]:page.locator(f'[data-meas="{row}"][data-key="{key}"]').fill(str(value))
            page.locator(f'[data-meas="{row}"][data-key="depth"]').press('Enter')
            page.wait_for_function(f'document.querySelector("#bridgeStatus").textContent.includes("延長 {station:g}m") && document.querySelector("#bridgeStatus").textContent.includes("JW_CADで確認しました")',timeout=45000)
            page.wait_for_function(f'document.hasFocus() && document.activeElement.dataset.meas==="{row+1}" && document.activeElement.dataset.key==="s"')
            if row==0:
                (folder/'PlanFirst.tag').touch()
                until=time.monotonic()+40
                while not (folder/'PlanFirstChecked.tag').exists():
                    if time.monotonic()>until:raise AssertionError('Native first-point geometry was not checked')
                    time.sleep(.1)
        print('NATIVE_PLAN_ONLY_RESUME_INPUT_OK empty PC snapshot restored, first .5m and second1m entered with separate up/depth',flush=True)
    assert not errors,errors
    page.screenshot(path=str(folder/('Plan-'+mode+'.png')))
    browser.close()
