# 한글문서 PDF 변환 프로그램

여러 HWP/HWPX 문서를 PDF로 일괄 변환하는 Windows 프로그램입니다.

## 주요 기능
- HWP/HWPX 파일·폴더 드래그앤드롭
- 하위 폴더 포함 및 중복 파일 자동 제거
- 원본 파일명 그대로 PDF 저장
- 원본 폴더 / 지정 폴더 저장
- 기존 PDF 자동 이름변경 / 건너뛰기 / 덮어쓰기
- 진행률 및 파일별 성공·실패 로그
- 작업 취소
- 한컴 FilePathChecker 보안모듈 설정 지원

## 필요 환경
- Windows
- 데스크톱 한컴오피스 한글 설치
- `HWPFrame.HwpObject` 자동화 가능 환경

## 다운로드
- [프로그램 패키지 ZIP](download/hwp-pdf-converter.zip)
- [실행파일 EXE](download/HancomPdfBatch.exe)

## 개발
- C# 소스: [`src/Program.cs`](src/Program.cs)
- 빌드 파일: [`build/`](build/)

> 프로그램에는 한컴 DLL 자체가 포함되어 있지 않습니다. 조직 내 배포 전 한컴 자동화 이용 조건, 코드서명 및 사내 보안정책을 확인하세요.
