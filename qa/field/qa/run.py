from pathlib import Path
import os,subprocess,sys
r=Path(__file__).resolve().parents[1];out=Path(os.environ['GITHUB_WORKSPACE'])/'qa-out-field';out.mkdir(exist_ok=True)
csc=Path(os.environ['SystemRoot'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
subprocess.run([str(csc),'/nologo','/noconfig','/codepage:65001','/langversion:5','/target:winexe','/out:'+str(r/'CurveDrive.exe'),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.Web.dll','/reference:System.Windows.Forms.dll',str(r/'CurveDrive.cs')],check=True)
subprocess.run([sys.executable,'-m','pip','install','playwright==1.56.0'],check=True)
with (out/'Windows.log').open('w',encoding='utf-8') as log:
 subprocess.run(['node',str(r/'qa/prediction.cjs')],env=dict(os.environ,CURVE_APP_JS=str(r/'dist/app.js')),stdout=log,stderr=subprocess.STDOUT,check=True)
 subprocess.run([sys.executable,str(r/'qa/windows.py')],env=dict(os.environ,CURVE_QA_OUT=str(out),PYTHONIOENCODING='utf-8'),stdout=log,stderr=subprocess.STDOUT,check=True,timeout=240)
