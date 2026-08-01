import bisect
import csv
import io
import math
import os
import re
import sys
import tempfile
import time
import tkinter as tk
from dataclasses import dataclass
from tkinter import filedialog, messagebox, ttk


@dataclass
class CsvRow:
    timestamp: float
    physical_line: int
    data_number: int
    values: list[str]


class CsvPlaybackViewer:
    TICK_MS = 16

    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("CSV 프레임 뷰어 - 패턴 지정")
        self.root.geometry("1120x900")
        self.root.minsize(820, 560)

        self.path = ""
        self.headers: list[str] = []
        self.rows: list[CsvRow] = []
        self.times: list[float] = []
        self.current_index = 0
        self.current_time = 0.0
        self.playing = False
        self.last_clock = time.perf_counter()
        self.dragging = False
        self.repeat_job = None

        self.status_var = tk.StringVar(value="CSV 파일을 열어 주세요.")
        self.line_var = tk.StringVar(value="현재 CSV 줄: -")
        self.data_var = tk.StringVar(value="데이터 행: -")
        self.time_var = tk.StringVar(value="시간: 0.000 s")
        self.play_text = tk.StringVar(value="▶ 재생")
        self.speed_var = tk.StringVar(value="1.0")
        self.loop_var = tk.BooleanVar(value=False)
        self.topmost_var = tk.BooleanVar(value=False)
        self.seek_var = tk.DoubleVar(value=0.0)
        self.pattern_state_var = tk.StringVar(value="is_pattern: -")

        self._build_ui()
        self.root.bind("<space>", lambda _e: self.set_current_pattern())
        self.root.bind("<Control-o>", lambda _e: self.open_csv())
        self.root.bind("<Left>", lambda _e: self.step(-1))
        self.root.bind("<Right>", lambda _e: self.step(1))
        self.root.protocol("WM_DELETE_WINDOW", self.close)

        if len(sys.argv) > 1:
            self.root.after(0, lambda: self.load_csv(sys.argv[1]))

    def _build_ui(self) -> None:
        toolbar = ttk.Frame(self.root, padding=10)
        toolbar.pack(fill="x")
        ttk.Button(toolbar, text="CSV 열기 (Ctrl+O)", command=self.open_csv).pack(side="left")
        ttk.Button(toolbar, text="|◀ 첫 프레임", command=self.stop).pack(side="left", padx=(8, 0))
        self.previous_button = ttk.Button(toolbar, text="◀ 이전 프레임")
        self.previous_button.pack(side="left", padx=(8, 0))
        self.next_button = ttk.Button(toolbar, text="다음 프레임 ▶")
        self.next_button.pack(side="left", padx=(4, 0))
        self._bind_hold_button(self.previous_button, -1)
        self._bind_hold_button(self.next_button, 1)
        self.pattern_button = ttk.Button(toolbar, text="현재 프레임을 패턴으로 지정 (Space)", command=self.set_current_pattern, state="disabled")
        self.pattern_button.pack(side="left", padx=(18, 0))
        ttk.Label(toolbar, textvariable=self.pattern_state_var, font=("Malgun Gothic", 11, "bold")).pack(side="left", padx=(10, 0))
        ttk.Checkbutton(toolbar, text="항상 위", variable=self.topmost_var, command=self.set_topmost).pack(side="left", padx=(12, 0))

        banner = ttk.Frame(self.root, padding=(14, 6, 14, 10))
        banner.pack(fill="x")
        ttk.Label(banner, textvariable=self.line_var, font=("Malgun Gothic", 26, "bold"), foreground="#1565c0").pack(anchor="center")
        details = ttk.Frame(banner)
        details.pack(anchor="center", pady=(3, 0))
        ttk.Label(details, textvariable=self.data_var, font=("Malgun Gothic", 12)).pack(side="left", padx=12)
        ttk.Label(details, textvariable=self.time_var, font=("Consolas", 13, "bold")).pack(side="left", padx=12)

        seek = ttk.Frame(self.root, padding=(14, 0, 14, 10))
        seek.pack(fill="x")
        self.scale = ttk.Scale(seek, variable=self.seek_var, from_=0, to=1, command=self.on_seek)
        self.scale.pack(fill="x")
        self.scale.bind("<ButtonPress-1>", lambda _e: setattr(self, "dragging", True))
        self.scale.bind("<ButtonRelease-1>", self.end_seek)

        avatar_box = ttk.Frame(self.root)
        avatar_box.pack(fill="x", padx=14, pady=(0, 8))
        current_box = ttk.Labelframe(avatar_box, text="현재 CSV 아바타", padding=4)
        pattern_box = ttk.Labelframe(avatar_box, text="패턴 미리보기 (-0.05초 ~ +0.05초)", padding=4)
        current_box.pack(side="left", fill="both", expand=True, padx=(0, 4))
        pattern_box.pack(side="left", fill="both", expand=True, padx=(4, 0))
        self.avatar_canvas = tk.Canvas(current_box, height=300, background="#172033", highlightthickness=0)
        self.pattern_canvas = tk.Canvas(pattern_box, height=300, background="#251b31", highlightthickness=0)
        self.avatar_canvas.pack(fill="x")
        self.pattern_canvas.pack(fill="x")
        self.avatar_canvas.bind("<Configure>", lambda _e: self.draw_avatar())
        self.pattern_canvas.bind("<Configure>", lambda _e: self.draw_avatar())

        panes = ttk.Panedwindow(self.root, orient="vertical")
        panes.pack(fill="both", expand=True, padx=14, pady=(0, 8))
        nearby_box = ttk.Labelframe(panes, text="현재 위치 주변 (파란 행이 현재 행)", padding=6)
        value_box = ttk.Labelframe(panes, text="현재 행 전체 값", padding=6)
        panes.add(nearby_box, weight=3)
        panes.add(value_box, weight=2)

        self.nearby = ttk.Treeview(nearby_box, columns=("line", "data", "time", "pattern"), show="headings", height=11)
        labels = (("line", "CSV 줄", 100), ("data", "데이터 행", 100), ("time", "time (초)", 150), ("pattern", "is_pattern", 110))
        for key, title, width in labels:
            self.nearby.heading(key, text=title)
            self.nearby.column(key, width=width, anchor="center")
        self.nearby.tag_configure("current", background="#bbdefb", foreground="#0d47a1")
        self.nearby.pack(fill="both", expand=True)
        self.nearby.bind("<Double-1>", self.jump_from_table)

        self.values = ttk.Treeview(value_box, columns=("column", "value"), show="headings")
        self.values.heading("column", text="열 이름")
        self.values.heading("value", text="값")
        self.values.column("column", width=210, anchor="w")
        self.values.column("value", width=800, anchor="w")
        value_scroll = ttk.Scrollbar(value_box, orient="vertical", command=self.values.yview)
        self.values.configure(yscrollcommand=value_scroll.set)
        value_scroll.pack(side="right", fill="y")
        self.values.pack(fill="both", expand=True)

        ttk.Label(self.root, textvariable=self.status_var, relief="sunken", anchor="w", padding=(8, 4)).pack(fill="x")

    def open_csv(self) -> None:
        path = filedialog.askopenfilename(title="재생할 CSV 선택", filetypes=(("CSV 파일", "*.csv"), ("모든 파일", "*.*")))
        if path:
            self.load_csv(path)

    def load_csv(self, path: str) -> None:
        try:
            headers, rows = self._read_csv(path)
            if not rows:
                raise ValueError("재생 가능한 데이터 행이 없습니다.")
        except Exception as exc:
            messagebox.showerror("CSV 열기 실패", str(exc))
            return

        self.path = os.path.abspath(path)
        self.headers = headers
        self.rows = sorted(rows, key=lambda row: (row.timestamp, row.data_number))
        self.times = [row.timestamp for row in self.rows]
        self.current_index = 0
        self.current_time = self.times[0]
        self.scale.configure(from_=self.times[0], to=max(self.times[-1], self.times[0] + 0.001))
        self.seek_var.set(self.current_time)
        self.root.title(f"CSV 프레임 뷰어 - {os.path.basename(path)}")
        self.status_var.set(f"{self.path}  |  {len(self.rows):,}행  |  {self.times[0]:.3f}s ~ {self.times[-1]:.3f}s")
        self.render_current(force=True)

    @staticmethod
    def _read_csv(path: str) -> tuple[list[str], list[CsvRow]]:
        last_error = None
        for encoding in ("utf-8-sig", "cp949"):
            try:
                with open(path, "r", encoding=encoding, newline="") as handle:
                    reader = csv.reader(handle)
                    headers = next(reader, None)
                    if not headers:
                        raise ValueError("CSV 헤더가 없습니다.")
                    normalized = [header.strip().lower() for header in headers]
                    if "time" not in normalized:
                        raise ValueError("Pattern Extract Test 형식처럼 'time' 열이 필요합니다.")
                    time_index = normalized.index("time")
                    rows = []
                    for data_number, values in enumerate(reader, start=1):
                        physical_line = reader.line_num
                        if not values or not any(value.strip() for value in values):
                            continue
                        if len(values) != len(headers):
                            continue
                        try:
                            timestamp = float(values[time_index].strip())
                        except ValueError:
                            continue
                        rows.append(CsvRow(timestamp, physical_line, data_number, values))
                    return headers, rows
            except UnicodeDecodeError as exc:
                last_error = exc
        raise ValueError(f"UTF-8 또는 CP949 CSV로 읽을 수 없습니다: {last_error}")

    def toggle_play(self) -> None:
        if not self.rows:
            return
        if not self.playing and self.current_time >= self.times[-1]:
            self.current_time = self.times[0]
        self.playing = not self.playing
        self.last_clock = time.perf_counter()
        self.play_text.set("⏸ 일시정지" if self.playing else "▶ 재생")

    def _toggle_from_key(self, _event=None):
        self.toggle_play()
        return "break"

    def stop(self) -> None:
        if not self.rows:
            return
        self.playing = False
        self.play_text.set("▶ 재생")
        self.current_time = self.times[0]
        self.update_index()

    def step(self, amount: int) -> None:
        if not self.rows:
            return
        self.playing = False
        self.play_text.set("▶ 재생")
        self.current_index = min(max(self.current_index + amount, 0), len(self.rows) - 1)
        self.current_time = self.rows[self.current_index].timestamp
        self.seek_var.set(self.current_time)
        self.render_current(force=True)

    def _bind_hold_button(self, button: ttk.Button, amount: int) -> None:
        button.bind("<ButtonPress-1>", lambda _e: self._start_repeat(amount))
        button.bind("<ButtonRelease-1>", self._stop_repeat)
        button.bind("<Leave>", self._stop_repeat)

    def _start_repeat(self, amount: int) -> None:
        self._stop_repeat()
        self.step(amount)
        self.repeat_job = self.root.after(220, lambda: self._repeat_step(amount))

    def _repeat_step(self, amount: int) -> None:
        self.step(amount)
        self.repeat_job = self.root.after(35, lambda: self._repeat_step(amount))

    def _stop_repeat(self, _event=None) -> None:
        if self.repeat_job is not None:
            self.root.after_cancel(self.repeat_job)
            self.repeat_job = None

    def set_current_pattern(self) -> None:
        if not self.rows or not self.path:
            return
        pattern_index = next((i for i, header in enumerate(self.headers) if header.strip().lower() == "is_pattern"), None)
        if pattern_index is None:
            messagebox.showerror("패턴 지정 실패", "CSV에 is_pattern 열이 없습니다.")
            return
        row = self.rows[self.current_index]
        if row.values[pattern_index].strip() == "1":
            return
        try:
            self._save_pattern_flag(row.physical_line, pattern_index)
        except Exception as exc:
            messagebox.showerror("패턴 지정 실패", str(exc))
            return
        row.values[pattern_index] = "1"
        self.status_var.set(f"저장 완료: CSV {row.physical_line}번째 줄의 is_pattern을 1로 변경했습니다.  |  {self.path}")
        self.render_current(force=True)

    def _save_pattern_flag(self, physical_line: int, pattern_index: int) -> None:
        with open(self.path, "rb") as handle:
            raw = handle.read()
        if raw.startswith(b"\xef\xbb\xbf"):
            encoding = "utf-8-sig"
        else:
            try:
                raw.decode("utf-8")
                encoding = "utf-8"
            except UnicodeDecodeError:
                encoding = "cp949"
        text = raw.decode(encoding)
        lines = text.splitlines(keepends=True)
        target = physical_line - 1
        if target < 1 or target >= len(lines):
            raise ValueError("원본 CSV 줄을 찾을 수 없습니다.")
        original = lines[target]
        ending = "\r\n" if original.endswith("\r\n") else ("\n" if original.endswith("\n") else ("\r" if original.endswith("\r") else ""))
        content = original[:-len(ending)] if ending else original
        values = next(csv.reader([content]))
        if pattern_index >= len(values):
            raise ValueError("현재 CSV 줄의 열 개수가 올바르지 않습니다.")
        values[pattern_index] = "1"
        stream = io.StringIO(newline="")
        csv.writer(stream, lineterminator=ending).writerow(values)
        lines[target] = stream.getvalue()
        directory = os.path.dirname(self.path)
        temp_path = ""
        try:
            with tempfile.NamedTemporaryFile("w", encoding=encoding, newline="", delete=False, dir=directory, suffix=".csv.tmp") as handle:
                temp_path = handle.name
                handle.write("".join(lines))
            os.replace(temp_path, self.path)
        finally:
            if temp_path and os.path.exists(temp_path):
                os.remove(temp_path)

    def on_seek(self, value: str) -> None:
        if not self.rows:
            return
        self.current_time = float(value)
        self.update_index()

    def end_seek(self, _event=None) -> None:
        self.dragging = False
        self.last_clock = time.perf_counter()

    def set_topmost(self) -> None:
        self.root.attributes("-topmost", self.topmost_var.get())

    def tick(self) -> None:
        now = time.perf_counter()
        elapsed = now - self.last_clock
        self.last_clock = now
        if self.playing and self.rows and not self.dragging:
            try:
                speed = max(0.01, float(self.speed_var.get()))
            except ValueError:
                speed = 1.0
                self.speed_var.set("1.0")
            self.current_time += elapsed * speed
            if self.current_time >= self.times[-1]:
                if self.loop_var.get() and self.times[-1] > self.times[0]:
                    duration = self.times[-1] - self.times[0]
                    self.current_time = self.times[0] + ((self.current_time - self.times[0]) % duration)
                else:
                    self.current_time = self.times[-1]
                    self.playing = False
                    self.play_text.set("▶ 재생")
            self.seek_var.set(self.current_time)
            self.update_index()
        self.root.after(self.TICK_MS, self.tick)

    def update_index(self) -> None:
        new_index = max(0, bisect.bisect_right(self.times, self.current_time) - 1)
        new_index = min(new_index, len(self.rows) - 1)
        changed = new_index != self.current_index
        self.current_index = new_index
        self.render_current(force=changed)

    def render_current(self, force: bool = False) -> None:
        if not self.rows:
            return
        row = self.rows[self.current_index]
        self.line_var.set(f"현재 CSV 줄: {row.physical_line:,}번째 줄")
        self.data_var.set(f"데이터 행 {row.data_number:,} / {len(self.rows):,}")
        self.time_var.set(f"시간: {self.current_time:.3f} s  (행 time: {row.timestamp:.3f} s)")
        pattern_column = next((i for i, h in enumerate(self.headers) if h.strip().lower() == "is_pattern"), None)
        pattern_value = row.values[pattern_column].strip() if pattern_column is not None else "없음"
        self.pattern_state_var.set(f"is_pattern: {pattern_value}")
        self.pattern_button.configure(state="normal" if pattern_value == "0" else "disabled")
        self.draw_avatar()
        if not force:
            return

        for item in self.nearby.get_children():
            self.nearby.delete(item)
        pattern_index = next((i for i, h in enumerate(self.headers) if h.strip().lower() == "is_pattern"), None)
        start = max(0, self.current_index - 5)
        end = min(len(self.rows), self.current_index + 6)
        for index in range(start, end):
            nearby_row = self.rows[index]
            pattern = nearby_row.values[pattern_index] if pattern_index is not None else "-"
            item = self.nearby.insert("", "end", values=(nearby_row.physical_line, nearby_row.data_number, f"{nearby_row.timestamp:.6f}", pattern), tags=("current",) if index == self.current_index else ())
            if index == self.current_index:
                self.nearby.selection_set(item)
                self.nearby.focus(item)

        for item in self.values.get_children():
            self.values.delete(item)
        for header, value in zip(self.headers, row.values):
            self.values.insert("", "end", values=(header, value))

    def jump_from_table(self, _event=None) -> None:
        selected = self.nearby.selection()
        if not selected:
            return
        line_number = int(self.nearby.item(selected[0], "values")[0])
        for index, row in enumerate(self.rows):
            if row.physical_line == line_number:
                self.current_index = index
                self.current_time = row.timestamp
                self.seek_var.set(self.current_time)
                self.render_current(force=True)
                break

    @staticmethod
    def _vector(value: str) -> tuple[float, float, float]:
        numbers = re.findall(r"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", value)
        if len(numbers) < 3:
            return 0.0, -1.0, 0.0
        return float(numbers[0]), float(numbers[1]), float(numbers[2])

    @staticmethod
    def _normalize(vector: tuple[float, float, float]) -> tuple[float, float, float]:
        length = math.sqrt(sum(value * value for value in vector))
        if length < 0.000001:
            return 0.0, -1.0, 0.0
        return tuple(value / length for value in vector)

    def _interpolated_vectors(self) -> dict[str, tuple[float, float, float]]:
        if not self.rows:
            return {}
        first = self.rows[self.current_index]
        second = self.rows[min(self.current_index + 1, len(self.rows) - 1)]
        duration = max(0.000001, second.timestamp - first.timestamp)
        blend = min(max((self.current_time - first.timestamp) / duration, 0.0), 1.0)
        header_map = {header.strip().lower(): index for index, header in enumerate(self.headers)}
        result = {}
        for name in ("neck", "shoulder_l", "shoulder_r", "elbow_l", "elbow_r", "hip_l", "hip_r", "knee_l", "knee_r"):
            column = header_map.get(name)
            if column is None:
                continue
            a = self._vector(first.values[column])
            b = self._vector(second.values[column])
            result[name] = self._normalize(tuple(a[i] + (b[i] - a[i]) * blend for i in range(3)))
        return result

    def _vectors_from_row(self, row_index: int) -> dict[str, tuple[float, float, float]]:
        row = self.rows[row_index]
        header_map = {header.strip().lower(): index for index, header in enumerate(self.headers)}
        result = {}
        for name in ("neck", "shoulder_l", "shoulder_r", "elbow_l", "elbow_r", "hip_l", "hip_r", "knee_l", "knee_r"):
            column = header_map.get(name)
            if column is not None:
                result[name] = self._normalize(self._vector(row.values[column]))
        return result

    def _visible_pattern_index(self) -> int | None:
        if not self.rows:
            return None
        pattern_column = next((i for i, header in enumerate(self.headers) if header.strip().lower() == "is_pattern"), None)
        if pattern_column is None:
            return None
        start = bisect.bisect_left(self.times, self.current_time - 0.05)
        end = bisect.bisect_right(self.times, self.current_time + 0.05)
        candidates = [i for i in range(start, end) if self.rows[i].values[pattern_column].strip() == "1"]
        return min(candidates, key=lambda i: abs(self.rows[i].timestamp - self.current_time)) if candidates else None

    def draw_avatar(self) -> None:
        if not hasattr(self, "avatar_canvas") or not hasattr(self, "pattern_canvas"):
            return
        self._draw_avatar_canvas(self.avatar_canvas, self._interpolated_vectors(), "")
        pattern_index = self._visible_pattern_index()
        if pattern_index is None:
            self._draw_avatar_canvas(self.pattern_canvas, {}, "패턴 표시 대기")
        else:
            row = self.rows[pattern_index]
            self._draw_avatar_canvas(
                self.pattern_canvas,
                self._vectors_from_row(pattern_index),
                f"PATTERN  CSV {row.physical_line}줄  /  {row.timestamp:.3f}s",
            )

    def _draw_avatar_canvas(self, canvas: tk.Canvas, vectors: dict[str, tuple[float, float, float]], caption: str) -> None:
        canvas.delete("all")
        width = max(canvas.winfo_width(), 500)
        height = max(canvas.winfo_height(), 260)
        if not vectors:
            message = caption if self.rows else "CSV를 열면 아바타가 표시됩니다."
            canvas.create_text(width / 2, height / 2, text=message, fill="#cbd5e1", font=("Malgun Gothic", 14))
            return

        def add(point, direction, length):
            return tuple(point[i] + direction[i] * length for i in range(3))

        pelvis = (0.0, 0.0, 0.0)
        chest = (0.0, 0.82, 0.0)
        neck_base = (0.0, 1.08, 0.0)
        shoulder_l = (-0.24, 0.93, 0.0)
        shoulder_r = (0.24, 0.93, 0.0)
        hip_l = (-0.14, 0.0, 0.0)
        hip_r = (0.14, 0.0, 0.0)
        head = add(neck_base, vectors.get("neck", (0, 1, 0)), 0.30)
        elbow_l = add(shoulder_l, vectors.get("shoulder_l", (-1, 0, 0)), 0.48)
        elbow_r = add(shoulder_r, vectors.get("shoulder_r", (1, 0, 0)), 0.48)
        hand_l = add(elbow_l, vectors.get("elbow_l", (-1, 0, 0)), 0.45)
        hand_r = add(elbow_r, vectors.get("elbow_r", (1, 0, 0)), 0.45)
        knee_l = add(hip_l, vectors.get("hip_l", (0, -1, 0)), 0.65)
        knee_r = add(hip_r, vectors.get("hip_r", (0, -1, 0)), 0.65)
        foot_l = add(knee_l, vectors.get("knee_l", (0, -1, 0)), 0.62)
        foot_r = add(knee_r, vectors.get("knee_r", (0, -1, 0)), 0.62)

        all_points = (head, hand_l, hand_r, foot_l, foot_r)
        min_y = min(point[1] for point in all_points)
        max_y = max(point[1] for point in all_points)
        scale = min(150.0, (height - 34) / max(max_y - min_y, 1.5))
        center_y = (min_y + max_y) / 2

        def project(point):
            # 정면 투영에 Z 깊이를 살짝 반영해 3차원 방향 변화를 알아보기 쉽게 한다.
            return width / 2 + (point[0] + point[2] * 0.20) * scale, height / 2 - (point[1] - center_y) * scale

        canvas.create_oval(width / 2 - 72, project(foot_l)[1] + 5, width / 2 + 72, project(foot_l)[1] + 18, fill="#0f172a", outline="")

        def limb(a, b, color="#67e8f9", thickness=15):
            ax, ay = project(a)
            bx, by = project(b)
            canvas.create_line(ax, ay, bx, by, fill="#07111f", width=thickness + 6, capstyle="round")
            canvas.create_line(ax, ay, bx, by, fill=color, width=thickness, capstyle="round")

        limb(pelvis, chest, "#38bdf8", 24)
        limb(shoulder_l, shoulder_r, "#38bdf8", 18)
        limb(hip_l, hip_r, "#38bdf8", 18)
        limb(neck_base, head, "#fbbf24", 12)
        limb(shoulder_l, elbow_l, "#22d3ee")
        limb(elbow_l, hand_l, "#67e8f9", 12)
        limb(shoulder_r, elbow_r, "#fb7185")
        limb(elbow_r, hand_r, "#fda4af", 12)
        limb(hip_l, knee_l, "#22d3ee", 18)
        limb(knee_l, foot_l, "#67e8f9", 15)
        limb(hip_r, knee_r, "#fb7185", 18)
        limb(knee_r, foot_r, "#fda4af", 15)

        hx, hy = project(head)
        canvas.create_oval(hx - 17, hy - 17, hx + 17, hy + 17, fill="#fde68a", outline="#07111f", width=4)
        for point in (elbow_l, elbow_r, hand_l, hand_r, knee_l, knee_r, foot_l, foot_r):
            x, y = project(point)
            canvas.create_oval(x - 5, y - 5, x + 5, y + 5, fill="#f8fafc", outline="")
        canvas.create_text(12, 12, anchor="nw", text="왼쪽(청록) / 오른쪽(분홍)", fill="#94a3b8", font=("Malgun Gothic", 10))
        if caption:
            canvas.create_text(width - 12, 12, anchor="ne", text=caption, fill="#f0abfc", font=("Malgun Gothic", 10, "bold"))

    def close(self) -> None:
        self.playing = False
        self.root.destroy()


if __name__ == "__main__":
    app_root = tk.Tk()
    CsvPlaybackViewer(app_root)
    app_root.mainloop()
