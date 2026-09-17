# Architecture & Roadmap

## 1. 현재: Local-first
현재 도구 대부분은 `index.html` 하나를 브라우저에서 실행합니다. 파일 입력, 화면 처리, JSON/CSV 저장 등을 사용자 PC 안에서 처리하는 방식입니다. 설치가 어렵거나 망분리 환경인 업무에 적합합니다.

## 2. 공동개발 단계
단일 HTML이 너무 커지면 다음처럼 분리합니다.

```text
project/
├─ index.html
├─ css/style.css
├─ js/app.js
├─ js/storage.js
├─ js/data-service.js
└─ sample/sample-data.json
```

핵심은 화면 코드가 `localStorage`나 `IndexedDB`를 직접 호출하지 않고 `loadData()`, `saveData()` 같은 저장 인터페이스를 거치게 만드는 것입니다.

## 3. 서버 전환이 필요한 프로젝트
### 우선 후보
- 개별 위원회 관리도구
- 부서 업무포털(청소년과 DEMO)

여러 사용자가 같은 데이터를 보고 고쳐야 하므로 로그인, 권한, DB, 변경이력의 가치가 큽니다.

### Local-first 유지 가치가 큰 프로젝트
- 자료요구 답변서 작성기
- 예산 사업설명서 작성 도구

업무파일을 외부 서버로 전송하지 않는 것이 장점일 수 있으므로, 서버화하더라도 문서 처리 자체는 브라우저에 남기는 하이브리드 구조를 검토할 수 있습니다.

## 4. 향후 서버 구조 예시

```text
Browser UI
   ↓ REST/JSON API
Application Server
   ↓
Database
```

위원회 관리도구라면 `committees / members / meetings / agendas / minutes / expenses / users` 등의 테이블로 나눌 수 있습니다.

## 5. 지금부터 지킬 원칙
- 실제 데이터와 프로그램 코드를 분리
- 공개 DEMO 데이터 별도 유지
- 저장 기능을 한 곳으로 모으기
- 외부 API/서버 의존성은 명확히 문서화
- 개인정보가 들어가는 필드는 기본적으로 공개 저장소에서 비워 두기
