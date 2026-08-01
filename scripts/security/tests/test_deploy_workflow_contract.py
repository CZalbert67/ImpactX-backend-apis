"""Static contract tests for the main deploy workflow.

These tests validate that .github/workflows/main_impactx-api-backend.yml
performs post-deploy health verification with safe, non-destructive
operations. They never deploy anything and never contact Azure.
"""

import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPO_ROOT / ".github" / "workflows" / "main_impactx-api-backend.yml"
EXPECTED_HOST_FALLBACK = "https://impactx-api-backend-h0eyf9c4fxd8dsbc.westus-01.azurewebsites.net"


def _load_workflow() -> str:
    if not WORKFLOW_PATH.exists():
        raise AssertionError(f"Workflow file not found: {WORKFLOW_PATH}")
    return WORKFLOW_PATH.read_text(encoding="utf-8")


class TestDeployWorkflowContract(unittest.TestCase):
    """Contract of the post-deploy health verification step."""

    def test_queries_health_live(self):
        content = _load_workflow()
        self.assertIn("health/live", content)

    def test_queries_health_ready(self):
        content = _load_workflow()
        self.assertIn("health/ready", content)

    def test_queries_openapi_spec(self):
        content = _load_workflow()
        self.assertIn("openapi/v1.json", content)

    def test_fails_after_exhausting_retries(self):
        content = _load_workflow()
        self.assertIn("exit 1", content)
        self.assertIn("ATTEMPTS", content)

    def test_uses_curl_fail_and_timeout(self):
        content = _load_workflow()
        self.assertIn("--fail", content)
        self.assertIn("--max-time", content)

    def test_host_overrideable_via_app_base_url_var(self):
        content = _load_workflow()
        self.assertIn("vars.APP_BASE_URL", content)
        self.assertIn("APP_BASE_URL:", content)

    def test_host_fallback_matches_public_host_exactly(self):
        content = _load_workflow()
        expected_line = (
            f"APP_BASE_URL: ${{{{ vars.APP_BASE_URL || '{EXPECTED_HOST_FALLBACK}' }}}}"
        )
        self.assertIn(expected_line, content)

    def test_host_fallback_is_not_a_secret_placeholder(self):
        content = _load_workflow()
        self.assertNotIn("YOUR_AZURE_COSMOS_KEY", content)
        self.assertNotIn("impactx-api-backend.azurewebsites.net", content)

    def test_no_auto_restart_or_redeploy(self):
        content = _load_workflow().lower()
        self.assertNotIn("restart", content)
        self.assertNotIn("redeploy", content)

    def test_credentials_via_github_secrets_only(self):
        content = _load_workflow()
        self.assertIn("secrets.AZUREAPPSERVICE_CLIENTID", content)
        self.assertNotIn("client-secret", content)
        self.assertNotIn("password:", content)

    def test_concurrency_prevents_parallel_deploys(self):
        content = _load_workflow()
        self.assertIn("concurrency:", content)
        self.assertIn("cancel-in-progress: false", content)


if __name__ == "__main__":
    unittest.main()
