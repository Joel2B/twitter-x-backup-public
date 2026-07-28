from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path
import re


CONTROL_FLOW_RE = re.compile(
    r"^\s*(?:"
    r"(?:await\s+)?foreach\s*\("
    r"|for\s*\("
    r"|while\s*\("
    r"|do\b"
    r"|(?:await\s+)?using\s*\("
    r"|lock\s*\("
    r"|fixed\s*\("
    r")"
)
UNBRACED_HEADER_RE = re.compile(
    r"^\s*(?:"
    r"if\s*\("
    r"|(?:await\s+)?foreach\s*\("
    r"|for\s*\("
    r"|while\s*\("
    r"|(?:await\s+)?using\s*\("
    r"|lock\s*\("
    r"|fixed\s*\("
    r"|else\b"
    r"|do\b"
    r")"
)
SKIP_DIRS = {
    ".git",
    ".vs",
    "bin",
    "obj",
    "node_modules",
    "App",
}


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def indentation(line: str) -> int:
    return len(line) - len(line.lstrip())


def unbraced_header_indent(lines: list[str], index: int) -> int | None:
    line = lines[index]
    stripped = line.strip()
    match = UNBRACED_HEADER_RE.match(line)
    if match and (
        stripped.startswith(("else", "do")) or re.search(r"\)\s*(?://.*)?$", stripped)
    ):
        return indentation(line)

    if not re.fullmatch(r"\)\s*(?://.*)?", stripped):
        return None

    # CSharpier puts a multiline header's closing parenthesis on its own line.
    for header_index in range(index - 1, -1, -1):
        header_line = lines[header_index]
        if not header_line.strip():
            break
        if UNBRACED_HEADER_RE.match(header_line):
            return indentation(header_line)
        if header_line.strip() in {"{", "}"} or header_line.rstrip().endswith(";"):
            break

    return None


def needs_blank_line_before_control_flow(lines: list[str], index: int) -> bool:
    if index == 0:
        return False

    previous_line = lines[index - 1]
    if previous_line.strip() == "":
        return False

    previous_non_empty_index = index - 1
    while previous_non_empty_index >= 0 and lines[previous_non_empty_index].strip() == "":
        previous_non_empty_index -= 1

    if previous_non_empty_index < 0:
        return False

    previous_non_empty = lines[previous_non_empty_index].strip()

    if (
        previous_non_empty == "{"
        or previous_non_empty.endswith(":")
        or previous_non_empty.startswith("[")
        or previous_non_empty.startswith("#")
        or previous_non_empty.startswith(("//", "/*", "*", "*/"))
    ):
        return False

    header_indent = unbraced_header_indent(lines, previous_non_empty_index)
    if header_indent is not None and header_indent < indentation(lines[index]):
        return False

    return True


def normalize_text(text: str) -> tuple[str, bool]:
    has_trailing_newline = text.endswith("\n")
    lines = text.splitlines()

    output: list[str] = []
    changed = False
    index = 0
    while index < len(lines):
        line = lines[index]
        if CONTROL_FLOW_RE.match(line) and needs_blank_line_before_control_flow(lines, index):
            output.append("")
            changed = True

        output.append(line)
        index += 1

    normalized = "\n".join(output)
    if has_trailing_newline or text == "":
        normalized += "\n"

    return normalized, changed


def process_file(path: Path, check_only: bool) -> bool:
    original = path.read_text(encoding="utf-8")
    normalized, changed = normalize_text(original)

    if not changed:
        return False

    if check_only:
        lines = original.splitlines()
        index = 0
        while index < len(lines):
            line = lines[index]
            match = CONTROL_FLOW_RE.match(line)
            if match and needs_blank_line_before_control_flow(lines, index):
                column = len(line) - len(line.lstrip()) + 1
                print(f"missing blank line before control flow: {path}:{index + 1}:{column}")

            index += 1
        return True

    path.write_text(normalized, encoding="utf-8", newline="\n")
    print(f"updated: {path}")
    return True


def run_csharpier(repo_root: Path, check_only: bool) -> int:
    command = ["dotnet", "csharpier", "."]
    if check_only:
        command.append("--check")

    return subprocess.run(command, cwd=repo_root, check=False).returncode


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument(
        "--skip-csharpier",
        action="store_true",
        help="Skip running dotnet csharpier and only apply the custom foreach rule.",
    )

    args = parser.parse_args()
    repo_root = Path(__file__).resolve().parent.parent

    if not args.skip_csharpier:
        exit_code = run_csharpier(repo_root, args.check)
        if exit_code != 0:
            return exit_code

    changed_any = False
    for path in repo_root.rglob("*.cs"):
        if should_skip(path.relative_to(repo_root)):
            continue

        changed_any = process_file(path, args.check) or changed_any

    if args.check and changed_any:
        return 1

    if not args.check and not args.skip_csharpier:
        exit_code = run_csharpier(repo_root, check_only=False)
        if exit_code != 0:
            return exit_code

    return 0


if __name__ == "__main__":
    sys.exit(main())
