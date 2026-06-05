# Soulslike Boss Combat Prototype

P의 거짓을 레퍼런스로 한 **퍼펙트가드 중심** 소울라이크 보스 전투 프로토타입.
Unity 6 (URP) · 1인 개발 · 포트폴리오.

> ⚙️ 개발 중 (WIP) — 데모 영상/GIF는 추후 추가

## 핵심 컨셉

거리를 벌리는 인내가 아니라, **제자리에서 정확한 타이밍에 막는 퍼펙트가드로 적 스태거를 쌓아 그로기 → 치명타로 잇는 공격적 방어**. 회피로는 스태거가 쌓이지 않는다는 단일 규칙이 퍼펙트가드를 주력 동사로 만든다.

- **퍼펙트가드** — 좁은 윈도우(8f) 내 방어 시 무피해 + 적 스태거 누적
- **스태거 → 치명타** — 누적 스태거로 보스 그로기 유발, 치명타로 큰 보상
- **가드 리제너레이션** — 막아 잃은 회색 체력을 반격으로 회복
- **보스 「그을린 파수병」** — 8패턴 2페이즈(무게 → 화염)

## 기술 스택

- Unity 6 (6000.4) / URP
- Input System, AI Navigation, Test Framework
- 데이터 주도 설계(ScriptableObject), FSM, 애니메이션 구동 판정(Animation Event)

## 설계 문서

전투 기획부터 시스템 아키텍처, 개발 로드맵까지 → **[`Docs/`](Docs/README.md)**

| | |
|--|--|
| [기획 (GDD)](Docs/01-Game-Design-Document.md) | 핵심 루프·동사·메커니즘 |
| [전투 파라미터](Docs/02-Combat-Parameters.md) | 손맛 수치표 |
| [보스 설계](Docs/03-Boss-Design.md) | 8패턴·페이즈 |
| [시스템 아키텍처](Docs/04-System-Architecture.md) | 컴포넌트·의존 구조 |
| [상태 머신](Docs/05-State-Machines.md) | 플레이어/보스 FSM |
| [데이터 모델](Docs/06-Data-Model.md) | ScriptableObject 스키마 |
| [데미지 파이프라인](Docs/07-Damage-Pipeline.md) | 판정 흐름 |
| [개발 로드맵](Docs/08-Development-Roadmap.md) | M0~M8 마일스톤 |

## 빌드

Unity 6000.4.6f1 이상에서 프로젝트 열기 → `Assets/Scenes` (개발 진행 중).

---

*포트폴리오 프로젝트입니다. 수치·설계는 플레이테스트로 튜닝됩니다.*
