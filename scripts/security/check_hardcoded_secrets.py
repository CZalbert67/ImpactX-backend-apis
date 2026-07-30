#!/usr/bin/env python3
"""ImpactX Hardcoded Secrets Policy

Scans tracked and untracked (non-ignored) files in a git repository
for hardcoded secrets. Returns exit code 0 if clean, 1 if violations
found, 2 on internal error or invalid arguments.
"""

import argparse
import os
import re
import subprocess
import sys
from typing import List, Tuple


REPORT_HEADER = "ImpactX Hardcoded Secrets Policy"

EXCLUDE_DIRS = {"bin", "obj", ".git", "node_modules", "TestResults", ".venv", "__pycache__"}
EXCLUDE_EXTENSIONS = {".md", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".pdf", ".drawio", ".trx", ".xml"}


PATTERNS: List[Tuple[str, str, str]] = [
    (
        "Jwt Secret in appsettings",
        r"ImpactXv1/appsettings.*\.json$",
        r'"(?:Secret|SecretKey)"\s*:\s*"[^"\s]+"',
    ),
    (
        "JWT fallback literal via null-coalescing",
        r"ImpactXv1/.*\.cs$",
        r'\["(?:Jwt:\w*Secret|SecretKey|Jwt:SecretKey)"\]\s*\?\?\s*"[^"]{20,}"',
    ),
    (
        "JWT fallback literal via return in Jwt files",
        r"ImpactXv1/(Infrastructure/Security/.*|Extensions/ServiceCollectionExtensions)\.cs$",
        r'return\s+"[^"]{20,}"',
    ),
    (
        "SymmetricSecurityKey with literal string",
        r"ImpactXv1/.*\.cs$",
        r'new\s+SymmetricSecurityKey\s*\(\s*Encoding\.UTF8\.GetBytes\s*\(\s*"[^"\s]+"\s*\)\s*\)',
    ),
    (
        "BEGIN PRIVATE KEY",
        r".*",
        r"-----BEGIN\s+PRIVATE\s+KEY-----",
    ),
    (
        "private_key assignment",
        r".*\.(cs|json|yml|yaml|env)$",
        r'["\']?private_key["\']?\s*[:=]\s*["\']?[^"\' \n]+["\']?',
    ),
    (
        "client_secret assignment",
        r".*\.(cs|json|yml|yaml|env)$",
        r'["\']?client_secret["\']?\s*[:=]\s*["\']?[^"\' \n]+["\']?',
    ),
    (
        "Firebase credential value",
        r".*\.(cs|json|yml|yaml)$",
        r'"type"\s*:\s*"service_account"',
    ),
]

TEST_INDICATORS = ["TEST_ONLY", "test-secret", "test-key", "test-value", "TestSecret", "TestKey"]


def should_exclude(filepath: str) -> bool:
    parts = filepath.split("/")
    if any(part in EXCLUDE_DIRS for part in parts):
        return True
    ext = os.path.splitext(filepath)[1].lower()
    if ext in EXCLUDE_EXTENSIONS:
        return True
    return False


def get_scannable_files(root_dir: str) -> List[str]:
    """Get tracked and untracked (non-ignored) files via git ls-files."""
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
        capture_output=True,
        text=True,
        cwd=root_dir,
    )
    if result.returncode != 0:
        print("Error: not a git repository or git not available", file=sys.stderr)
        sys.exit(2)

    seen = set()
    files = []
    for f in result.stdout.strip().split("\n"):
        f = f.strip()
        if not f or f in seen:
            continue
        if should_exclude(f):
            continue
        seen.add(f)
        files.append(f)
    return files


def matches_any_pattern(filename: str, pattern: str) -> bool:
    return bool(re.search(pattern, filename))


