#!/usr/bin/env python3
"""
hwpx_table_merge.py
-------------------
첫 문서의 표 아래에 나머지 문서들의 표 행(row)을 순서대로 이어붙여 새 HWPX 문서를 만든다.
표는 '첫 행(제목행) 내용이 같은 표'끼리 짝을 맞추며, 1열짜리 표(제목 글상자)는 건너뛴다.
결과 파일명이 이미 있으면 '이름 (1).hwpx' 처럼 번호를 붙여 저장한다.

사용법:
    python hwpx_table_merge.py A.hwpx B.hwpx C.hwpx -o 결과.hwpx
    python hwpx_table_merge.py A.hwpx B.hwpx -o 결과.hwpx --skip-header 2   # 제목행을 수동으로 2줄 지정

필요 패키지:  pip install lxml
"""

import argparse
import copy
import os
import sys
import zipfile
from lxml import etree

NS = {
    "hp": "http://www.hancom.co.kr/hwpml/2011/paragraph",
    "hs": "http://www.hancom.co.kr/hwpml/2011/section",
    "hc": "http://www.hancom.co.kr/hwpml/2011/core",
}
HP = "{%s}" % NS["hp"]

# 셀/문단/글자에서 서식을 참조하는 속성명 (header.xml 의 ID를 가리킴)
STYLE_ATTRS = ("borderFillIDRef", "paraPrIDRef", "charPrIDRef", "styleIDRef")


# ---------------------------------------------------------------- 유틸
def section_names(zf: zipfile.ZipFile):
    """Contents/section0.xml, section1.xml ... 을 번호순으로 반환"""
    names = [n for n in zf.namelist()
             if n.startswith("Contents/section") and n.endswith(".xml")]
    return sorted(names, key=lambda n: int(n[len("Contents/section"):-4]))


def load_tables(zf: zipfile.ZipFile):
    """모든 섹션을 파싱해 (섹션이름, 트리, [tbl 요소...]) 리스트를 돌려준다."""
    result = []
    for name in section_names(zf):
        tree = etree.fromstring(zf.read(name))
        tbls = tree.findall(".//hp:tbl", NS)
        result.append((name, tree, tbls))
    return result


def rows_of(tbl):
    return tbl.findall("hp:tr", NS)


def cells_of(tr):
    return tr.findall("hp:tc", NS)


def col_of(tc):
    addr = tc.find("hp:cellAddr", NS)
    return int(addr.get("colAddr", "0")) if addr is not None else 0


def apply_template_style(new_tc, tmpl_tc):
    """B 셀의 서식 ID를 A 셀(템플릿)의 것으로 바꿔 A 문서 header.xml 과 맞춘다."""
    if tmpl_tc is None:
        return
    # 셀 테두리/배경
    if tmpl_tc.get("borderFillIDRef") is not None:
        new_tc.set("borderFillIDRef", tmpl_tc.get("borderFillIDRef"))
    # 셀 안 문단/글자 서식: 템플릿 첫 문단, 첫 run 을 기준으로
    tmpl_p = tmpl_tc.find(".//hp:p", NS)
    tmpl_run = tmpl_tc.find(".//hp:run", NS)
    for p in new_tc.findall(".//hp:p", NS):
        if tmpl_p is not None:
            for a in ("paraPrIDRef", "styleIDRef"):
                if tmpl_p.get(a) is not None:
                    p.set(a, tmpl_p.get(a))
    for run in new_tc.findall(".//hp:run", NS):
        if tmpl_run is not None and tmpl_run.get("charPrIDRef") is not None:
            run.set("charPrIDRef", tmpl_run.get("charPrIDRef"))


def cell_text(tc):
    return " ".join("".join(tc.itertext()).split())


def table_key(tbl):
    """표의 첫 행(제목행) 텍스트로 '같은 표'인지 판단할 키를 만든다."""
    rows = rows_of(tbl)
    if not rows:
        return None
    return (tbl.get("colCnt"), tuple(cell_text(tc) for tc in cells_of(rows[0])))


def is_data_table(tbl):
    """1열짜리 표는 제목/안내 글상자로 보고 병합 대상에서 제외한다."""
    return int(tbl.get("colCnt", "0")) >= 2


def header_row_count(tbl):
    """한글이 제목행으로 표시한(header="1") 앞쪽 행 개수를 센다."""
    n = 0
    for tr in rows_of(tbl):
        cells = cells_of(tr)
        if cells and all(tc.get("header") == "1" for tc in cells):
            n += 1
        else:
            break
    return n


def describe(tbl):
    k = table_key(tbl)
    head = " | ".join(k[1])[:40] if k else "(빈 표)"
    return f"{tbl.get('colCnt')}열 {tbl.get('rowCnt')}행 [{head}]"


