"""Synthetic browser-to-native-CAD QA. No user project data is used."""
import sys
from playwright.sync_api import sync_playwright

station, offset = float(sys.argv[1]), float(sys.argv[2])
data = {
    'basics': {'site':'CI fixture','x0':'0','y0':'0','heading':'0','pitch':'0.5','z0':'0','grade':'0'},
    'segs': [{'type':'S','l':'50','r':''}],
    'meas': [{'s':'10','left':'0','up':''}, {'s':'','left':'','up':''}],
}
with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page()
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.goto('http://127.0.0.1:18718/')
    page.evaluate('(data) => localStorage.setItem("curve-drive-v28", JSON.stringify(data))', data)
    page.reload()
    page.wait_for_function('document.getElementById("autoStatus").textContent.includes("ON")')
    page.locator('[data-meas="1"][data-key="s"]').fill(str(station))
    page.locator('[data-meas="1"][data-key="left"]').fill(str(offset))
    page.locator('[data-meas="1"][data-key="up"]').press('Enter')
    page.wait_for_function('document.getElementById("bridgeStatus").textContent.includes("CADへ自動引渡ししました")', timeout=45000)
    page.wait_for_function('document.querySelectorAll("[data-key=s][data-meas]").length === 3')
    assert not errors, errors
    print('NATIVE_EDGE_ENTER_OK Edge=' + browser.version + ' station=' + str(station) + ' offsetMm=' + str(offset), flush=True)
    browser.close()
