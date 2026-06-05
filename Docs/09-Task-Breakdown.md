# 마일스톤별 태스크 분해 — M0~M8

> 규모: **S=반나절 / M=1일 / L=2일** (2일 초과는 분할). 담당 시스템: Combat / Player / Boss / Feel / UI.
> **DoD는 검증 가능한 문장** — QA가 이 문장으로 검증한다.
> **★리스크** = 손맛/리듬 불확실성 큼(앞쪽 배치). **[스파이크]** = 조사/실험 태스크(추측 금지).
> 의존 표기: `Tn.m` = 마일스톤 n의 태스크 m. 전 마일스톤 전체 의존은 `M(n)` 표기.

**변경 메모:**
- 2026-06-05 최초 작성 (v1).

---

## 0순위 리스크 태스크 (일정 앞쪽 강제 배치)

| 우선 | 태스크 | 위치 | 이유 |
|------|--------|------|------|
| **R1** | T4.2 퍼펙트가드 윈도우 8f 시간 판정 | M4 | 정체성의 핵심. 8f가 너무 좁으면 좌절·넓으면 정체성 붕괴. 손맛 0순위 튜닝값 |
| **R2** | T5.4 보스 #1 공방 리듬(텔레그래프↔PG↔펀시) | M5 | "버티며 받아쳐 무너뜨린다" 루프가 안 나오면 프로젝트 가치 없음 |
| **R3** | T4.0 [스파이크] PG/i-frame/입력버퍼 판정 순서 검증 | M4 | i-frame이 모든 판정을 덮으면 회피 남용→정체성 훼손. 겹침 우선순위 실측 필요 |

> R1·R3는 M4 착수 즉시, R2는 M5 착수 즉시 진행. 셋 다 [스파이크]/PlayMode 테스트로 일찍 실패·일찍 수정.

---

## M0 — 프로젝트 스캐폴딩

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T0.1 폴더 구조 + asmdef 5종(Combat/Player/Boss/Feel/UI)+Tests | — | S | `Player/Boss/Feel/UI→Combat` 단방향 의존, Combat은 상위 미참조, 전 asmdef 컴파일 통과 | Combat |
| T0.2 Layer 4종(Player/BossHitbox·Hurtbox) + Collision Matrix | T0.1 | S | PlayerHitbox↔BossHurtbox만 충돌, 동팀 레이어 충돌 Matrix에서 차단됨 | Combat |
| T0.3 Input Action Asset(Move/Dodge/Attack/Guard/LockOn/Critical) | T0.1 | S | 각 액션 바인딩 후 디버그 로그로 입력 감지 확인 | Player |
| T0.4 테스트 씬 + 캡슐 placeholder + NavMesh 베이크 | T0.1 | S | 씬 로드 시 플레이어/보스 placeholder 배치, NavMesh 영역 베이크됨 | Combat |
| T0.5 Test Framework 셋업 + EditMode 더미 테스트 1개 | T0.1 | S | `Project.Tests` asmdef 구성, EditMode 테스트 1개 그린 | Combat |

---

## M1 — 플레이어 이동

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T1.1 PlayerInputReader (Input System 래퍼→intent) | T0.3 | M | MoveIntent/DodgePressed/GuardHeld/AttackBuffered/GuardPressedTime 노출, 입력→intent 매핑 로그 확인 | Player |
| T1.2 PlayerStateMachine 골격(IPlayerState OnEnter/Update/OnExit, ChangeState OnExit→OnEnter 보장) | T0.1 | M | Idle↔Move 전이 동작, ChangeState가 OnExit 후 OnEnter 호출 순서 보장 | Player |
| T1.3 PlayerLocomotion(보행2.5/달리기5.0/스트레이프2.0/회전720°/s) | T1.2 | M | 8방향 이동·달리기 스태미나(-8/s, M2 Stamina 선행 시) 동작, 락온 시 스트레이프 | Player |
| T1.4 Dodge 상태 + i-frame(AE_IFrameOn 3f~Off 14f, Hurtbox.enabled 토글) | T1.2, T1.3 | M | 회피 입력 시 i-frame 11f 동안 더미 공격이 통과(피격 0), Dodge.OnExit에서 Hurtbox 강제 복구로 i-frame 누수 0, 후딜 9f 동안 입력 제한 | Player |
| T1.5 LockOnSystem(타겟 선택/전환, Transform LookAt 폴백) | T1.2 | M | 락온 시 보스(CombatActor) 지향, lockRange 12m 밖 타겟 해제, SwitchTarget 동작. Cinemachine 미설치→Transform LookAt | Player |

