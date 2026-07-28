import unittest

import format


class FormatTests(unittest.TestCase):
    def test_preserves_discard_assignments(self):
        source = "_ = Run();\n_ = new Worker\n{\n    Enabled = true,\n};\n"

        self.assertEqual(format.normalize_text(source), (source, False))

    def test_does_not_split_unbraced_bodies(self):
        headers = (
            "if (ready)",
            "for (;;)",
            "while (ready)",
            "using (resource)",
            "lock (gate)",
            "fixed (byte* pointer = bytes)",
            "else // fallback",
        )

        for header in headers:
            with self.subTest(header=header):
                source = f"{header}\n    if (ready)\n        Run();\n"
                self.assertEqual(format.normalize_text(source), (source, False))

    def test_handles_multiline_header_and_leading_comment(self):
        multiline = "if (\n    ready\n)\n    foreach (var item in items)\n        Run(item);\n"
        commented = "else\n    // fallback\n    while (ready)\n        Run();\n"

        self.assertEqual(format.normalize_text(multiline), (multiline, False))
        self.assertEqual(format.normalize_text(commented), (commented, False))

    def test_adds_one_blank_line_idempotently(self):
        source = "Run();\nforeach (var item in items)\n{\n    Use(item);\n}\n"
        expected = "Run();\n\nforeach (var item in items)\n{\n    Use(item);\n}\n"

        self.assertEqual(format.normalize_text(source), (expected, True))
        self.assertEqual(format.normalize_text(expected), (expected, False))


if __name__ == "__main__":
    unittest.main()
