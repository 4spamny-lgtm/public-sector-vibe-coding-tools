#!/usr/bin/env python3
"""
hwpx_merge_gui.py — HWPX 표 병합 프로그램 (GUI)
- 파일을 창에 드래그해서 넣거나 '파일 추가' 버튼으로 선택
- 목록 맨 위 문서가 기준, 나머지 문서의 표 행이 순서대로 아래에 붙음
- 결과 파일 이름이 이미 있으면 자동으로 (1), (2) … 를 붙여 저장

필요 패키지: pip install lxml tkinterdnd2
"""
import os
import subprocess
import sys
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

sys.path.insert(0, os.path.dirname(os.path.abspath(sys.argv[0])))
import hwpx_table_merge as core  # noqa: E402

try:
    from tkinterdnd2 import DND_FILES, TkinterDnD
    BaseTk = TkinterDnD.Tk
    HAS_DND = True
except ImportError:          # tkinterdnd2 가 없으면 버튼으로만 동작
    BaseTk = tk.Tk
    HAS_DND = False


class App(BaseTk):
    def __init__(self):
        super().__init__()
        self.title("HWPX 표 병합")
        self.resizable(False, False)
        self.files = []
        self.path_out = tk.StringVar()
        self.auto_header = tk.BooleanVar(value=True)
        self.skip_header = tk.IntVar(value=2)
        self.keep_style = tk.BooleanVar(value=False)
        self.open_after = tk.BooleanVar(value=True)
        self._build()

    # ---------------- UI
    def _build(self):
        frm = ttk.Frame(self, padding=12)
        frm.grid()

        hint = "여기에 HWPX 파일을 끌어다 놓으세요 (맨 위 문서가 기준)" if HAS_DND \
            else "'파일 추가' 버튼으로 문서를 넣으세요 (맨 위 문서가 기준)"
        ttk.Label(frm, text=hint).grid(row=0, column=0, columnspan=4, sticky="w")

        self.lb = tk.Listbox(frm, width=64, height=8, selectmode="extended",
                             font=("맑은 고딕", 10), activestyle="none")
        self.lb.grid(row=1, column=0, columnspan=4, pady=(4, 4))
        if HAS_DND:
            self.lb.drop_target_register(DND_FILES)
            self.lb.dnd_bind("<<Drop>>", self._on_drop)

        btns = ttk.Frame(frm)
        btns.grid(row=2, column=0, columnspan=4, sticky="w")
        for txt, cmd in (("파일 추가", self._add_dialog), ("위로", lambda: self._move(-1)),
                         ("아래로", lambda: self._move(1)), ("선택 제거", self._remove),
                         ("모두 지우기", self._clear)):
            ttk.Button(btns, text=txt, command=cmd).pack(side="left", padx=(0, 4))

        ttk.Label(frm, text="결과 저장 위치 (비워두면 기준 문서 옆에 '_병합' 이름으로 저장)").grid(
            row=3, column=0, columnspan=4, sticky="w", pady=(10, 0))
        ttk.Entry(frm, textvariable=self.path_out, width=54).grid(row=4, column=0, columnspan=3, sticky="w")
        ttk.Button(frm, text="찾아보기", command=self._pick_out).grid(row=4, column=3, padx=(4, 0))

        opt = ttk.LabelFrame(frm, text="옵션", padding=8)
        opt.grid(row=5, column=0, columnspan=4, sticky="ew", pady=8)
        ttk.Checkbutton(opt, text="제목행 자동 감지 (한글에서 '제목 줄'로 지정된 행 제외 — 권장)",
                        variable=self.auto_header, command=self._toggle_header).grid(
            row=0, column=0, columnspan=2, sticky="w")
        ttk.Label(opt, text="   수동 지정: 추가 문서 표의 위쪽").grid(row=1, column=0, sticky="w", pady=(4, 0))
        self.spin = ttk.Spinbox(opt, from_=0, to=10, width=4, textvariable=self.skip_header, state="disabled")
        self.spin.grid(row=1, column=1, padx=6, pady=(4, 0))
        ttk.Label(opt, text="행 제외").grid(row=1, column=2, sticky="w", pady=(4, 0))
        ttk.Checkbutton(opt, text="추가 문서의 글자/테두리 서식 그대로 유지 (보통 체크 안 함)",
                        variable=self.keep_style).grid(row=2, column=0, columnspan=3, sticky="w", pady=(6, 0))
        ttk.Checkbutton(opt, text="완료 후 병합된 파일 바로 열기",
                        variable=self.open_after).grid(row=3, column=0, columnspan=3, sticky="w", pady=(4, 0))

        self.btn = tk.Button(frm, text="▶  병합 실행", command=self._run,
                             font=("맑은 고딕", 14, "bold"), bg="#1a73e8", fg="white",
                             activebackground="#1557b0", activeforeground="white",
                             disabledforeground="#dddddd", relief="flat", cursor="hand2",
                             height=2)
        self.btn.grid(row=6, column=0, columnspan=4, sticky="ew", pady=(4, 10))

        self.log = tk.Text(frm, height=8, width=70, state="disabled", font=("맑은 고딕", 9))
        self.log.grid(row=7, column=0, columnspan=4)

    # ---------------- 파일 목록 조작
    def _add(self, paths):
        for p in paths:
            p = os.path.normpath(p)
            if p.lower().endswith(".hwpx") and p not in self.files:
                self.files.append(p)
                self.lb.insert("end", os.path.basename(p))

    def _on_drop(self, event):
        self._add(self.tk.splitlist(event.data))

    def _add_dialog(self):
        self._add(filedialog.askopenfilenames(title="HWPX 파일 선택", filetypes=[("한글 문서", "*.hwpx")]))

    def _move(self, d):
        sel = list(self.lb.curselection())
        if not sel:
            return
        if d < 0 and sel[0] == 0 or d > 0 and sel[-1] == len(self.files) - 1:
            return
        for i in (sel if d < 0 else reversed(sel)):
            self.files[i], self.files[i + d] = self.files[i + d], self.files[i]
        self._refresh([i + d for i in sel])

    def _remove(self):
        for i in reversed(self.lb.curselection()):
            del self.files[i]
        self._refresh()

    def _clear(self):
        self.files.clear()
        self._refresh()

    def _refresh(self, select=()):
        self.lb.delete(0, "end")
        for p in self.files:
            self.lb.insert("end", os.path.basename(p))
        for i in select:
            self.lb.selection_set(i)

    def _toggle_header(self):
        self.spin.configure(state="disabled" if self.auto_header.get() else "normal")

    def _pick_out(self):
        p = filedialog.asksaveasfilename(title="결과 저장", defaultextension=".hwpx",
                                         filetypes=[("한글 문서", "*.hwpx")], initialfile="병합결과.hwpx")
        if p:
            self.path_out.set(p)

    def _write(self, msg):
        self.log.configure(state="normal")
        self.log.insert("end", msg + "\n")
        self.log.see("end")
        self.log.configure(state="disabled")

    def _done(self, saved):
        if self.open_after.get():
            try:
                if sys.platform == "win32":
                    os.startfile(saved)
                elif sys.platform == "darwin":
                    subprocess.Popen(["open", saved])
                else:
                    subprocess.Popen(["xdg-open", saved])
            except Exception as e:  # noqa: BLE001
                messagebox.showinfo("완료", f"저장했습니다:\n{saved}\n\n(자동으로 열지 못했습니다: {e})")
                return
        messagebox.showinfo("완료", f"저장했습니다:\n{saved}")

    # ---------------- 실행
    def _run(self):
        if len(self.files) < 2:
            messagebox.showwarning("확인", "문서를 2개 이상 넣어 주세요.")
            return
        out = self.path_out.get().strip()
        if not out:
            base, _ = os.path.splitext(self.files[0])
            out = base + "_병합.hwpx"
        self.btn.configure(state="disabled", bg="#9e9e9e", text="병합 중…")
        self.log.configure(state="normal"); self.log.delete("1.0", "end"); self.log.configure(state="disabled")
        threading.Thread(target=self._work, args=(list(self.files), out), daemon=True).start()

    def _work(self, files, out):
        class _Redirect:
            def write(s, t):
                if t.strip():
                    self.after(0, self._write, t.rstrip())
            def flush(s): pass
        old_out, old_err = sys.stdout, sys.stderr
        sys.stdout = sys.stderr = _Redirect()
        try:
            skip = None if self.auto_header.get() else self.skip_header.get()
            saved = core.merge(files, out, skip, self.keep_style.get())
            self.after(0, lambda: self._done(saved))
        except Exception as e:  # noqa: BLE001
            self.after(0, lambda: messagebox.showerror("오류", f"병합 중 문제가 생겼습니다:\n{e}"))
        finally:
            sys.stdout, sys.stderr = old_out, old_err
            self.after(0, lambda: self.btn.configure(state="normal", bg="#1a73e8", text="▶  병합 실행"))


if __name__ == "__main__":
    App().mainloop()
