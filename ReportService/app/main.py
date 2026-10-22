from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from sqlalchemy import text

from app.routers.statistics import router as statistics_router
from app.store import engine

app = FastAPI(title="LV-Vision Report Service")
app.include_router(statistics_router)


@app.exception_handler(HTTPException)
async def http_error(_: Request, exc: HTTPException) -> JSONResponse:
    code = {401: "unauthorized", 403: "forbidden", 422: "validation_error",
            503: "dependency_unavailable"}.get(exc.status_code, "bad_request")
    return JSONResponse(status_code=exc.status_code, content={"code": code, "message": str(exc.detail)})


@app.exception_handler(RequestValidationError)
async def validation_error(_: Request, exc: RequestValidationError) -> JSONResponse:
    return JSONResponse(status_code=422, content={"code": "validation_error",
                                                 "message": f"Dữ liệu không hợp lệ: {exc.errors()[0]['msg']}"})


@app.get("/health")
def health() -> dict:
    with engine.connect() as connection:
        connection.execute(text("SELECT 1 FROM alert.alerts LIMIT 1"))
    return {"status": "OK", "service": "report-service"}
