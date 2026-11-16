"""Drive the delivered guide in real Edge; CAD clicks are supplied by the native fixture actor."""
import sys, json, time, urllib.request
from pathlib import Path
from playwright.sync_api import sync_playwright

mode = sys.argv[1]
folder = Path.cwd()
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
def info():
    with opener.open('http://127.0.0.1:18718/guide/info', timeout=10) as r:
        return json.load(r)
def wait_for(predicate):
    until = time.monotonic()+60
    while time.monotonic()<until:
        if predicate(): return
        time.sleep(.1)
    raise AssertionError('Native fixture actor did not finish the requested CAD step')

data = {'basics':{'site':'CI fixture','x0':'0','y0':'0','heading':'0','pitch':'0.5','z0':'0','grade':'0'},
        'segs':[{'type':'S','l':'50','r':''}], 'meas':[{'s':'10','left':'0','up':''}]}
if mode == 'resume':
    data['meas'] += [{'s':'10.5','left':'20','up':''},{'s':'11','left':'-20','up':''},{'s':'11.5','left':'30','up':''}]
with sync_playwright() as p:
    browser=p.chromium.launch(channel='msedge',headless=False)
    page=browser.new_page(viewport={'width':680,'height':700},accept_downloads=True)
    errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto('http://127.0.0.1:18718/')
    page.evaluate('data => {localStorage.setItem("curve-drive-v28",JSON.stringify(data));localStorage.removeItem("curve-drive-guide-v1")}',data)
    page.reload()
    page.wait_for_function('document.querySelector("#guideConnection").textContent === "PCに接続済み"')
    if mode == 'new':
        assert '入力内容を確認する' in page.locator('#guidePrimary').inner_text()
        page.locator('#guidePrimary').click()
        with page.expect_download() as event: page.locator('#guidePrimary').click()
        downloaded=event.value
        # The already-open native JWW was produced from this exact synthetic DXF.
        assert Path(downloaded.path()).read_text().splitlines()==(folder/'Fixture.dxf').read_text().splitlines()
        page.locator('#guidePrimary').click()
        page.locator('#guidePrimary').click()
        page.wait_for_function('document.querySelector("#guideStep").textContent.includes("4 / 5")')
        assert info()['latest']['automation']['prepared']
        (folder/'GuideReady.tag').touch()
        wait_for(lambda:info()['latest']['automation']['calibrated'])
        page.bring_to_front()
        page.locator('#guidePrimary').click()
        page.wait_for_function('document.querySelector("#guideStep").textContent.includes("5 / 5")')
        page.locator('#guidePrimary').click()
        target,offset,row=10.5,20,1
    else:
        assert '前回の作業を続ける' in page.locator('#guidePrimary').inner_text()
        before=info()['latest']['session']
        page.locator('#guidePrimary').click()
        page.locator('#guidePrimary').click()
        page.wait_for_function('document.querySelector("#guideStep").textContent.includes("再開 2 / 2")')
        (folder/'ResumeReady.tag').touch()
        wait_for(lambda:info()['latest']['automation'].get('fieldReady') and info()['cadReady'])
        page.bring_to_front()
        page.locator('#guidePrimary').click()
        assert info()['latest']['session']==before
        target,offset,row=12,-10,4
    page.wait_for_function('document.querySelector("#guideTitle").textContent.includes("準備できました")')
    page.locator('#guidePrimary').click()
    page.locator(f'[data-meas="{row}"][data-key="s"]').fill(str(target))
    assert page.locator('#activeAlignment').inner_text()=='直線'
    page.locator(f'[data-meas="{row}"][data-key="left"]').fill(str(offset))
    page.bring_to_front()
    page.locator(f'[data-meas="{row}"][data-key="up"]').press('Enter')
    page.wait_for_function('document.querySelector("#bridgeStatus").textContent.includes("CADへ自動引渡ししました")',timeout=45000)
    page.wait_for_function(f'document.hasFocus() && document.activeElement.dataset.meas === "{row+1}" && document.activeElement.dataset.key === "s"',timeout=10000)
    assert not errors,errors
    print('NATIVE_GUIDE_'+mode.upper()+'_OK screen guide, real registration, Enter to CAD, next input focus',flush=True)
    browser.close()
