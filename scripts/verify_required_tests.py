"""Fail release verification when mandatory PostgreSQL/HTTP tests are absent or skipped."""
import sys
import re
import xml.etree.ElementTree as ET

GROUP_MINIMUMS = {
    "VisitorVehicleConcurrencyTests": 4,
    "VerificationStoreTests": 25,
    "VehiclePlateConflictTests": 7,
    "DatabasePlateOwnershipTests": 9,
    "MigrationRehearsalTests": 2,
    "AuthenticatedHttpIntegrationTests": 5,
}
VISITOR_METHODS = {
    "ConcurrentFirstRegistrationCreatesOnlyOnePersonVehicleAndSession",
    "ConcurrentReentryCreatesOneActiveSessionAndPreservesCompletedHistory",
    "ConcurrentExitCompletesTheSelectedSessionOnlyOnce",
    "TwoVehiclesUseIndependentCarAndMotorcycleCapacity",
}


def verify(root, minimum_total=548):
    results = [x for x in root.iter() if x.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
    errors = []
    if sum(x.get("outcome") == "Passed" for x in results) < minimum_total:
        errors.append("Full-suite passing test count is below the Phase 4 baseline.")
    for group, count in GROUP_MINIMUMS.items():
        selected = [x for x in results if ("." + group + ".") in x.get("testName", "")
                    and ".Measure" not in x.get("testName", "")]
        if len(selected) < count:
            errors.append(f"Required group missing/incomplete: {group} (minimum {count}).")
        if any(x.get("outcome") != "Passed" for x in selected):
            errors.append(f"Required group contains failed/skipped tests: {group}.")
    runtime_output = " ".join((item.text or "") for result in results if ".MigrationRehearsalTests." in result.get("testName", "")
                              for item in result.iter() if item.tag.rsplit("}", 1)[-1] == "StdOut")
    runtime = re.search(r"Verification runtime: \.NET (\d+)\.(\d+)\.(\d+)", runtime_output)
    if not runtime or tuple(map(int, runtime.groups())) < (10, 0, 13):
        errors.append("Required execution evidence for patched .NET 10.0.13 or later is missing.")
    for method in VISITOR_METHODS:
        if not any(x.get("testName", "").endswith("VisitorVehicleConcurrencyTests." + method)
                   and x.get("outcome") == "Passed" for x in results):
            errors.append(f"Required visitor concurrency scenario missing or unpassed: {method}.")
    return errors


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: verify_required_tests.py <full-suite.trx>")
    errors = verify(ET.parse(sys.argv[1]).getroot())
    if errors:
        raise SystemExit("\n".join(errors))
    print("Required PostgreSQL, migration, HTTP and full-suite execution gate PASS.")
