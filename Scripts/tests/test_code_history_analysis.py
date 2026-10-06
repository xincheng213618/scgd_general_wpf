"""Statistical contracts, isolated from the user's live working tree."""
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from code_history_analysis import collect_worktree, history_analysis, write_dashboard
from count_code_lines import collect_counts, total_count
from generate_code_history_dashboard import aggregate_periods, daily_snapshot_rows


class WorktreeTests(unittest.TestCase):
    def test_unique_nearest_project_ownership_and_worktree_content(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory).resolve()
            subprocess.run(["git", "init", "-q", str(repo)], check=True)
            sources = {
                "App/App.csproj": "<Project/>\n",
                "App/Alternative.csproj": "<Project/>\n",
                "App/A.cs": "// comment\nclass A {}\n\n",
                "App/Nested/Nested.csproj": "<Project/>\n",
                "App/Nested/B.cs": "class B {}\n",
                "App/Auto.Designer.cs": "class Auto {}\n",
                "docs/readme.md": "# Documentation\n",
                ".gitignore": "ignored/\n",
            }
            for name, value in sources.items():
                path = repo / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(value, encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=repo, check=True, capture_output=True)
            (repo / "App/A.cs").write_text("// changed\nclass A {}\nclass New {}\n\n", encoding="utf-8")
            (repo / "App/untracked.cs").write_text("class Untracked {}\n", encoding="utf-8")
            for name in ["ignored/secret.cs", "App/obj/generated.cs"]:
                path = repo / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("class Excluded {}\n", encoding="utf-8")
            result = collect_worktree(repo, False)
            files = {row["path"]: row for row in result["files"]}
            self.assertEqual(files["App/A.cs"]["code"], 2)
            self.assertIn("App/untracked.cs", files)
            self.assertNotIn("ignored/secret.cs", files)
            self.assertNotIn("App/obj/generated.cs", files)
            self.assertEqual(files["App/Nested/B.cs"]["project"], "project:App/Nested")
            self.assertEqual(files["App/A.cs"]["project"], "project:App")
            self.assertEqual(files["docs/readme.md"]["project"], "folder:docs")
            self.assertEqual(result["project_count"], 2)
            self.assertEqual(result["project_definition_count"], 3)
            counts, _ = collect_counts(repo, False, False, None)
            independent = total_count(counts.values())
            self.assertEqual(result["total"]["code"], independent.code)
            self.assertEqual(result["total"]["lines"], independent.lines)
            for key in ("code", "comments", "blank", "lines", "files"):
                self.assertEqual(result["total"][key], sum(row[key] for row in result["projects"]))
                self.assertEqual(result["total"][key], sum(row[key] for row in result["languages"]))
            without_generated = collect_worktree(repo, True)
            self.assertNotIn("App/Auto.Designer.cs", {row["path"] for row in without_generated["files"]})
            self.assertEqual(without_generated["total"]["code"], result["total"]["code"] - 1)


def node(day, added=10, deleted=4):
    return SimpleNamespace(commit=day, timestamp=datetime.fromisoformat(day), author="Test",
                           subject="fix: example", category="缺陷修复", primary_area="UI",
                           directory_churn={"UI": added + deleted}, files_changed=1,
                           additions=added, deletions=deleted, net=added-deleted, churn=added+deleted)


class HistoryTests(unittest.TestCase):
    def test_complete_calendar_week_and_original_offset(self):
        nodes = [node("2026-08-31T01:00:00+08:00"), node("2026-09-07T23:00:00-05:00", 30, 8),
                 node("2026-09-13T12:00:00+08:00", 4, 10), node("2026-09-18T11:00:00+08:00")]
        result = history_analysis(nodes, [], [], "HEAD", "now", "develop")
        self.assertEqual(result["recent_week"]["start"], "2026-09-07")
        self.assertEqual(result["recent_week"]["end"], "2026-09-13")
        self.assertEqual(result["recent_week"]["net"], 16)
        self.assertEqual(result["recent_week"]["churn"], 52)
        self.assertEqual(result["recent_week"]["active_days"], 2)
        self.assertTrue(result["previous_week"]["available"])
        self.assertEqual(result["commits"][1]["hour"], 23)
        self.assertEqual(result["commits"][1]["weekday"], 0)

    def test_short_history_does_not_invent_complete_week(self):
        result = history_analysis([node("2026-09-18T11:00:00+08:00")], [], [], "HEAD", "now", "test")
        self.assertFalse(result["recent_week"]["available"])
        self.assertFalse(result["previous_week"]["available"])

    def test_daily_snapshots_use_last_commit_and_carry_empty_days(self):
        nodes = [node("2026-09-16T09:00:00+08:00"), node("2026-09-16T22:00:00+08:00"),
                 node("2026-09-18T01:00:00+08:00")]
        counts = {}
        for index, item in enumerate(nodes):
            counts[item.commit] = {"files": 1, "code": 10 + index, "comments": 2, "blanks": 3}
            item.total_lines = 15 + index
        days = daily_snapshot_rows(aggregate_periods(nodes, 0), counts)
        self.assertEqual([row["date"] for row in days], ["2026-09-16", "2026-09-17", "2026-09-18"])
        self.assertEqual([row["end_commit"] for row in days], [nodes[1].commit, nodes[1].commit, nodes[2].commit])
        self.assertEqual([row["commits"] for row in days], [2, 0, 1])
        self.assertEqual([row["code_lines"] for row in days], [11, 11, 12])
        self.assertTrue(all(row["physical_lines"] == row["code_lines"] + row["comment_lines"] + row["blank_lines"]
                            for row in days))
        result = history_analysis(nodes, days, [], "HEAD", "now", "develop")
        self.assertEqual(result["days"][-1]["date"], result["last"])

    def test_new_html_needs_no_existing_template_or_external_builder(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifact, output = root / "artifact.json", root / "index.html"
            payload = {"subject": "</script><script>alert('unsafe')</script>"}
            artifact.write_text(json.dumps({"analysis": payload}), encoding="utf-8")
            write_dashboard(artifact, output)
            html = output.read_text(encoding="utf-8")
            match = re.search(r'<script id="dashboard-data" type="application/json">(.*?)</script>', html, re.S)
            self.assertEqual(json.loads(match.group(1)), payload)
            self.assertNotIn("</script><script>alert", html)
            self.assertNotIn("/* DASHBOARD_SCRIPT */", html)
            self.assertNotIn("/* DASHBOARD_STYLE */", html)
            self.assertNotRegex(html, r'<script[^>]+src=')


if __name__ == "__main__":
    unittest.main()
