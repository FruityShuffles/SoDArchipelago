"""Tiny DeathLink tester for the in-game checklist (TESTING.md).

Connects to an Archipelago server as a text-only client on some slot, prints every DeathLink it sees (so you can check
the mod sends one when your Traveler is knocked down), and sends one when you press Enter.

Run it with Archipelago's venv Python (it needs the `websockets` package):
    python tools/deathlink_test.py ws://localhost:38281 SomeOtherSlot
"""
import asyncio
import json
import sys
import time
import uuid

import websockets


async def main(url: str, slot: str, password: str) -> None:
    async with websockets.connect(url, max_size=None) as ws:
        await ws.recv()  # RoomInfo
        await ws.send(json.dumps([{
            "cmd": "Connect", "game": "", "name": slot, "password": password, "uuid": uuid.uuid4().hex,
            "version": {"major": 0, "minor": 6, "build": 0, "class": "Version"},
            "items_handling": 0, "tags": ["TextOnly", "DeathLink"], "slot_data": False,
        }]))

        async def reader() -> None:
            async for raw in ws:
                for packet in json.loads(raw):
                    if packet.get("cmd") == "Connected":
                        print(f"connected as {slot}; press Enter to send a DeathLink, Ctrl+C to quit")
                    elif packet.get("cmd") == "ConnectionRefused":
                        print("refused:", packet.get("errors"))
                    elif packet.get("cmd") == "Bounced" and "DeathLink" in packet.get("tags", []):
                        print("DeathLink received:", packet.get("data"))

        async def writer() -> None:
            loop = asyncio.get_running_loop()
            while True:
                if not await loop.run_in_executor(None, sys.stdin.readline):
                    return  # stdin closed: keep listening only
                await ws.send(json.dumps([{
                    "cmd": "Bounce", "tags": ["DeathLink"],
                    "data": {"time": time.time(), "source": slot, "cause": f"{slot} pressed Enter"},
                }]))
                print("DeathLink sent")

        await asyncio.gather(reader(), writer())


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    asyncio.run(main(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else ""))
