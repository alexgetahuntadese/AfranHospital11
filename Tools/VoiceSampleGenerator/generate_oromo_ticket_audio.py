from __future__ import annotations

import argparse
import asyncio
from pathlib import Path

import edge_tts


# Use the Amharic voices already available in the project/system voice set.
# The Oromo voice IDs are not available in the configured TTS service.
MALE_VOICE = "am-ET-AmehaNeural"
FEMALE_VOICE = "am-ET-MekdesNeural"


def ticket_text(prefix: str, number: int, room: str) -> str:
    return f"Lakkoofsa {prefix} {number}, gara kutaa yaalaa {room} deemaa."


async def generate_one(prefix: str, number: int, room: str, output_dir: Path, force: bool) -> None:
    output = output_dir / f"{prefix}{number:03}.mp3"
    if output.exists() and not force:
        return

    voice = MALE_VOICE if prefix == "M" else FEMALE_VOICE
    await edge_tts.Communicate(ticket_text(prefix, number, room), voice).save(str(output))


async def main() -> None:
    parser = argparse.ArgumentParser(description="Generate Afaan Oromo ticket MP3 files.")
    parser.add_argument("--output", default="Assets/Voices/Oromo")
    parser.add_argument("--start", type=int, default=1)
    parser.add_argument("--end", type=int, default=300)
    parser.add_argument("--male-room", default="101")
    parser.add_argument("--female-room", default="102")
    parser.add_argument("--concurrency", type=int, default=6)
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    semaphore = asyncio.Semaphore(args.concurrency)

    async def guarded(prefix: str, number: int) -> None:
        async with semaphore:
            room = args.male_room if prefix == "M" else args.female_room
            await generate_one(prefix, number, room, output_dir, args.force)
            print(f"generated {prefix}{number:03}")

    await asyncio.gather(*(
        guarded(prefix, number)
        for prefix in ("M", "F")
        for number in range(args.start, args.end + 1)
    ))


if __name__ == "__main__":
    asyncio.run(main())
