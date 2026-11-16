"""Static Windows handoff checks; this does NOT execute CMD or Jw_cad.

Protocol reference: official Jw_cad 8.25a JWW_SMPL.BAT, sections 1 and 3.
The official download is https://www.jwcad.net/download/jww825a.exe .
Run: python3 tests/batch.py
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parent.parent


class BatchChecks(unittest.TestCase):
    def test_command_file_encoding(self):
        for name in ('Curve_Latest.bat', 'Open.cmd', 'Start.cmd'):
            with self.subTest(name=name):
                data = (ROOT / name).read_bytes()
                data.decode('ascii')
                self.assertFalse(data.startswith(b'\xef\xbb\xbf'))
                self.assertNotIn(b'\n', data.replace(b'\r\n', b''))

    def test_jww_protocol_directives(self):
        lines = (ROOT / 'Curve_Latest.bat').read_text().splitlines()
        directives = []
        for line in lines:
            match = re.match(r'^REM\s+(#[^\s]+)', line, re.I)
            if match:
                self.assertLess(line.index('#'), 10, 'Jw_cad ignores # after byte 10')
                directives.append(match.group(1))
                if match.group(1) == '#e':
                    break
        self.assertEqual(directives, ['#jww', '#cd', '#hf', '#h0', '#0', '#e'])

    def test_error_output_does_not_request_deletion_or_unexecuted(self):
        source = (ROOT / 'Curve_Latest.bat').read_text()
        emitted = re.findall(r'^\s*>\s*"[^"\n]+"\s+echo\s+([^\r\n]+)', source, re.M | re.I)
        self.assertTrue(emitted, 'Fail-closed output must be visible for review')
        self.assertTrue(all(line.startswith('heCurveDrive:') for line in emitted))
        self.assertTrue(all(not line.startswith(('hd', 'hq')) for line in emitted))

    def test_paths_in_diagnostic_echo_are_quoted(self):
        source = (ROOT / 'Curve_Latest.bat').read_text()
        for name in ('%~f0', '%CD%', '%TARGET%'):
            for line in source.splitlines():
                if re.search(r'\becho\b', line, re.I) and name in line:
                    self.assertIn('"' + name + '"', line,
                                  'Unquoted path expansion permits CMD metacharacters')

    def test_critical_execution_paths_are_quoted(self):
        source = (ROOT / 'Curve_Latest.bat').read_text()
        self.assertIn('"%~dp0Bridge.exe" --import "%TARGET%"', source)
        self.assertIn('set "TARGET=%~dp0JWC_TEMP.TXT"', source)
        self.assertIn('DisableDelayedExpansion', source)

    def test_geometry_contains_no_delete_or_unexecuted_directive(self):
        source = (ROOT / 'Bridge.cs').read_text()
        body = source.split('static string Geometry(State s)', 1)[1].split('static int Import(', 1)[0]
        strings = re.findall(r'"([^"\r\n]*)"', body)
        self.assertTrue(strings)
        self.assertFalse(any(value.strip() in ('hd', 'hq') for value in strings))

    def test_import_scope_is_checked_before_handoff(self):
        source = (ROOT / 'Bridge.cs').read_text()
        body = source.split('static int Import(string filename)', 1)[1].split('static int Main(', 1)[0]
        self.assertIn('Path.Combine(Root,"JWC_TEMP.TXT")', body)
        self.assertLess(body.index('String.Equals(target,expected,'), body.index('File.WriteAllText(target,data,'))

    def test_start_rebuilds_source_before_launch(self):
        source = (ROOT / 'Start.cmd').read_text()
        compile_at = source.index('"%CSC%" /nologo')
        move_at = source.index('move /y ')
        run_at = source.index('"%~dp0Bridge.exe" --open')
        self.assertLess(compile_at, move_at)
        self.assertLess(move_at, run_at)
        self.assertNotRegex(source, r'(?im)^if exist .*goto launch')

    def test_start_uses_explicit_references_without_csc_response_file(self):
        source = (ROOT / 'Start.cmd').read_text()
        compile_line = next(line for line in source.splitlines() if line.startswith('"%CSC%" /nologo'))
        self.assertIn('/noconfig', compile_line)
        for name in ('System.dll', 'System.Core.dll', 'System.Web.Extensions.dll'):
            self.assertEqual(compile_line.count('/reference:' + name), 1)

    def test_no_powershell_commands(self):
        for name in ('Curve_Latest.bat', 'Open.cmd', 'Start.cmd'):
            source = (ROOT / name).read_text()
            self.assertNotRegex(source, r'(?im)^\s*@?(?:powershell|pwsh)(?:\.exe)?\b')


if __name__ == '__main__':
    unittest.main(verbosity=2)
