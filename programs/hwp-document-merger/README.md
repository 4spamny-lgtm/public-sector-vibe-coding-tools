# 한글문서 병합 프로그램

여러 HWP/HWPX 문서를 원하는 순서대로 하나의 HWPX 문서로 취합하는 Windows 프로그램입니다.

## 주요 기능
- HWP/HWPX 파일·폴더 드래그앤드롭
- 여러 폴더의 파일 함께 추가 및 중복 제거
- 목록 순서 / 파일명 자연정렬
- ▲ / ▼ 버튼으로 수동 순서 변경
- 결과 저장 폴더 지정
- 파일별 성공·실패 로그
- 오류 파일을 건너뛰고 계속 취합

## 필요 환경
- Windows
- 데스크톱 한컴오피스 한글 설치
- `HWPFrame.HwpObject` 자동화 가능 환경

## 다운로드
- [프로그램 패키지 ZIP](download/hwp-document-merger.zip)
- [실행파일 EXE](download/HwpDocumentMerger.exe)

## 개발
- C# 소스: [`src/HwpMergeDragDrop.cs`](src/HwpMergeDragDrop.cs)
- 빌드 파일: [`build/`](build/)

> 서명되지 않은 EXE는 Windows SmartScreen 경고가 나타날 수 있습니다. 조직 내 배포 전에는 코드서명과 사내 보안정책을 확인하세요.
