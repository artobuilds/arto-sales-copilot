"""Local acoustic observations, not emotion recognition. No network or model downloads.

YIN: de Cheveigne & Kawahara (2002), normalized difference + first trough.
RMS is digital dBFS, not perceived loudness or ITU-T P.56 active speech level.
Silero VAD uses the model already bundled with the existing faster-whisper runtime.
This small subset of acoustic descriptors is NOT a GeMAPS implementation.
"""
import json
import sys
from pathlib import Path
import numpy as np

SR = 16000
FRAME = 1024
HOP = 320


def yin(frame):
    """Return reliable F0 in 60..500 Hz, or None for aperiodic/unvoiced audio."""
    low, high = SR // 500, SR // 60
    x = np.asarray(frame, dtype=np.float64)
    x = x - np.mean(x)
    if np.mean(x * x) < 1e-8:
        return None
    # Fixed integration length for each lag avoids favoring longer lags.
    length = len(x) - high
    differences = np.array([np.sum((x[:length] - x[t:t + length]) ** 2)
                            for t in range(1, high + 1)])
    cmnd = np.ones(high + 1)
    cmnd[1:] = differences * np.arange(1, high + 1) / np.maximum(np.cumsum(differences), 1e-20)
    lag = low
    while lag < high:
        if cmnd[lag] < .15:
            while lag + 1 <= high and cmnd[lag + 1] < cmnd[lag]:
                lag += 1
            break
        lag += 1
    else:
        return None
    if lag >= high or cmnd[lag] >= .15:
        return None
    a, b, c = cmnd[lag - 1:lag + 2]
    denom = a - 2 * b + c
    refined = lag + (0.5 * (a - c) / denom if abs(denom) > 1e-12 else 0)
    frequency = SR / refined
    return float(frequency) if 60 <= frequency <= 500 else None


def measure(audio, spans):
    x = np.asarray(audio, dtype=np.float32)
    if x.ndim != 1 or len(x) == 0 or not np.all(np.isfinite(x)):
        raise ValueError("Invalid audio")
    duration = len(x) / SR
    mask = np.zeros(len(x), dtype=bool)
    for span in spans:
        mask[max(0, span['start']):min(len(x), span['end'])] = True
    speech = x[mask]
    seconds = len(speech) / SR
    clip_fraction = float(np.mean(np.abs(x) >= .995))
    rms = float(np.sqrt(np.mean(speech.astype(np.float64) ** 2))) if len(speech) else 0
    db = float(20 * np.log10(max(rms, 1e-6)))
    pitch = []
    eligible = 0
    for i in range(0, len(x) - FRAME + 1, HOP):
        if np.mean(mask[i:i + FRAME]) < .8:
            continue
        eligible += 1
        f0 = yin(x[i:i + FRAME])
        if f0 is not None:
            pitch.append(f0)
    fraction = len(pitch) / max(1, eligible)
    noise = x[~mask]
    # Within-chunk noise estimate only; absent noise is not a claim of clean audio.
    noise_rms = float(np.sqrt(np.mean(noise.astype(np.float64) ** 2))) if len(noise) >= 1600 else None
    snr = float(20 * np.log10(max(rms, 1e-6) / max(noise_rms, 1e-6))) if noise_rms is not None else None
    quality = ('clipped' if clip_fraction > .01 else
               'insufficient' if seconds < .65 else
               'too_quiet' if db < -48 else
               'noisy' if snr is not None and snr < 6 else 'ok')
    # Weak periodicity suppresses pitch only. Clear unvoiced speech can still
    # provide valid level and pause measurements; do not label it an audio failure.
    reliable_pitch = fraction >= .3 and len(pitch) >= 8
    pauses = [(b['start'] - a['end']) / SR for a, b in zip(spans, spans[1:])
              if b['start'] - a['end'] >= .2 * SR]
    return dict(duration=duration, speechSeconds=seconds, levelDb=db,
                pitchHz=float(np.median(pitch)) if reliable_pitch else None,
                pitchRangeSemitones=float(12 * np.log2(np.percentile(pitch, 90) / np.percentile(pitch, 10))) if reliable_pitch else None,
                voicedFraction=fraction, clippingFraction=clip_fraction, snrDb=snr,
                pauses=pauses, quality=quality)


def main():
    from faster_whisper.audio import decode_audio
    from faster_whisper.vad import get_speech_timestamps, get_vad_model, VadOptions
    get_vad_model()  # Local bundled ONNX; CPU. Fail before capture if unavailable.
    print(json.dumps({'ready': True}), flush=True)
    for line in sys.stdin:
        try:
            req = json.loads(line)
            path = Path(req['path'])
            if path.suffix.lower() != '.wav' or not path.is_file() or path.stat().st_size > 20_000_000:
                raise ValueError('Invalid bounded WAV')
            audio = decode_audio(str(path), sampling_rate=SR)
            if len(audio) > SR * 14:
                raise ValueError('Chunk too long')
            spans = get_speech_timestamps(audio, VadOptions(min_speech_duration_ms=200,
                min_silence_duration_ms=200, speech_pad_ms=0))
            print(json.dumps(measure(audio, spans), allow_nan=False), flush=True)
        except Exception:
            print(json.dumps({'error': 'Local voice analysis unavailable for this chunk'}), flush=True)


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print(json.dumps({'ready': False, 'error': 'Local voice worker could not initialize'}), flush=True)
        sys.exit(1)
