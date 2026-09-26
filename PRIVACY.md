# Data handling

## Before capture

Startup and the offline example do not capture audio/video or call AI providers. Capture starts only after the user selects Start call and accepts the displayed processing permission. The operator is responsible for obtaining any participant/client permissions required for recording and external text processing.

## Local data

- `app/local-data/`: settings, briefing and DPAPI-encrypted API keys, tied to the current Windows user. Briefs/settings themselves are not encrypted.
- `sessions/`: optional recordings, separate microphone/computer WAVs, transcript, suggested replies, brief snapshot and acoustic observations. If video is enabled: source screen video and a finalized video with audio tracks. These files are ordinary unencrypted local files; no automatic deletion schedule is provided.
- Temporary speech/acoustic chunks are removed by the application during normal cleanup. A crash can leave partial files; inspect your local folders before sharing a computer or archive.
- `test-output/`: diagnostic tests and fictional fixtures. Some manually invoked integration tests can contain input text; never publish this directory.

Output loopback captures everything played through the selected output. A shared output is not an isolated Zoom track. Screen recording/sharing can include the assistant and other visible content. Video selection resets to off at launch.

## External services

Live coaching and typed-text analysis send the supplied briefing and bounded conversation text to TypeSafe. Optional contextual wording sends the same kind of text to OpenAI, and a candidate reply is checked with TypeSafe before display. Provider accounts, billing, regional availability and retention rules apply separately.

The application sets `store=false` on OpenAI Responses requests. This controls that API parameter, not every provider's logging/retention practice. Do not include unnecessary sensitive information in the briefing.

The application does not upload audio/video or acoustic measurements. It has no background monitoring service, meeting participant bot or automatic messages. Updating the source does not authorize capture or paid requests.

## Sharing and reporting problems

Use fictional text to reproduce problems. Do not upload keys, credentials, `settings.json`, personal/client briefs, recordings, logs or screenshots containing private content. Describe the error and software/runtime versions. A saved-key indicator is not an API connectivity test.

DPAPI protects a stored key on this Windows account; it does not make an `app/` folder safe to publish. Review every file before sharing it.
