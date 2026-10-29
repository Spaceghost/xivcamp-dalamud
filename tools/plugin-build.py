#!/usr/bin/env python3
from pathlib import Path
import argparse,hashlib,json,os,subprocess,shutil
r=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--references',type=Path,default=r/'.dalamud');p.add_argument('--umbra-references',type=Path);p.add_argument('--test',action='store_true');p.add_argument('--locked',action='store_true');a=p.parse_args()
dotnet=os.environ.get('DOTNET') or shutil.which('dotnet') or str(Path.home()/'.dotnet/dotnet')
reviewed=json.loads((r/'tools/references.json').read_text())
for name,digest in reviewed['files'].items():
 f=a.references/name
 if not f.is_file() or hashlib.sha256(f.read_bytes()).hexdigest()!=digest:raise SystemExit('Reviewed references required: '+name)
if (r/'viewer').exists():subprocess.run(['npm','run','build'],cwd=r,check=True)
common=['-c','Release','-p:DalamudLibPath='+str(a.references.resolve())+'/','-p:RestorePackagesWithLockFile=true','-p:NuGetAudit=false']
if a.locked:common+=['-p:RestoreLockedMode=true']
subprocess.run([dotnet,'build',str(r/'src/XivCamp.Plugin/XivCamp.Plugin.csproj'),*common],cwd=r,check=True)
if a.umbra_references:subprocess.run([dotnet,'build',str(r/'src/Umbra.XivCamp/Umbra.XivCamp.csproj'),*common,'-p:UmbraLibPath='+str(a.umbra_references.resolve())+'/'],cwd=r,check=True)
if a.test:subprocess.run([dotnet,'test',str(r/'tests/XivCamp.Tests/XivCamp.Tests.csproj'),*common],cwd=r,check=True)
