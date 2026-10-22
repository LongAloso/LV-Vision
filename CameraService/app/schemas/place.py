from datetime import datetime

from pydantic import Field

from app.schemas.common import ApiModel


class PlaceWrite(ApiModel):
    name: str = Field(min_length=1, max_length=160)
    floor: str | None = Field(default=None, max_length=80)
    description: str | None = Field(default=None, max_length=500)
    plc_device: str | None = Field(default=None, max_length=160)


class PlaceRead(PlaceWrite):
    id: str
    created_at: datetime

