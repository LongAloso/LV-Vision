"""HTTP client for live authorization state from IdentityService."""

import httpx

from app.core.config import settings


async def get_current_user(token: str) -> dict:
    async with httpx.AsyncClient(timeout=5) as client:
        response = await client.get(
            f"{settings.identity_url}/api/auth/me",
            headers={"Authorization": f"Bearer {token}"},
        )
    response.raise_for_status()
    return response.json()

