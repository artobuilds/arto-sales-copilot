# Call screen 0.7 — implementation spec

Target: port the approved mockups in `mockups/` to the WPF app, on top of `design/arto-signature` (0.6.1). This is a layout and hierarchy change of the **Call** page plus a narrow navigation rail and a new mini window. Call logic, providers, audio capture, voice algorithms, storage and localization keys stay as they are unless a line below says otherwise.

Read `AGENTS.md` and `DESIGN.md` first; their rules still apply.

## Reference files

| Mockup (open the `.html` in a browser, or view the `.png`) | What it shows |
| --- | --- |
| `mockups/call-live-day.html` / `.png` | Main target: live call, Day theme, 1320×860 client area |
| `mockups/call-live-night.html` / `.png` | Same screen, Night theme |
| `mockups/call-idle-day.html` / `.png` | Before Start: readiness checklist, voice strip with dashes |
| `mockups/call-live-1000x720.html` / `.png` | Minimum window 1000×720 (compact mode) |
| `mockups/mini-overlay.html` / `.png` | New always-on-top mini window |

The PNGs were rendered on Linux without Segoe UI, so their text is wider than in Windows and some labels wrap. **The HTML is the source of truth for sizes; in the app every header row, voice label and voice value must stay on one line** (trim with ellipsis if a translation is too long). All texts in the mockups are fictional demo content from `Demo.cs`.

## Keep unchanged (explicit decisions of the owner)

- Voice strip stays visible on the Call page at every size down to 1000×720, including the four `—` placeholders before a call and in the text example. Do not hide or collapse it.
- The consent checkbox on the Audio & recording page stays where it is.
- Four native tabs keep their indices and handlers (`Tabs.SelectedIndex` 0 Call, 1 Preparation, 2 Audio, 3 Settings).
- Palette keys in `UiTheme.Palette` keep their names and values. Add new keys only if needed (see Colors).
- Honesty rules: no fake levels, no decorative waveforms, no emotion wording. Meters and the "client speaking" indicator are driven only by real `MicMeter` / `OutputMeter` values.

## 1. Navigation rail: 184 → 76 px

- App.xaml, the TabControl template with `ColumnDefinition Width="184"` (line ~77): change to **76**.
- Each tab item: 62×58, corner radius 12, icon 20 px (existing Segoe Fluent glyph from `Tag`) above a label of 11 px, vertical gap 4. Selected: `Selected` fill + `Accent` foreground + SemiBold. Unselected: `Muted`.
- Short labels: RU `Звонок / Бриф / Звук / Настройки`, UK `Дзвінок / Бриф / Звук / Налаштування`, EN `Call / Brief / Audio / Settings`. Add new `UiStrings.json` keys for the short labels; keep the full names as `AutomationProperties.Name` and `ToolTip`.
- Top of rail: wordmark `Arto` only, 17 px SemiBold. Remove the "Sales Copilot" subtitle, the tagline block ("Твой следующий разговор…") and replace `ARTOBUILDS / 0.6.1` with the plain version number, 10 px `Muted`.
- Compact mode (see 6): rail 64 px, items 52×52, icon 18, label 10.
- All `Margin="212,…"` offsets that assume the old rail (HeaderPanel, TopStatusBox, content host in `Interface.cs`) must follow the new width: 76 + 28 = **104** (compact: 64 + 20 = **84**).

## 2. Call header (one row, replaces HeaderPanel row + action row + TopStatusBox)

Order left → right, vertically centered, gap 14 (compact 10):

