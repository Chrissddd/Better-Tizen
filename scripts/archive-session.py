#!/usr/bin/env python3
"""One temporary SDK archive session; retain cleanup and unchanged-system evidence."""
import argparse
import asyncio
import json
import os
from tv_config import add_device_arguments, import_root_transport, load_device


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    add_device_arguments(parser)
    parser.add_argument("commands", nargs="*", help="Explicitly authorized shell commands; default reads process identity")
    args = parser.parse_args()
    try:
        device = load_device(args.tv, args.callback_ip, args.sdb)
    except (OSError, ValueError) as error:
        parser.error(str(error))
    os.environ["SDB"] = str(device.sdb)
    probe = import_root_transport()

    async def run():
        result = await probe(device.tv_ip, callback_host=device.callback_ip,
                             bind_host=device.callback_ip, payloads=device.payloads,
                             commands=tuple(args.commands or [
                                 'id; cat /proc/self/attr/current; grep -E "^(Cap|Uid|Gid)" /proc/self/status']))
        print(json.dumps(result, indent=2))
        if any(command["exit_code"] != 0 or command["timed_out"] for command in result.get("commands", [])):
            raise SystemExit(1)
    asyncio.run(run())


if __name__ == "__main__":
    main()
