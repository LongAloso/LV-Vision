from threading import Lock

from ultralytics import YOLO

from app.core.config import settings


class PoseModel:
    def __init__(self) -> None:
        self.model = YOLO(settings.model_path)
        self.lock = Lock()

    def predict(self, frame):
        with self.lock:
            return self.model.predict(frame, conf=0.4, imgsz=settings.model_image_size,
                                      device=settings.model_device, verbose=False)[0]