1. Title block (fills remaining width): 11 px SemiBold `Muted` "Звонок" + `PageTitle` 22 px SemiBold (compact 18, and the 11 px label is hidden). Ellipsis on overflow, tooltip = full title.
2. **Status pill** (new), height 40, radius 999, padding 0 14:
   - Idle: `Chip` background, `Muted` text, hollow 9 px ring + text from the existing idle status ("Микрофон выключен").
   - Live: new `LivePill` background + `Danger` text, filled 9 px dot, text "Идёт звонок", then elapsed time `mm:ss` (tabular digits) from call start. Compact: only dot + timer.
   - Text example (demo): `Chip` background, `Accent` text "Текстовый пример".
   - The dot pulse: a 1.8 s ring fading out (ScaleTransform/opacity on a second ellipse). Disabled when `UiMotion.Enabled` is false.
3. **Meters** (only while live or in demo), 120 px wide (compact 90): two rows "Ты" / "Клиент", label 11 px `Muted` 42 px wide + 4 px bar. Reuse existing `MicMeter` (Accent) and `OutputMeter` (AudioAccent) — move them here, do not duplicate.
4. Language: `LanguageBox` restyled as a 44 px button. Live/demo: show the short code `EN / RU / UK`; idle: full name ("Английский"). Its caption "Язык разговора" moves into the tooltip.
5. Mini mode button: 44×44 icon-only (picture-in-picture glyph), `AutomationProperties.Name` "Мини-режим поверх окон". Replaces `PinBox` visually; see section 5.
6. Overflow `⋯` button 44×44 opening a context menu with: New call (`NewConversation`), Try the example (`PlayDemo`), Save notes (`ExportNotes`), Always on top (checkable, bound to the existing `PinBox` logic). Keep `NewButton`, `DemoButton` names on the menu items so existing handlers/tests keep working, or update the tests.
7. Primary action, 44 high:
   - Idle: `LiveButton` "▶ Начать звонок" (existing Primary style, soft shadow).
   - Live: `StopButton` "■ Остановить" — outline 1 px `Danger`, `Danger` text, Panel background. Only one of the two is visible at a time.

`TopStatusBox` (bottom capture footer) is removed. Its messages move:
- `ModeText` → the status pill.
- `StatusText` (errors, capture notes) → shown as a single-line banner **only when non-default**, directly under the header, `StatusPanel` background, radius 8, padding 10 14, `Warning` icon for problems. Keep `AutomationProperties.LiveSetting="Polite"` and the tooltip with details (`FriendlyError`).
- `SessionMeta` → tooltip of the status pill.

## 3. Call body grid

Two columns `1.4* : 1*`, gap 24 (compact `1.35* : 1*`, gap 18). Page padding 20 28 24 (compact 16 20 18). Header→body gap 18 (compact 14).

### Left column: advice card (fills height) + voice strip (auto height), gap 16

**AdviceCard**: `AdvicePanel` fill, 1 px `AdviceBorder`, radius 16 (compact 14), padding 26 32 22 (compact 20 24 16). Night only: soft glow `DropShadowEffect` color Accent, opacity ~0.25, blur 40, depth 0.
- Row 1: 3×14 `Metal` bar + "ЧТО СКАЗАТЬ" 12 px Bold, letter spacing ~0.6, `Accent`; right: `StageText` as a pill (radius 999, padding 5 11, 12 px, Panel-ish fill: Day `#F6F8F2`, Night `Selected`).
- `AdviceTitle` 16 px `Muted` (compact 14), margin-top 22 (compact 14).
- `SayText` in `DisplayFont`, **32 px / line height 42** (compact 24/32), weight Medium, left rule 3 px `Metal`, padding-left 20 (compact 16). The whole phrase must be visible without scrolling at 1320×860 for phrases up to ~140 characters; keep `AdviceScroll` only as a fallback for longer text.
- Footer: `AdviceMeta` 12 px `Muted` left; `CopyButton` right, 44 high (compact 40), icon + "Копировать фразу" (compact "Копировать"), Panel fill, 1 px border.
- New advice: keep `UiMotion.Reveal` 180 ms.

