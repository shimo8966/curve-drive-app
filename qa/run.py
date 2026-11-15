import os, subprocess, urllib.request, hashlib, shutil, sys, time, socket, zipfile
from pathlib import Path

root = Path(__file__).resolve().parent.parent
out = root / 'qa-out'
out.mkdir(exist_ok=True)
scratch = Path(os.environ['RUNNER_TEMP']) / 'curve-ci'
scratch.mkdir(exist_ok=True)
installer = scratch / 'jww.exe'
release = sys.argv[1] if len(sys.argv)>1 else '825a'
versions = {'825a':('jww825a.exe','47632125be65d95c0a3722521dfa892cab2ecadd043194494e2e6feb12c87d78'), '10038':('jww10038.exe','8aacc55c4d63775c351c955709c30716d3efb0e0038e720d6a0808153670c370')}
name, digest = versions[release]
urls = ['https://www.jwcad.net/download/' + name, 'http://www.jwcad.net/download/' + name, 'https://jwcad.net/download/' + name]
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
last_error = None
if not installer.exists() or hashlib.sha256(installer.read_bytes()).hexdigest()!=digest:
    for url in urls:
        try:
            request = urllib.request.Request(url, headers={'User-Agent':'CurveDrive-CI'})
            with opener.open(request, timeout=20) as response: installer.write_bytes(response.read())
            if hashlib.sha256(installer.read_bytes()).hexdigest()==digest: break
        except OSError as error:
            last_error = error
            print('Official download temporarily unavailable:',type(error).__name__,flush=True)
    else: raise RuntimeError('Cannot download the pinned official installer') from last_error
assert hashlib.sha256(installer.read_bytes()).hexdigest() == digest, 'Official installer hash changed'
print('Official JW_CAD '+release+' installer hash verified', flush=True)
cad = scratch / 'jww'
subprocess.run([str(installer), '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/DIR=' + str(cad)], timeout=150, check=True)
exe = next(cad.rglob('Jw_win.exe'))
appzip = scratch / 'app.zip'
with urllib.request.urlopen('https://raw.githubusercontent.com/shimo8966/curve-drive-app/download-v29/CurveDrive_v29.zip',timeout=30) as response: appzip.write_bytes(response.read())
assert hashlib.sha256(appzip.read_bytes()).hexdigest()=='f9f1f784eb3180d8a3787b99da2f6433507e287a759471ea05e45640e35b5520'
with zipfile.ZipFile(appzip) as archive:
    for name in ('Bridge.cs','AutoBridge.cs','CadContext.cs','CadNative.cs','app.html'):
        (exe.parent/name).write_bytes(archive.read('CurveDrive_v29/'+name))
csc_dir = Path(os.environ['SystemRoot']) / 'Microsoft.NET/Framework64/v4.0.30319'
csc = csc_dir / 'csc.exe'
command = [str(csc), '/noconfig', '/nologo', '/codepage:65001', '/langversion:5', '/target:exe', '/out:' + str(scratch / 'JwCheck.exe'), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:' + str(csc_dir / 'Accessibility.dll'), str(root / 'qa/JwCheck.cs'), *[str(exe.parent/name) for name in ('Bridge.cs','AutoBridge.cs','CadContext.cs','CadNative.cs')], '/reference:System.Web.Extensions.dll', '/main:JwCheck']
subprocess.run(command, check=True, timeout=30)
subprocess.run([str(csc),'/noconfig','/nologo','/codepage:65001','/langversion:5','/target:exe','/out:'+str(exe.parent/'Bridge.exe'),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll',*[str(exe.parent/name) for name in ('Bridge.cs','AutoBridge.cs','CadContext.cs','CadNative.cs')]],check=True,timeout=30)
shutil.copy2(root / 'qa/Fixture.dxf', exe.parent / 'Fixture.dxf')
subprocess.run([str(scratch / 'JwCheck.exe'), str(exe), str(out)], check=True, timeout=180)
