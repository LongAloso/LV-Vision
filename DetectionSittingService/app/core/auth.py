import hmac

import httpx
import jwt
from fastapi import Header, HTTPException

from app.core.config import settings


def is_internal(key: str | None) -> bool:
    return bool(settings.internal_api_key and key and
                hmac.compare_digest(settings.internal_api_key, key))


async def _current_user(token: str, claims: dict) -> dict:
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            response = await client.get(f"{settings.identity_url}/api/auth/me",
                                        headers={"Authorization": f"Bearer {token}"})
    except httpx.HTTPError as exc:
        raise HTTPException(503, "IdentityService chưa sẵn sàng") from exc
    if response.status_code != 200:
        raise HTTPException(401, "Phiên đăng nhập không còn hợp lệ")
    user = response.json()
    if user.get("userId") != claims["uid"] or not user.get("isActive", False):
        raise HTTPException(401, "Phiên đăng nhập không còn hợp lệ")
    return user


def _verify(authorization: str | None) -> tuple[str, dict]:
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(401, "Thiếu Bearer token")
    token = authorization[7:]
    try:
        claims = jwt.decode(token, settings.jwt_key, algorithms=["HS256"],
                            issuer=settings.jwt_issuer, audience=settings.jwt_audience,
                            options={"require": ["uid", "exp", "nbf"]}, leeway=60)
    except (jwt.PyJWTError, KeyError) as exc:
        raise HTTPException(401, "JWT không hợp lệ") from exc
    return token, claims


async def require_access(authorization: str | None, internal_key: str | None,
                         publish: bool = False) -> None:
    if is_internal(internal_key):
        return
    if publish:
        raise HTTPException(403, "Chỉ worker nội bộ được phát sự kiện")
    token, claims = _verify(authorization)
    user = await _current_user(token, claims)
    if not set(user.get("permissions", [])) & {"Camera.Read", "Camera.Manage"}:
        raise HTTPException(403, "Thiếu quyền Camera.Read")


async def view_evidence(authorization: str | None = Header(default=None),
                        x_internal_key: str | None = Header(default=None)) -> None:
    await require_access(authorization, x_internal_key)


async def require_manage(authorization: str | None, internal_key: str | None) -> None:
    if is_internal(internal_key):
        return
    token, claims = _verify(authorization)
    user = await _current_user(token, claims)
    if "Camera.Manage" not in user.get("permissions", []):
        raise HTTPException(403, "Thiếu quyền Camera.Manage")

