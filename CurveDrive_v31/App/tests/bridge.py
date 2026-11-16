#!/usr/bin/env python3
"""Bridge integration checks. Runs only against isolated temporary copies.

Example (Linux/Mono):
  python3 tests/bridge.py --exe Bridge.exe \
    --mono /path/to/mono -v

This verifies HTTP, persisted state and import text, not JW_CAD execution.
"""
import argparse
import json
import math
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import unittest
import urllib.error
import urllib.request


OPTIONS = None
BASE_URL = "http://127.0.0.1:18718/"
ORIGIN = "http://127.0.0.1:18718"


class BridgeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="CurveTest_")
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.exe = self.folder / "Bridge.exe"
        shutil.copy2(OPTIONS.exe, self.exe)
        self.folder.joinpath("app.html").write_text(
            "<!doctype html><title>Bridge test</title>", encoding="utf-8")
        self.process = None
        self.log = self.folder.joinpath("Server.log").open("wb")
        self.addCleanup(self.log.close)
        self.addCleanup(self.stop_server)
        self.start_server()

    def command(self, *args):
        prefix = [OPTIONS.mono] if OPTIONS.mono else []
        return prefix + [str(self.exe)] + list(args)

    def start_server(self):
        # Refuse to test an unrelated process already listening on the fixed port.
        with socket.socket() as sock:
            sock.settimeout(.2)
            self.assertNotEqual(sock.connect_ex(("127.0.0.1", 18718)), 0,
                                "Port 18718 is occupied; close the other bridge first.")
        self.process = subprocess.Popen(self.command(), cwd=self.folder,
                                        stdin=subprocess.DEVNULL,
                                        stdout=self.log, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 6
        while time.monotonic() < deadline:
            if self.process.poll() is not None:
                self.fail("Bridge exited during startup: " + self.server_log())
            try:
                code, data = self.request("health")
                if code == 200 and data.get("version") == "3.1":
                    return
            except (OSError, ValueError):
                pass
            time.sleep(.05)
        self.fail("Bridge did not start: " + self.server_log())

    def server_log(self):
        self.log.flush()
        return self.folder.joinpath("Server.log").read_text(encoding="utf-8", errors="replace")

    def stop_server(self):
        if self.process is not None and self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=3)
        self.process = None

    def request(self, path, body=None, origin=ORIGIN):
        content = None if body is None else json.dumps(body, allow_nan=False).encode("utf-8")
        request = urllib.request.Request(BASE_URL + path, data=content,
            headers={"Origin": origin, "Content-Type": "text/plain;charset=utf-8"})
        try:
            response = urllib.request.urlopen(request, timeout=3)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.code, json.loads(response.read().decode("utf-8-sig"))

    def baseline(self, **changes):
        data = {"site": "Test", "planKey": "test-plan", "station": 10,
                "p": {"x": 10, "y": .02}}
        data.update(changes)
        code, result = self.request("baseline", data)
        self.assertEqual(code, 200, result)
        self.assertTrue(result.get("session"), result)
        self.session = result["session"]
        self.identity = {"site": data["site"], "planKey": data["planKey"],
                         "session": self.session}
        return result

    def point(self, **changes):
        data = dict(self.identity)
        data.update({"station": 10.5, "previousStation": 10,
                     "p": {"x": 10.5, "y": .018}, "q": {"x": 10, "y": .02},
                     "offsetMm": 18})
        data.update(changes)
        return data

    def latest(self):
        code, data = self.request("latest")
        self.assertEqual(code, 200, data)
        return data

    def queue_point(self):
        self.baseline()
        data = self.point()
        code, result = self.request("point", data)
        self.assertEqual(code, 200, result)
        return data

    def import_point(self, target=None):
        target = target or self.folder / "JWC_TEMP.TXT"
        # This is an import-file fixture, not a real CAD drawing.
        target.write_text("hq\r\n0 0 1 1\r\n", encoding="ascii")
        result = subprocess.run(self.command("--import", str(target)), cwd=self.folder,
                                capture_output=True, text=True, timeout=6)
        return result, target.read_text(encoding="ascii")

    @staticmethod
    def geometry_lines(text):
        found = []
        for line in text.splitlines():
            fields = line.split()
            if len(fields) == 4:
                try:
                    values = [float(v) for v in fields]
                    if all(math.isfinite(v) for v in values):
                        found.append(values)
                except ValueError:
                    pass
        return found

    def test_health_and_uninitialized_latest(self):
        self.assertEqual(self.request("health")[1]["version"], "3.1")
        self.assertFalse(self.latest()["hasBaseline"])

    def test_managed_project_is_empty_without_creating_files(self):
        code, data = self.request("managed/project")
        self.assertEqual(code, 200)
        self.assertIsNone(data["project"])
        self.assertFalse((self.folder / "State.json").exists())

    def test_managed_point_cannot_use_an_unmanaged_session(self):
        self.baseline()
        before = (self.folder / "State.json").read_bytes()
        code, data = self.request("managed/point", {})
        self.assertEqual(code, 409)
        self.assertEqual(data["error"], "MANAGED_WORK_REQUIRED")
        self.assertEqual((self.folder / "State.json").read_bytes(), before)

    def test_guide_info_describes_uninitialized_work_without_creating_a_session(self):
        code, data = self.request("guide/info")
        self.assertEqual(code, 200)
        self.assertEqual(data["version"], "3.1")
        self.assertEqual(data["workId"], self.request("health")[1]["workId"])
        self.assertEqual(Path(data["folder"]).resolve(), self.folder.resolve())
        self.assertFalse(data["cadReady"])
        self.assertFalse(data["latest"]["hasBaseline"])
        self.assertFalse(self.folder.joinpath("State.json").exists())

    def test_guide_info_preserves_a_pending_measurement(self):
        self.queue_point()
        before = self.folder.joinpath("State.json").read_bytes()
        code, data = self.request("guide/info")
        self.assertEqual(code, 200)
        self.assertEqual(data["latest"]["state"], "pending")
        self.assertEqual(self.folder.joinpath("State.json").read_bytes(), before)

    def test_guide_folder_rejects_unlisted_paths(self):
        code, data = self.request("guide/folder", {"file": "../Bridge.exe"})
        self.assertEqual(code, 400)
        self.assertEqual(data["error"], "GUIDE_FILE_INVALID")
        self.assertFalse(self.folder.joinpath("State.json").exists())

    def test_guide_folder_requires_a_generated_file(self):
        code, data = self.request("guide/folder", {"file": "setup"})
        self.assertEqual(code, 409)
        self.assertEqual(data["error"], "GUIDE_FILE_NOT_READY")

    def test_guide_folder_rejects_nonlocal_web_origin(self):
        self.assertEqual(self.request("guide/folder", {"file": "setup"}, origin="https://example.com")[0], 403)

    def test_guide_info_rejects_nonlocal_web_origin(self):
        self.assertEqual(self.request("guide/info", origin="https://example.com")[0], 403)

    def test_baseline_persists_ready_session(self):
        self.baseline()
        data = self.latest()
        self.assertTrue(data["hasBaseline"])
        self.assertEqual(data["state"], "ready")
        self.assertTrue(self.folder.joinpath("State.json").is_file())

    def test_point_requires_baseline(self):
        self.identity = {"site": "Test", "planKey": "test-plan", "session": "missing"}
        self.assertEqual(self.request("point", self.point())[0], 409)

    def test_pending_retry_is_idempotent(self):
        payload = self.queue_point()
        self.assertEqual(self.latest()["state"], "pending")
        before = self.folder.joinpath("State.json").read_bytes()
        code, result = self.request("point", payload)
        self.assertEqual(code, 200, result)
        self.assertTrue(result["idempotent"], result)
        self.assertEqual(self.folder.joinpath("State.json").read_bytes(), before)

    def test_pending_different_point_is_rejected(self):
        self.queue_point()
        self.assertEqual(self.request("point", self.point(station=11))[0], 409)
        self.assertEqual(self.latest()["state"], "pending")

    def test_wrong_identity_is_rejected_without_mutation(self):
        self.baseline()
        before = self.folder.joinpath("State.json").read_bytes()
        for key, value in (("site", "Other"), ("planKey", "other-plan"), ("session", "other-session")):
            with self.subTest(key=key):
                self.assertEqual(self.request("point", self.point(**{key: value}))[0], 409)
                self.assertEqual(self.folder.joinpath("State.json").read_bytes(), before)

    def test_skipped_or_mismatched_previous_point_is_rejected(self):
        self.baseline()
        for change in ({"previousStation": 9}, {"q": {"x": 9.9, "y": .02}}):
            with self.subTest(change=change):
                self.assertEqual(self.request("point", self.point(**change))[0], 409)
        self.assertEqual(self.latest()["state"], "ready")

    def test_invalid_numbers_are_rejected_without_mutation(self):
        self.baseline()
        before = self.folder.joinpath("State.json").read_bytes()
        invalid = ({"p": {"x": "abc", "y": .018}},
                   {"p": {"x": None, "y": .018}},
                   {"station": 10}, {"station": -1})
        for change in invalid:
            with self.subTest(change=change):
                self.assertEqual(self.request("point", self.point(**change))[0], 400)
                self.assertEqual(self.folder.joinpath("State.json").read_bytes(), before)

    def test_disallowed_origin_is_rejected(self):
        self.assertEqual(self.request("health", origin="https://example.com")[0], 403)

    def test_import_issues_geometry_without_delete_commands(self):
        self.queue_point()
        result, text = self.import_point()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        lines = [line.strip().lower() for line in text.splitlines()]
        self.assertIn("b0", lines)
        self.assertIn("lc3", lines)
        self.assertFalse(any(line.startswith(("hq", "hd")) for line in lines), text)
        self.assertIn([10000, 20, 10500, 18], self.geometry_lines(text))
        self.assertIn("ci 10500.0000000 18.0000000 35.0000000", lines)
        self.assertEqual(self.latest()["state"], "issued")
        self.assertTrue(self.folder.joinpath("Import.log").is_file())

    def test_reimport_is_blocked_and_clears_stale_geometry(self):
        self.queue_point()
        first, _ = self.import_point()
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        second, text = self.import_point()
        self.assertNotEqual(second.returncode, 0)
        self.assertTrue(text.lower().startswith("he"), text)
        self.assertEqual(self.geometry_lines(text), [])
        self.assertNotIn("ci ", text.lower())
        self.assertEqual(self.latest()["state"], "issued")

    def test_import_without_pending_clears_stale_input(self):
        self.baseline()
        result, text = self.import_point()
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(text.lower().startswith("he"), text)
        self.assertEqual(self.geometry_lines(text), [])

    def test_restart_preserves_pending_then_issued(self):
        self.queue_point()
        before = self.folder.joinpath("State.json").read_bytes()
        self.stop_server()
        self.start_server()
        self.assertEqual(self.latest()["state"], "pending")
        self.assertEqual(self.folder.joinpath("State.json").read_bytes(), before)
        result, _ = self.import_point()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.stop_server()
        self.start_server()
        self.assertEqual(self.latest()["state"], "issued")
        result, text = self.import_point()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.geometry_lines(text), [])

    def test_new_baseline_archives_previous_session(self):
        self.queue_point()
        result, _ = self.import_point()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        previous_session = self.session
        self.baseline(site="NewTest", planKey="new-plan", station=0, p={"x": 0, "y": 0})
        self.assertNotEqual(self.session, previous_session)
        self.assertEqual(self.latest()["state"], "ready")
        old_session_in_archive = False
        for file in self.folder.rglob("*"):
            if not file.is_file() or file.name in {"Bridge.exe", "State.json", "Server.log", "Import.log"}:
                continue
            if previous_session in file.read_text(encoding="utf-8", errors="replace"):
                old_session_in_archive = True
                break
        self.assertTrue(old_session_in_archive, "Previous session was not retained in an archive file.")

    def test_invalid_json_state_fails_closed_without_overwrite(self):
        self.queue_point()
        state = self.folder / "State.json"
        corrupt = b'{"schema":1,"partial":'
        state.write_bytes(corrupt)
        code, response = self.request("latest")
        self.assertEqual(code, 500, response)
        self.assertIn("STATE_INVALID", response["error"])
        self.assertEqual(state.read_bytes(), corrupt)
        result, text = self.import_point()
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(text.lower().startswith("he"), text)
        self.assertEqual(self.geometry_lines(text), [])
        self.assertEqual(state.read_bytes(), corrupt)

    def test_wrong_import_path_does_not_touch_other_file_or_issue_point(self):
        self.queue_point()
        other = self.folder / "Other.txt"
        original = b"Other file must stay unchanged."
        other.write_bytes(original)
        result = subprocess.run(self.command("--import", str(other)), cwd=self.folder,
                                capture_output=True, text=True, timeout=6)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(other.read_bytes(), original)
        self.assertEqual(self.latest()["state"], "pending")

    def test_structurally_corrupt_state_blocks_latest_reset_and_import(self):
        self.queue_point()
        state = self.folder / "State.json"
        pending = json.loads(state.read_text(encoding="utf-8"))
        variants = (("missing_q", {"q": None}),
                    ("huge_coordinate", {"p": {"x": 10000001, "y": .018}}),
                    ("invalid_previous_station", {"previousStation": 10.5}),
                    ("nan_coordinate", {"p": {"x": float("nan"), "y": .018}}))
        for name, changes in variants:
            with self.subTest(corruption=name):
                corrupt = dict(pending)
                corrupt.update(changes)
                encoded = json.dumps(corrupt).encode("utf-8")
                state.write_bytes(encoded)
                code, response = self.request("latest")
                self.assertEqual(code, 500, response)
                self.assertIn("STATE_INVALID", response["error"])
                code, response = self.request("baseline", {
                    "site": "Test", "planKey": "test-plan", "station": 0,
                    "p": {"x": 0, "y": 0}})
                self.assertEqual(code, 500, response)
                self.assertEqual(state.read_bytes(), encoded)
                result, text = self.import_point()
                self.assertNotEqual(result.returncode, 0)
                self.assertTrue(text.lower().startswith("he"), text)
                self.assertEqual(self.geometry_lines(text), [])
                self.assertEqual(state.read_bytes(), encoded)

    def test_restart_does_not_silently_repair_or_overwrite_corruption(self):
        self.queue_point()
        state = self.folder / "State.json"
        corrupt = b'{"schema":1,"partial":'
        state.write_bytes(corrupt)
        self.stop_server()
        self.start_server()
        self.assertEqual(self.request("latest")[0], 500)
        self.assertEqual(state.read_bytes(), corrupt)

    def test_two_concurrent_imports_issue_at_most_once(self):
        self.queue_point()
        target = self.folder / "JWC_TEMP.TXT"
        target.write_text("hq\r\n0 0 1 1\r\n", encoding="ascii")
        processes = [subprocess.Popen(self.command("--import", str(target)), cwd=self.folder,
                                     stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                     for _ in range(2)]
        try:
            results = [p.communicate(timeout=6) for p in processes]
            self.assertEqual(sum(p.returncode == 0 for p in processes), 1, results)
            self.assertEqual(self.latest()["state"], "issued")
        finally:
            for process in processes:
                if process.poll() is None:
                    process.kill()
                    process.wait(timeout=3)

    def test_actual_mm_handoff_is_independent_of_paper_scale(self):
        # Synthetic CAD headers only. Real millimetres must not be multiplied
        # or divided by the layer group's paper scale.
        for scale in (1, 100, 1000):
            with self.subTest(scale=scale):
                self.queue_point()
                target = self.folder / "JWC_TEMP.TXT"
                header = "hq\r\nhk 0\r\nhs " + " ".join([str(scale)] * 16) + "\r\nlg0\r\nly0\r\n"
                target.write_bytes(header.encode("ascii"))
                result = subprocess.run(self.command("--import", str(target)), cwd=self.folder,
                                        capture_output=True, text=True, timeout=6)
                self.assertEqual(result.returncode, 0, result.stderr)
                text = target.read_text(encoding="ascii")
                self.assertEqual(text.splitlines()[0], "b0")
                self.assertNotIn("bz", text.splitlines())
                self.assertIn([10000, 20, 10500, 18], self.geometry_lines(text))
                self.assertIn("ci 10500.0000000 18.0000000 35.0000000", text)

    def test_v26_handoff_state_is_rejected_without_replay(self):
        self.queue_point()
        state_file = self.folder / "State.json"
        state = json.loads(state_file.read_text(encoding="utf-8"))
        self.assertEqual(state["schema"], 4)
        state["schema"] = 2
        encoded = json.dumps(state).encode("utf-8")
        state_file.write_bytes(encoded)
        code, response = self.request("latest")
        self.assertEqual(code, 500, response)
        result, output = self.import_point()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.geometry_lines(output), [])
        self.assertTrue(output.startswith("he"))
        self.assertEqual(state_file.read_bytes(), encoded)

    def test_auto_prepare_creates_only_origin_setup_and_unique_macro(self):
        self.baseline()
        code, response = self.request("auto/prepare", {"session": self.identity["session"], "end": {"x": 50, "y": 0}})
        self.assertEqual(code, 200, response)
        latest = self.latest()
        self.assertTrue(latest["automation"]["prepared"])
        self.assertFalse(latest["automation"]["enabled"])
        setup = (self.folder / "Setup_Link.bat").read_text()
        self.assertIn('findstr /b /c:"heCurveDrive:"', setup)
        self.assertIn('if not errorlevel 1 exit /b 1', setup)
        macros = list(self.folder.glob("Auto_*.bat"))
        self.assertEqual(len(macros), 1)
        self.assertIn("REM #1", setup)
        self.assertIn("REM #hp", setup)
        automatic = macros[0].read_text()
        self.assertIn("REM #hp", automatic)
        self.assertIn("REM #h4", automatic)
        self.assertNotIn("REM #0", automatic)
        self.assertNotIn("REM #1", automatic)
        self.assertNotIn("echo hd", automatic)
        self.assertEqual(self.latest()["state"], "ready")

    def test_auto_enable_requires_completed_calibration(self):
        self.baseline()
        code, response = self.request("auto/enable", {})
        self.assertEqual(code, 409, response)
        self.assertFalse(self.latest()["automation"]["enabled"])

    def test_auto_prepare_rejects_wrong_session(self):
        self.baseline()
        code, response = self.request("auto/prepare", {"session": "wrong", "end": {"x": 50, "y": 0}})
        self.assertEqual(code, 409, response)
        self.assertFalse(self.folder.joinpath("Auto.json").exists())

    def test_auto_apply_does_not_issue_pending_point_without_enabled_link(self):
        self.queue_point()
        code, response = self.request("auto/apply", {"session": self.identity["session"], "station": 10.5})
        self.assertEqual(code, 409, response)
        self.assertEqual(self.latest()["state"], "pending")

    def test_auto_control_requires_loopback_page_origin(self):
        self.baseline()
        for origin in ("null", ""):
            code, response = self.request("auto/prepare", {"session": self.identity["session"], "end": {"x": 50, "y": 0}}, origin=origin)
            self.assertEqual(code, 403, response)
            self.assertFalse(self.folder.joinpath("Auto.json").exists())

    def field_config(self, ready=False):
        data = {"schema":1,"session":self.identity["session"],"token":"a"*32,
                "end":{"x":50,"y":0},"calibrated":True,"enabled":False,
                "drawing":"/synthetic/Work.jww","pid":42,"started":1,
                "x":12,"y":34,"scale":100,"group":0,"width":420,"height":297,
                "fieldMode":True,"fieldReady":ready,"message":"Test"}
        (self.folder/"Auto.json").write_text(json.dumps(data))
        return data

    def test_field_start_requires_first_setup_without_resetting_data(self):
        self.baseline()
        before=(self.folder/"State.json").read_bytes()
        code,result=self.request("field/start",self.identity)
        self.assertEqual(code,409,result)
        self.assertEqual((self.folder/"State.json").read_bytes(),before)

    def test_field_start_reuses_registration_and_writes_only_manual_macro(self):
        self.baseline()
        link=self.field_config(True)
        before=(self.folder/"State.json").read_bytes()
        code,result=self.request("field/start",self.identity)
        self.assertEqual(code,200,result)
        current=json.loads((self.folder/"Auto.json").read_text())
        self.assertEqual(current["token"],link["token"])
        self.assertFalse(current["enabled"])
        self.assertFalse(current["fieldReady"])
        self.assertEqual((self.folder/"State.json").read_bytes(),before)
        macro=(self.folder/"Field_Run.bat").read_text()
        self.assertIn('--field',macro)
        self.assertNotIn('REM #1',macro)
        self.assertNotIn('REM #0',macro)

    def test_field_point_waits_for_cad_start_without_discarding_baseline(self):
        self.baseline()
        self.field_config(False)
        before=(self.folder/"State.json").read_bytes()
        code,result=self.request("point",self.point())
        self.assertEqual(code,409,result)
        self.assertEqual(result["error"],"FIELD_CAD_START_REQUIRED")
        self.assertEqual((self.folder/"State.json").read_bytes(),before)

    def test_field_ready_point_queues_and_never_automatically_drives_cad(self):
        self.baseline()
        self.field_config(True)
        code,result=self.request("point",self.point())
        self.assertEqual(code,200,result)
        self.assertEqual(self.latest()["state"],"pending")
        self.assertFalse(self.latest()["automation"]["enabled"])
        self.assertFalse((self.folder/"JWC_TEMP.TXT").exists())

    def test_field_start_rejects_wrong_project_and_cross_origin(self):
        self.baseline()
        self.field_config()
        before=(self.folder/"Auto.json").read_bytes()
        wrong=dict(self.identity);wrong["session"]="other"
        self.assertEqual(self.request("field/start",wrong)[0],409)
        self.assertEqual(self.request("field/start",self.identity,origin="null")[0],403)
        self.assertEqual((self.folder/"Auto.json").read_bytes(),before)

    def test_field_start_requires_saved_jww_for_repeatable_daily_work(self):
        self.baseline()
        link=self.field_config();link["drawing"]="/synthetic/Unsaved.dxf"
        (self.folder/"Auto.json").write_text(json.dumps(link))
        self.assertEqual(self.request("field/start",self.identity)[1]["error"],"FIELD_SAVE_JWW_FIRST")
        self.assertFalse((self.folder/"Field_Run.bat").exists())

    def test_reopen_probe_accepts_same_folder_without_changing_state(self):
        self.baseline()
        before=(self.folder/"State.json").read_bytes()
        result=subprocess.run(self.command("--ready"),capture_output=True,timeout=5)
        self.assertEqual(result.returncode,0,result.stderr)
        self.assertEqual((self.folder/"State.json").read_bytes(),before)
        self.assertNotIn(str(self.folder),json.dumps(self.request("health")[1]))

    def test_reopen_probe_refuses_another_folder_and_detects_stopped_server(self):
        with tempfile.TemporaryDirectory(prefix="OtherCurve_") as folder:
            other=Path(folder)/"Bridge.exe";shutil.copy2(self.exe,other)
            cmd=([OPTIONS.mono] if OPTIONS.mono else [])+[str(other),"--ready"]
            result=subprocess.run(cmd,capture_output=True,timeout=5)
            self.assertEqual(result.returncode,2,result.stderr)
        self.stop_server()
        result=subprocess.run(self.command("--ready"),capture_output=True,timeout=5)
        self.assertEqual(result.returncode,1,result.stderr)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path, default=Path(os.environ.get("CURVE_EXE", "Bridge.exe")))
    parser.add_argument("--mono", default=os.environ.get("CURVE_MONO", ""))
    OPTIONS, remaining = parser.parse_known_args()
    OPTIONS.exe = OPTIONS.exe.resolve()
    if not OPTIONS.exe.is_file():
        parser.error("Bridge executable not found: " + str(OPTIONS.exe))
    unittest.main(argv=[sys.argv[0]] + remaining)
