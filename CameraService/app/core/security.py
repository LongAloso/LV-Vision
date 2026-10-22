"""Encrypt stored RTSP URLs; only the internal runtime API decrypts them."""

import base64
import hashlib

from cryptography.fernet import Fernet

from app.core.config import settings


PREFIX = "enc:v1:"
_key_material = settings.camera_rtsp_key.encode("utf-8")
if len(_key_material) < 32:
    raise RuntimeError("CAMERA_RTSP_KEY cần ít nhất 32 ký tự")
_cipher = Fernet(base64.urlsafe_b64encode(hashlib.sha256(_key_material).digest()))


def encrypt_rtsp(url: str) -> str:
    return PREFIX + _cipher.encrypt(url.encode("utf-8")).decode("ascii")


def decrypt_rtsp(value: str) -> str:
    if not value.startswith(PREFIX):
        return value
    return _cipher.decrypt(value[len(PREFIX):].encode("ascii")).decode("utf-8")