> Boss 어셈블리 import 금지 — LockOn은 보스를 CombatActor로만 다룬다.

---

## M2 — 전투 actor 기반 (데미지 파이프라인) ★선행 루트

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T2.1 DamageInfo/DamageResult struct + DamageType/Team/DamageOutcome enum | T0.1 | S | DamageInfo 13필드·DamageResult 5필드 정의, 컴파일 통과 | Combat |
| T2.2 CombatActor(Team 묶음) + IDamageable/IDamageResolver 인터페이스 | T2.1 | S | CombatActor가 Health/Stamina/Posture/Animator 참조 캐시, 인터페이스 계약 정의 | Combat |
| T2.3 Health — 회색체력 3분기(실데미지+회색소멸/칩 적립/반격 35% 회복) | T2.2 | L | 일반가드 칩→recoverableHealth 적립(5s 만료), 반격 명중→35% 회복, 풀피격(Hit)→회색 0 소멸, HP 0→OnDeath. EditMode 테스트로 3분기 검증 | Combat |
| T2.4 Stamina(max120/regen40·delay1.0s/0처리) | T2.2 | S | 소모 후 1.0s 뒤 40/s 회복, 0 시 회피·공격 불가·가드는 탈진 가능(엣지 검증) | Combat |
| T2.5 Posture(스태거 적립/감소12·delay1.5s/그로기4s, usesGroggy 플래그) | T2.2 | M | 만탱100→OnGroggyEnter, 마지막 적립1.5s 후 12/s 감소, 플레이어 usesGroggy=false·보스 true, OnStaggerChanged 방송 | Combat |
| T2.6 Hurtbox(Trigger, TakeDamage→resolver 위임, OnDamageResolved 방송) | T2.2 | M | OnTriggerEnter→소유 actor IDamageResolver.Resolve 호출, DamageResult 방송, i-frame 시 enabled=false로 미수신 | Combat |
| T2.7 Hitbox(Trigger, 1타1판정 HashSet, AttackData→DamageInfo 생성, AE 제어) | T2.2 | M | AE_HitboxOn에서 alreadyHit.Clear()·Enable, 동팀/자가/중복 무시, 한 스윙 같은 대상 중복판정 0 | Combat |
| T2.8 AttackData SO 스키마 + 더미 공격 에셋 + 허수아비 테스트 리그 | T2.1 | S | AttackData(damage/poise/stagger/type 등) 생성, 더미 Hitbox로 허수아비 타격→데미지 적용 시연 | Combat |
| T2.9 [스파이크] 더미↔허수아비 풀 파이프라인 PlayMode 검증 | T2.3–T2.8 | S | Hitbox→Hurtbox→Resolver→Health 한 방향 흐름·1타1판정·동팀 무시·회색 적립이 PlayMode에서 모두 그린 | Combat |

---

## M3 — 플레이어 공격

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T3.1 WeaponData SO(대검 lightCombo[]/heavyAttack/chargeTime) + 공격 AttackData 에셋 | T2.8 | S | 약공 2~3타 체인(nextCombo)·강공·차지 데이터 매핑(데미지18/40/70, 스태거+8/+20/+38) | Player |
| T3.2 Attack 상태 + PlayerCombat(콤보인덱스, AttackData 주입, Animator 트리거) | T1.2, T2.7, T3.1 | M | 약공 1타 발동→AE_HitboxOn/Off로 active 구간만 판정, 모션 종료 AE_AttackEnd→Idle | Player |
| T3.3 입력 버퍼(모션 종료 전 10f 큐잉) + 콤보 윈도우(60~90%) | T3.2 | M | **약공격 3타 콤보가 입력 버퍼로 끊김 없이 이어짐**, 콤보윈도우 밖 입력은 무시, AE_AttackEnd에서 미소비 버퍼 클리어 | Player |
| T3.4 차지 강공(홀드 45f→75f, 데미지40→70, 스태거+20→+38) | T3.2 | M | 입력 홀드로 차지 진입, 차지 완성 시 데미지70/스태거+38 적용, 미완성 릴리즈는 일반 강공 | Player |
| T3.5 Attack 상태 타이머 폴백(AE 누락 시 모션길이+여유로 Idle 탈출) | T3.2 | S | AE_AttackEnd 누락 상황에서도 타이머로 Idle 복귀(영구 경직 0 — 데드락 검증) | Player |

