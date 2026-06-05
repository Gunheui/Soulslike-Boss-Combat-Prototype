# 설계 문서 — Soulslike Boss Combat Prototype

P의 거짓을 레퍼런스로 한 **퍼펙트가드 중심** 소울라이크 보스 전투 프로토타입의 설계 문서.
기획 → 시스템 아키텍처 → 개발 로드맵 순으로 정리.

> Unity 6 (6000.4) / URP / Input System · 1인 개발 · 포트폴리오
> MVP: 플레이어 코어 전투 + 보스 1체(「그을린 파수병」)

## 핵심 컨셉

전투의 심장은 **"버티며 받아쳐 무너뜨리는 공격적 방어"**다. 회피로 거리를 벌리는 인내가 아니라, 제자리에서 정확한 타이밍에 막는 **퍼펙트가드**로 적 스태거를 쌓아 → 그로기 → 치명타로 잇는 루프. 회피로는 스태거가 쌓이지 않는다는 단일 규칙이 퍼펙트가드를 주력 동사로 만든다.

## 문서 색인

### 기획 (Game Design)
| 문서 | 내용 |
|------|------|
| [01 — Game Design Document](01-Game-Design-Document.md) | 핵심 전투 루프, 플레이어 동사, 퍼펙트가드/스태거/치명타/가드 리제너레이션 메커니즘, 자원 시스템 |
| [02 — Combat Parameters](02-Combat-Parameters.md) | 전 수치 표 (i-frame·PG 윈도우·콤보·스태거 등, @60fps 프레임+초) |
| [03 — Boss Design](03-Boss-Design.md) | 보스 「그을린 파수병」 8패턴 명세, 페이즈 전환, 한 판 흐름 |

### 시스템 아키텍처 (Architecture)
| 문서 | 내용 |
|------|------|
| [04 — System Architecture](04-System-Architecture.md) | 컴포넌트/클래스 분해, 어셈블리 의존 구조 |
| [05 — State Machines](05-State-Machines.md) | 플레이어/보스 FSM, 상태·전이, 데드락 검증 |
| [06 — Data Model](06-Data-Model.md) | ScriptableObject 스키마, 컴포넌트 직렬화 필드 |
| [07 — Damage Pipeline](07-Damage-Pipeline.md) | DamageInfo 계약, 히트박스/허트박스 흐름, Animation Event 훅 |

### 개발 일정 (Planning)
| 문서 | 내용 |
|------|------|
| [08 — Development Roadmap](08-Development-Roadmap.md) | M0~M8 마일스톤, 수직 슬라이스, 캡처 포인트 |
| [09 — Task Breakdown](09-Task-Breakdown.md) | 마일스톤별 태스크 (의존·규모·DoD) |
| [10 — Backlog](10-Backlog.md) | MVP 밖 기능 |

## 설계 원칙

- **데이터 주도** — 손맛 수치는 ScriptableObject/Inspector로 노출, 재컴파일 없이 튜닝.
- **경계면 계약** — `DamageInfo` 한 방향 흐름(공격자 Hitbox → 방어자 Hurtbox → Resolver → Health/Posture).
- **애니메이션 구동 판정** — 히트박스·i-frame·퍼펙트가드 윈도우는 Animation Event로 제어("보이는 것 = 맞는 것").
- **수직 슬라이스** — 매 마일스톤 끝에 플레이 가능한 빌드.

---

*수치·결정은 1차 가설이며 플레이테스트로 튜닝됩니다. 0순위 튜닝값: 퍼펙트가드 윈도우(8f), 스태거 감소율(12/s).*
