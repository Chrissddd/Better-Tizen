#!/usr/bin/env python3
"""Restore this exact TV's saved Home and boot preferences via native APIs.
Requires the paired SDB and already staged HomeBoot/HomeAssign helpers.
Does not uninstall Dusk or alter the Samsung Home installation.
"""
import argparse
import asyncio
import json
import os
import sys
from tv_config import ROOT, add_device_arguments, import_root_transport, load_device


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    add_device_arguments(parser)
    args = parser.parse_args()
    try:
        device = load_device(args.tv, args.callback_ip, args.sdb)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    os.environ["SDB"] = str(device.sdb)
    probe = import_root_transport()

    async def run():
        result = await probe(device.tv_ip, callback_host=device.callback_ip,
                             bind_host=device.callback_ip, payloads=device.payloads, commands=(
            '/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeBoot.dll restore',
            '/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll restore',
            '/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeBoot.dll read'))
        report = ROOT / 'reports/restore-samsung-home-last.json'
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(json.dumps(result, indent=2) + '\n')
        for command in result.get('commands', []):
            print(command['stdout'])
            print(command['stderr'], file=sys.stderr)
            if command['exit_code'] != 0 or command['timed_out']:
                raise SystemExit('Restore command failed; inspect ' + str(report))
        print('Original Home and boot preferences verified. Evidence: ' + str(report))
    asyncio.run(run())


if __name__ == "__main__":
    main()