---

## M4 — 플레이어 방어 ★리스크1

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T4.0 [스파이크] ★R3 PG/i-frame/입력버퍼 판정 순서 실측 | M2, T1.4 | M | i-frame(콜라이더 비활성)>Unblockable/Grab>PG>일반가드>가드브레이크>무방비 순서가 실제 피격에서 성립 확인, 딜레이드 어택은 i-frame(14f) 이후 판정되도록 보스 AE 타이밍 가설 검증 | Player |
| T4.1 GuardSystem 골격 + 일반가드 칩(18%)·가드게이지 소모(poise×1.0) | M2 | M | Guard 상태 피격 시 칩18%만 받고 회색 적립, 가드게이지 -poise, GuardPressedTime 기록 | Player |
| T4.2 ★R1 퍼펙트가드 윈도우 8f 시간 판정(IsInPerfectGuardWindow) | T4.1 | M | 가드 입력 후 0.133s(8f) 내 피격 시 데미지0+적 스태거+18+적 미세경직6f, PG 후딜 4f, 윈도우는 시각 델타(프레임 드랍 견고)·SO로 6~10f 튜닝 가능 | Player |
| T4.3 PlayerDamageResolver — 4분기(Unblockable/Grab > PG > 일반가드 > 가드브레이크 > 무방비) | T4.1, T4.2 | M | Resolve가 info.type/FSM/윈도우 읽어 6 outcome 분기, 가드불가는 가드/PG 관통(①②), PG 실패→일반가드 폴백, PG 시 DoT 무효 | Player |
| T4.4 가드게이지 회복(비가드2.0s 후 20/s) + 가드브레이크(게이지0→Staggered 30f) | T4.1 | S | 게이지 0 시 GuardBreak→Staggered 30f 강제 피격, 비가드 2.0s 후 20/s 회복 | Player |
| T4.5 Hit/Staggered 상태 + 타이머 폴백(Hit 리액션/Staggered 30f·90f) | T4.3 | M | 피격해소=Hit→Hit 상태(회색 적립·입력잠금)→AE_HitReactEnd/타이머로 Idle, Staggered는 OnEnter 타이머 강제(영구 경직 0) | Player |
| T4.6 ★R1 PG/일반가드/가드브레이크 경계 PlayMode 테스트 | T4.2–T4.5 | M | PG 성립/실패→일반가드 폴백/가드브레이크 3경계가 윈도우 경계 프레임에서 정확히 분기, QA 타이밍 검증 그린 | Player |

---

## M5 — 보스 코어 ★리스크2

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T5.1 BossBrain FSM 골격(Idle/Approach/Attack/Recover/Staggered, enum+switch) | M2 | M | 5상태 OnEnter/Tick/OnExit, 거리·쿨다운 조건 전이, 모든 비종단 상태 OnEnter 타이머 폴백(데드락 0) | Boss |
| T5.2 BossLocomotion(NavMesh Approach/Retreat) + BossDamageResolver(무적/슈퍼아머/일반) | T5.1 | M | NavMeshAgent로 사거리 진입, BossDamageResolver가 PhaseManager.IsInvulnerable·슈퍼아머 읽어 분기, 슈퍼아머 중 평타 피격엔 경직 없이 데미지·스태거만 적용 | Boss |
| T5.3 AttackPattern SO + #1 횡베기 패턴 + BossAnimationDriver(PlayAttack/AE 스케줄) | T5.1, T2.8 | M | #1 횡베기 AttackPattern 에셋(minRange/recoverTime/weight), Attack OnEnter→Animator 트리거→AE_HitboxOn/Off, AE_PatternEnd→Recover | Boss |
| T5.4 ★R2 보스 #1 공방 리듬(텔레그래프22f→판정8f→펀시34f) 튜닝 | T5.3, M4 | M | 텔레그래프에 반응해 PG→펀시 윈도우에 약공 2타 반격이 "버티며 받아친다" 리듬으로 성립, 플레이어 PG 성공 시 보스 미세경직6f로 콤보 끊김 체감 | Boss |
| T5.5 Posture 그로기→Staggered 인터럽트 + CriticalAttackSystem(치명타) | T5.1, T2.5 | M | 스태거 만탱100→OnGroggyEnter→BossBrain이 현재 상태 인터럽트하고 Staggered 전이, 그로기4s 내 2.5m 치명타 입력→고정150+플레이어 무적1.5s, 미사용 시 4s 후 스태거 0 리셋 | Boss |
| T5.6 ★R2 풀 공방 루프 PlayMode 검증(PG→스태거→그로기→치명타) | T5.4, T5.5 | M | PG로 스태거 쌓아 그로기 도달→치명타로 큰 데미지 박는 한 루프가 끊김 없이 완주, "공격적 방어" 동사가 회피보다 이득(스태거+18 vs 0) 확인 | Boss |

