"""Authentication and authorization matrix for the framework-neutral policy."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from db_cache import CacheManager
from services.api_key_service import create_api_key
from services.auth_policy import AuthPolicy
from services.request_context import RequestContext


class AuthPolicyTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.cache = CacheManager(Path(self._temp.name) / "marketplace.db")
        self.cache.init_db()
        self.policy = AuthPolicy(self.cache, lambda: ("admin", "secret"))

    def tearDown(self):
        self._temp.cleanup()

    def test_anonymous_is_unauthenticated(self):
        decision = self.policy.authorize(RequestContext(), ["cache:read"])

        self.assertFalse(decision.allowed)
        self.assertEqual(decision.reason, "unauthenticated")
        self.assertEqual(decision.principal.actor_type, "anonymous")

    def test_admin_session_and_basic_auth_have_admin_identity(self):
        cases = (
            RequestContext(
                session_authenticated=True,
                session_username="session-admin",
                session_role="admin",
            ),
            RequestContext(basic_username="admin", basic_password="secret"),
        )
        for context in cases:
            with self.subTest(auth=context):
                decision = self.policy.authorize(context, ["jobs:write"])
                self.assertTrue(decision.allowed)
                self.assertTrue(decision.principal.is_admin)
                self.assertEqual(decision.principal.actor_type, "user")

    def test_non_admin_session_is_allowed_only_when_endpoint_opts_in(self):
        context = RequestContext(
            session_user_authenticated=True,
            session_username="operator",
            session_role="user",
        )

        admin = self.policy.authorize(context, ["cache:read"])
        transfer = self.policy.authorize(
            context,
            ["file:transfer"],
            allow_user_session=True,
        )

        self.assertFalse(admin.allowed)
        self.assertTrue(transfer.allowed)
        self.assertEqual(transfer.principal.actor_id, "operator")

    def test_registered_user_defaults_to_full_role_permissions_when_opted_in(self):
        context = RequestContext(
            session_user_authenticated=True,
            session_username="registered-user",
            session_role="user",
        )

        decision = self.policy.authorize(
            context,
            ["users:manage"],
            allow_user_session=True,
        )

        self.assertTrue(decision.allowed)
        self.assertIn("users:manage", decision.principal.scopes)
        self.assertFalse(decision.principal.is_admin)

    def test_password_change_required_session_is_denied_until_password_changes(self):
        context = RequestContext(
            session_user_authenticated=True,
            session_username="temporary-user",
            session_role="user",
            session_must_change_password=True,
        )

        decision = self.policy.authorize(
            context,
            ["file:transfer"],
            allow_user_session=True,
        )

        self.assertFalse(decision.allowed)
        self.assertEqual(decision.reason, "password_change_required")
        self.assertEqual(decision.principal.actor_id, "temporary-user")

    def test_explicit_credentials_can_override_a_restricted_browser_session(self):
        context = RequestContext(
            session_user_authenticated=True,
            session_username="temporary-user",
            session_role="user",
            session_must_change_password=True,
            basic_username="admin",
            basic_password="secret",
        )

        decision = self.policy.authorize(
            context,
            ["plugin:publish"],
            allow_user_session=True,
        )

        self.assertTrue(decision.allowed)
        self.assertEqual(decision.principal.auth_method, "basic")

    def test_api_key_scope_and_admin_wildcard_matrix(self):
        limited = create_api_key(
            self.cache,
            name="limited",
            scopes="cache:read",
            created_by="test",
        )
        admin = create_api_key(
            self.cache,
            name="admin",
            scopes="admin:*",
            created_by="test",
        )

        allowed = self.policy.authorize(
            RequestContext(bearer_token=limited["key"]),
            ["cache:read"],
        )
        forbidden = self.policy.authorize(
            RequestContext(bearer_token=limited["key"]),
            ["cache:refresh"],
        )
        wildcard = self.policy.authorize(
            RequestContext(bearer_token=admin["key"]),
            ["jobs:write"],
        )

        self.assertTrue(allowed.allowed)
        self.assertEqual(allowed.principal.actor_type, "api_key")
        self.assertFalse(forbidden.allowed)
        self.assertTrue(forbidden.forbidden)
        self.assertTrue(wildcard.allowed)
        self.assertTrue(wildcard.principal.is_admin)


if __name__ == "__main__":
    unittest.main()
