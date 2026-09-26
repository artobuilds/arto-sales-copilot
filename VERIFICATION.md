# Preview verification

The public source snapshot is based on interface version 0.5, with neutral briefing defaults, relative runtime paths and user-selected key imports. It excludes the developer's installed application, personal profile, provider-account diagnostics, credentials, recordings and history.

Validation on 2026-09-26: Release publication succeeds using .NET SDK 10.0.301 and cached NAudio 2.2.1 dependencies. The clean public snapshot passes 36 offline checks and 107 UI checks on the published executable, including additional neutral-default and selected-key-file import checks. Test fixtures use fictional data and isolated storage; no microphone, video capture or live provider request was used.

The checks cover provider request contracts, bounded context, reply verification, DPAPI storage with fake keys, recording arithmetic, acoustic baselines, key imports, six language/theme combinations, two window sizes, readable text contrast and preference persistence. The optional Python suite checks synthetic pitch, signal levels, pauses and VAD silence rejection.

Not yet accepted: a complete fresh CUDA/runtime setup on another PC, full Jev + OpenAI live-call operation with a new account, actual Zoom/device synchronization, long calls, natural-voice usefulness, high-DPI/multi-monitor and screen-reader behavior. The development provider account previously returned an access/billing error; this source publication does not establish that other accounts will or will not work.

Screenshots in docs/images are generated from fictional UI fixtures. They are illustrations of the interface, not evidence of live-client results.
