"""JWT verification and FastAPI authorization dependencies."""

import hmac

import httpx
import jwt
from fastapi import Depends, Header, HTTPException, status

from app.core.config import settings
from app.infrastructure.identity_client import get_current_user


def verify_token(token: str) -> dict:
    try:
        return jwt.decode(
            token, settings.jwt_key, algorithms=["HS256"],
            issuer=settings.jwt_issuer, audience=settings.jwt_audience,
            options={"require": ["exp", "nbf", "uid"]}, leeway=60,
        )
    except (jwt.PyJWTError, KeyError) as exc:
        raise HTTPException(401, "JWT không hợp lệ hoặc đã hết hạn") from exc


async def authorize_token(token: str, permission: str) -> tuple[dict, dict]:
    claims = verify_token(token)
    try:
        user = await get_current_user(token)
    except httpx.HTTPStatusError as exc:
        raise HTTPException(401, "Phiên đăng nhập không còn hợp lệ") from exc
    except httpx.HTTPError as exc:
        raise HTTPException(503, "IdentityService chưa sẵn sàng") from exc
    if user.get("userId") != claims["uid"] or not user.get("isActive", False):
        raise HTTPException(401, "Người dùng không còn hoạt động")
    permissions = set(user.get("permissions", []))
    permitted = bool(permissions & {"Camera.Read", "Camera.Manage"}) \
        if permission == "Camera.Read" else permission in permissions
    if not permitted:
        raise HTTPException(403, f"Thiếu quyền {permission}")
    return claims, user


def bearer_token(authorization: str | None = Header(default=None)) -> str:
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Thiếu Bearer token")
    return authorization[7:]


async def require_read(token: str = Depends(bearer_token)) -> dict:
    _, user = await authorize_token(token, "Camera.Read")
    return user


async def require_manage(token: str = Depends(bearer_token)) -> dict:
    _, user = await authorize_token(token, "Camera.Manage")
    return user


def require_internal_key(x_internal_key: str | None = Header(default=None)) -> None:
    if not settings.internal_api_key or not x_internal_key or not hmac.compare_digest(
            settings.internal_api_key, x_internal_key):
        raise HTTPException(403, "Internal key không hợp lệ")

