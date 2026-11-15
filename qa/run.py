import os, subprocess, urllib.request, hashlib, shutil
from pathlib import Path

root = Path(__file__).resolve().parent.parent
out = root / 'qa-out'
out.mkdir(exist_ok=True)
scratch = Path(os.environ['RUNNER_TEMP']) / 'curve-ci'
scratch.mkdir(exist_ok=True)
installer = scratch / 'jww.exe'
url = 'https://www.jwcad.net/download/jww825a.exe'
urllib.request.urlretrieve(url, installer)
assert hashlib.sha256(installer.read_bytes()).hexdigest() == '47632125be65d95c0a3722521dfa892cab2ecadd043194494e2e6feb12c87d78', 'Official installer hash changed'
print('Official JW_CAD installer hash verified', flush=True)
cad = scratch / 'jww'
subprocess.run([str(installer), '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/DIR=' + str(cad)], timeout=150, check=True)
exe = next(cad.rglob('Jw_win.exe'))
csc_dir = Path(os.environ['SystemRoot']) / 'Microsoft.NET/Framework64/v4.0.30319'
csc = csc_dir / 'csc.exe'
command = [str(csc), '/noconfig', '/nologo', '/codepage:65001', '/langversion:5', '/target:exe', '/out:' + str(scratch / 'JwCheck.exe'), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', '/reference:' + str(csc_dir / 'Accessibility.dll'), str(root / 'qa/JwCheck.cs'), str(root / 'qa/Legacy.cs')]
subprocess.run(command, check=True, timeout=30)
subprocess.run([str(scratch / 'JwCheck.exe'), str(exe), str(out)], check=True, timeout=120)
