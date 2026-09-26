"""Offline known-signal checks; synthetic tones do not validate real emotion/speech accuracy."""
import json
import time
import unittest
import numpy as np
from prosody_worker import SR, FRAME, yin, measure


def tone(f, seconds=2, amplitude=.15):
    t = np.arange(int(SR * seconds)) / SR
    return (amplitude * (np.sin(2 * np.pi * f * t) + .2 * np.sin(4 * np.pi * f * t))).astype(np.float32)


class AcousticTests(unittest.TestCase):
    def test_pitch_for_different_voices(self):
        for f in [70, 100, 150, 220, 300, 450]:
            with self.subTest(f=f):
                self.assertAlmostEqual(yin(tone(f)[:FRAME]), f, delta=2)

    def test_aperiodic_noise_and_silence_rejected(self):
        self.assertIsNone(yin(np.zeros(FRAME)))
        self.assertIsNone(yin(np.random.default_rng(7).normal(0, .1, FRAME)))

    def test_level_and_pitch_independent(self):
        a = tone(180)
        spans = [dict(start=0, end=len(a))]
        high, low = measure(a, spans), measure(a / 2, spans)
        self.assertAlmostEqual(high['levelDb'] - low['levelDb'], 6.0206, places=3)
        self.assertAlmostEqual(high['pitchHz'], low['pitchHz'], places=3)
        self.assertEqual(high['quality'], 'ok')

    def test_internal_pause_and_no_cross_turn_pause(self):
        a = np.concatenate([tone(180, 1), np.zeros(SR // 2), tone(180, 1)])
        result = measure(a, [dict(start=0, end=SR), dict(start=SR * 3 // 2, end=len(a))])
        self.assertEqual(result['pauses'], [.5])
        self.assertEqual(result['speechSeconds'], 2)

    def test_clipping_and_short_audio(self):
        a = tone(180)
        self.assertEqual(measure(a * 12, [dict(start=0, end=len(a))])['quality'], 'clipped')
        self.assertEqual(measure(a[:1600], [dict(start=0, end=1600)])['quality'], 'insufficient')

    def test_json_finite_for_silence(self):
        result = measure(np.zeros(SR), [])
        json.dumps(result, allow_nan=False)
        self.assertIsNone(result['pitchHz'])
        self.assertEqual(result['quality'], 'insufficient')

    def test_local_vad_rejects_silence(self):
        from faster_whisper.vad import get_speech_timestamps
        self.assertEqual(get_speech_timestamps(np.zeros(SR, dtype=np.float32)), [])

    def test_processing_budget(self):
        a = tone(180, 8)
        start = time.perf_counter()
        measure(a, [dict(start=0, end=len(a))])
        elapsed = time.perf_counter() - start
        print(f'8 seconds synthetic acoustic processing: {elapsed:.3f}s')
        self.assertLess(elapsed, 3, 'CPU processing cannot keep up with capture')


if __name__ == '__main__':
    unittest.main()
