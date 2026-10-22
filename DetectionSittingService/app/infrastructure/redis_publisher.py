from redis import Redis

from app.core.config import settings
from app.domain.events import BehaviorDetected


redis_client = Redis.from_url(settings.redis_url, decode_responses=True, socket_timeout=5)


def publish_behavior(event: BehaviorDetected) -> str:
    return redis_client.xadd("analytics:events", {"payload": event.model_dump_json()},
                             maxlen=100_000, approximate=True)

