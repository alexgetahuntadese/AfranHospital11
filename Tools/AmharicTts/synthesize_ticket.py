from __future__ import annotations

import argparse
import os
from pathlib import Path

import scipy.io.wavfile
import torch
from transformers import AutoTokenizer, VitsModel


MODEL_ID = "facebook/mms-tts-amh"


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate Amharic ticket announcement audio.")
    parser.add_argument("--text", required=True, help="Romanized Amharic text to speak.")
    parser.add_argument("--output", required=True, help="Destination WAV file.")
    parser.add_argument("--model-dir", default="", help="Optional local model/cache directory.")
    parser.add_argument("--offline", action="store_true", help="Use only already downloaded model files.")
    args = parser.parse_args()

    cache_dir = None
    if args.model_dir:
        model_dir = Path(args.model_dir).resolve()
        cache_dir = str(model_dir / "hf-cache")
        os.environ.setdefault("HF_HOME", cache_dir)

    tokenizer = AutoTokenizer.from_pretrained(MODEL_ID, cache_dir=cache_dir, local_files_only=args.offline)
    model = VitsModel.from_pretrained(MODEL_ID, cache_dir=cache_dir, local_files_only=args.offline)
    model.eval()

    inputs = tokenizer(args.text, return_tensors="pt")
    with torch.no_grad():
        waveform = model(**inputs).waveform.squeeze().cpu().numpy()

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    scipy.io.wavfile.write(output, rate=model.config.sampling_rate, data=waveform)


if __name__ == "__main__":
    main()
