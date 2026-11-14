#!/usr/bin/env python3
"""Run startup recovery tests against a fake local vconf library, never a TV."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--dotnet", default=os.environ.get("DOTNET") or "dotnet")
parser.add_argument("--cc", default=os.environ.get("CC") or "cc")
args = parser.parse_args()
if not shutil.which(args.dotnet) or not shutil.which(args.cc):
    parser.error("Install .NET 8 and a C compiler, or pass --dotnet/--cc")
if sys.platform not in ("darwin", "linux"):
    parser.error("The local native shim test currently supports macOS and Linux")
out = HERE.parent / ".local/startup-tests"
out.mkdir(parents=True, exist_ok=True)
library = out / ("startup-vconf-shim.dylib" if sys.platform == "darwin" else "startup-vconf-shim.so")
flags = ["-dynamiclib"] if sys.platform == "darwin" else ["-shared", "-fPIC"]
subprocess.run([args.cc, *flags, "-o", str(library), str(HERE / "startup-vconf-shim.c")], check=True)
subprocess.run([args.dotnet, "run", "--project", str(HERE / "StartupTests.csproj"), "-c", "Release", "--", str(library)], check=True)
