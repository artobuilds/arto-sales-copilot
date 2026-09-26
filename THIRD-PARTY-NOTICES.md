# Third-party components and provenance

This repository contains application source and an authored vector-style icon. It does not bundle .NET, Python environments, native GPU libraries, model weights, FFmpeg or third-party binary packages. Building or setting up live speech obtains dependencies separately; their original terms apply.

| Component | Role | License / authoritative source |
| --- | --- | --- |
| NAudio 2.2.1 | Windows audio capture, sample conversion, WAV files | [MIT license](https://github.com/naudio/NAudio/blob/release/2.x/license.txt) |
| faster-whisper | Local transcription, bundled VAD integration | [MIT license](https://github.com/SYSTRAN/faster-whisper/blob/master/LICENSE) |
| NumPy | Acoustic numerical processing | [BSD license](https://github.com/numpy/numpy/blob/main/LICENSE.txt) |
| CTranslate2 | Optional speech-runtime dependency | [MIT license](https://github.com/OpenNMT/CTranslate2/blob/master/LICENSE) |
| PyAV | Optional speech-runtime audio dependency | [License](https://github.com/PyAV-Org/PyAV/blob/main/LICENSE.txt) and licenses of linked libraries |
| ONNX Runtime | Local VAD inference | [MIT license](https://github.com/microsoft/onnxruntime/blob/main/LICENSE) |
| Silero VAD | Voice activity model used through faster-whisper | [MIT license](https://github.com/snakers4/silero-vad/blob/master/LICENSE) |
| FFmpeg | User-installed optional video capture/mux | [License and build-dependent GPL/LGPL requirements](https://ffmpeg.org/legal.html) |

Model weights and NVIDIA runtime components are separate downloads with their own licenses. Check the exact distributions you install. This document does not relicense any third-party component. If distributing compiled binaries later, include the notices and comply with the exact licenses of the bundled versions; this initial source publication does not ship such a bundle.

## Inspiration

The sales-copilot concept was inspired by [moritzkremb/jev-sales-copilot](https://github.com/moritzkremb/jev-sales-copilot), reviewed at revision `09d1612`. Its Python/browser files, recordings, data and playbook are not included in this C#/WPF repository. No LICENSE file was listed in its root during publication review on 2026-09-26; a public repository should not be treated as permission to copy or relicense its content. Attribution here records inspiration, not a grant of rights to upstream material.

The local pitch estimator is a small original implementation of the YIN method; see references in [VOICE-ANALYSIS.md](VOICE-ANALYSIS.md). This is not a full GeMAPS implementation, and no openSMILE software is included.
