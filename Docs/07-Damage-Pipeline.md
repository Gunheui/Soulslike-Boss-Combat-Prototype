# 데미지 파이프라인 — DamageInfo 계약 + 판정 흐름 + Animation Event 훅

> 원칙: 모든 피해는 **`DamageInfo` 단일 struct 한 방향**으로만 흐른다. Hitbox→Hurtbox→Resolver→Health/Posture.
> 해소 난점: **#1 PG 윈도우 시간 판정·판정 순서**, **#3 회색 체력 3분기**, **#5 unblockable 플래그 분기**.
> @60fps.

**변경 메모:**
- 2026-06-05 최초 작성 (v1).

---

## 1. DamageInfo (struct) — 한 방향 계약, 공격자가 생성

```csharp
public struct DamageInfo {
    public float amount;            // HP 데미지
    public float poiseDamage;       // 경직 누적(= 플레이어 일반가드 게이지 소모량)
    public float staggerDamage;     // 상대 스태거 게이지 적립
    public DamageType type;         // Normal / Unblockable / Grab  ← 난점 #5 분기 키
    public bool isPerfectGuardable; // PG 가능 여부(가드불가는 false)
    public Vector3 sourcePos;       // 방향(넉백·측면판정)
    public GameObject source;       // 출처(자가/동팀 무시)
    public Team sourceTeam;         // 진영
    public float knockback;         // 넉백 거리(m)
    public float hitstunFrames;     // 명중 시 상대 경직(f)
    public float dotPerSec;         // DoT(화상 등, 없으면 0)
    public float dotDuration;       // DoT 지속(s)
}
```

- **생성 위치:** `Hitbox`가 활성 중 OnTriggerEnter 시, 자신의 `activeAttack`(AttackData) + source actor 정보로 생성. **AttackData → DamageInfo 1:1 복사 + 런타임 정보(sourcePos/source/team) 주입.**
- **한 방향:** DamageInfo는 공격자→방어자로만 흐른다. 방어자는 DamageInfo를 수정하지 않고 **`DamageResult`를 반환**(역방향 응답).

```csharp
public struct DamageResult {
    public DamageOutcome outcome;   // PerfectGuard/Blocked/Dodged/Hit/GuardBreak/Immune/Grabbed
    public float finalDamage;       // 실제 적용된 HP 데미지
    public float chipDamage;        // 회색으로 들어간 칩(일반가드)
    public float recoverableAdded;  // 회색 적립량
    public bool staggeredTarget;    // 이 피격으로 상대가 스태거됐나
}
public enum DamageOutcome { PerfectGuard, Blocked, GuardBreak, Dodged, Hit, Grabbed, Immune }
```

---

## 2. 전체 흐름 (Hitbox → Hurtbox → Resolver → Health/Posture)

```
[공격자]
 Animator 공격 모션 ── AE_HitboxOn ──▶ Hitbox.Enable(activeAttack = 현재 AttackData)
        │                                  alreadyHit.Clear()
        ▼
 Hitbox.OnTriggerEnter(other)
   ├─ other가 Hurtbox인가? 아니면 무시
   ├─ alreadyHit.Contains(target)?  → 무시 (1타 1판정)
   ├─ target.Team == source.Team?   → 무시 (동팀/자가)
   ├─ DamageInfo 생성(AttackData + sourcePos/source/team)
   └─ target.Hurtbox.TakeDamage(info)  ──────────┐
        │                                         │
        ▼                                         │
[방어자] Hurtbox.TakeDamage(info)                  │
   └─ resolver.Resolve(info)  → DamageResult       │  (resolver = 소유 actor의 IDamageResolver)
        │                                          │
        ├─ PlayerDamageResolver  (가드/PG/회피/일반)  §3
        └─ BossDamageResolver    (무적/슈퍼아머/일반)  §3
        │
        ▼  DamageResult로 적용
   ├─ Health.ApplyDamage(result)        (실데미지/칩/회색 — 난점 #3, §4)
   ├─ Posture.AddStagger(info.stagger)  (해당 시)
   ├─ FSM.ChangeState(outcome→상태)      (Hit/Staggered/PerfectGuard/GuardBreak)
   └─ Hurtbox.OnDamageResolved(result)  방송 ──▶ Feel(히트스톱/셰이크/VFX), UI(플래시/바)
        │
        ▼  (공격자 측 후처리)
   ├─ alreadyHit.Add(target)            (1타 1판정 확정)
   ├─ 공격자 Posture.AddStagger?         (PG 당했으면 공격자=보스 스태거+18, 미세경직6f)
   └─ 공격자 Health.RecoverFromHit()     (명중 성공 시 회색 회복 — 난점 #3, §4.3)
```

