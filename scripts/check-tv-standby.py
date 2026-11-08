#!/usr/bin/env python3
"""Bounded local API monitor with guarded Wake-on-LAN fallback.

This script never turns the TV off, opens SDB/root, sends a remote power toggle,
or changes TV settings. A separate finite native off/on sequence owns power-off.
"""
import argparse
from datetime import datetime, timezone
import ipaddress
import json
from pathlib import Path
import re
import socket
import time
import urllib.error
import urllib.request
from tv_config import configured_tv


def api_read(host, timeout):
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    request = urllib.request.Request("http://%s:8001/api/v2/" % host)
    try:
        with opener.open(request, timeout=timeout) as response:
            payload = response.read(65537)
            if len(payload) > 65536:
                return {"ok": False, "error": "oversized_response"}
            device = json.loads(payload).get("device", {})
            state = device.get("PowerState")
            return {"ok": True, "http_status": response.status,
                    "power_state": str(state).lower() if state is not None else None,
                    "wifi_mac": device.get("wifiMac")}
    except urllib.error.HTTPError as error:
        return {"ok": False, "http_status": error.code, "error": "http_error"}
    except (urllib.error.URLError, TimeoutError, OSError):
        return {"ok": False, "error": "http_unreachable"}
    except (ValueError, TypeError, AttributeError):
        return {"ok": False, "error": "invalid_api_response"}


def mac_bytes(value):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9A-Fa-f]{2}([:-][0-9A-Fa-f]{2}){5}", value):
        raise ValueError("API wifiMac missing or malformed; no wake fallback can be armed")
    result = bytes.fromhex(value.replace(":", "").replace("-", ""))
    if result == b"\x00" * 6 or result == b"\xff" * 6 or result[0] & 1:
        raise ValueError("API wifiMac is not a unicast device address")
    return result


def wake_burst(mac, host, broadcast):
    packet = b"\xff" * 6 + mac * 16
    targets = [(broadcast, 9), (broadcast, 7), (host, 9)]
    successes, failures = 0, 0
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        sock.settimeout(0.5)
        for target in targets:
            try:
                sock.sendto(packet, target)
                successes += 1
            except OSError:
                failures += 1
    return {"packets_sent": successes, "send_failures": failures}


def utc_now():
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds")


def monitor(host, off_after, wake_after, timeout, report_path, broadcast="255.255.255.255",
            interval=1.0, fetch=api_read, send=wake_burst, now=time.monotonic, sleep=time.sleep):
    started = now()
    result = {"started_utc": utc_now(), "host": host, "off_after_seconds": off_after,
              "wake_after_off_seconds": wake_after, "timeout_seconds": timeout,
              "samples": [], "wake_attempts": [], "off_evidence": None,
              "final_power_on_confirmed": False, "boot_id": None,
              "boot_id_note": "Not supplied by this HTTP API; compare separate native before/after reads.",
              "status": "starting"}

    def save():
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(result, indent=2) + "\n")

    def sample():
        remaining = max(0.05, timeout - (now() - started))
        response = fetch(host, min(1.0, remaining))
        # Deliberately omit wifiMac and all other identity/network/API data.
        public = {key: value for key, value in response.items() if key != "wifi_mac"}
        public.update({"elapsed_seconds": round(now() - started, 3), "utc": utc_now()})
        result["samples"].append(public)
        return response

    initial = sample()
    if not initial.get("ok") or initial.get("power_state") != "on":
        result["status"] = "refused_no_on_baseline"
        save()
        return result
    try:
        mac = mac_bytes(initial.get("wifi_mac"))
    except ValueError as error:
        result["status"] = "refused_no_valid_wake_mac"
        result["error"] = str(error)
        save()
        return result
    result["status"] = "monitoring"
    result["wake_mac_source"] = "initial API device.wifiMac; value intentionally omitted"
    save()
    print("READY: on baseline saved; monitoring scheduled power-off. WOL is guarded.", flush=True)
    off_at = None
    consecutive_unreachable, consecutive_on = 0, 0
    last_wake = None
    while now() - started < timeout:
        sleep(min(interval, max(0.0, timeout - (now() - started))))
        if now() - started >= timeout:
            break
        response = sample()
        elapsed = now() - started
        state = response.get("power_state")
        # A non-empty explicit non-on value is stronger evidence than HTTP loss.
        explicit_off = response.get("ok") and state not in (None, "", "on", "unknown")
        eligible_unreachable = elapsed >= off_after and response.get("error") == "http_unreachable"
        consecutive_unreachable = consecutive_unreachable + 1 if eligible_unreachable else 0
        if off_at is None and (explicit_off or consecutive_unreachable >= 2):
            off_at = now()
            result["off_evidence"] = "api_non_on" if explicit_off else "http_unreachable_after_scheduled_off"
            result["off_observed_elapsed_seconds"] = round(elapsed, 3)
            print("OFF_EVIDENCE=" + result["off_evidence"], flush=True)
        if explicit_off and result["off_evidence"] == "http_unreachable_after_scheduled_off":
            result["off_evidence"] = "api_non_on"
        if off_at is not None:
            consecutive_on = consecutive_on + 1 if response.get("ok") and state == "on" else 0
            if consecutive_on >= 2:
                result["final_power_on_confirmed"] = True
                result["returned_on_elapsed_seconds"] = round(elapsed, 3)
                result["status"] = "off_then_on_confirmed" if result["off_evidence"] == "api_non_on" else "http_loss_then_on_confirmed"
                save()
                return result
            ready_for_wake = now() - off_at >= wake_after
            retry_due = last_wake is None or now() - last_wake >= 5
            if ready_for_wake and retry_due and state != "on" and len(result["wake_attempts"]) < 3:
                outcome = send(mac, host, broadcast)
                last_wake = now()
                outcome["elapsed_seconds"] = round(last_wake - started, 3)
                result["wake_attempts"].append(outcome)
                print("WOL_FALLBACK_BURST=" + str(len(result["wake_attempts"])), flush=True)
        save()
    result["status"] = "no_off_transition_observed" if off_at is None else "final_on_unconfirmed_timeout"
    save()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", help="TV IPv4 address; defaults to TV_IP or local configuration")
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--off-after-seconds", type=float, required=True,
                        help="Known remaining delay of the separately scheduled native off command")
    parser.add_argument("--wake-after-off-seconds", type=float, default=22,
                        help="Give the scheduled native on command time before WOL fallback")
    parser.add_argument("--timeout", type=float, default=60)
    parser.add_argument("--broadcast", default="255.255.255.255")
    args = parser.parse_args()
    try:
        args.host = configured_tv(args.host)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    for value in (args.host, args.broadcast):
        if ipaddress.ip_address(value).version != 4:
            parser.error("IPv4 addresses required")
    if not 0 <= args.off_after_seconds < args.timeout <= 60:
        parser.error("Require 0 <= off-after < timeout <= 60 seconds")
    if not 2 <= args.wake_after_off_seconds < args.timeout - args.off_after_seconds:
        parser.error("wake-after must be >= 2 and leave time within the 60-second total window")
    if args.report.exists():
        parser.error("Report already exists; choose a new evidence filename")
    result = monitor(args.host, args.off_after_seconds, args.wake_after_off_seconds,
                     args.timeout, args.report, broadcast=args.broadcast)
    print("RESULT=" + result["status"])
    print("EVIDENCE=" + str(args.report.resolve()))
    return 0 if result["final_power_on_confirmed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
