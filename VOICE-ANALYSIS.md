# Local voice observations — Preview 0.5

## Scope and user interface

A separate **Voice & intonation / Голос и интонация** strip below the suggestion/transcript shows the client/output or seller/microphone channel. It runs only after explicit Start call, if local voice analysis is enabled. It remains visible in the default compact layout. No audio analysis is claimed for typed text or the offline scripted demo.

Every new live call starts fresh references for each channel. At least three good audio chunks and 12 seconds of VAD-detected speech establish the signal-level reference. Pitch and within-phrase pauses need their own usable samples. Different clients can have different baseline pitch, level and pace. The **New speaker / Новый собеседник** button resets only the remote voice reference and assigns a new session-local number. It does not clear the client brief or transcript; use New conversation / a new briefing for a different client meeting.

New speaker rejects queued results from the old profile, including delayed transcripts, and discards the next acoustic chunk because it may straddle the change. There is no automatic speaker identity recognition, diarization or cross-call voice database. A shared remote track with several people cannot maintain individual baselines. Use headphones, one remote speaker at a time, and reset after changing microphones or audio processing. Automatic gain control and compression can still distort level changes.

## Measurements and decisions

- Silero VAD from the already installed faster-whisper runtime identifies speech spans. Bundled local ONNX model, CPU only, no downloads.
- Original small NumPy implementation of **YIN** (normalized difference, first trough below 0.15, parabolic refinement): 16 kHz mono, 1024-sample frames, 320-sample hop, F0 range 60–500 Hz. No inferred demographics. Outside this range or with too few periodic frames, pitch is withheld.
- **RMS dBFS** over detected speech estimates digital signal level. It is not perceived loudness, dB SPL, or an implementation of ITU-T P.56.
- Pitch median and 10th–90th percentile range in semitones are measured. The panel compares median pitch; detailed measurements are in the optional session log.
- Pauses are gaps of at least 200 ms between VAD spans **within captured chunks**. Capture splits around pauses, so long/cross-chunk and response-time pauses are intentionally not measured. The other person's speaking time is not counted as silence/hesitation.
- Approximate pace comes from local transcript word count divided by chunk duration. It arrives after transcription and is language/recognition dependent. It is not syllable articulation rate.

Reference medians and median absolute deviations (MAD) are session-only. Initial engineering thresholds: pitch change greater than max(2 semitones, 3 scaled MAD); level greater than max(6 dB, 3 scaled MAD); pace greater than max(30 words/minute, 30% of baseline, 3 scaled MAD); internal pause change greater than max(250 ms, 50% of baseline, 3 scaled MAD). A change must repeat in two valid measurements before a directional label appears. These thresholds require real-call validation; they are not research-validated emotion boundaries. References freeze once established to avoid absorbing a sustained change; explicit reset learns a new reference.

Less than 650 ms of detected speech, clipping over 1%, very low level or poor within-chunk speech/noise contrast withhold comparisons. Low pitch reliability suppresses only pitch, allowing valid level and pause observations. There are no labels for trust, deceit, buying intent, nervousness, or other inferred emotions. This is a modest acoustic feature subset inspired by voice research, **not a full GeMAPS/eGeMAPS implementation or certified standard**.

## Runtime and data

`prosody_worker.py` runs separately from GPU Whisper using the existing Python runtime. Capture creates bounded, temporary copies of STT audio chunks; a four-item queue prevents unbounded work. Samples can take up to the existing roughly eight-second capture chunk length to arrive, plus local CPU processing. A 15-second processing timeout disables the voice worker without stopping recording/STT. Skips/failures are shown explicitly. Stop kills the worker, drains the queue and deletes owned temporary copies before finalization completes.

Only local UI receives observations. No audio observations are sent to Jev/OpenAI and they do not automatically rewrite coaching suggestions. With Save session enabled, `voice-observations.jsonl` includes timestamp, channel, session-local profile number and measured features. No keys or voice identity templates are saved there. With persistence off, only temporary audio copies are used and removed.

## Verification and limitations

`prosody_tests.py`: known frequencies across 70–450 Hz, level scaling, silence/noise, clipping, short segments, pause timing, bundled VAD silence rejection and processing budget. This verifies measurements on controlled input, not human emotion accuracy.

`--self-test`: isolated client baselines, repeated-change gating, bad-audio rejection, absent-pitch fallback, new-speaker reset, approximate pace, numeric validation, localization, queued profile rejection and persistence. `--voice-test`: actual local worker IPC on a previously synthesized English fixture; no microphone, client recording or external request. `--ui-smoke`: native controls, EN/RU/UK labels, channel isolation and clearing reference state. Example screenshots are explicitly synthetic UI fixtures.

Real microphone + Zoom, EN/RU/UK natural speech, echo, mixed speakers, automatic gain control, Bluetooth changes, long-call performance and usefulness/false-alarm rate remain unverified. Synthetic English speech had measurable level/pauses but insufficient periodicity for reliable pitch; the panel correctly withholds pitch for that sample. Do not call this validated emotion recognition or a production-ready live assistant.

## Sources

- YIN: https://librosa.org/doc/main/api/generated/librosa.yin.html ; original paper https://pubmed.ncbi.nlm.nih.gov/12002874/
- Silero VAD: local installed `faster_whisper/vad.py` and bundled ONNX implementation; https://github.com/snakers4/silero-vad
- GeMAPS rationale: https://ieeexplore.ieee.org/document/7160715/
- ITU-T P.56 (comparison only, not implemented): https://www.itu.int/ITU-T/recommendations/rec.aspx?rec=11461

No openSMILE package or commercial license is required by this implementation. Optional Python dependencies are listed in requirements.txt.