> **핵심 분리:** Hurtbox는 "맞았다"만 안다. "어떻게 막았나/피했나"는 전부 `IDamageResolver` 구현체(플레이어/보스)가 결정. → Hitbox/Hurtbox 코드는 actor 종류와 무관(재사용).

---

## 3. 상태 분기 — Resolver 내부 (난점 #1·#5)

### 3.1 PlayerDamageResolver.Resolve(info) — 판정 순서 (위→아래, 먼저 매칭 시 종료)

```
[전제] 회피 i-frame 중이면 Hurtbox.enabled==false → OnTriggerEnter 자체가 안 일어남
       (Dodge 무적 = 콜라이더 비활성. Resolve 진입 전 컷. → outcome 'Dodged'는 사실상 무판정)

Resolve(info):
 ① info.type == Unblockable           → Hit      (가드·PG 무효, 풀데미지)           ← 난점 #5
 ② info.type == Grab                  → Grabbed  (가드·PG 무효, 다운90f → Staggered) ← 난점 #5
 ③ FSM∈{Guard} AND GuardSystem.IsInPerfectGuardWindow(Time.time)
        AND info.isPerfectGuardable    → PerfectGuard (데미지0, 칩0, 게이지0,
                                          적 스태거+18·미세경직6f, DoT무효)            ← 난점 #1
 ④ FSM==Guard AND guardGauge >= info.poiseDamage
                                       → Blocked   (칩=amount×0.18, 회색적립,
                                          게이지-=poise, 스태미나-=poise)
 ⑤ FSM==Guard AND guardGauge <  info.poiseDamage
                                       → GuardBreak (게이지0, 풀데미지, Staggered 30f)
 ⑥ else (Idle/Move/Attack 무방비, 슈퍼아머 없음)
                                       → Hit       (풀데미지, 회색 미적립, Hit 상태)
```

