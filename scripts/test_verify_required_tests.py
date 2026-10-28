import unittest
import xml.etree.ElementTree as ET
from scripts.verify_required_tests import GROUP_MINIMUMS, VISITOR_METHODS, verify


class RequiredTestsGateTests(unittest.TestCase):
    def result(self):
        root = ET.Element("TestRun")
        for group, count in GROUP_MINIMUMS.items():
            names = sorted(VISITOR_METHODS) if group == "VisitorVehicleConcurrencyTests" else [f"Case{i}" for i in range(count)]
            for name in names:
                result = ET.SubElement(root, "UnitTestResult", testName=f"Test.{group}.{name}", outcome="Passed")
                if group == "MigrationRehearsalTests": ET.SubElement(result,"StdOut").text="Verification runtime: .NET 10.0.13"
        return root

    def test_complete_groups_pass(self):
        self.assertEqual([], verify(self.result(), 0))

    def test_missing_tests_fail(self):
        root = self.result(); root.remove(root[0])
        self.assertTrue(verify(root, 0))

    def test_skipped_test_fails(self):
        root = self.result(); root[0].set("outcome", "NotExecuted")
        self.assertTrue(verify(root, 0))

    def test_failed_test_fails(self):
        root = self.result(); root[0].set("outcome", "Failed")
        self.assertTrue(verify(root, 0))

    def test_unpatched_execution_runtime_fails(self):
        root=self.result()
        for item in root.iter("StdOut"): item.text="Verification runtime: .NET 10.0.12"
        self.assertTrue(verify(root,0))

    def test_empty_or_filtered_suite_fails(self):
        self.assertTrue(verify(ET.Element("TestRun")))
        self.assertTrue(verify(self.result()))


if __name__ == "__main__":
    unittest.main()
