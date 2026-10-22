"""ReportService reads AlertService data; it never owns or changes alert rows."""

import os
from collections.abc import Generator

from sqlalchemy import create_engine
from sqlalchemy.engine import Connection

engine = create_engine(os.environ["DATABASE_URL"], pool_pre_ping=True)


def get_connection() -> Generator[Connection, None, None]:
    with engine.connect() as connection:
        yield connection