def search_file(filepath: str, regex: str) -> List[Tuple[int, str]]:
    findings = []
    try:
        with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
            for i, line in enumerate(f, 1):
                if re.search(regex, line):
                    findings.append((i, line.rstrip("\n")))
    except (IOError, OSError) as e:
        print(f"Warning: could not read {filepath}: {e}", file=sys.stderr)
    return findings


def is_test_file(filepath: str) -> bool:
    return ("ImpactX.Tests" in filepath or "test_" in filepath or "/tests/" in filepath
            or "scripts/security/tests" in filepath)


def is_test_file_with_test_value(filepath: str, matched_line: str) -> bool:
    if not is_test_file(filepath):
        return False
    for indicator in TEST_INDICATORS:
        if indicator in matched_line:
            return True
    return False


def is_config_reference_not_value(filepath: str, matched_line: str) -> bool:
    if not re.search(r'["\'](Jwt__Secret|Jwt:Secret|Jwt:SecretKey)["\']', matched_line):
        return False
    if re.search(r'\?\?\s*"[^"]{20,}"', matched_line):
        return False
    if re.search(r'return\s+"[^"]{20,}"', matched_line):
        return False
    return True


def scan_violations(root_dir: str, files: List[str]) -> List[Tuple[str, int, str, str]]:
    violations = []
    for filepath in files:
        abs_path = os.path.join(root_dir, filepath)
        if not os.path.isfile(abs_path):
            continue

        for policy_name, file_pattern, regex in PATTERNS:
            if not matches_any_pattern(filepath, file_pattern):
                continue

            broad_pattern = policy_name in ("BEGIN PRIVATE KEY", "private_key assignment",
                                            "client_secret assignment", "Firebase credential value")
            if broad_pattern and is_test_file(filepath):
                continue

            findings = search_file(abs_path, regex)

            for line_no, matched_line in findings:
                if is_test_file_with_test_value(filepath, matched_line):
                    continue
                if is_config_reference_not_value(filepath, matched_line):
                    continue
                violations.append((filepath, line_no, policy_name, "[REDACTED]"))

    return violations


def deduplicate(violations: List[Tuple[str, int, str, str]]) -> List[Tuple[str, int, str, str]]:
    seen = set()
    result = []
    for v in violations:
        key = (v[0], v[1], v[2])
        if key not in seen:
            seen.add(key)
            result.append(v)
    return result


def write_report(violations: List[Tuple[str, int, str, str]], report_path: str) -> None:
    with open(report_path, "w", encoding="utf-8") as f:
        f.write(f"{REPORT_HEADER}\n")
        f.write(f"{'=' * len(REPORT_HEADER)}\n\n")
        if not violations:
            f.write("No violations found.\n")
        else:
            f.write(f"Found {len(violations)} violation(s):\n\n")
            for filepath, line_no, policy_name, _ in violations:
                f.write(f"  {filepath}:{line_no}  [{policy_name}]\n")
            f.write("\n")


def main() -> None:
    parser = argparse.ArgumentParser(description="ImpactX Hardcoded Secrets Policy Scanner")
    parser.add_argument("--report", type=str, default=None, help="Path to write the report file")
    args = parser.parse_args()

    root_dir = os.getcwd()

    print(f"{REPORT_HEADER}")
    print("=" * len(REPORT_HEADER))

    try:
        files = get_scannable_files(root_dir)
    except Exception as e:
        print(f"Error getting scannable files: {e}", file=sys.stderr)
        sys.exit(2)

    if not files:
        print("No scannable files found.")
        sys.exit(2)

    violations = scan_violations(root_dir, files)
    violations = deduplicate(violations)

    print(f"\nScanned {len(files)} files.")
    print(f"Violations found: {len(violations)}")

    if args.report:
        write_report(violations, args.report)
        print(f"Report written to: {args.report}")

    if violations:
        print("\nViolations:")
        for filepath, line_no, policy_name, _ in violations:
            print(f"  {filepath}:{line_no}  [{policy_name}]")
        sys.exit(1)

    sys.exit(0)


if __name__ == "__main__":
    main()
