# 한글문서 표 합치기 프로그램

여러 HWPX 문서에 흩어져 있는 같은 형식의 표를 한 문서로 이어붙이는 Windows 프로그램입니다.
목록 맨 위 문서가 기준이 되고, 나머지 문서의 표 행이 순서대로 아래에 붙습니다.

## 주요 기능
- HWPX 파일 드래그앤드롭 또는 버튼으로 추가
- 첫 행(제목행) 내용이 같은 표끼리 자동으로 짝을 맞춤
- 제목행 자동 감지(한글의 '제목 줄' 지정 인식) 또는 제외할 행 수 수동 지정
- 1열짜리 표(제목 글상자)는 자동으로 건너뜀
- 기준 문서 서식으로 통일하거나, 추가 문서 서식을 그대로 유지
- 목록 순서 변경(위로/아래로), 선택 제거, 모두 지우기
- 결과 파일명이 이미 있으면 `이름 (1).hwpx`처럼 자동으로 번호를 붙여 저장
- 진행 로그 표시 및 완료 후 결과 파일 바로 열기

## 필요 환경
- Windows
- **한컴오피스 설치 불필요** — HWPX 파일의 XML을 직접 다룹니다.

> **HWPX 전용입니다.** 구버전 `.hwp` 파일은 지원하지 않습니다.
> 한글에서 '다른 이름으로 저장 → HWPX'로 변환한 뒤 사용하세요.

## 다운로드
- [프로그램 패키지 ZIP](download/hwp-table-merger.zip)
- [실행파일 EXE](download/HwpxTableMerge.exe)

## 명령행 사용
핵심 모듈은 단독 실행이 가능해 배치 작업에 붙일 수 있습니다.

```
python hwpx_table_merge.py A.hwpx B.hwpx -o 결과.hwpx
python hwpx_table_merge.py A.hwpx B.hwpx -o 결과.hwpx --skip-header 2
```

## 개발
- GUI 소스: [`src/hwpx_merge_gui.py`](src/hwpx_merge_gui.py)
- 병합 로직: [`src/hwpx_table_merge.py`](src/hwpx_table_merge.py)
- 빌드 파일: [`build/`](build/)

빌드에는 `lxml`, `tkinterdnd2`, `pyinstaller`가 필요합니다.

```
pip install lxml tkinterdnd2 pyinstaller
build\BUILD_EXE.bat
```

> 서명되지 않은 EXE는 Windows SmartScreen 경고가 나타날 수 있습니다. 조직 내 배포 전에는 코드서명과 사내 보안정책을 확인하세요.
