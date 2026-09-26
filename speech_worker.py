"""Local-only JSONL speech worker. Never downloads models or sends audio externally."""
import argparse
import json
import sys
from pathlib import Path


def emit(value):
    print(json.dumps(value, ensure_ascii=False), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", required=True)
    args = parser.parse_args()
    try:
        from faster_whisper import WhisperModel
        model = WhisperModel("large-v3", device="cuda", compute_type="int8_float16",
                             download_root=args.cache, local_files_only=True)
        emit({"ready": True, "model": "large-v3", "device": "cuda"})
    except Exception as exc:
        emit({"ready": False, "error": type(exc).__name__ + ": local Whisper could not load. Check runtime, model cache and GPU memory."})
        return 1
    for raw in sys.stdin:
        try:
            req = json.loads(raw)
            language = req.get("language", "en")
            if language not in ("en", "ru", "uk"):
                raise ValueError("Unsupported language")
            path = Path(req["path"])
            if path.suffix.lower() != ".wav" or not path.is_file():
                raise ValueError("Expected a local WAV")
            segments, info = model.transcribe(str(path), language=language, beam_size=3,
                vad_filter=True, condition_on_previous_text=False,
                hotwords=str(req.get("hotwords", ""))[:1000], word_timestamps=False)
            text = " ".join(s.text.strip() for s in segments if s.no_speech_prob < .65).strip()
            emit({"text": text, "language": info.language})
        except Exception as exc:
            emit({"error": type(exc).__name__ + ": audio chunk could not be transcribed."})
    return 0


if __name__ == "__main__":
    sys.exit(main())
