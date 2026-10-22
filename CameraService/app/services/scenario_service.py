from uuid import uuid4

from fastapi import HTTPException
from sqlalchemy.orm import Session

from app.infrastructure.db.models import Scenario
from app.infrastructure.db.repositories import cameras, scenarios
from app.schemas import Page, ScenarioRead, ScenarioWrite


DEFAULT_DETECTORS = {"PERSON_SITTING": "http://detection-sitting:8001/detect"}


def detector_registry() -> dict[str, str]:
    return DEFAULT_DETECTORS


def _check_detector(body: ScenarioWrite) -> None:
    if not body.enabled:
        return
    expected = detector_registry().get(body.behavior_code)
    if expected is None:
        raise HTTPException(422, f"Chưa có detector cho {body.behavior_code}")
    if body.detector_url != expected:
        raise HTTPException(422, "detectorUrl không khớp detector đã đăng ký")
    if body.behavior_code == "PERSON_SITTING" and body.zone_mode != "ANY":
        raise HTTPException(422, "Detector PERSON_SITTING hiện chỉ hỗ trợ zoneMode ANY")


def _check_camera(db: Session, camera_id: str) -> None:
    if cameras.get(db, camera_id) is None:
        raise HTTPException(404, f"Không tìm thấy camera {camera_id}")


def get_scenario(db: Session, scenario_id: str) -> Scenario:
    scenario = scenarios.get(db, scenario_id)
    if scenario is None:
        raise HTTPException(404, f"Không tìm thấy kịch bản {scenario_id}")
    return scenario


def list_scenarios(db: Session, camera_id: str | None, page: int, page_size: int) -> Page:
    total, rows = scenarios.page(db, camera_id, (page - 1) * page_size, page_size)
    return Page(items=[ScenarioRead.model_validate(row).model_dump(mode="json", by_alias=True) for row in rows],
                page=page, page_size=page_size, total=total)


def create_scenario(db: Session, body: ScenarioWrite) -> Scenario:
    _check_camera(db, body.camera_id)
    _check_detector(body)
    scenario = Scenario(id=f"scenario-{uuid4().hex[:10]}", **body.model_dump())
    scenarios.add(db, scenario)
    db.commit()
    db.refresh(scenario)
    return scenario


def update_scenario(db: Session, scenario_id: str, body: ScenarioWrite) -> Scenario:
    scenario = get_scenario(db, scenario_id)
    _check_camera(db, body.camera_id)
    _check_detector(body)
    for key, value in body.model_dump().items():
        setattr(scenario, key, value)
    db.commit()
    db.refresh(scenario)
    return scenario


def delete_scenario(db: Session, scenario_id: str) -> None:
    scenarios.remove(db, get_scenario(db, scenario_id))
    db.commit()

