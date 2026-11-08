#!/usr/bin/env python3
"""Authenticated, bounded SDK readback from this project's owner TV."""
import argparse, importlib.util, secrets, shlex, socket, subprocess, tempfile, threading
from pathlib import Path
from tv_config import add_device_arguments, load_device
p=argparse.ArgumentParser(description=__doc__);p.add_argument('output',type=Path);p.add_argument('paths',nargs='+');add_device_arguments(p);a=p.parse_args()
try: device=load_device(a.tv,a.callback_ip,a.sdb)
except (OSError,ValueError) as error:p.error(str(error))
root=Path(__file__).resolve().parent.parent
s=importlib.util.spec_from_file_location('diag',root/'samsung-tv-diagnose.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
for name in a.paths:
 if not name.startswith('/') or '..' in Path(name).parts: raise ValueError(name)
sdb=str(device.sdb);tv=device.tv_ip;mac=device.callback_ip;token=secrets.token_hex(24);remote='/home/owner/share/tmp/sdk_tools/.fetch-'+secrets.token_hex(8)+'.sh'
with socket.socket() as server:
 server.bind((mac,0));server.listen(1);server.settimeout(25);port=server.getsockname()[1]
 script="trap '/bin/rm -f "+remote+"' EXIT\nexec 3<>/dev/tcp/"+mac+'/'+str(port)+" || exit 1\nprintf '"+token+"\\n' >&3\n/bin/tar -czhf - "+' '.join(shlex.quote(x) for x in a.paths)+" >&3 2>/dev/null\nexec 3>&-\n"
 with tempfile.TemporaryDirectory() as d:
  f=Path(d)/'fetch.sh';f.write_text(script)
  subprocess.run([sdb,'-s',tv+':26101','push',str(f),remote],check=True,capture_output=True)
 state={}
 def launch():
  try: state['r']=subprocess.run([sdb,'-s',tv+':26101','shell',m.injection_argument('. '+remote)],capture_output=True,timeout=40)
  except Exception as e: state['error']=str(e)
 worker=threading.Thread(target=launch,daemon=True);worker.start()
 partial=a.output.with_suffix(a.output.suffix+'.part')
 try:
  c,peer=server.accept()
  with c:
   if peer[0]!=tv: raise RuntimeError('Unexpected source')
   c.settimeout(20)
   with c.makefile('rb') as stream:
    if stream.readline(100).strip().decode()!=token: raise RuntimeError('Token mismatch')
    count=0
    with partial.open('wb') as out:
     while True:
      b=stream.read(65536)
      if not b: break
      count+=len(b)
      if count>25*1024*1024: raise RuntimeError('Transfer too large')
      out.write(b)
  worker.join(2)
  if 'error' in state: raise RuntimeError(state['error'])
  partial.replace(a.output);print(str(a.output),count,'bytes')
 finally:
  subprocess.run([sdb,'-s',tv+':26101','shell',m.injection_argument('/bin/rm -f '+remote)],capture_output=True,timeout=5)