# ---------------------------------------------------------------- 핵심
def append_rows(tbl_a, tbl_b, skip_header=None, keep_b_style=False):
    """tbl_b 의 행을 tbl_a 아래에 붙인다. 추가된 행 수를 반환."""
    if skip_header is None:                     # 자동: 제목행 표시된 행 제외
        skip_header = header_row_count(tbl_b)
    rows_a = rows_of(tbl_a)
    rows_b = rows_of(tbl_b)[skip_header:]
    if not rows_b:
        return 0

    col_a = int(tbl_a.get("colCnt", "0"))
    col_b = int(tbl_b.get("colCnt", "0"))
    if col_a != col_b:
        print(f"  [경고] 열 개수 불일치 (A={col_a}, B={col_b}) — 그대로 붙입니다.",
              file=sys.stderr)

    offset = len(rows_a)
    # 서식 템플릿: A 표의 마지막 행 (열 번호 -> 셀)
    tmpl = {col_of(tc): tc for tc in cells_of(rows_a[-1])} if rows_a else {}

    for tr_b in rows_b:
        tr_new = copy.deepcopy(tr_b)
        for tc in cells_of(tr_new):
            addr = tc.find("hp:cellAddr", NS)
            if addr is not None:
                addr.set("rowAddr", str(int(addr.get("rowAddr", "0")) - skip_header + offset))
            if not keep_b_style:
                apply_template_style(tc, tmpl.get(col_of(tc)))
        tbl_a.append(tr_new)

    # 행 수 갱신
    tbl_a.set("rowCnt", str(offset + len(rows_b)))

    # 표 전체 높이 갱신 (셀 높이 합산 근사) — 한글이 열 때 재계산하지만 맞춰두면 안전
    sz_a = tbl_a.find("hp:sz", NS)
    if sz_a is not None:
        add_h = 0
        for tr_b in rows_b:
            hs = [int(tc.find("hp:cellSz", NS).get("height", "0"))
                  for tc in cells_of(tr_b) if tc.find("hp:cellSz", NS) is not None]
            add_h += max(hs) if hs else 0
        sz_a.set("height", str(int(sz_a.get("height", "0")) + add_h))
    return len(rows_b)


def unique_path(path):
    """같은 이름의 파일이 있으면 '이름 (1).hwpx', '이름 (2).hwpx' 식으로 새 이름을 만든다."""
    if not os.path.exists(path):
        return path
    base, ext = os.path.splitext(path)
    n = 1
    while os.path.exists(f"{base} ({n}){ext}"):
        n += 1
    return f"{base} ({n}){ext}"


def merge(paths, path_out, skip_header=None, keep_b_style=False):
    """
    paths[0] 을 기준 문서로 삼고, paths[1:] 문서들의 표 행을 순서대로 이어붙인다.
    실제 저장된 경로를 반환한다 (동일 이름이 있으면 자동으로 번호를 붙임).
    """
    if len(paths) < 2:
        raise ValueError("문서가 2개 이상 필요합니다.")
    path_out = unique_path(path_out)

    with zipfile.ZipFile(paths[0]) as za:
        secs_a = load_tables(za)
        tbls_a = [t for _, _, ts in secs_a for t in ts if is_data_table(t)]
        print(f"기준 문서: {os.path.basename(paths[0])} — 병합 대상 표 {len(tbls_a)}개")
        for i, t in enumerate(tbls_a):
            print(f"  표 {i + 1}: {describe(t)}")
        if not tbls_a:
            raise ValueError("기준 문서에 2열 이상인 표가 없습니다.")

        # 제목행 내용 -> A 표 목록 (같은 제목행이 여러 개면 등장 순서대로 짝을 맞춤)
        by_key = {}
        for t in tbls_a:
            by_key.setdefault(table_key(t), []).append(t)

        for pb in paths[1:]:
            with zipfile.ZipFile(pb) as zb:
                tbls_b = [t for _, _, ts in load_tables(zb) for t in ts if is_data_table(t)]
            print(f"추가 문서: {os.path.basename(pb)} — 병합 대상 표 {len(tbls_b)}개")
            used = {k: 0 for k in by_key}
            for t_b in tbls_b:
                k = table_key(t_b)
                cands = by_key.get(k, [])
                if used.get(k, 0) >= len(cands):
                    print(f"  건너뜀: {describe(t_b)} — 기준 문서에 제목행이 같은 표가 없음",
                          file=sys.stderr)
                    continue
                t_a = cands[used[k]]
                used[k] += 1
                idx = tbls_a.index(t_a) + 1
                added = append_rows(t_a, t_b, skip_header, keep_b_style)
                print(f"  표 {idx}에 {added}행 추가 → 총 {t_a.get('rowCnt')}행")

        modified = {name: etree.tostring(tree, xml_declaration=True,
                                         encoding="UTF-8", standalone=True)
                    for name, tree, _ in secs_a}
        with zipfile.ZipFile(path_out, "w") as zout:
            for info in za.infolist():
                data = modified.get(info.filename, za.read(info.filename))
                comp = zipfile.ZIP_STORED if info.filename == "mimetype" \
                    else zipfile.ZIP_DEFLATED
                zout.writestr(info.filename, data, compress_type=comp)

    print(f"완료: {path_out}")
    return path_out


# ---------------------------------------------------------------- CLI
def main():
    ap = argparse.ArgumentParser(
        description="HWPX 표 병합: 첫 문서의 표 아래에 나머지 문서의 표 행을 순서대로 추가")
    ap.add_argument("files", nargs="+", help="병합할 문서들 (첫 번째가 기준)")
    ap.add_argument("-o", "--out", required=True, help="결과 파일 경로 (.hwpx)")
    ap.add_argument("--skip-header", type=int, default=None, metavar="N",
                    help="추가 문서 표의 첫 N행을 제외 (생략 시 제목행 자동 감지)")
    ap.add_argument("--keep-b-style", action="store_true",
                    help="추가 문서 셀의 서식 ID를 그대로 유지")
    args = ap.parse_args()
    merge(args.files, args.out, args.skip_header, args.keep_b_style)


if __name__ == "__main__":
    main()
