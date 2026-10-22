"""Use the same current-session and permission check as AlertService."""

import os

import httpx
import jwt
from fastapi import Header, HTTPException


async def require_alert_read(authorization: str | None = Header(default=None)) -> dict:
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(401, "Thiếu Bearer token")
    token = authorization[7:]
    try:
        claims = jwt.decode(token, os.environ["JWT_KEY"], algorithms=["HS256"],
                            issuer=os.environ["JWT_ISSUER"], audience=os.environ["JWT_AUDIENCE"],
                            options={"require": ["uid", "exp", "nbf"]}, leeway=60)
    except (jwt.PyJWTError, KeyError) as exc:
        raise HTTPException(401, "JWT không hợp lệ") from exc
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            response = await client.get(os.environ["IDENTITY_URL"].rstrip("/") + "/api/auth/me",
                                        headers={"Authorization": f"Bearer {token}"})
    except httpx.HTTPError as exc:
        raise HTTPException(503, "IdentityService chưa sẵn sàng") from exc
    if response.status_code != 200:
        raise HTTPException(401, "Phiên đăng nhập không còn hợp lệ")
    user = response.json()
    if user.get("userId") != claims["uid"] or not user.get("isActive"):
        raise HTTPException(401, "Người dùng không còn hoạt động")
    if not set(user.get("permissions", [])) & {"Alert.Read", "Alert.Manage"}:
        raise HTTPException(403, "Thiếu quyền Alert.Read")
    return user
