# 마일스톤 로드맵 — M0~M8

> 의존 뼈대(architect 확정): `Project.Combat`(DamageInfo/DamageResult/IDamageable/IDamageResolver, Health·Stamina·Posture, Hitbox·Hurtbox)가 **모든 것의 선행 루트**. 데미지 파이프라인이 플레이어 공격·보스 AI·가드보다 **먼저**(M2 < M3,M4). 그 위에 (a) Input→FSM→Locomotion/Combat, (b) GuardSystem+PlayerDamageResolver, (c) BossBrain+PatternSelector+BossDamageResolver를 쌓고, 마지막에 BossPhaseManager·CriticalAttackSystem·Feel/UI(이벤트 구독자). SO 4종(AttackData/WeaponData/AttackPattern/BossPhaseData)은 Combat 코어 직후·각 시스템 직전 준비.

**변경 메모:**
- 2026-06-05 최초 작성 (v1).

---

## 0. 일정 원칙 (요약)

- **매 마일스톤 끝 = 플레이 가능한 빌드.** 포트폴리오는 언제 캡처해도 보여줄 게 있어야 한다. M1부터 캡처 가능(캡슐이 움직이고 구른다).
- **리스크 앞으로.** 최대 리스크 2개 — **퍼펙트가드 손맛(8f 윈도우)**, **보스 공방 리듬(텔레그래프↔PG↔펀시)** — 를 M4·M5에서 일찍 검증한다. 화려한 VFX/UI(M7)는 뒤로.
- **데미지 파이프라인 선행.** M2에서 Combat 코어 전체(Health/Stamina/Posture + DamageInfo/Resolver + Hitbox/Hurtbox)를 완성해야 M3 공격·M4 방어가 의미를 가진다. "때려도 아무 일 안 일어남" 방지.
- **MVP 사수.** 플레이어 코어 + 보스 1체(그을린 파수병). 다중 무기·레전 암·두 번째 보스 등은 `10-Backlog.md`. 마일스톤에 넣지 않는다.
- **스파이크 명시.** 손맛처럼 불확실한 건 조사/실험 태스크로 일정에 박는다(추측 금지). 상세 태스크는 `09-Task-Breakdown.md`.

---

## 1. 마일스톤 로드맵 표 (M0~M8)

