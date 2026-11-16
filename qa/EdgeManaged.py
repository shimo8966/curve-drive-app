"""Only the delivered input UI drives CAD preparation, calibration and updates."""
import sys, json
from pathlib import Path
from playwright.sync_api import sync_playwright

folder=Path.cwd()
mode=sys.argv[1]
data={'basics':{'site':'CI managed','x0':'0','y0':'0','heading':'0','pitch':'0.5','z0':'0','grade':'0'},'segs':[{'type':'S','l':'50','r':''}],'meas':[{'s':'10','left':'0','up':''}]}
if mode=='resume': data=json.loads((folder/'Project.json').read_text(encoding='utf-8-sig'))
with sync_playwright() as p:
    browser=p.chromium.launch(channel='msedge',headless=False)
    page=browser.new_page(viewport={'width':700,'height':700})
    errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto('http://127.0.0.1:18718/')
    page.evaluate('data=>{localStorage.setItem("curve-drive-v28",JSON.stringify(data));localStorage.removeItem("curve-drive-guide-v1")}',data)
    page.reload()
    page.wait_for_function('document.querySelector("#guideConnection").textContent === "PCに接続済み"')
    page.locator('#guidePrimary').click()
    if mode=='new':
        assert 'JW_CADを自動で準備' in page.locator('#guidePrimary').inner_text()
        page.locator('#guidePrimary').click()
    try:
        page.wait_for_function('document.querySelector("#guideTitle").textContent.includes("準備できました") || (!document.querySelector("#guidePrimary").disabled && document.querySelector("#guideNotice").textContent.trim())',timeout=70000)
        assert '準備できました' in page.locator('#guideTitle').inner_text(),ascii(page.locator('#guideNotice').inner_text())
    except Exception:
        print(ascii(page.locator('#guideNotice').inner_text()),flush=True)
        page.screenshot(path=str(folder/'Managed-failure.png'))
        raise
    page.locator('#guidePrimary').click()
    row=1 if mode=='new' else 2
    station,offset=(10.5,20) if mode=='new' else (11,-20)
    page.locator(f'[data-meas="{row}"][data-key="s"]').fill(str(station))
    page.locator(f'[data-meas="{row}"][data-key="left"]').fill(str(offset))
    page.locator(f'[data-meas="{row}"][data-key="up"]').press('Enter')
    page.wait_for_function('document.querySelector("#bridgeStatus").textContent.includes("実測点と線をJW_CADで確認しました")',timeout=45000)
    page.wait_for_function(f'document.hasFocus() && document.activeElement.dataset.meas==="{row+1}" && document.activeElement.dataset.key==="s"')
    assert not errors,errors
    print('INPUT_ONLY_'+mode.upper()+'_OK automatic CAD launch, folder, origin, Enter, readback, automatic DXF and project save',flush=True)
    browser.close()