#### 난점 #5 — unblockable/grab 분기 상세
- 가드 시스템(=`PlayerDamageResolver` + `GuardSystem`)이 **공격의 `info.type`/`isPerfectGuardable`를 가장 먼저 읽어** PG/일반가드를 무효화(①②). designer의 #6 포박(Grab/적색)·가드불가 패턴이 여기서 관통.
- `isPerfectGuardable==false`(가드불가지만 Grab 아님)인 경우도 ③에서 PG 차단 → ④ 일반가드 시도 → 정책상 가드불가는 보통 `type=Unblockable`로 ①에서 컷. (Grab=잡기, Unblockable=막아도 관통하는 적색 베기.)
- **PG 성공 시 DoT 무효(#7 화염):** outcome==PerfectGuard면 `info.dotPerSec` 무시. 일반가드(Blocked)는 칩+DoT 둘 다 적용 → designer 명세 일치.

#### 난점 #1 — PG 윈도우 8f 시간 판정 + 겹침 순서
- **시간 기반:** `IsInPerfectGuardWindow(now) = (now - guardPressedTime) <= perfectGuardWindow(0.133s)`. `guardPressedTime`은 가드 버튼 다운 시각(`Time.time`). 프레임 카운트 아님 → 프레임 드랍에 견고, SO로 윈도우 튜닝(난점 #6).
- **"가드 입력 시각 ~ 피격 판정 델타":** 보스 `AE_HitboxOn`으로 켜진 히트박스가 플레이어 Hurtbox에 닿는 프레임이 "피격 판정 시각". 그 프레임에서 델타 측정 → 같은 타임라인(보이는 칼=판정).
- **판정 순서 우선권(확정):**
  `i-frame(콜라이더 비활성)` > `Unblockable/Grab(①②)` > `PerfectGuard(③)` > `일반가드(④)` > `가드브레이크(⑤)` > `무방비 Hit(⑥)`.
  - i-frame이 최우선: Resolve 진입 전 차단. 회피와 PG는 **상호배타**(Dodge 중엔 가드 분기 없음).
  - 입력 버퍼는 **공격 입력 전용** 큐 → 가드/PG 판정과 독립. 단 피격 해소(Hit/Staggered)가 나면 버퍼는 클리어(다음 공격 취소).
  - designer 우려(i-frame이 모든 판정을 덮음): **딜레이드 어택(#2 내려찍기)은 보스 `AE_HitboxOn`을 i-frame 종료(14f) 이후로 배치** → 성급한 회피 처벌·PG 유도. QA 튜닝4 검증.

### 3.2 BossDamageResolver.Resolve(info)

```
 ① PhaseManager.IsInvulnerable (PhaseTransition 1.5s 중)  → Immune (데미지0, 스태거0)
 ② info가 치명타(고정150, 특수 플래그)                      → Hit (무조건 적용, Staggered 탈출)
 ③ 보스 Attack 모션 중(슈퍼아머)                            → Hit (데미지·스태거 적용, 단 경직 없음)
 ④ else                                                    → Hit (데미지·스태거 적용)
```
- 보스는 가드/회피 없음 → Immune(무적)/슈퍼아머만 분기. **스태거 만탱이면** 이후 `Posture.OnGroggyEnter`로 BossBrain이 Staggered 전이(슈퍼아머여도 만탱엔 무너짐 — designer "PG로 무너뜨린다").
- PhaseTransition 무적 중(①) 플레이어 공격은 데미지0 + **스태거 미적립** → 무적 중 그로기 진입 불가(난점 #2 게이트와 짝).

---

## 4. 난점 #3 — 회색 체력 데미지 파이프라인 3분기

`Health.ApplyDamage(DamageResult)`가 outcome에 따라 3갈래:

### 4.1 분기 1 — 실데미지 (Hit / GuardBreak / Grabbed)
```
currentHealth -= result.finalDamage
recoverableHealth = 0           // ★재피격 시 회색 소멸(회복 기회 상실 — designer 명세)
OnHealthChanged 방송
if currentHealth <= 0 → OnDeath → FSM=Dead
```

### 4.2 분기 2 — 회색 적립 (Blocked = 일반가드 칩)
```
chip = info.amount * normalGuardChip(0.18)
currentHealth      -= chip       // 즉시 빠지되
recoverableHealth  += chip       // 회색으로 보존(별도 추적)
recoverableExpireTime = Time.time + recoverableDuration(5s)
OnHealthChanged / OnRecoverableChanged 방송
// PG(PerfectGuard)는 칩0 → 이 분기 안 탐. 회색 적립 없음(애초에 안 잃음).
```

### 4.3 분기 3 — 회복 (반격 명중 시, Rally)
```
[트리거] 플레이어 Hitbox가 보스에게 명중(DamageResult.outcome ∈ {Hit, Blocked})
  → 공격자(플레이어) Health.RecoverFromHit() 호출
RecoverFromHit():
  if Time.time > recoverableExpireTime → recoverableHealth=0; return  // 5s 만료
  heal = recoverableHealth * recoverFraction(0.35)
  currentHealth     += heal (max 클램프)
  recoverableHealth -= heal
  OnHealthChanged / OnRecoverableChanged 방송
```

### 4.4 회색 만료 (Update)
```
if recoverableHealth>0 AND Time.time>recoverableExpireTime
  → recoverableHealth=0; OnRecoverableChanged 방송   // 5s 경과 소멸
```

> **3분기 요약:** ①실데미지(+회색소멸) ②회색적립(일반가드 칩) ③회복(반격 35%). PG는 칩0이라 ②를 안 타고, 재피격(①)은 회색을 날린다. designer "막고→받아치면 손해 0 수렴" 랠리 철학을 데이터 흐름으로 구현.

---

## 5. Animation Event 훅 목록 (애니메이션 구동 판정)

> "보이는 것 = 맞는 것" 보장. **모든 모션 종료 훅은 시간 타이머와 이중화**(데드락 차단, state-machines §4).

### 5.1 플레이어
| 훅 | 위치(클립) | 동작 |
|----|------------|------|
| `AE_HitboxOn` | 공격 active 시작 | Hitbox.Enable(activeAttack), alreadyHit.Clear() |
| `AE_HitboxOff` | 공격 active 종료 | Hitbox.Disable() |
| `AE_IFrameOn` | 회피 3f 지점 | Hurtbox.enabled=false (무적) |
| `AE_IFrameOff` | 회피 14f 지점(11f 지속) | Hurtbox.enabled=true |
| `AE_AttackEnd` | 공격 모션 끝 | 입력버퍼 소비/클리어, Idle 전이 신호 |
| `AE_HitReactEnd` | 히트리액션 끝 | Hit→Idle 전이 신호 |
| (PG 윈도우) | **AE 아님** | GuardSystem 입력 기반 타이머(guardPressedTime) |

### 5.2 보스
| 훅 | 위치 | 동작 |
|----|------|------|
| `AE_HitboxOn` / `AE_HitboxOff` | 패턴 active 구간 | Hitbox on/off. **active 프레임 고정 → 2P에서도 불변(난점 #4)** |
| `AE_TelegraphStart` | 선딜 시작 | (2P)BossAnimationDriver 텔레그래프 구간 speed 가속. HitboxOn에서 speed=1 복귀 |
| `AE_PatternEnd` | 패턴 모션 끝 | Attack→Recover 전이 신호 + Hitbox.Disable 보증 |
| `AE_PhaseTransitionEnd` | 전환 연출 1.5s 끝 | PhaseTransition→Approach, 무적 off |
| `AE_GroggyVfx` (선택) | 그로기 진입 | VFX 큐(Feel) |

> **난점 #4 재확인:** 보스 active 판정(`AE_HitboxOn`~`AE_HitboxOff`) 프레임 수는 **클립에 고정**. 2P 텔레그래프 가속은 `AE_TelegraphStart`~`AE_HitboxOn` 구간 speed만 건드림 → PG 윈도우 8f 절대값 보존. "선딜만 압축, 판정 동일" 달성.

---

## 6. 1타 1판정 / 진영 / DoT 처리

- **1타 1판정:** Hitbox는 `HashSet<CombatActor> alreadyHit`. `AE_HitboxOn`에서 Clear, 명중마다 Add → 한 스윙에 같은 대상 중복 판정 방지(QA 중복판정 검증 대상).
- **진영/자가:** `info.sourceTeam == target.Team` 또는 `info.source == self` → 무시.
- **DoT(#7 화염):** Blocked/Hit이면 `dotPerSec×dotDuration`을 대상에 DoT 컴포넌트로 부착(틱마다 Health.ApplyDamage 실데미지 분기). **PerfectGuard면 DoT 미부착**(designer "PG 시 화상 무효").

---

## 7. 미설치 의존성 / 추가 필요

- **Cinemachine 미설치:** 히트스톱은 Time.timeScale로 가능하나 **카메라 셰이크/락온 카메라 지향은 Cinemachine Impulse가 정석 → "추가 필요".** MVP는 카메라 Transform 노이즈 + LookAt 폴백으로 구현 가능하나, 폴리시 단계에서 Cinemachine 도입 권장.
