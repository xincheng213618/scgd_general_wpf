import tempfile
import unittest
from pathlib import Path

from flask import Flask

from routes import frontend_spa as frontend_spa_module
from routes.frontend_spa import FrontendSpaContext, register_frontend_spa


class FrontendSpaTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.dist = Path(self.temp_dir.name)
        (self.dist / "assets").mkdir()
        (self.dist / "index.html").write_text(
            '<!doctype html><div id="root"></div>',
            encoding="utf-8",
        )
        (self.dist / "assets" / "app-deadbeef.js").write_text(
            "export default true",
            encoding="utf-8",
        )
        self.original_context = frontend_spa_module._ctx
        self.responses = []
        app = Flask(__name__)
        app.config["TESTING"] = True
        register_frontend_spa(app, FrontendSpaContext(
            check_auth=lambda: True,
            dist_dir=self.dist,
        ))
        self.client = app.test_client()

    def tearDown(self):
        for response in self.responses:
            response.close()
        frontend_spa_module._ctx = self.original_context
        self.temp_dir.cleanup()

    def test_hashed_assets_are_immutable_and_missing_assets_stay_404(self):
        existing = self.client.get("/assets/app-deadbeef.js")
        missing = self.client.get("/assets/removed-build.js")
        self.responses.extend((existing, missing))

        self.assertEqual(existing.status_code, 200)
        self.assertEqual(
            existing.headers["Cache-Control"],
            "public, max-age=31536000, immutable",
        )
        self.assertEqual(missing.status_code, 404)
        self.assertNotIn(b'<div id="root">', missing.data)

    def test_spa_html_always_revalidates(self):
        response = self.client.get("/plugins/DemoPlugin")
        self.responses.append(response)

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.headers["Cache-Control"], "no-cache, must-revalidate")


if __name__ == "__main__":
    unittest.main()
