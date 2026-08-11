import tempfile
import unittest
from pathlib import Path

from services.docs_site import _read_doc_excerpt


class DocsExcerptTests(unittest.TestCase):
    def _excerpt(self, markdown: str, title: str = "文档标题") -> str:
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "doc.md"
            path.write_text(markdown, encoding="utf-8")
            return _read_doc_excerpt(path, title)

    def test_uses_the_first_prose_paragraph_only(self):
        excerpt = self._excerpt(
            "# 文档标题\n\n"
            "第一段介绍 [部署概览](./overview.md) 与 `ColorVision/Update/`。\n\n"
            "## 后续章节\n\n"
            "这里不是首页摘要。\n"
        )

        self.assertEqual(excerpt, "第一段介绍 部署概览 与 ColorVision/Update/。")

    def test_skips_fenced_code_before_finding_prose(self):
        excerpt = self._excerpt(
            "---\ntitle: 文档标题\n---\n\n"
            "```mermaid\nflowchart LR\nA --> B\n```\n\n"
            "这才是应该展示的文档摘要。\n"
        )

        self.assertEqual(excerpt, "这才是应该展示的文档摘要。")
        self.assertNotIn("mermaid", excerpt)
        self.assertNotIn("-->", excerpt)

    def test_skips_indented_code_blocks(self):
        excerpt = self._excerpt(
            "# 文档标题\n\n"
            "    dotnet build ColorVision.csproj\n\n"
            "面向用户的构建说明。\n"
        )

        self.assertEqual(excerpt, "面向用户的构建说明。")


if __name__ == "__main__":
    unittest.main()
