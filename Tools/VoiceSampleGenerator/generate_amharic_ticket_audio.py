from __future__ import annotations

import argparse
import asyncio
import random
from pathlib import Path

import edge_tts


MALE_VOICE = "am-ET-AmehaNeural"
FEMALE_VOICE = "am-ET-MekdesNeural"

ONES = {
    0: "ዜሮ",
    1: "አንድ",
    2: "ሁለት",
    3: "ሶስት",
    4: "አራት",
    5: "አምስት",
    6: "ስድስት",
    7: "ሰባት",
    8: "ስምንት",
    9: "ዘጠኝ",
}

TENS = {
    10: "አስር",
    20: "ሃያ",
    30: "ሰላሳ",
    40: "አርባ",
    50: "ሃምሳ",
    60: "ስልሳ",
    70: "ሰባ",
    80: "ሰማንያ",
    90: "ዘጠና",
}


def below_hundred(number: int) -> str:
    if number < 10:
        return ONES[number]
    if number in TENS:
        return TENS[number]
    return f"{TENS[number // 10 * 10]} {ONES[number % 10]}"


def ticket_number_words(number: int) -> str:
    if number < 100:
        return f"ዜሮ {below_hundred(number)}"

    hundreds = number // 100
    remainder = number % 100
    words = "መቶ" if hundreds == 1 else f"{ONES[hundreds]} መቶ"
    return words if remainder == 0 else f"{words} {below_hundred(remainder)}"


def ticket_text(prefix: str, number: int, room: str) -> str:
    letter = "ኤም" if prefix == "M" else "ኤፍ"
    return f"ቁጥር {letter} {ticket_number_words(number)}፣ ወደ ሐኪም ክፍል {room} ይሂዱ።"


async def generate_one(prefix: str, number: int, room: str, output_dir: Path, force: bool) -> None:
    output = output_dir / f"{prefix}{number:03}.mp3"
    if output.exists() and not force:
        return

    voice = MALE_VOICE if prefix == "M" else FEMALE_VOICE
    communicate = edge_tts.Communicate(ticket_text(prefix, number, room), voice)
    await communicate.save(str(output))


async def main() -> None:
    parser = argparse.ArgumentParser(description="Generate Amharic ticket MP3 files with online Edge TTS.")
    parser.add_argument("--output", default="Assets/Voices/Amharic", help="Output folder.")
    parser.add_argument("--start", type=int, default=1, help="First ticket number.")
    parser.add_argument("--end", type=int, default=999, help="Last ticket number.")
    parser.add_argument("--prefix", choices=["M", "F", "both", "random"], default="both", help="Ticket prefix to generate.")
    parser.add_argument("--room", default="101", help="Doctor room number spoken in the sentence.")
    parser.add_argument("--concurrency", type=int, default=6, help="Parallel online TTS requests.")
    parser.add_argument("--seed", type=int, default=None, help="Optional seed for repeatable random prefix selection.")
    parser.add_argument("--force", action="store_true", help="Regenerate existing files.")
    args = parser.parse_args()

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)

    rng = random.Random(args.seed)
    if args.prefix == "both":
        ticket_jobs = [
            (prefix, number)
            for prefix in ["M", "F"]
            for number in range(args.start, args.end + 1)
        ]
    elif args.prefix == "random":
        ticket_jobs = [
            (rng.choice(["M", "F"]), number)
            for number in range(args.start, args.end + 1)
        ]
        manifest = output_dir / f"random-{args.start:03}-{args.end:03}.txt"
        manifest.write_text(
            "\n".join(f"{prefix}{number:03}" for prefix, number in ticket_jobs) + "\n",
            encoding="utf-8",
        )
        print(f"manifest {manifest}")
    else:
        ticket_jobs = [
            (args.prefix, number)
            for number in range(args.start, args.end + 1)
        ]

    semaphore = asyncio.Semaphore(args.concurrency)

    async def guarded(prefix: str, number: int) -> None:
        async with semaphore:
            await generate_one(prefix, number, args.room, output_dir, args.force)
            print(f"generated {prefix}{number:03}")

    tasks = [
        guarded(prefix, number)
        for prefix, number in ticket_jobs
    ]
    await asyncio.gather(*tasks)


if __name__ == "__main__":
    asyncio.run(main())
