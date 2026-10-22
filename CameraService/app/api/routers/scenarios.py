from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session

from app.api.dependencies import get_session, require_manage, require_read
from app.infrastructure.db.models import Scenario
from app.schemas import Page, ScenarioRead, ScenarioWrite
from app.services import scenario_service


router = APIRouter(prefix="/api/scenarios", tags=["scenarios"])


@router.get("")
def list_scenarios(camera_id: str | None = Query(default=None, alias="cameraId"),
                   page: int = Query(1, ge=1), page_size: int = Query(10, alias="pageSize", ge=1, le=100),
                   _: dict = Depends(require_read), db: Session = Depends(get_session)) -> Page:
    return scenario_service.list_scenarios(db, camera_id, page, page_size)


@router.get("/detectors")
def list_detectors(_: dict = Depends(require_read)) -> dict:
    return {"items": [{"behaviorCode": code, "detectorUrl": url}
                      for code, url in sorted(scenario_service.detector_registry().items())]}


@router.post("", status_code=201, response_model=ScenarioRead)
def create_scenario(body: ScenarioWrite, _: dict = Depends(require_manage),
                    db: Session = Depends(get_session)) -> Scenario:
    return scenario_service.create_scenario(db, body)


@router.get("/{scenario_id}", response_model=ScenarioRead)
def get_scenario(scenario_id: str, _: dict = Depends(require_read),
                 db: Session = Depends(get_session)) -> Scenario:
    return scenario_service.get_scenario(db, scenario_id)


@router.put("/{scenario_id}", response_model=ScenarioRead)
def update_scenario(scenario_id: str, body: ScenarioWrite, _: dict = Depends(require_manage),
                    db: Session = Depends(get_session)) -> Scenario:
    return scenario_service.update_scenario(db, scenario_id, body)


@router.delete("/{scenario_id}", status_code=204)
def delete_scenario(scenario_id: str, _: dict = Depends(require_manage),
                    db: Session = Depends(get_session)) -> None:
    scenario_service.delete_scenario(db, scenario_id)