---

## M6 — 보스 완성

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T6.1 패턴 #3·#4·#5(방패밀치기/돌진베기/3연콤보) AttackPattern + AE | T5.3 | M | 3패턴 텔레그래프/판정/펀시 동작, #3 포이즈24로 일반가드 시 가드게이지 큰 압박(PG 강제), #5 연속 PG×3 성립 | Boss |
| T6.2 ★ 패턴 #2 내려찍기 딜레이드(28f+딜레이10f, 늦게 PG, hitstun12f) | T5.3 | M | 텔레그래프 후 검 10f 정지→성급한 PG/회피는 윈도우 끝나 처벌·늦게 막아야 PG 성립, 명중 시 12f 경직, AE_HitboxOn을 i-frame(14f) 이후 배치로 회피 남용 차단 | Boss |
| T6.3 ★ 패턴 #6 포박 잡기(Grab, 가드불가, 적색, 회피만, 다운90f) | T5.3, T4.3 | M | 적색 점멸+전용 SFX, 가드/PG 관통(50데미지+다운90f), 회피만 정답, 빗나가면 펀시36f, 1P HP 60% 예고 1회(oneShotPreview) | Boss |
| T6.4 BossPatternSelector(거리·페이즈·쿨다운·직전패턴 가중랜덤) | T6.1, T6.2, T6.3 | M | 거리/페이즈/쿨다운 조건 후보 중 직전패턴 제외 가중랜덤 선택, 근/중거리별 패턴 분기, 후보 없으면 Approach 복귀 | Boss |
| T6.5 BossPhaseManager + BossPhaseData SO 2종 + 전환 무적1.5s | T6.4, T2.3 | M | HP 50%(600) 도달→무적1.5s+후퇴2m+포효+패턴셋 교체+스태거 0 리셋, 무적 중 플레이어 공격 데미지0·스태거 미적립 | Boss |
| T6.6 ★ 전환 게이트(그로기/치명타 연출 우선 완료 후 전환) | T6.5, T5.5 | M | pendingPhaseTransition 예약→그로기/치명타 연출/판정 중 Hitbox 중이면 보류, 안전 시점에만 PhaseTransition 진입, 치명타로 HP 50% 내려가도 연출 완료 후 전환(치명타 보상 정상 지급) | Boss |
| T6.7 2페이즈 텔레그래프 -15%(active 프레임 8f 불변, 클립/state speed 분리) | T6.5 | M | 2P 텔레그래프 구간만 speed 가속(AttackData.phase2TelegraphMult 0.85)·AE_HitboxOn에서 speed=1 복귀, active 판정 8f 절대값 보존(PG 윈도우 안 깨짐) | Boss |
| T6.8 패턴 #7 화염사선(PG시 DoT무효) + #8 광분4연타(연속 PG×4) | T6.4, T6.7 | M | #7 PG 성공 시 화상 DoT(3/s×3s) 무효·일반가드는 칩+화상, #8 4연타 풀 PG 시 스태거+72(그로기 근접), 2P 등장 | Boss |
| T6.9 [스파이크] 풀 보스전 1라운드 완주 + 데드락 교차 검증 | T6.6, T6.8 | M | 1P(그로기2~3회→치명타)→전환→2P(그로기2~3회→치명타)→격파 완주, state-machines §4.3 교차 데드락 시나리오 5개 모두 무교착 | Boss |

---

