"""Verify JWT plus current Identity permission and active session."""

import os

import httpx
import jwt
from fastapi import Depends, Header, HTTPException


def bearer_token(authorization: str | None = Header(default=None)) -> str:
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(401, "Thiếu Bearer token")
    return authorization[7:]


async def authorize(token: str, permission: str) -> dict:
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
    if user.get("userId") != claims["uid"] or not user.get("isActive", False):
        raise HTTPException(401, "Người dùng không còn hoạt động")
    permissions = set(user.get("permissions", []))
    allowed = ({permission, permission.replace(".Read", ".Manage")}
               if permission.endswith(".Read") else {permission})
    if not permissions & allowed:
        raise HTTPException(403, f"Thiếu quyền {permission}")
    return user


async def require_alert_read(token: str = Depends(bearer_token)) -> dict:
    return await authorize(token, "Alert.Read")


async def require_alert_manage(token: str = Depends(bearer_token)) -> dict:
    return await authorize(token, "Alert.Manage")


async def require_behavior_read(token: str = Depends(bearer_token)) -> dict:
    return await authorize(token, "Behavior.Read")


async def require_behavior_manage(token: str = Depends(bearer_token)) -> dict:
    return await authorize(token, "Behavior.Manage")
