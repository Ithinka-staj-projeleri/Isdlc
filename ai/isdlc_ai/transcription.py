"""Speech to text for meeting recordings, on this machine: the audio never leaves it.

Uses faster-whisper, which is optional (requirements-audio.txt; the Docker image installs it). Without it the service
still runs and says transcription is off. The model is loaded on first use and kept; its size comes from WHISPER_MODEL.
Decoding is done here with PyAV (installed with faster-whisper), so no ffmpeg program is needed.
"""

import importlib.util
import threading
from dataclasses import dataclass
from pathlib import Path


class TranscriptionError(Exception):
    """The file could not be read as audio."""


@dataclass
class Transcript:
    text: str  # one segment (about a sentence) per line
    duration_seconds: float
    language: str
    model_name: str


def available() -> bool:
    return importlib.util.find_spec("faster_whisper") is not None


class Transcriber:
    def __init__(self, model_name: str, device: str = "cpu", compute_type: str = "int8"):
        self.model_name = model_name
        self._device = device
        self._compute_type = compute_type
        self._model = None
        self._lock = threading.Lock()

    def transcribe(self, path: Path, language: str = "tr") -> Transcript:
        audio, duration = _decode(path)
        segments, info = self._load().transcribe(audio, language=language, vad_filter=True)
        lines = [segment.text.strip() for segment in segments if segment.text.strip()]
        return Transcript("\n".join(lines), duration, info.language, self.model_name)

    def _load(self):
        # One model per process; loading takes seconds and a few hundred MB, so requests share it.
        with self._lock:
            if self._model is None:
                from faster_whisper import WhisperModel

                self._model = WhisperModel(self.model_name, device=self._device, compute_type=self._compute_type)
            return self._model


_SAMPLE_RATE = 16_000


def _decode(path: Path):
    """The first audio track as 16 kHz mono float samples, which is what Whisper expects."""
    import av
    import numpy as np

    try:
        with av.open(str(path)) as container:
            if not container.streams.audio:
                raise TranscriptionError("Kayıtta ses yok.")
            resampler = av.AudioResampler(format="s16", layout="mono", rate=_SAMPLE_RATE)
            chunks = []
            for frame in container.decode(audio=0):
                for resampled in resampler.resample(frame):
                    chunks.append(resampled.to_ndarray().reshape(-1))
            for resampled in resampler.resample(None):  # what the resampler still holds
                chunks.append(resampled.to_ndarray().reshape(-1))
    except av.FFmpegError as error:
        raise TranscriptionError(str(error)) from error

    if not chunks:
        raise TranscriptionError("Kayıtta ses yok.")
    audio = np.concatenate(chunks).astype(np.float32) / 32768.0
    return audio, len(audio) / _SAMPLE_RATE
