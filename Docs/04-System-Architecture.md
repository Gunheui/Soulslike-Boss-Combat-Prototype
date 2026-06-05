# 시스템 설계 — 컴포넌트/클래스 분해 + 어셈블리 의존도

> 대상: engineer (이 문서 + 동급 3문서만 보고 코딩 가능해야 함)
> 패키지 전제: Input System, AI Navigation(NavMesh), Timeline, URP, Test Framework **설치됨**.
> **Cinemachine 미설치** — 락온 카메라 지향/카메라 셰이크는 "추가 필요" 표기.
> Unity 6 (6000.x) / @60fps 기준. 보스 1체 MVP → 경량 FSM + SO. 거대 BT/ECS/이벤트버스 금지.

**변경 메모:**
- 2026-06-05 최초 작성 (v1). designer v1 기획 대응.

---

## 0. 설계 4대 원칙 (이 프로젝트 적용 요약)

1. **데이터/로직 분리(SO 주도):** 모든 손맛 수치(feel-params 전 항목)는 `AttackData/WeaponData/BossPhaseData/AttackPattern` SO 또는 컴포넌트 `[SerializeField]`로 노출 → 재컴파일 없이 튜닝(난점 #6 해소). 상세는 `06-Data-Model.md`.
2. **경계면 = 계약:** 모든 피해는 `DamageInfo` 단일 struct 한 방향으로만 흐른다. Hitbox → Hurtbox → 상태분기 → Health/Posture. 상세는 `07-Damage-Pipeline.md`.
3. **상태 머신 명시(데드락 차단):** 플레이어 9상태 + 보스 6상태. 모든 비종단 상태에 시간/이벤트 탈출 전이 보장. 상세는 `05-State-Machines.md`.
4. **애니메이션 구동 판정:** 히트박스 on/off, i-frame은 Animation Event. PG 윈도우만 입력 기반 타이머(애니 아님). 훅 목록은 `07-Damage-Pipeline.md`.

---

## 1. 어셈블리(asmdef) 구조 + 의존 방향

```
Assets/_Project/Scripts/
├── Combat/   → asmdef: Project.Combat   (의존: 없음 — 루트)
├── Player/   → asmdef: Project.Player   (의존: Project.Combat)
├── Boss/     → asmdef: Project.Boss     (의존: Project.Combat, AI Navigation)
├── Feel/     → asmdef: Project.Feel     (의존: Project.Combat)
├── UI/       → asmdef: Project.UI       (의존: Project.Combat)
└── Tests/    → asmdef: Project.Tests    (의존: 전부 + UnityEngine.TestRunner, 테스트 전용)
```

**의존 방향 규칙 (강제):** `Player`, `Boss`, `Feel`, `UI` → `Combat` (단방향). **역참조 금지.** Combat은 다른 어셈블리를 모른다.

```
        ┌──────────┐
        │  Player  │──┐
        └──────────┘  │
        ┌──────────┐  │     ┌──────────┐
        │   Boss   │──┼────▶│  Combat  │ (루트, 의존 없음)
        └──────────┘  │     └──────────┘
        ┌──────────┐  │
        │   Feel   │──┤
        └──────────┘  │
        ┌──────────┐  │
        │    UI    │──┘
        └──────────┘
```

### 어셈블리 간 통신 — Combat이 역참조 없이 상위에 알리는 법
Combat은 Player/Feel/UI를 컴파일 의존할 수 없으므로, **Combat이 정의한 인터페이스/C# 이벤트(Action)** 를 상위가 구독한다(의존성 역전).

- `Health`가 `public event Action<float> OnHealthChanged;`, `OnDeath`, `OnRecoverableChanged` 노출 → UI/Feel이 구독.
- `Posture`가 `OnStaggerChanged`, `OnGroggyEnter`, `OnGroggyExit` 노출 → UI(보스 스태거바)/Feel(그로기 VFX) 구독.
- `Hurtbox`가 `OnDamageResolved(DamageResult)` 노출 → Feel(히트스톱/셰이크), UI(피격 플래시) 구독.
- 즉 **Combat은 "무슨 일이 일어났다"만 방송**하고, Player/Feel/UI가 "어떻게 보여줄지"를 구현. 컴파일 의존은 항상 상위→Combat.

> Player→Boss 직접 참조 금지. 락온이 보스를 알아야 하지만 보스를 `CombatActor`(Combat 정의)로만 다룬다 → Player는 Boss 어셈블리를 import하지 않는다.

---

## 2. Combat 어셈블리 (Project.Combat) — 공용 코어

actor(플레이어/보스 공통) 컴포넌트. 진영(Team) 구분으로 자가/아군 피해 방지.

| 컴포넌트 | 책임 | 핵심 멤버 |
|----------|------|-----------|
| `DamageInfo` (struct) | 피해 데이터 패킷(한 방향 계약). `07-Damage-Pipeline.md` §1 | amount, poiseDamage, staggerDamage, type, sourcePos, source, isPerfectGuardable, dotPerSec, dotDuration, knockback |
| `DamageResult` (struct) | 해소 결과(상위 방송용) | outcome(Enum: Blocked/PerfectGuard/Dodged/Hit/GuardBreak/Immune), finalDamage, chipDamage, recoverableAdded |
| `DamageType` (enum) | Normal / Unblockable / Grab | — |
| `Team` (enum) | Player / Enemy | — |
| `IDamageable` (interface) | `DamageResult TakeDamage(DamageInfo info)` | Hurtbox가 호출 대상에 위임 |
| `CombatActor` | actor 루트. 컴포넌트 묶음 + Team. Hitbox가 source 식별·동팀 무시에 사용 | Team team; Health/Stamina/Posture/Animator 참조 캐시 |
| `Health` | HP, **회색(회복가능) 체력**, 사망. `IDamageable` 구현 진입점은 Hurtbox지만 최종 HP 적용은 여기 | maxHealth, currentHealth, recoverableHealth, recoverableDuration(5s); ApplyDamage/AddRecoverable/RecoverFromHit/Die. 난점 #3 핵심 |
| `Stamina` | 스태미나 소모/회복/지연/0처리 | maxStamina(120), regenRate(40), regenDelay(1s); TrySpend(cost), 탈진 플래그 |
| `Posture` | 스태거 게이지 적립/감소/그로기 트리거. **보스만 그로기 사용**(플레이어는 가드브레이크가 별도) | maxStagger(100), decayRate(12), decayDelay(1.5s), groggyDuration(4s); AddStagger, OnGroggyEnter/Exit |
| `Hurtbox` | 피격 콜라이더(Trigger). DamageInfo 수신 → **소유 actor의 상태 해소자**에게 위임 → DamageResult 반환·방송. Dodge i-frame 시 비활성 | enabled(=피격 가능), resolver 참조(IDamageResolver), OnDamageResolved 이벤트 |
| `Hitbox` | 공격 콜라이더(Trigger). **1타 1판정**(맞은 대상 HashSet). source actor의 현재 `AttackData`에서 DamageInfo 생성 | activeAttack(AttackData), alreadyHit(HashSet), Enable/Disable(Animation Event로 제어) |
| `IDamageResolver` (interface) | "이 피해를 내 상태로 어떻게 해소하나" — 플레이어/보스가 각자 구현 | `DamageResult Resolve(DamageInfo info)` |

> **`IDamageResolver`가 경계면의 핵심.** Hurtbox는 actor가 가드 중인지/회피 중인지/그로기인지 모른다. 그저 소유 actor의 `IDamageResolver.Resolve(info)`를 호출한다. 플레이어는 `PlayerDamageResolver`(가드/PG/회피/일반 분기), 보스는 `BossDamageResolver`(무적/슈퍼아머/일반 분기)를 구현. → Hurtbox 코드는 actor 종류와 무관, 재사용. 난점 #1·#5는 `PlayerDamageResolver` 안에서 해소.

---

## 3. Player 어셈블리 (Project.Player)

| 컴포넌트 | 책임 | 핵심 멤버 |
|----------|------|-----------|
| `PlayerStateMachine` | 9상태(Idle/Move/Dodge/Attack/Guard/PerfectGuard/Hit/Staggered/Dead). State 패턴(클래스 기반 — 상태 복잡) | currentState, ChangeState(); 각 IPlayerState는 OnEnter/Update/OnExit |
| `PlayerInputReader` | Input System 래퍼 → intent. **입력 버퍼**(공격 10f 큐잉) | MoveIntent, DodgePressed, AttackBuffered, GuardHeld, GuardPressedTime(PG 판정용) |
| `PlayerDamageResolver` | `IDamageResolver` 구현. **가드/PG/회피무적/일반 4분기**. 난점 #1·#5 해소 지점 | Resolve(info): FSM 상태 + GuardSystem 윈도우 + DamageType 읽어 분기 |
| `GuardSystem` | PG 윈도우 판정(시간 기반), 가드게이지, 회색 적립 트리거 | guardGauge, pgWindow(8f), IsInPerfectGuardWindow(now), 가드브레이크 |
| `PlayerLocomotion` | 이동/스트레이프/회전/달리기 스태미나. Move/Dodge 상태가 구동 | walkSpeed, strafeSpeed, runSpeed, rotateSpeed |
| `LockOnSystem` | 타겟 선택/전환, **카메라 지향(Cinemachine 미설치 → 추가 필요)**. MVP는 트랜스폼 LookAt 폴백 | currentTarget(CombatActor), lockRange(12m), SwitchTarget() |
| `PlayerCombat` | 공격 실행: AttackData 선택(콤보 체인), Animator 트리거, Hitbox에 activeAttack 주입 | currentWeapon(WeaponData), comboIndex, charge |
| `CriticalAttackSystem` | 그로기 보스 2.5m 내 치명타 입력 → 고정 데미지 + 1.5s 무적 연출 | critRange(2.5m), critDamage(150), invulnDuration(1.5s) |

> **회색 체력 회복 트리거(난점 #3):** `Hitbox`가 적에게 명중(`DamageResult.outcome == Hit/Blocked`)하면 `CombatActor` 경유로 자기 `Health.RecoverFromHit()` 호출 → 회색의 35% 실HP 전환. 즉 "반격 명중 = 회복"은 Hitbox 명중 콜백에서 발생. Combat 내부에서 완결(Player 어셈블리 불필요).

---

## 4. Boss 어셈블리 (Project.Boss)

**경량 FSM + AttackPattern SO.** 거대 BT 금지(보스 1체 = 과설계).

| 컴포넌트 | 책임 | 핵심 멤버 |
|----------|------|-----------|
| `BossBrain` | 6상태 FSM(Idle/Approach/Attack/Recover/Staggered/PhaseTransition). enum + switch(경량). 상태별 OnEnter/Tick/OnExit | currentState, distanceToPlayer, attackCooldown |
| `BossPhaseManager` | HP 임계값 페이즈 전환, **무적 연출 1.5s**, 패턴셋 교체, 스태거 0 리셋. **전환 우선순위 게이트**(난점 #2) | phases(BossPhaseData[]), currentPhase, RequestPhaseTransition() — 그로기/치명타 중이면 보류 |
| `BossPatternSelector` | Attack 진입 시 거리·페이즈·쿨다운·직전패턴으로 AttackPattern 1개 가중랜덤 선택 | SelectPattern(dist, phase, lastPattern) |
| `BossDamageResolver` | `IDamageResolver` 구현. 무적(페이즈전환)/슈퍼아머/일반 분기 | Resolve(info): PhaseManager.IsInvulnerable, 슈퍼아머 플래그 읽음 |
| `BossLocomotion` | NavMesh 접근/스텝/후퇴. AI Navigation 패키지 사용 | NavMeshAgent, Approach(), Retreat(2m) |
| `BossAnimationDriver` | AttackPattern → Animator 트리거. **2페이즈 텔레그래프 -15%**(난점 #4) | PlayAttack(AttackData), SetTelegraphScale(0.85f) |

> **#4 텔레그래프 -15% 권고(상세는 data-model §2페이즈):** 애니 `speed` 전역 스케일은 판정 프레임까지 빨라져 PG 윈도우 절대값(8f)을 깨뜨릴 위험. **권장: 선딜(텔레그래프) 구간만 별도 클립 또는 Animator state speed로 압축, 히트박스 활성(AE_HitboxOn) 시점부터는 speed=1 복귀.** `BossAnimationDriver`가 AttackData의 `telegraphSpeedMult`(페이즈별)를 텔레그래프 구간 한정 적용. 클립 분리가 가장 안전(권고), speed 스케일은 차선.

---

## 5. Feel 어셈블리 (Project.Feel) — 손맛

| 컴포넌트 | 책임 | 비고 |
|----------|------|------|
| `HitStop` | 명중 시 `Time.timeScale` 순간 정지(또는 actor 애니 정지). DamageResult 구독 | 권장: unscaled 복귀. PG 성공 시 강한 히트스톱 |
| `CameraShake` | **Cinemachine 미설치 → 추가 필요.** MVP는 카메라 Transform 노이즈 폴백 구현 | Cinemachine Impulse가 정석. 폴백 명시 |
| `VfxSfxHooks` | DamageResult.outcome별 VFX/SFX. PG 섬광, 가드불가 적색 점멸, 그로기 점멸, 치명타 연출 | Posture.OnGroggyEnter, Hurtbox.OnDamageResolved 구독 |

> Feel은 전적으로 Combat의 이벤트 구독자. Combat은 Feel을 모른다(역참조 금지).

---

## 6. UI 어셈블리 (Project.UI)

| 컴포넌트 | 책임 | 구독 소스 |
|----------|------|-----------|
| `PlayerHUD` | HP바(+회색 영역 별도 색), 스태미나바, 가드게이지바 | Health.OnHealthChanged/OnRecoverableChanged, Stamina, GuardSystem |
| `BossHealthBar` | 보스 HP바 + **스태거 게이지바**(그로기 임박 표시) | Boss Health, Posture.OnStaggerChanged/OnGroggyEnter |
| `InputPrompt` | 치명타 입력 프롬프트(그로기·2.5m 내) | CriticalAttackSystem(또는 Posture.OnGroggyEnter + 거리) |

---

## 7. 씬/프리팹 구성 (engineer 셋업 가이드)

```
Player (프리팹)
├── CombatActor (Team=Player)
├── Health / Stamina / Posture(플레이어용, 그로기 미사용)
├── PlayerStateMachine / PlayerInputReader / PlayerDamageResolver
├── GuardSystem / PlayerLocomotion / PlayerCombat / CriticalAttackSystem
├── LockOnSystem
├── Animator (player 컨트롤러)
├── Hurtbox (자식, Trigger Collider)  ← Dodge i-frame 시 enabled=false
└── WeaponRoot/Hitbox (자식, Trigger Collider, 평소 disabled)

Boss (프리팹)
├── CombatActor (Team=Enemy)
├── Health / Posture(그로기 사용) / (Stamina 불필요)
├── BossBrain / BossPhaseManager / BossPatternSelector
├── BossDamageResolver / BossLocomotion / BossAnimationDriver
├── NavMeshAgent
├── Animator (boss 컨트롤러, 1P/2P state)
├── Hurtbox (자식, Trigger Collider)  ← PhaseTransition 무적 시 enabled=false 또는 Resolver Immune
└── WeaponRoot/Hitbox (자식, Trigger Collider, 평소 disabled)

Systems (씬 오브젝트)
├── Feel (HitStop / CameraShake / VfxSfxHooks)
└── UI Canvas (PlayerHUD / BossHealthBar / InputPrompt)
```

물리: Hitbox/Hurtbox는 **Trigger**. Layer 분리(PlayerHitbox/PlayerHurtbox/BossHitbox/BossHurtbox) + Collision Matrix로 동팀 충돌 차단 권장(1차 방어). source==self·동팀은 코드(Team)로 2차 차단.

---

## 8. 의존성/통신 요약 한 장

```
입력 ─▶ PlayerInputReader ─▶ PlayerStateMachine ─▶ (Locomotion/Combat/Guard)
                                                        │
공격 모션 ─Animation Event(AE_HitboxOn)─▶ Hitbox.Enable
Hitbox.OnTriggerEnter ─▶ DamageInfo 생성 ─▶ 대상 Hurtbox.TakeDamage
        Hurtbox ─▶ 소유 actor IDamageResolver.Resolve(info)
                ├─ PlayerDamageResolver: 가드/PG/회피/일반 분기
                └─ BossDamageResolver: 무적/슈퍼아머/일반 분기
        ─▶ DamageResult ─▶ Health/Posture 갱신 ─▶ OnXxxChanged 이벤트(방송)
                                                   ├─▶ UI (바 갱신)
                                                   └─▶ Feel (히트스톱/셰이크/VFX)
```

상위(Player/Boss/Feel/UI)는 항상 Combat을 향해 컴파일 의존, Combat은 이벤트로만 위로 방송 → 단방향 유지.
