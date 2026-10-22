from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.infrastructure.db.models import Scenario


def get(db: Session, scenario_id: str) -> Scenario | None:
    return db.get(Scenario, scenario_id)


def page(db: Session, camera_id: str | None, offset: int, limit: int) -> tuple[int, list[Scenario]]:
    query = select(Scenario)
    if camera_id is not None:
        query = query.where(Scenario.camera_id == camera_id)
    total = db.scalar(select(func.count()).select_from(query.subquery())) or 0
    rows = list(db.scalars(query.order_by(Scenario.created_at, Scenario.id)
                           .offset(offset).limit(limit)).all())
    return total, rows


def add(db: Session, scenario: Scenario) -> Scenario:
    db.add(scenario)
    return scenario


def remove(db: Session, scenario: Scenario) -> None:
    db.delete(scenario)