## M7 — 전투 손맛

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T7.1 HitStop(DamageResult 구독, PG 성공 시 강한 히트스톱, unscaled 복귀) | M(6) | M | outcome별 히트스톱 길이 차등(PG>일반 명중), Time.timeScale 정지 후 unscaled로 정확히 복귀(타이머 누수 0) | Feel |
| T7.2 CameraShake(Transform 노이즈 폴백) | M(6) | S | 명중·PG·치명타 시 카메라 셰이크, Cinemachine 미설치→Transform 노이즈 폴백 동작 | Feel |
| T7.3 VfxSfxHooks(PG 섬광/가드불가 적색/그로기 점멸/치명타 연출 + SFX) | M(6) | M | DamageResult.outcome·Posture.OnGroggyEnter 구독해 VFX/SFX 재생, PG 섬광·가드불가 적색 점멸·그로기 흰점멸·치명타 연출 표시 | Feel |
| T7.4 PlayerHUD(HP+회색영역 별도색/스태미나/가드게이지 바) | M(6) | M | Health.OnHealthChanged/OnRecoverableChanged·Stamina·GuardSystem 구독, 회색 영역 별도 색 실시간 갱신 | UI |
| T7.5 BossHealthBar(HP+스태거게이지바, 그로기 임박 표시) | M(6) | S | Boss Health·Posture.OnStaggerChanged/OnGroggyEnter 구독, 스태거 만탱 임박 시각 표시 | UI |
| T7.6 InputPrompt(치명타 입력, 그로기+2.5m 내) | T5.5 | S | 그로기 상태 + 2.5m 내 진입 시 치명타 프롬프트 표시, 윈도우 종료/거리 이탈 시 숨김 | UI |

> Feel/UI는 전적으로 Combat 이벤트 구독자 — Combat 역참조 0 검증 포함.

---

## M8 — 폴리시 + 튜닝 + 캡처

| 태스크 | 의존 | 규모 | DoD (검증 가능) | 담당 |
|--------|------|------|------------------|------|
| T8.1 [스파이크] 0순위 튜닝값 플레이테스트(PG 윈도우 8f / 스태거 감소율 12/s) | M(7) | M | 한 보스전당 그로기 3~5회 목표로 두 값 조정, 그로기 너무 안 나면 감소율 우선 하향, PG 너무 좁으면 윈도우 상향 — 조정 결과 기록 | Boss |
| T8.2 밸런스 패스(보스 maxHP/aggressionCooldown/펀시 윈도우) | T8.1 | M | 클리어 3~6분·숙련 PG 성공률 60%+ 구간으로 수렴, 2P 공세 빈도(0.8s) 체감 검증 | Boss |
| T8.3 회귀 버그 정리(데드락/중복판정/i-frame 누수/스태미나0 엣지) | M(7) | M | QA 회귀 스위트(데드락·중복판정·i-frame 누수·스태미나 0 가드) 전부 그린, 신규 회귀 0 | Combat |
| T8.4 [스파이크] (선택) Cinemachine 도입 — 락온 카메라/Impulse 셰이크 | T7.2 | M | Cinemachine 설치 후 락온 지향·Impulse 셰이크로 폴백 대체(도입 여부는 일정 여유 판단), 미도입 시 폴백 유지 결정 기록 | Feel |
| T8.5 데모 영상 + 스크린샷 캡처(1P→전환→2P→격파) | T8.2, T8.3 | S | 풀 보스전 데모 영상 1본 + 핵심 순간(PG 섬광/그로기/치명타/페이즈전환) 스크린샷 세트 캡처 | — |

---

## 규모 합계 (참고)

| 마일스톤 | S | M | L | 태스크 수 |
|----------|---|---|---|-----------|
| M0 | 5 | 0 | 0 | 5 |
| M1 | 0 | 5 | 0 | 5 |
| M2 | 4 | 4 | 1 | 9 |
| M3 | 2 | 3 | 0 | 5 |
| M4 | 1 | 5 | 0 | 7 (T4.0 포함) |
| M5 | 0 | 6 | 0 | 6 |
| M6 | 0 | 9 | 0 | 9 |
| M7 | 3 | 3 | 0 | 6 |
| M8 | 1 | 4 | 0 | 5 |
| **합** | 16 | 39 | 1 | **57** |

> 규모 환산(S=0.5일/M=1일/L=2일): 약 16×0.5 + 39×1 + 1×2 = **49일** 1인 작업 가설(검증·튜닝·예비 버퍼 별도). 순서는 가설 — 개발 중 막히면 남은 순서를 조정.
