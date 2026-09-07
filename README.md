<p align="center">
  <img height="300" alt="MapleStory Multiplayer Screenshot 1" src="https://github.com/user-attachments/assets/43c9fb13-df4b-4ad5-b815-0809bbc99255" />
  <img height="300" alt="MapleStory Multiplayer Screenshot 2" src="https://github.com/user-attachments/assets/1b486fac-b971-48ba-a182-6a59f3d7b8d3" />
</p>
<p align="center">
  <a href="https://youtu.be/j2gM4iNW_JY"><b>▶ MapleStory Multiplayer 플레이 영상</b></a>
</p>

**MapleStory Multiplayer**는 메이플스토리의 주요 플레이 요소를 참고하여
**Unity와 Mirror 기반의 멀티플레이 환경으로 구현한 개인 프로젝트**입니다.

단순한 콘텐츠 모작보다 **멀티플레이 게임의 구조 설계와 확장성**에 중점을 두고,
퀵슬롯과 스킬 실행 구조, 서버 기반 다중 맵 관리, 전투 및 드롭 아이템,
플레이어 데이터의 DB 저장과 복원을 직접 설계하고 구현했습니다.

AWS EC2에서 **Headless Dedicated Server**를 운영하며
서버는 여러 맵을 동시에 관리하고, 클라이언트는 자신이 위치한 맵만 로드하도록 구성했습니다.

또한 플레이어의 주요 행동과 전투 판정을 **Server Authority** 기반으로 처리하고,
계정 및 플레이어 데이터를 DB와 연동하여 멀티플레이 환경에서의 전체 게임 흐름을 구현했습니다.

현재 저장소는 **최종 프로젝트 버전** 기준으로 정리되어 있습니다.

---

## 프로젝트 개요

* **프로젝트명** : MapleStory Multiplayer
* **장르** : 2D MMORPG / 멀티플레이
* **개발 인원** : 1인 개인 프로젝트
* **개발 기간** : 2026.08 ~ 2026.09
* **개발 환경** : Unity 6.2, C#
* **네트워크** : AWS EC2, Mirror
* **데이터베이스** : MariaDB
* **서버 구조** : Headless Dedicated Server / Server Authority

---

## 주요 구현 요소

* 메이플스토리 형태의 2D 멀티플레이 환경 구현
* 키보드 형태의 커스텀 퀵슬롯 시스템
* Command 기반 스킬 및 행동 실행 구조
* 서버에서 여러 맵을 관리하는 다중 맵 구조
* 플레이어별 현재 맵에 따른 클라이언트 씬 로딩
* 서버 권한 기반 플레이어 전투 및 피격 판정
* 몬스터 전투, 사망 및 드롭 아이템 처리
* 아이템 획득 및 소비 아이템 인벤토리
* 로그인 / 회원가입 및 플레이어 데이터 DB 연동
* 마지막 접속 위치 및 주요 플레이 데이터 저장 / 복원

---

## 최종 구현 상태

* AWS EC2 기반 Headless Dedicated Server 구축
* Mirror 기반 Server Authority 멀티플레이 적용
* 로그인 / 회원가입 및 계정 DB 연동
* 플레이어 접속 및 마지막 스폰 위치 복원
* Command 기반 플레이어 행동 / 스킬 실행 구조 구현
* 키보드 형태의 퀵슬롯 설정 및 실행 시스템 구현
* 퀵슬롯 데이터 DB 저장 / 복원
* 서버 다중 맵 로딩 및 맵별 플레이어 관리
* 클라이언트 현재 맵 기준 씬 로딩 구조 구현
* 맵 이동 및 포탈 시스템 구현
* 플레이어 공격 / 피격 / 사망 / 부활 구현
* 몬스터 전투 / 사망 / 리스폰 구현
* 몬스터 활성화 범위 및 맵 단위 관리
* 드롭 아이템 생성 / 획득 / 자동 제거 구현
* 소비 아이템 인벤토리 및 사용 처리
* 주요 플레이어 데이터 DB 저장 / 복원

---

## 주요 시스템 구조

### QuickSlot & Command

플레이어의 입력과 실제 행동 실행을 분리하고,
퀵슬롯에는 행동 자체가 아닌 **Command를 바인딩하는 구조**로 구현했습니다.

기본 공격, 점프, 상호작용 등의 행동을 동일한 실행 흐름으로 관리하여
새로운 스킬이나 행동을 추가할 때 기존 입력 및 UI 코드의 변경을 최소화했습니다.

### Multi Map

Dedicated Server는 여러 맵 Scene을 동시에 로드하여 관리하고,
각 클라이언트는 플레이어가 현재 위치한 맵 Scene만 로드하도록 구성했습니다.

맵 이동 시 서버가 플레이어의 위치와 상태를 관리하며,
몬스터 및 전투 객체 역시 맵 단위로 관리되도록 구현했습니다.

### Combat

공격 요청 이후 실제 데미지 판정, 체력 변경, 몬스터 사망 및 드롭 처리는
서버에서 수행하는 **Server Authority 구조**를 적용했습니다.

이를 통해 각 클라이언트가 독립적으로 전투 결과를 결정하지 않고
서버의 결과를 기준으로 동일한 게임 상태를 유지하도록 구성했습니다.

### Database

계정 정보뿐만 아니라 플레이어의 게임 상태를 DB와 연동했습니다.

로그인 시 저장된 데이터를 불러와 플레이어 상태를 복원하고,
맵 위치와 스폰 지점, 퀵슬롯 등 주요 플레이 데이터를 서버에서 관리 및 저장하도록 구현했습니다.

---

## 실행 방법

프로젝트 실행을 위해서는 **Dedicated Server와 Database 환경이 필요합니다.**

1. Dedicated Server 실행
2. MariaDB 서버 연결 확인
3. 클라이언트 실행
4. 회원가입 또는 로그인
5. 캐릭터로 게임 월드 입장

서버 및 DB 접속 정보와 같은 민감한 설정 값은 저장소에 포함하지 않습니다.

---

## Documents

프로젝트의 설계 과정과 주요 문제 해결 과정은 별도의 기술문서에서 확인할 수 있습니다.

* **Technical Document** : 기술문서 링크
* **Notion Portfolio** : Notion 링크
* **Demo Video** : YouTube 링크
