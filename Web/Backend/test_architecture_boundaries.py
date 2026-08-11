from __future__ import annotations

import ast
import unittest
from dataclasses import fields
from pathlib import Path

from marketplace_api_routes import MarketplaceApiRouteContext


BACKEND_ROOT = Path(__file__).resolve().parent


def _tree(relative_path: str) -> ast.AST:
    path = BACKEND_ROOT / relative_path
    return ast.parse(path.read_text(encoding="utf-8"), filename=str(path))


def _imports_module(tree: ast.AST, module_name: str) -> bool:
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            if any(alias.name == module_name for alias in node.names):
                return True
        elif isinstance(node, ast.ImportFrom) and node.module == module_name:
            return True
    return False


def _direct_sql_execute_counts(relative_path: str) -> dict[str, int]:
    counts = {}
    for node in ast.walk(_tree(relative_path)):
        if not isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            continue
        execute_count = sum(
            1
            for child in ast.walk(node)
            if isinstance(child, ast.Call)
            and isinstance(child.func, ast.Attribute)
            and child.func.attr in {"execute", "executemany", "executescript"}
        )
        if execute_count:
            counts[node.name] = execute_count
    return counts


class ArchitectureBoundaryTests(unittest.TestCase):
    def test_app_setup_does_not_reverse_import_app(self):
        self.assertFalse(_imports_module(_tree("app_setup.py"), "app"))

    def test_routes_do_not_import_app_compatibility_shell(self):
        route_files = [
            "marketplace_api_routes.py",
            "routes/health_api.py",
            "routes/pages.py",
            "routes/cvws_api.py",
            "routes/spectrum_api.py",
        ]
        for relative_path in route_files:
            with self.subTest(path=relative_path):
                self.assertFalse(_imports_module(_tree(relative_path), "app"))

    def test_service_policy_and_marketplace_use_cases_are_flask_free(self):
        service_files = [
            "marketplace_services.py",
            "services/auth_policy.py",
            "services/request_context.py",
            "services/marketplace_api.py",
        ]
        forbidden_names = {"request", "session", "g"}
        for relative_path in service_files:
            tree = _tree(relative_path)
            with self.subTest(path=relative_path, rule="flask import"):
                self.assertFalse(_imports_module(tree, "flask"))
            accessed_names = {
                node.id
                for node in ast.walk(tree)
                if isinstance(node, ast.Name) and node.id in forbidden_names
            }
            with self.subTest(path=relative_path, rule="flask request globals"):
                self.assertEqual(set(), accessed_names)

    def test_jobs_http_handlers_contain_no_sql_or_connection_execute(self):
        tree = _tree("routes/admin_api.py")
        handler_names = {"list_jobs", "run_job", "enable_job", "disable_job"}
        handlers = {
            node.name: node
            for node in ast.walk(tree)
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
            and node.name in handler_names
        }
        self.assertEqual(handler_names, set(handlers))
        for name, handler in handlers.items():
            with self.subTest(handler=name):
                execute_calls = [
                    node
                    for node in ast.walk(handler)
                    if isinstance(node, ast.Call)
                    and isinstance(node.func, ast.Attribute)
                    and node.func.attr in {"execute", "executemany", "executescript"}
                ]
                self.assertEqual([], execute_calls)
                string_literals = " ".join(
                    node.value.upper()
                    for node in ast.walk(handler)
                    if isinstance(node, ast.Constant) and isinstance(node.value, str)
                )
                for keyword in ("SELECT ", "INSERT ", "UPDATE ", "DELETE "):
                    self.assertNotIn(keyword, string_literals)

    def test_transitional_route_sql_does_not_expand(self):
        expected_counts = {
            "routes/admin_api.py": {
                "stats_overview": 4,
                "create_api_key": 1,
            },
            "routes/operations_relay.py": {
                "heartbeat": 1,
                "poll_tasks": 2,
                "create_task": 5,
                "task_receipt": 3,
                "list_hosts": 1,
                "list_receipts": 2,
                "list_support_events": 2,
                "support_event": 1,
            },
        }

        for relative_path, expected in expected_counts.items():
            with self.subTest(path=relative_path):
                self.assertEqual(expected, _direct_sql_execute_counts(relative_path))

    def test_index_refresh_services_do_not_own_index_state_sql(self):
        for relative_path in (
            "services/plugin_index.py",
            "services/artifact_index.py",
            "services/docs_site.py",
            "services/scheduler.py",
        ):
            tree = _tree(relative_path)
            sql_literals = [
                node.value
                for node in ast.walk(tree)
                if isinstance(node, ast.Constant)
                and isinstance(node.value, str)
                and "index_state" in node.value.lower()
                and any(
                    keyword in node.value.upper()
                    for keyword in ("SELECT ", "INSERT ", "UPDATE ", "DELETE ")
                )
            ]
            with self.subTest(path=relative_path):
                self.assertEqual([], sql_literals)

    def test_marketplace_route_context_is_a_small_typed_bundle(self):
        self.assertEqual(
            ["services", "require_upload_auth", "request_context_factory"],
            [field.name for field in fields(MarketplaceApiRouteContext)],
        )


if __name__ == "__main__":
    unittest.main()
