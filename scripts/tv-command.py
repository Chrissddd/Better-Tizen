#!/usr/bin/env python3
"""Run an explicitly supplied local shell script on the owner's TV as SDK user.
The existing diagnostic transport verifies source IP and a random callback token.
"""
import argparse
import importlib.util
from pathlib import Path
from tv_config import add_device_arguments, load_device
p=argparse.ArgumentParser()
p.add_argument('script',type=Path)
add_device_arguments(p)
a=p.parse_args()
try: device=load_device(a.tv,a.callback_ip,a.sdb)
except (OSError,ValueError) as error:p.error(str(error))
root=Path(__file__).resolve().parent.parent
s=importlib.util.spec_from_file_location('diagnose',root/'samsung-tv-diagnose.py')
m=importlib.util.module_from_spec(s); s.loader.exec_module(m)
m.READ_COMMANDS=a.script.read_text()
m.run_check(device.sdb,device.tv_ip,device.callback_ip)
