import unittest
from unittest.mock import patch

import build_update


class BuildUpdateTests(unittest.TestCase):
    def test_main_fails_when_version_is_missing(self):
        with patch("build_update.get_file_version", return_value=None), patch("builtins.print") as mocked_print:
            exit_code = build_update.main()

        self.assertEqual(exit_code, 1)
        self.assertTrue(mocked_print.called)
        self.assertIn("无法从", mocked_print.call_args[0][0])


if __name__ == "__main__":
    unittest.main()

