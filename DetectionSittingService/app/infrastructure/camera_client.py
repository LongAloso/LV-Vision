import httpx

from app.core.config import settings


class CameraServiceClient:
    @property
    def headers(self) -> dict[str, str]:
        return {"X-Internal-Key": settings.internal_api_key}

    def get_runtime(self, camera_id: str) -> dict:
        with httpx.Client(timeout=6) as client:
            response = client.get(f"{settings.camera_service_url}/api/cameras/{camera_id}/runtime",
                                  headers=self.headers)
            response.raise_for_status()
            return response.json()

    def update_status(self, camera_id: str, payload: dict) -> None:
        with httpx.Client(timeout=4) as client:
            response = client.put(f"{settings.camera_service_url}/api/cameras/{camera_id}/status",
                                  headers=self.headers, json=payload)
            response.raise_for_status()

