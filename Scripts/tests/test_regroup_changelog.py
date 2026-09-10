import unittest

from Scripts import regroup_changelog


class RegroupChangelogTests(unittest.TestCase):
    def test_archives_every_exact_release_and_keeps_only_latest_public(self):
        source = """# CHANGELOG

## [1.4.14.3] 2026.09.10

1. New behavior
   - A useful historical detail

## [1.4.14.2] 2026.09.09

1. Previous behavior

## [1.4.14.1] 2026.09.08

1. Series start
"""

        public, history, summary = regroup_changelog.build_changelog_files(source)

        self.assertEqual(3, summary.release_count)
        self.assertEqual(3, summary.added_count)
        self.assertEqual(0, summary.preserved_count)
        self.assertTrue(history.startswith("#   CHANGELOG\n\n## [1.4.14.3]"))
        self.assertNotIn("knowledge_id", history)
        self.assertEqual("1.4.14.3", summary.latest_version)
        self.assertIn("## [1.4.14.3] 2026.09.10", public)
        self.assertNotIn("1.4.14.2", public)
        self.assertIn("A useful historical detail", history)
        self.assertLess(history.index("1.4.14.3"), history.index("1.4.14.2"))

    def test_existing_history_is_idempotent(self):
        source = """# CHANGELOG

## [1.4.14.2] 2026.09.10

1. Current release

## [1.4.14.1] 2026.09.09

1. Previous release
"""
        public, history, _ = regroup_changelog.build_changelog_files(source)

        next_public, next_history, summary = regroup_changelog.build_changelog_files(public, history)

        self.assertEqual(public, next_public)
        self.assertEqual(history, next_history)
        self.assertEqual(0, summary.added_count)

    def test_adds_new_release_without_rewriting_existing_history(self):
        old_source = """# CHANGELOG

## [1.4.14.1] 2026.09.09

1. Previous release
"""
        _, old_history, _ = regroup_changelog.build_changelog_files(old_source)
        new_source = """# CHANGELOG

## [1.4.14.2] 2026.09.10

1. Current release
"""

        public, history, summary = regroup_changelog.build_changelog_files(new_source, old_history)

        self.assertEqual(1, summary.added_count)
        self.assertIn("1.4.14.2", public)
        self.assertIn("1.4.14.2", history)
        self.assertIn("1.4.14.1", history)

    def test_preserves_archived_release_when_public_summary_is_shorter(self):
        old_source = """# CHANGELOG

## [1.4.14.1] 2026.09.09

1. Original text
"""
        _, history, _ = regroup_changelog.build_changelog_files(old_source)
        changed_source = old_source.replace("Original text", "Rewritten text")

        public, next_history, summary = regroup_changelog.build_changelog_files(changed_source, history)

        self.assertIn("Rewritten text", public)
        self.assertIn("Original text", next_history)
        self.assertNotIn("Rewritten text", next_history)
        self.assertEqual(1, summary.preserved_count)

    def test_public_release_is_limited_to_three_items(self):
        source = """# CHANGELOG

## [1.4.14.1] 2026.09.09

1. One
2. Two
3. Three
4. Four
"""

        with self.assertRaises(regroup_changelog.ChangelogFormatError):
            regroup_changelog.build_changelog_files(source)


if __name__ == "__main__":
    unittest.main()