| MS | 목표 | 산출물 | **플레이 가능 상태** (수직 슬라이스) | DoD |
|----|------|--------|--------------------------------------|-----|
| **M0** | 프로젝트 스캐폴딩 | 폴더 구조, asmdef 5종(Combat/Player/Boss/Feel/UI)+Tests, Layer/Collision Matrix, Input Actions, 테스트 씬, Test Framework 셋업 | 빈 테스트 씬이 로드되고 캡슐 placeholder가 보임 | asmdef 의존 방향(상위→Combat 단방향) 컴파일 통과, Hitbox/Hurtbox 레이어 4종 + Collision Matrix로 동팀 차단 설정, Input Action Asset(Move/Dodge/Attack/Guard/LockOn) 바인딩, EditMode 테스트 1개 그린 |
| **M1** | 플레이어 이동 | PlayerInputReader, PlayerStateMachine(Idle/Move/Dodge), PlayerLocomotion, LockOnSystem(Transform LookAt 폴백) | 캡슐이 8방향 이동·달리기·회피(i-frame)·락온 스트레이프 | 락온 중 보스 중심 스트레이프(2.0m/s)·회전 720°/s 동작, 회피 입력 시 i-frame(AE_IFrameOn 3f~Off 14f) 동안 더미 공격 통과, Dodge.OnExit에서 Hurtbox 강제 복구(i-frame 누수 0), 스태미나 -25 소모·1.0s 후 40/s 회복 |
| **M2** | 전투 actor 기반 (데미지 파이프라인) | CombatActor/Team, Health(회색체력 3분기)/Stamina/Posture, DamageInfo/DamageResult/DamageType, IDamageable/IDamageResolver, Hitbox(1타1판정)/Hurtbox, AttackData SO | 더미 허수아비가 데미지 받고 회색체력 적립하고 죽음 | DamageInfo가 Hitbox→Hurtbox→Resolver→Health 한 방향으로 흐름, 1타 1판정(HashSet) 중복판정 0, 동팀/자가 피해 무시(Team), 회색체력 3분기(실데미지+회색소멸/일반가드 칩 적립/반격 35% 회복) 동작, HP 0 시 OnDeath→Dead, EditMode 테스트로 파이프라인 검증 |
| **M3** | 플레이어 공격 | PlayerCombat, Attack 상태, WeaponData SO(대검), 콤보 AttackData 체인, 입력 버퍼, Animation Event 판정 | 약/강(차지) 콤보로 허수아비를 콤보 끊김 없이 처치 | 약공 2~3타 콤보가 입력 버퍼(10f 큐잉)+콤보윈도우(60~90%)로 끊김 없이 이어짐, AE_HitboxOn/Off로 active 구간만 판정, 차지 강공(75f 홀드)→데미지70/스태거+38, 스태미나 부족 시 공격 불가, Attack 상태 AE 누락 시 타이머 폴백으로 탈출(데드락 0) |
| **M4** | 플레이어 방어 ★리스크1 | GuardSystem(PG 윈도우/가드게이지), PlayerDamageResolver(가드/PG/회피/일반 4분기), Guard/PerfectGuard/Hit/Staggered 상태 | 더미 공격을 일반가드·퍼펙트가드로 받아내고 가드브레이크·회색체력 회복 | PG 윈도우 8f(0.133s, 시간 기반) 내 가드 시 데미지0+적 스태거+18+적 미세경직6f, 윈도우 놓치면 일반가드 폴백(칩18%+게이지-poise), 가드게이지 0 시 GuardBreak→Staggered 30f, 가드불가(Unblockable/Grab)는 가드/PG 관통, PG 후딜 4f(회피 9f보다 짧음), PlayMode 테스트로 PG/일반가드/가드브레이크 경계 검증 |
| **M5** | 보스 코어 ★리스크2 | BossBrain FSM(Idle/Approach/Attack/Recover/Staggered), BossLocomotion(NavMesh), BossDamageResolver, BossAnimationDriver, AttackPattern SO, 1패턴(#1 횡베기), Posture 그로기→CriticalAttackSystem | 보스가 1패턴으로 공격, 플레이어가 PG로 스태거 쌓아 그로기→치명타로 잡음 | 보스 FSM 5상태 전이 동작(모든 비종단 상태 타이머 폴백 탈출), #1 횡베기 텔레그래프(22f)→판정(8f)→펀시(34f) 리듬 성립, PG로 스태거 만탱(100)→그로기 4s→2.5m 내 치명타 입력→고정150+무적1.5s, 슈퍼아머 규칙(평타엔 안 흔들리고 스태거 만탱에만 무너짐), 풀 공방 루프 1회 완주 |
| **M6** | 보스 완성 | 8패턴 전체, BossPhaseSelector(거리·페이즈·쿨다운·직전패턴 가중랜덤), BossPhaseManager(전환 게이트), BossPhaseData SO 2종, 딜레이드(#2)·가드불가(#6)·화염(#7)·4연타(#8), 2페이즈 텔레그래프 -15% | 8패턴 2페이즈 풀 보스전 1라운드 완주 가능 | 8패턴 거리/페이즈/쿨다운 조건으로 선택(직전패턴 제외, 후보 없으면 Approach 복귀), #2 내려찍기 딜레이10f(늦게 PG), #6 포박 가드불가(적색, 회피만)·1P 60% 예고 1회, #7 화염 PG시 DoT 무효, 2페이즈 전환 무적1.5s + 텔레그래프 -15%(active 프레임 8f 불변), 전환 게이트(그로기/치명타 연출 우선 완료 후 전환), 한 판당 그로기 4~6회 도달 가능 |
| **M7** | 전투 손맛 | HitStop, CameraShake(Transform 노이즈 폴백), VfxSfxHooks, PlayerHUD(HP+회색/스태미나/가드게이지), BossHealthBar(HP+스태거바), InputPrompt(치명타) | 타격감·피드백 완비된 풀 전투(히트스톱/셰이크/VFX/SFX/HUD) | DamageResult.outcome별 히트스톱(PG 성공 시 강한 히트스톱)·카메라셰이크·VFX(PG 섬광/가드불가 적색/그로기 점멸/치명타 연출)·SFX, HUD 3바가 이벤트 구독으로 실시간 갱신(회색 영역 별도 색), 보스 스태거바 그로기 임박 표시, 치명타 프롬프트(그로기+2.5m) 표시, Feel/UI는 Combat 이벤트 구독만(역참조 0) |
| **M8** | 폴리시 + 튜닝 + 캡처 | 0순위 튜닝값 조정(PG 윈도우/스태거 감소율), 밸런스 패스, 버그 정리, 데모 영상/스크린샷, (선택)Cinemachine 도입 | 포트폴리오용 데모 빌드 | 목표 지표 충족(한 판당 그로기 4~6회·치명타 4~6회·클리어 3~6분·숙련 PG 성공률 60%+), QA 회귀 버그 0(데드락/중복판정/i-frame 누수), 데모 영상(1페이즈→전환→2페이즈→격파) + 스크린샷 세트 캡처, 0순위 튜닝값 2개 플레이테스트 반영 |

---

## 2. 마일스톤 의존 그래프 (위상 정렬)

```
M0 스캐폴딩 (asmdef/레이어/Input/테스트씬)
  │
  ├─▶ M1 플레이어 이동 (Input→FSM→Locomotion/LockOn, 회피 i-frame)
  │
  └─▶ M2 전투 actor 기반 ★선행 루트 (Health/Stamina/Posture + DamageInfo/Resolver + Hitbox/Hurtbox)
        │  └ SO: AttackData 준비
        │
        ├─▶ M3 플레이어 공격 (PlayerCombat/콤보/입력버퍼/AE) ── M1·M2 의존
        │        └ SO: WeaponData 준비
        │
        ├─▶ M4 플레이어 방어 ★리스크1 (GuardSystem/PG/PlayerDamageResolver) ── M1·M2 의존
        │
        └─▶ M5 보스 코어 ★리스크2 (BossBrain/1패턴/그로기→치명타) ── M2·(M4 PG로 검증) 의존
              │  └ SO: AttackPattern 준비
              │
              └─▶ M6 보스 완성 (8패턴/2페이즈/전환게이트) ── M5 의존
                    │  └ SO: BossPhaseData 준비
                    │
                    └─▶ M7 전투 손맛 (Feel/UI 이벤트 구독자) ── M2~M6 이벤트 의존
                          │
                          └─▶ M8 폴리시 + 튜닝 + 캡처
```

> **순서 근거:** M2(데미지 파이프라인)가 M3·M4·M5의 공통 선행. M4(PG)를 M5(보스) 직전에 두어 "PG로 스태거 쌓아 무너뜨린다"는 핵심 루프를 보스가 등장하자마자 검증. M7(손맛)은 모든 Combat 이벤트의 구독자라 최후행. M3와 M4는 M2 위에서 병렬 가능하나, **리스크 우선 원칙상 M4(PG 손맛)를 먼저 검증 권장**(M3 공격은 상대적으로 저위험).

## 3. 수직 슬라이스 캡처 포인트 (포트폴리오)

| 시점 | 보여줄 것 |
|------|-----------|
| M1 끝 | 캡슐 이동·달리기·회피·락온 스트레이프 |
| M2 끝 | 허수아비 타격·회색체력·사망 |
| M3 끝 | 약/강 콤보로 허수아비 처치 |
| M4 끝 | **퍼펙트가드 섬광·가드브레이크(핵심 정체성 첫 시연)** |
| M5 끝 | **보스 1패턴 공방 → 그로기 → 치명타(핵심 루프 풀 시연)** |
| M6 끝 | 8패턴 2페이즈 풀 보스전 |
| M7 끝 | 타격감·HUD 완비 전투 |
| M8 끝 | 데모 영상 최종본 |
