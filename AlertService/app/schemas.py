"""Camel-case API shapes and bounded user input for the alert module."""

from datetime import datetime
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field


def to_camel(name: str) -> str:
    first, *rest = name.split("_")
    return first + "".join(part.title() for part in rest)


class ApiModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, from_attributes=True)


class BehaviorWrite(ApiModel):
    code: str = Field(pattern=r"^[A-Z][A-Z0-9_]+$", max_length=100)
    name_vi: str = Field(min_length=1, max_length=160)
    name_en: str = Field(min_length=1, max_length=160)
    detect_template: str = Field(min_length=1, max_length=80)
    severity: Literal["INFO", "WARNING", "CRITICAL"]
    zone_type: str = Field(min_length=1, max_length=80)
    parameters: dict = Field(default_factory=dict)
    is_active: bool = True


class BehaviorRead(BehaviorWrite):
    id: str
    version: str
    is_system: bool
    created_at: datetime


class AssignRequest(ApiModel):
    assignee: str = Field(min_length=1, max_length=160)


class CloseRequest(ApiModel):
    reason: str = Field(min_length=1, max_length=1000)


class RemediationWrite(ApiModel):
    description: str = Field(min_length=1, max_length=2000)
    person: str = Field(min_length=1, max_length=160)
    result: str = Field(min_length=1, max_length=500)


class RemediationRead(RemediationWrite):
    id: str
    alert_id: str
    created_at: datetime


class AlertRead(ApiModel):
    id: str
    title: str
    camera_id: str
    camera_name: str
    place_id: str | None
    place_name: str | None
    behavior_code: str
    behavior_version: str
    behavior_name: str
    severity: str
    zone_id: str
    in_zone: bool
    confidence: float
    posture: str
    bbox: list
    skeleton: list
    metrics: dict
    snapshot_url: str
    clip_url: str | None
    clip_status: str
    detected_at: datetime
    status: str
    assignee: str | None
    reason: str | None
    created_at: datetime
    updated_at: datetime


class AlertDetail(AlertRead):
    remediations: list[RemediationRead]