**VoiceCard** (unchanged logic in `VoiceUi.cs`): top rule 1 px `VoiceBorder`, padding-top 14 (compact 10), gap 12 (compact 8).
- Row 1: `VoiceHeading` 15 px SemiBold (compact 14) + `VoicePersonText` inline after it (12 px Muted, prefixed "·") — hidden in compact; right: `VoiceSpeakerBox` 36 high (compact 32) and `ResetVoiceButton` as a quiet text button in `Accent`.
- Row 2: 4 equal columns. Label 12 px `Muted` (compact 11). Value 16 px (compact 13), one line:
  - changed values (`higher/lower/louder/quieter/faster/slower/longer/shorter`): SemiBold `Ink` with a 15 px arrow glyph in `Accent` (↑ for higher/louder/faster/longer, ↓ for the opposite);
  - `usual` and everything else: Regular `Muted`, no arrow;
  - `—` placeholder: SemiBold `Ink` as today.
  - Compact may use shortened value strings ("↓ Медленнее"); add them as new localized keys, full text in tooltip.
- Row 3: `VoiceStatus` + `VoiceNote` merged into one 11 px `Muted` line (status first). Compact: only `VoiceNote` text (status goes to the heading tooltip as today).

### Right column: conversation feed

- Header: "Разговор" 15 px SemiBold + `TurnCount` 12 px Muted inline (baseline aligned).
- `TranscriptScroll` fills height; newest at the bottom, auto-scroll to end.
  - Rep turn: `TranscriptRep` fill, radius 12, padding 10 14. Client turn: no fill, padding 0 14. Meta line 11 px Muted "Ты · 00:08" / "Клиент · 00:16"; text 14 px, line height 21.
  - **Client speaking row** (new, live only): last item, 1 px dashed border in AudioAccent at ~50%, radius 12, padding 10 14; header "Клиент говорит" 11 px SemiBold `AudioAccent` with three 3 px bars whose heights follow `OutputMeter` level (no timer-driven animation). Shows the interim text if the speech worker provides one; otherwise just the header. Hidden when output level is below the VAD threshold for > 1.5 s.
  - Empty state (idle): dashed 1 px `Line` box, radius 16, centered chat glyph, "Здесь появится расшифровка" 14 SemiBold + existing helper text 13 Muted.
- Below the feed, top rule 1 px `Line`, padding-top 12:
  - **Topics (replaces `CoverageExpander`)** — always visible: "Что уже обсудили" 12 SemiBold + "N из M" Muted; `SignalsPanel` as a WrapPanel of chips, gap 6. Covered chip: `Selected` fill, `Accent` text, check glyph. Pending chip: 1 px dashed `ControlLine` border, `Muted` text. Idle heading: "Что обсудить".
  - **Manual text (replaces `ManualExpander` header)** — quiet text button "+ Проверить ИИ на тексте" (`Accent`, SemiBold 13). Clicking reveals the existing `SpeakerBox` + `ManualText` + send button in place of the button (keep `ManualKeyDown`, Esc collapses back).
  - Compact: topics collapse to a one-line summary "Обсудили 3 из 6" with a "Темы ▾" button that opens the chip panel in a Popup, and "+ Текст для ИИ".

## 4. Idle state (before Start)

The advice card shows a readiness checklist instead of the old instruction sentence (see `call-idle-day`):
- Label "ПЕРЕД ЗВОНКОМ"; heading 28 px DisplayFont: "Два шага готовы. Нажми «Начать звонок» — подсказка появится после первых слов клиента." (text adapts to how many steps are ready).
- Three rows, min height 56, radius 12, gap 10:
  1. Brief — done when `BriefTitle` and client/goal fields are non-empty; detail line = brief title; link "Изменить" → `Tabs.SelectedIndex=1`.
  2. Audio — done when both devices are selected; detail = "Ты: {mic} · Клиент: {output}"; link "Изменить" → `GoAudio`.
  3. Start — dashed border, number badge "3", helper "Запись начнётся только после нажатия. «Остановить» завершает запись."
  Done badge: 26 px circle `PrimaryFill` with check; pending: 2 px ring with number.
