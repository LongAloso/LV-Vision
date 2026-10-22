import threading
from collections import deque

import cv2
import numpy as np


class FrameBuffer:
    def __init__(self, seconds: float = 30) -> None:
        self.seconds = seconds
        self.frames: deque[tuple[float, bytes]] = deque()
        self.lock = threading.Lock()

    def add(self, timestamp: float, image: np.ndarray) -> None:
        ok, encoded = cv2.imencode(".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, 83])
        if not ok:
            return
        with self.lock:
            self.frames.append((timestamp, encoded.tobytes()))
            while self.frames and timestamp - self.frames[0][0] > self.seconds:
                self.frames.popleft()

    def between(self, start: float, end: float) -> list[tuple[float, bytes]]:
        with self.lock:
            return [(timestamp, image) for timestamp, image in self.frames if start <= timestamp <= end]

    def latest(self) -> bytes | None:
        with self.lock:
            return self.frames[-1][1] if self.frames else None

    def __len__(self) -> int:
        with self.lock:
            return len(self.frames)

