"""Only the delivered input UI drives CAD preparation, calibration and updates."""
import sys, json
from pathlib import Path
from playwright.sync_api import sync_playwright

folder=Path.cwd()
mode=sys.argv[1]
data={'basics':{'site':'CI managed','x0':'0','y0':'0','heading':'0','pitch':'0.5','z0':'0','grade':'0'},'segs':[{'type':'S','l':'50','r':''}],'meas':[{'s':'10','left':'0','up':''}]}
if mode=='curve':
    data['basics'].update(site='CI managed curve',x0='1000',y0='-2000',heading='30')
    data['segs']=[{'type':'S','l':'10','r':''},{'type':'L','l':'15','r':'100'},{'type':'S','l':'8','r':''},{'type':'R','l':'12','r':'80'},{'type':'S','l':'5','r':''}]
    data['meas']=[{'s':'10.5','left':'18','up':''}]
with sync_playwright() as p:
    browser=p.chromium.launch(channel='msedge',headless=False)
    page=browser.new_page(viewport={'width':700,'height':700})
    errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto('http://127.0.0.1:18718/')
    if mode not in ('resume','cold'):
        page.evaluate('data=>{localStorage.setItem("curve-drive-v28",JSON.stringify(data));localStorage.removeItem("curve-drive-guide-v1")}',data)
        page.reload()
    page.wait_for_function('document.querySelector("#guideConnection").textContent === "PCに接続済み"')
    page.locator('#guidePrimary').click()
    if mode not in ('resume','cold'):
        assert 'JW_CADを自動で準備' in page.locator('#guidePrimary').inner_text()
        page.locator('#guidePrimary').click()
    try:
        page.wait_for_function('document.querySelector("#guideTitle").textContent.includes("準備できました") || (!document.querySelector("#guidePrimary").disabled && document.querySelector("#guideNotice").textContent.trim())',timeout=70000)
        assert '準備できました' in page.locator('#guideTitle').inner_text(),ascii(page.locator('#guideNotice').inner_text())
    except Exception:
        print(ascii(page.locator('#guideNotice').inner_text()),flush=True)
        page.screenshot(path=str(folder/'Managed-failure.png'))
        raise
    assert page.evaluate('document.hasFocus()'),'Input screen did not regain focus after automatic CAD preparation'
    page.locator('#guidePrimary').click()
    row=2 if mode in ('resume','cold') else 1
    station,offset=(10.5,20) if mode=='new' else (11,-20) if mode=='resume' else (49,30) if mode=='cold' else (40,-20)
    page.locator(f'[data-meas="{row}"][data-key="s"]').fill(str(station))
    page.locator(f'[data-meas="{row}"][data-key="left"]').fill(str(offset))
    page.locator(f'[data-meas="{row}"][data-key="up"]').press('Enter')
    page.wait_for_function('document.querySelector("#bridgeStatus").textContent.includes("実測点と線をJW_CADで確認しました")',timeout=45000)
    page.wait_for_function(f'document.hasFocus() && document.activeElement.dataset.meas==="{row+1}" && document.activeElement.dataset.key==="s"')
    assert not errors,errors
    print('INPUT_ONLY_'+mode.upper()+'_OK automatic CAD launch, folder, origin, Enter, readback, automatic DXF and project save',flush=True)
    browser.close()
