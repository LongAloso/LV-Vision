import threading

from app.core.config import settings
from app.inference.detector import SittingDetector
from app.infrastructure.camera_client import CameraServiceClient
from app.streaming.reader import StreamWorker


class StreamManager:
    def __init__(self, detector: SittingDetector,
                 camera_client: CameraServiceClient | None = None,
                 capacity: int | None = None) -> None:
        self.detector = detector
        self.camera_client = camera_client or CameraServiceClient()
        self.capacity = capacity if capacity is not None else settings.stream_capacity
        self.workers: dict[str, StreamWorker] = {}
        self.lock = threading.Lock()

    def start(self, camera_id: str) -> str:
        with self.lock:
            if camera_id in self.workers:
                return "already_running"
            if len(self.workers) >= self.capacity:
                return "capacity_reached"
        runtime = self.camera_client.get_runtime(camera_id)
        if not runtime.get("rtspUrl", "").startswith(("rtsp://", "rtsps://")):
            raise ValueError("Camera không có RTSP URL")
        worker = StreamWorker(camera_id, runtime, self.detector, self.camera_client)
        with self.lock:
            if camera_id in self.workers:
                return "already_running"
            if len(self.workers) >= self.capacity:
                return "capacity_reached"
            self.workers[camera_id] = worker
        worker.start()
        return "started"

    def stop(self, camera_id: str) -> bool:
        with self.lock:
            worker = self.workers.pop(camera_id, None)
        if worker is None:
            return False
        worker.stop()
        return True

    def stop_all(self) -> None:
        for camera_id in list(self.workers):
            self.stop(camera_id)

    def statuses(self) -> list[dict]:
        with self.lock:
            return [{"cameraId": camera_id, "running": worker.thread.is_alive(),
                     "connected": worker.connected, "bufferedFrames": len(worker.buffer)}
                    for camera_id, worker in self.workers.items()]