- Footer: `AdviceMeta` "Подсказок пока нет" + quiet button "Посмотреть пример без микрофона →" (`PlayDemo`).
- Status pill shows the idle ring; meters hidden; `ResetVoiceButton` disabled.

## 5. Mini window (new)

A separate borderless `Window` (e.g. `MiniWindow.xaml`), 420×280 default, resizable 360–640 wide, `Topmost=true`, `ShowInTaskbar=false`, remembers position in settings (position only, no content).
- Background: Night palette always (reads best over video). On Windows 11 22621+ with transparency enabled and high contrast off, request Mica/Acrylic backdrop like the main window does; otherwise opaque `Bg` at 94%. Corner radius 16, 1 px `Line` border, shadow.
- Row 1: live dot + timer (`Danger`), "Клиент говорит" indicator with the same real-level bars; right: 36×36 copy button and 36×36 "Развернуть" button (restores and activates the main window, closes mini).
- Row 2: 3×12 `Metal` bar + current `AdviceTitle` uppercase 11 Bold `Accent`.
- Row 3: current `SayText`, DisplayFont 20/27 Medium, wraps, trims at 4 lines with the full text in tooltip.
- Row 4: top rule, 11 px line "Тон ↑ выше · Громкость как обычно · Темп ↓ медленнее · Паузы ↑ длиннее" from the same `VoiceReading` (changed values Ink SemiBold, usual Muted).
- Drag anywhere on the background to move. Esc or "Развернуть" returns to the main window. Opening mini minimizes the main window; the call keeps running.
- Everything shown is bound to the same state as the main window — no second copy of call logic. Available only while live or in demo; the button is disabled otherwise.
- Keyboard: Tab order copy → expand; `AutomationProperties.Name` on both icon buttons.

## 6. Compact mode

Keep the existing trigger `ActualHeight<820 || ActualWidth<1150` in `Interface.cs`; update the numbers there to the compact values given in each section above. At 1000×720 the whole phrase (ordinary ~100 characters), all four voice values and the header must be visible without scrolling, as in `call-live-1000x720`.

## 7. Colors

Use existing keys. Add:

| Key | Night | Day | Use |
| --- | --- | --- | --- |
| `LivePill` | `#3A221D` | `#F6E1DC` | live status pill background |
| `LiveDot` | `#FF8B74` | `#A32E23` | live dot fill |

High-contrast branch in `UiTheme.Apply`: `LivePill` → WindowColor, `LiveDot` → HighlightColor. Check `Danger` on `LivePill` ≥ 4.5:1 in both themes.

## 8. Motion (only these)

- Live dot pulse 1.8 s (see 2). "Client speaking" bars follow the real output level (no free-running animation). New advice reveal 180 ms (existing). Mini window open: opacity .7→1 + 4 px, 160 ms.
- All respect `UiMotion.Enabled` (animation setting, high contrast, keyboard input).

## 9. Tests to update / add

- `DisclosureTests.cs` depends on `TopStatusBox`, `CoverageExpander`, `ManualExpander`. Rewrite for the new layout: nothing clips at 1000×720 and 1320×860 in both themes and all three UI languages; topics panel and manual input fit; status banner does not push the voice strip off-screen.
- `SignatureTests.cs` / `AppearanceTests.cs`: update rail width and removed elements.
- New: status pill text/colour per state (idle / demo / live); Start↔Stop swap; overflow menu items call the same handlers; mini window binds to the same advice and closes back to main; high contrast colours for new keys.
- Update `docs/redesign/after/*` screenshots via the existing exporter and add a short 0.7 section to `DESIGN.md`. Bump `<Version>` to 0.7.0.

## Required checks before pushing

Release build, `--self-test`, `--ui-smoke` (see `AGENTS.md`). Synthetic content only. Inspect staged files: no settings, keys, transcripts or recordings.
