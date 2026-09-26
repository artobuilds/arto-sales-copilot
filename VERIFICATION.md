# Preview 0.6.1 verification

The public source uses neutral briefing defaults, relative runtime paths and user-selected key imports. It excludes the developer's installed application, personal profile, provider-account diagnostics, credentials, recordings and history.

## Automated checks

Validation on 2026-09-26 used .NET SDK 10.0.301 and the existing NAudio 2.2.1 dependency. Release publication, `--self-test` and `--ui-smoke` run on the published executable. The final accepted run contains 36 offline checks and 300 UI checks, plus one informational DPI line. Test fixtures use fictional content and isolated storage; no microphone, desktop recording or live provider request is involved.

The checks cover provider request contracts, bounded context, reply verification, DPAPI storage with fake keys, recording arithmetic, acoustic baselines, selected-file key imports, RU/UK/EN in both Day/Night themes, 1000×720 and 1320×900 windows, targeted text/control contrast and preference persistence. Appearance changes preserve unfinished briefs, unrelated settings, conversation language and encrypted keys.

The redesign adds checks for native automation names, stable keyboard focus after a suggestion, the immediate reduced-motion path, repeated animations settling without a queue, non-modal audio instructions, all four compact page viewports and transient save feedback. The animation test waits for the WPF render clock within a bounded interval; a fixed 230 ms wall-clock delay was unreliable in a hidden window under load. Viewport tests and screenshots do not establish that every possible long user-supplied value fits.

The actual Windows window ran at 96 DPI (100%). WPF raster exports at 100%, 125% and 150% checked vector rendering only. They are **not** a Windows display-scale change or multi-monitor acceptance test. The optional Python acoustic suite was not rerun for this presentation-only change.

Version 0.6.1 adds regression checks for both call disclosures open/closed in all six language/theme combinations and both window sizes. They verify Open/Hide labels, 44-pixel click targets, retained reply viewport, footer/voice separation, full footer padding, empty topics and native toggle focus. In a compact open state, the working-area scrollbar intentionally reveals the lower controls; Start/Stop and the footer stay fixed.

## Native interaction checks

An isolated fictional `--ui-review` window was operated with native mouse/keyboard input. Ctrl+Tab and arrow keys selected the Audio page; the output dropdown opened and selected a fictional CABLE-A entry with Down/Enter; Tab/Space toggled the local voice option and expanded the cable instructions. The accessible tree exposed the expanded instructions. An initially missing Start-button automation name was fixed. No real audio endpoint or provider was exercised.

The two concepts, before/after screen captures and second visual-polish pass are documented in [DESIGN.md](DESIGN.md). The short interaction video consists of the application's rendered frames with fictional content and no audio. It demonstrates advice, feedback, disclosure and appearance transitions; the feedback segment alone does not prove a clipboard operation.

## Remaining acceptance work

- Actual Windows scale changes at 125%/150%, multi-monitor movement, a screen-reader session and a Windows high-contrast session. System-color/reduced-motion handling and selected contrast pairs were checked, not full accessibility certification.
- Complete fresh CUDA/runtime setup on another PC, full Jev + OpenAI live-call operation with a new account, actual Zoom/device synchronization, long calls and natural-voice usefulness.
- Native Mica appearance across supported and unsupported Windows builds. The app requests a backdrop conditionally and keeps reading surfaces opaque; a successful API call alone is not visual acceptance.

The development provider account previously returned an access/billing error. This interface release does not establish live provider availability. Screenshots illustrate the interface, not live-client results.
