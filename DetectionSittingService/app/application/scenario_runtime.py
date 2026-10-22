from app.domain.zone import valid_polygon


def target_fps(runtime: dict) -> float:
    return max(5.0, min(10.0, float(runtime.get("targetFps", 10))))


def active_sitting_scenarios(runtime: dict) -> list[dict]:
    return [scenario for scenario in runtime.get("scenarios", [])
            if scenario.get("behaviorCode") == "PERSON_SITTING"
            and valid_polygon(scenario.get("zonePolygon", []))]

