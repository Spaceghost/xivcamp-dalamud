#!/usr/bin/env python3
from pathlib import Path
import hashlib,json,subprocess,zipfile,datetime
r=Path(__file__).resolve().parents[1];out=r/'release';out.mkdir(exist_ok=True)
name='XivCamp';manifest=json.loads((r/('src/'+name+'.Plugin/'+name+'.json')).read_text());version=manifest['AssemblyVersion'];source=r/('artifacts/bin/'+name+'.Plugin/release')
files={f.name:f.read_bytes() for f in source.glob('*.dll')}
assert set(files)=={name+'.dll',name+'.Core.dll'},'Unexpected dependencies: '+str(list(files))
files[name+'.json']=(json.dumps(manifest,indent=2)+'\n').encode();files['LICENSE']=(r/'LICENSE').read_bytes()
epoch=int(subprocess.check_output(['git','log','-1','--format=%ct'],cwd=r,text=True));t=datetime.datetime.fromtimestamp(epoch,datetime.timezone.utc);stamp=(t.year,t.month,t.day,t.hour,t.minute,t.second//2*2)
artifact=name+'-dev-'+version+'.zip'
with zipfile.ZipFile(out/artifact,'w',compression=zipfile.ZIP_DEFLATED) as z:
 for key,data in sorted(files.items()):
  entry=zipfile.ZipInfo(key,stamp);entry.compress_type=zipfile.ZIP_DEFLATED;entry.create_system=3;entry.external_attr=0o100644<<16;z.writestr(entry,data)
url='https://github.com/Spaceghost/xivcamp-dalamud/releases/download/development/'+artifact
entry={**manifest,'DownloadLinkInstall':url,'DownloadLinkUpdate':url,'DownloadLinkTesting':url,'TestingAssemblyVersion':version,'TestingDalamudApiLevel':15,'IsTestingExclusive':True,'LastUpdate':epoch}
(out/'pluginmaster-development.json').write_text(json.dumps([entry],indent=2)+'\n')
shim=r/('artifacts/bin/Umbra.'+name+'/release/Umbra.'+name+'.dll')
if shim.exists():(out/shim.name).write_bytes(shim.read_bytes())
info={'sourceCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip(),'references':json.loads((r/'tools/references.json').read_text()),'manualDevelopmentOnly':True}
(out/'build-info.json').write_text(json.dumps(info,indent=2)+'\n')
(out/'SHA256SUMS').write_text(''.join(hashlib.sha256(f.read_bytes()).hexdigest()+'  '+f.name+'\n' for f in sorted(out.iterdir()) if f.name!='SHA256SUMS'))
print(artifact)
