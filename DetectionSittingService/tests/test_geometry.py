import sys
import unittest
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.domain.posture import posture_metrics  # noqa: E402
from app.domain.zone import point_in_polygon, valid_polygon  # noqa: E402


class GeometryTests(unittest.TestCase):
    def test_boundary_is_inside_polygon(self):
        zone = [(0, 0), (100, 0), (100, 100), (0, 100)]
        self.assertTrue(valid_polygon(zone))
        self.assertTrue(point_in_polygon((0, 50), zone))
        self.assertTrue(point_in_polygon((50, 50), zone))
        self.assertFalse(point_in_polygon((101, 50), zone))

    def test_degenerate_polygon_rejected(self):
        self.assertFalse(valid_polygon([(0, 0), (1, 1), (2, 2)]))

    def test_seated_hip_and_knee(self):
        joints = [None] * 17
        joints[5], joints[11], joints[13], joints[15] = (0, 0), (0, 1), (1, 1), (1, 2)
        posture, torso, hip = posture_metrics(joints)
        self.assertEqual("SITTING", posture)
        self.assertAlmostEqual(90, torso)
        self.assertAlmostEqual(90, hip)

    def test_standing_does_not_match(self):
        joints = [None] * 17
        joints[5], joints[11], joints[13], joints[15] = (0, 0), (0, 1), (0, 2), (0, 3)
        self.assertEqual("OTHER", posture_metrics(joints)[0])

    def test_missing_joints_do_not_match(self):
        self.assertEqual("OTHER", posture_metrics([None] * 17)[0])


if __name__ == "__main__":
    unittest.main()
