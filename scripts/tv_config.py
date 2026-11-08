"""Local device configuration shared by maintenance tools; no network requests."""
import ipaddress
import json
import os
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def local_settings():
    filename = Path(os.environ.get("TV_CONFIG", ROOT / ".local/device.json")).expanduser()
    if not filename.is_absolute():
        filename = ROOT / filename
    if not filename.is_file():
        return {}
    value = json.loads(filename.read_text())
    if not isinstance(value, dict):
        raise ValueError("Device configuration must be a JSON object")
    return value


def ipv4(value, name):
    if not value:
        raise ValueError("Set " + name + " or copy scripts/device.example.json to .local/device.json")
    if ipaddress.ip_address(value).version != 4:
        raise ValueError(name + " must be an IPv4 address")
    return str(ipaddress.IPv4Address(value))


def configured_tv(value=None):
    return ipv4(value or os.environ.get("TV_IP") or local_settings().get("tv_ip"), "TV_IP")


def local_path(value):
    candidate = Path(value).expanduser()
    return candidate if candidate.is_absolute() else ROOT / candidate


@dataclass(frozen=True)
class DeviceConfig:
    tv_ip: str
    callback_ip: str
    sdb: Path
    payloads: Path


def load_device(tv=None, callback=None, sdb=None, payloads=None, require_callback=True):
    settings = local_settings()
    tv = configured_tv(tv)
    callback_value = callback or os.environ.get("TV_CALLBACK_IP") or settings.get("callback_ip")
    callback = ipv4(callback_value, "TV_CALLBACK_IP") if require_callback or callback_value else None
    executable = sdb or os.environ.get("SDB") or settings.get("sdb") or shutil.which("sdb")
    if not executable:
        raise ValueError("Set SDB to the official Tizen Studio sdb executable")
    sdb = local_path(executable)
    if not sdb.is_file():
        raise ValueError("SDB executable was not found: " + str(sdb))
    payload_value = payloads or os.environ.get("TV_ROOT_PAYLOADS") or settings.get("payloads")
    if payload_value:
        payload_dir = local_path(payload_value)
    else:
        local = ROOT / "runtime/payloads"
        payload_dir = local if local.is_dir() else ROOT / "research/quiet-tizen/.local/build/payloads"
    return DeviceConfig(tv, callback, sdb, payload_dir)


def add_device_arguments(parser, callback=True):
    parser.add_argument("--tv", help="TV IPv4 address; defaults to TV_IP or local configuration")
    parser.add_argument("--sdb", type=Path, help="Official SDB executable; defaults to SDB or local configuration")
    if callback:
        parser.add_argument("--callback-ip", help="This computer's IPv4 address; defaults to TV_CALLBACK_IP or local configuration")


def import_root_transport():
    # The public copy includes the tested ARM32 runtime inventory compatibility fix.
    for directory in (ROOT / "transport", ROOT / "runtime/root-src", ROOT / "research/quiet-tizen/vendor"):
        if (directory / "samsung_tv_root/archive_root.py").is_file():
            sys.path.insert(0, str(directory))
            from samsung_tv_root.archive_root import probe
            return probe
    raise RuntimeError("Root transport source is missing; restore transport/ or initialize upstream dependencies")
