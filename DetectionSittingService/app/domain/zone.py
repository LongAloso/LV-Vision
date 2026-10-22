from collections.abc import Sequence


Point = tuple[float, float]


def point_in_polygon(point: Point, polygon: Sequence[Point]) -> bool:
    if len(polygon) < 3:
        return False
    x, y = point
    inside = False
    for index, (x1, y1) in enumerate(polygon):
        x2, y2 = polygon[(index + 1) % len(polygon)]
        cross = (x - x1) * (y2 - y1) - (y - y1) * (x2 - x1)
        if abs(cross) <= 1e-6 and min(x1, x2) - 1e-6 <= x <= max(x1, x2) + 1e-6 \
                and min(y1, y2) - 1e-6 <= y <= max(y1, y2) + 1e-6:
            return True
        if (y1 > y) != (y2 > y):
            intersection_x = (x2 - x1) * (y - y1) / (y2 - y1) + x1
            if x < intersection_x:
                inside = not inside
    return inside


def valid_polygon(polygon: Sequence[Point]) -> bool:
    if len(polygon) < 3 or any(x < 0 or y < 0 for x, y in polygon):
        return False
    double_area = sum(x1 * y2 - x2 * y1 for (x1, y1), (x2, y2)
                      in zip(polygon, polygon[1:] + polygon[:1]))
    return abs(double_area) > 1e-6

