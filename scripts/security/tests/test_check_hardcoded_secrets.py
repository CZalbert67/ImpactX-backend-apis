"""Unit tests for check_hardcoded_secrets.py scanner.

Uses temporary git repositories with synthetic TEST_ONLY values.
Never contains real secrets.
"""

import io
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_hardcoded_secrets as scanner


class TestScannerOnCleanRepo(unittest.TestCase):
    """Test that a clean repository returns exit code 0."""

    def setUp(self):
        self.tmpdir = tempfile.TemporaryDirectory()
        self.root = self.tmpdir.name
        subprocess.run(["git", "init"], cwd=self.root, capture_output=True)
        subprocess.run(["git", "config", "user.email", "test@test.com"], cwd=self.root, capture_output=True)
        subprocess.run(["git", "config", "user.name", "Test"], cwd=self.root, capture_output=True)

    def tearDown(self):
        self.tmpdir.cleanup()

    def _write_and_commit(self, path: str, content: str):
        full_path = os.path.join(self.root, path)
        os.makedirs(os.path.dirname(full_path), exist_ok=True)
        with open(full_path, "w") as f:
            f.write(content)
        subprocess.run(["git", "add", path], cwd=self.root, capture_output=True)

    def test_clean_repo_returns_0(self):
        self._write_and_commit("ImpactXv1/appsettings.json", '{"Jwt": {"Issuer": "Test"}}')
        self._write_and_commit("ImpactXv1/Test.cs", "public class Test { }")
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 0)

    def test_non_empty_secret_in_appsettings_returns_1(self):
        self._write_and_commit("ImpactXv1/appsettings.json",
                               '{"Jwt": {"Secret": "TEST_ONLY_my_secret_value"}}')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        _, _, name, _ = violations[0]
        self.assertIn("Jwt Secret", name)
        self.assertNotIn("TEST_ONLY_my_secret_value", str(violations))

    def test_fallback_literal_via_coalescing_returns_1(self):
        self._write_and_commit("ImpactXv1/JwtConfig.cs",
                               'var x = configuration["Jwt:Secret"] ?? "TEST_ONLY_long_fallback_secret_value";')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("fallback", violations[0][2].lower())

    def test_symmetric_security_key_with_literal_returns_1(self):
        self._write_and_commit("ImpactXv1/JwtConfig.cs",
                               'var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TEST_ONLY_inline_key_value"));')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("SymmetricSecurityKey", violations[0][2])

    def test_begin_private_key_returns_1(self):
        self._write_and_commit("secret.key",
                               "-----BEGIN PRIVATE KEY-----\nABCDEF\n-----END PRIVATE KEY-----")
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("PRIVATE KEY", violations[0][2])

    def test_private_key_assignment_returns_1(self):
        self._write_and_commit("config.json",
                               '{"private_key": "TEST_ONLY_some_private_key_value"}')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("private_key", violations[0][2].lower())

    def test_client_secret_assignment_returns_1(self):
        self._write_and_commit("config.json",
                               '{"client_secret": "TEST_ONLY_some_client_secret"}')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("client_secret", violations[0][2].lower())

    def test_jwt_secret_reference_no_value_does_not_false_positive(self):
        self._write_and_commit("ImpactXv1/JwtConfig.cs",
                               'var secret = configuration["Jwt:Secret"];')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 0)

    def test_jwt_secret_read_from_iconfiguration_no_false_positive(self):
        self._write_and_commit("ImpactXv1/JwtConfig.cs",
                               'var x = JwtSecurityConfiguration.GetRequiredSecret(configuration);')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 0)

    def test_values_in_test_directory_no_false_positive(self):
        self._write_and_commit("ImpactX.Tests/SomeTest.cs",
                               'var x = "TEST_ONLY_test_value_for_unit_test";')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 0)

    def test_untracked_file_with_violation_is_detected(self):
        self._write_and_commit("ImpactXv1/clean.cs", "// clean file")
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        untracked_file = os.path.join(self.root, "ImpactXv1", "appsettings.json")
        os.makedirs(os.path.dirname(untracked_file), exist_ok=True)
        with open(untracked_file, "w") as f:
            f.write('{"Jwt": {"Secret": "TEST_ONLY_untracked_secret"}}')

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("Jwt Secret", violations[0][2])

    def test_report_does_not_contain_secret_value(self):
        self._write_and_commit("ImpactXv1/appsettings.json",
                               '{"Jwt": {"Secret": "TEST_ONLY_my_secret_value"}}')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)

        report_path = os.path.join(self.root, "report.txt")
        scanner.write_report(violations, report_path)
        with open(report_path) as f:
            content = f.read()
        self.assertNotIn("TEST_ONLY_my_secret_value", content)

    def test_jwt_fallback_via_return_returns_1(self):
        self._write_and_commit("ImpactXv1/Infrastructure/Security/JwtTokenService.cs",
                               'return "TEST_ONLY_long_fallback_secret_string_in_return";')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("fallback", violations[0][2].lower())

    def test_issuer_and_audience_no_false_positive(self):
        self._write_and_commit("ImpactXv1/appsettings.json",
                               '{"Jwt": {"Issuer": "ImpactX", "Audience": "ImpactX-Client"}}')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 0)

    def test_firebase_service_account_returns_1(self):
        self._write_and_commit("firebase-creds.json",
                               '{"type": "service_account", "project_id": "test"}')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)

        files = scanner.get_scannable_files(self.root)
        violations = scanner.scan_violations(self.root, files)
        violations = scanner.deduplicate(violations)
        self.assertEqual(len(violations), 1)
        self.assertIn("Firebase", violations[0][2])


