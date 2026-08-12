import unittest

from flask import Flask

from services.http_method_safety import disable_unsafe_automatic_head


class HttpMethodSafetyTests(unittest.TestCase):
    def setUp(self):
        self.calls = {"list": 0, "poll": 0}
        self.app = Flask(__name__)

        def list_tasks():
            self.calls["list"] += 1
            return {"ok": True}

        def poll_tasks(host_id):
            self.calls["poll"] += 1
            return {"ok": True, "hostId": host_id}

        self.app.add_url_rule(
            "/api/ops/v1/tasks",
            endpoint="operations_relay.create_task",
            view_func=list_tasks,
            methods=["GET", "POST"],
        )
        self.app.add_url_rule(
            "/api/ops/v1/hosts/<host_id>/tasks",
            endpoint="operations_relay.poll_tasks",
            view_func=poll_tasks,
            methods=["GET"],
        )
        disable_unsafe_automatic_head(self.app)
        self.client = self.app.test_client()

    def test_head_is_rejected_without_executing_side_effectful_get_handlers(self):
        self.assertEqual(self.client.head("/api/ops/v1/tasks").status_code, 405)
        self.assertEqual(self.client.head("/api/ops/v1/hosts/host-1/tasks").status_code, 405)
        self.assertEqual(self.calls, {"list": 0, "poll": 0})

    def test_get_handlers_remain_available(self):
        self.assertEqual(self.client.get("/api/ops/v1/tasks").status_code, 200)
        self.assertEqual(self.client.get("/api/ops/v1/hosts/host-1/tasks").status_code, 200)
        self.assertEqual(self.calls, {"list": 1, "poll": 1})


if __name__ == "__main__":
    unittest.main()