class TestScannerMainExitCodes(unittest.TestCase):
    """Test scanner.main() exit codes via temp git repos."""

    def setUp(self):
        self.tmpdir = tempfile.TemporaryDirectory()
        self.root = self.tmpdir.name
        subprocess.run(["git", "init"], cwd=self.root, capture_output=True)
        subprocess.run(["git", "config", "user.email", "test@test.com"], cwd=self.root, capture_output=True)
        subprocess.run(["git", "config", "user.name", "Test"], cwd=self.root, capture_output=True)
        self._write_and_commit("ImpactXv1/appsettings.json", '{"Jwt": {"Issuer": "Test"}}')
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.root, capture_output=True)
        self._os_getcwd_patch = patch("check_hardcoded_secrets.os.getcwd", return_value=self.root)
        self._os_getcwd_patch.start()

    def tearDown(self):
        self._os_getcwd_patch.stop()
        self.tmpdir.cleanup()

    def _write_and_commit(self, path: str, content: str):
        full_path = os.path.join(self.root, path)
        os.makedirs(os.path.dirname(full_path), exist_ok=True)
        with open(full_path, "w") as f:
            f.write(content)
        subprocess.run(["git", "add", path], cwd=self.root, capture_output=True)

    def test_clean_repo_exit_code_0(self):
        argv = ["check_hardcoded_secrets.py"]
        with patch.object(sys, "argv", argv):
            with self.assertRaises(SystemExit) as ctx:
                with redirect_stdout(io.StringIO()):
                    with redirect_stderr(io.StringIO()):
                        scanner.main()
            self.assertEqual(ctx.exception.code, 0)

    def test_violation_in_appsettings_exit_code_1(self):
        self._write_and_commit("ImpactXv1/appsettings.Development.json",
                               '{"Jwt": {"Secret": "TEST_ONLY_my_secret_value"}}')
        subprocess.run(["git", "commit", "-m", "add violation"], cwd=self.root, capture_output=True)

        argv = ["check_hardcoded_secrets.py"]
        with patch.object(sys, "argv", argv):
            stdout = io.StringIO()
            with self.assertRaises(SystemExit) as ctx:
                with redirect_stdout(stdout):
                    with redirect_stderr(io.StringIO()):
                        scanner.main()
            self.assertEqual(ctx.exception.code, 1)

        output = stdout.getvalue()
        self.assertIn("appsettings.Development.json", output)
        self.assertIn("Jwt Secret", output)
        self.assertNotIn("TEST_ONLY_my_secret_value", output)

    def test_empty_repo_exit_code_2(self):
        empty_tmpdir = tempfile.TemporaryDirectory()
        empty_root = empty_tmpdir.name
        subprocess.run(["git", "init"], cwd=empty_root, capture_output=True)
        subprocess.run(["git", "config", "user.email", "test@test.com"], cwd=empty_root, capture_output=True)
        subprocess.run(["git", "config", "user.name", "Test"], cwd=empty_root, capture_output=True)

        argv = ["check_hardcoded_secrets.py"]
        with patch("check_hardcoded_secrets.os.getcwd", return_value=empty_root):
            with patch.object(sys, "argv", argv):
                stdout = io.StringIO()
                stderr = io.StringIO()
                with self.assertRaises(SystemExit) as ctx:
                    with redirect_stdout(stdout):
                        with redirect_stderr(stderr):
                            scanner.main()
                self.assertEqual(ctx.exception.code, 2)

        empty_tmpdir.cleanup()

    def test_invalid_argument_exit_code_2(self):
        argv = ["check_hardcoded_secrets.py", "--invalid-option"]
        with patch.object(sys, "argv", argv):
            stderr = io.StringIO()
            with self.assertRaises(SystemExit) as ctx:
                with redirect_stderr(stderr):
                    scanner.main()
            self.assertEqual(ctx.exception.code, 2)
            self.assertNotIn("TEST_ONLY", stderr.getvalue())
            self.assertNotIn("secret", stderr.getvalue().lower())


if __name__ == "__main__":
    unittest.main()
