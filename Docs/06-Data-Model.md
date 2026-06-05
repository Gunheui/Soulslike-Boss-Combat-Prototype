# 데이터 모델 — ScriptableObject 스키마 + 컴포넌트 직렬화 필드

> 원칙: **모든 손맛 수치**는 여기 SO 필드 또는 컴포넌트 `[SerializeField]`로 매핑 → Inspector에서 **재컴파일 없이 튜닝**(난점 #6 해소).
> 해소 난점: **#4 2페이즈 텔레그래프 -15%(판정 프레임 고정)**, **#6 `// 튜닝 필요` 미확정값 SO 노출**.
> @60fps. 프레임값은 f로 명시되어 있으나 **SO/필드에는 초 단위 저장 권장**(timeScale·deltaTime 일관). f→s 환산 병기.

**변경 메모:**
- 2026-06-05 최초 작성 (v1).

---

## 1. AttackData (SO) — 공격 1종 = 에셋 1개

플레이어/보스 공용. 판정 **타이밍은 Animation Event**로 제어, 여기 수치는 밸런스+가중치용.

```csharp
[CreateAssetMenu(menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject {
    [Header("ID / Animation")]
    public string attackId;
    public string animTrigger;            // Animator 트리거 이름(클립 직접참조 대신 권장)

    [Header("Damage")]
    public float damage;                  // HP 데미지
    public float poiseDamage;             // 경직 누적(가드게이지 소모량 = poise×1.0)
    public float staggerDamage;           // 상대 스태거 게이지 적립

    [Header("Type / Guard")]
    public DamageType type = DamageType.Normal;  // Normal / Unblockable / Grab
    public bool isPerfectGuardable = true;       // 가드불가면 false(잡기/적색)

    [Header("Cost / Combo (플레이어 공격용)")]
    public float staminaCost;
    [Range(0,1)] public float comboWindowStart = 0.60f;  // 모션 진행률
    [Range(0,1)] public float comboWindowEnd   = 0.90f;
    public AttackData nextCombo;          // 콤보 체인(없으면 null)

    [Header("Status / Knockback (보스 공격용)")]
    public float knockback;               // 넉백 거리(m)
    public float hitstunFrames;           // 명중 시 플레이어 경직(f)
    public float dotPerSec;               // 화상 DoT 등(없으면 0)
    public float dotDuration;             // DoT 지속(s)

    [Header("Telegraph — 난점 #4")]
    public float telegraphTime;           // 선딜(s, 밸런스 참고. 실판정은 AE)
    [Range(0.5f,1f)] public float phase2TelegraphMult = 0.85f; // 2P 선딜 압축 -15%
    // ※ 이 배율은 '텔레그래프 구간 전용'. 히트박스 활성 후엔 speed=1 복귀(§4 참조).
}
```

### feel-params → AttackData 매핑 (플레이어 공격)

| AttackData 필드 | 약공격 | 강공격 | 차지 강공 | 출처 |
|-----------------|--------|--------|-----------|------|
| damage | 18 | 40 | 70 | feel-params D |
| poiseDamage | 10 | 30 | 45 | D |
| staggerDamage | +8 | +20 | +38 | D |
| staminaCost | 16 | 30 | 30 | D |
| comboWindowStart/End | 0.60 / 0.90 | (홀드차지) | — | D "모션 60~90%" |
| type / isPerfectGuardable | Normal / — | Normal / — | — | — |
| (모션 길이는 Animator 클립) | 24f(0.40s) | 45f(0.75s) | 75f(1.25s) | D |

> 치명타(고정150 / 무적1.5s / 사거리2.5m)는 공격 콤보가 아닌 **그로기 전용 동사** → `CriticalAttackSystem`의 `[SerializeField]`로(아래 §5).

### feel-params → AttackData 매핑 (보스 패턴 8종)

| # | 패턴 | damage | poise | stagger(플레이어에게) | type / PGable | dot | telegraph | 페이즈 |
|---|------|--------|-------|------------------------|---------------|-----|-----------|--------|
| 1 | 횡베기 | 22 | 18 | — | Normal / ✔ | — | 0.37s | 1,2 |
| 2 | 내려찍기·딜레이 | 36 | 30 | hitstun 12f | Normal / ✔ | — | 0.47s + 딜레이10f | 1,2 |
| 3 | 방패밀치기 | 18 | 24 | knockback 1.5m | Normal / ✔ | — | 0.30s | 1,2 |
| 4 | 돌진베기 | 30 | 26 | — | Normal / ✔ | — | 0.43s | 1,2 |
| 5 | 3연콤보 | 20/타 | 16/타 | — | Normal / ✔ | — | 0.27s/타(초타20f) | 1,2 |
| 6 | 포박잡기 | 50 | — | 다운90f | **Grab / ✘** | — | 0.50s | 2(1P 60% 예고) |
| 7 | 화염사선 | 34 | 28 | — | Normal / ✔(PG시 화상무효) | 3/s×3s | 0.40s | 2 |
| 8 | 광분4연 | 18/타 | 14/타 | — | Normal / ✔ | — | 0.23s/타(초타18f) | 2 |

> **판정(active) 프레임은 AttackData에 저장하지 않는다 — Animation Event(`AE_HitboxOn/Off`)로 클립에 직접 박는다.** #1=8f, #2=6f, #6=6f 등 designer 판정값은 **클립의 AE 위치**로 구현. 난점 #4의 "판정 프레임 고정"이 여기서 보장(§4).

---

## 2. WeaponData (SO) — MVP 1종(대검)

```csharp
[CreateAssetMenu(menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject {
    public string weaponName;
    public AttackData[] lightCombo;       // 약공 콤보 시퀀스(2~3타)
    public AttackData heavyAttack;        // 강공(차지 가능)
    public float chargeTime = 1.25f;      // 차지 완성(s) — 75f
    public float chargedDamageMult;       // 차지 데미지 배율(또는 chargedAttack 별도 SO)
}
```
> MVP는 플레이어 무기 1종. 보스 대검은 패턴 SO로 직접 구성(WeaponData 불필요).

---

## 3. AttackPattern (SO) — 보스 공격 + 선택 메타

```csharp
[CreateAssetMenu(menuName = "Combat/Attack Pattern")]
public class AttackPattern : ScriptableObject {
    public string patternId;
    public AttackData attack;             // 데미지/판정/타입 데이터
    [Header("선택 조건")]
    public float minRange;                // 유효 거리 하한(m)
    public float maxRange;                // 유효 거리 상한(m)
    public float cooldown;                // 이 패턴 자체 쿨다운(s)
    public float selectionWeight = 1f;    // 가중 랜덤
    [Header("타이밍(밸런스 참고; 실판정은 AE)")]
    public float telegraphTime;           // 선딜(s)
    public float recoverTime;             // 펀시 윈도우(후딜, s)
    public bool[] activePhases;           // 등장 페이즈(예: {true,true} = 1·2P)
}
```

### feel-params/boss-patterns → AttackPattern 매핑

| patternId | minRange | maxRange | recoverTime(펀시) | activePhases | selectionWeight 비고 |
|-----------|----------|----------|-------------------|--------------|----------------------|
| Slash(#1) | 0 | 3.0 | 0.57s(34f) | 1,2 | 주력, 높음 |
| Overhead(#2) | 0 | 3.0 | 0.70s(42f) | 1,2 | 바이트베이트, 중 |
| ShieldBash(#3) | 0 | 2.5 | 0.33s(20f) | 1,2 | 가드압박, 중 |
| ChargeSlash(#4) | 3.0 | 7.0 | 0.37s(22f) | 1,2 | 중거리, 중 |
| TripleString(#5) | 0 | 3.0 | 0.47s(28s 종료후) | 1,2 | 콤보, 중 |
| Grapple(#6) | 0 | 2.5 | 0.60s(36f, 빗나감) | 2(+1P 예고) | 가드불가, 낮음 |
| FlameCrescent(#7) | 0 | 3.5 | 0.50s(30f) | 2 | 화염, 중 |
| FrenzyString(#8) | 0 | 3.0 | 0.40s(24f 종료후) | 2 | 4연타, 중 |

> **#6 1페이즈 60% 예고 1회:** `BossPhaseManager`가 1P HP 60% 도달 시 1회성 강제 패턴 트리거(SelectionWeight 무시, 직접 호출). 일반 풀에는 2P부터 포함. 구현은 PhaseManager의 `oneShotPreview` 플래그.

---

## 4. 난점 #4 — 2페이즈 텔레그래프 -15% (판정 프레임 고정) 데이터 설계

**문제:** designer는 "텔레그래프 -15%, 판정 동일"을 요구. 단순 Animator `speed=0.85`(역: 빠르게는 speed↑)를 전체 클립에 걸면 **active 프레임까지 짧아져 PG 윈도우 절대값(8f)이 깨진다.**

**해결(권장 → 차선 순):**

1. **(권장) 텔레그래프/판정 클립 분리 또는 Animator State 구간 speed 분리.**
   - 한 공격을 `Telegraph` state + `Active/Recover` state로 분할. `BossAnimationDriver`가 2P일 때 **Telegraph state의 speed만 1/0.85≈1.176배**로 가속, Active state speed=1 고정.
   - active 프레임 수(#1=8f 등)는 **클립 + AE 위치로 불변** → PG 윈도우 8f 보존.
2. **(차선) 단일 클립 + AE 기반 speed 토글.**
   - `AE_TelegraphStart`(speed=phase2TelegraphMult의 역수) → `AE_HitboxOn`에서 `speed=1` 복귀 → `AE_HitboxOff`. 클립 1개로 처리하나 AE 누락 시 위험 → 1번 권장.

**데이터 노출:** `AttackData.phase2TelegraphMult`(0.85, Inspector 튜닝) + `BossAnimationDriver.SetTelegraphScale()`가 적용. -15%를 -10%/-20%로 바꾸려면 SO 값만 수정(재컴파일 없음). 난점 #6과 결합.

> active 프레임이 클립에 고정되므로 "판정 동일·선딜만 압축"이 데이터 레벨에서 보장된다. engineer는 클립 분리(1번)로 구현.

---

## 5. 런타임 상태 — 컴포넌트 직렬화 필드 (SO 아님)

actor 인스턴스마다 다른 값(현재 HP 등) + actor별 최대치는 컴포넌트 `[SerializeField]`. **프리팹별로 다르게**(플레이어 vs 보스) 설정.

### 5.1 Health (Combat) — 난점 #3 회색 체력

```csharp
public class Health : MonoBehaviour, IDamageable {
    [SerializeField] float maxHealth = 100;        // 플레이어100 / 보스1200
    [SerializeField] float recoverableDuration = 5f;   // 회색 유지(s)
    [SerializeField, Range(0,1)] float recoverFraction = 0.35f; // 반격1회 회복비율

    float currentHealth;
    float recoverableHealth;                        // 회색 영역(별도 추적)
    float recoverableExpireTime;                    // 회색 소멸 시각

    public event Action<float,float> OnHealthChanged;       // (current, max)
    public event Action<float> OnRecoverableChanged;        // 회색량
    public event Action OnDeath;

    // 데미지 3분기(난점 #3) — damage-pipeline §4와 짝
    // 1) 실데미지: currentHealth -= dmg
    // 2) 회색적립: 일반가드 칩 → recoverableHealth += chip, currentHealth -= chip, expire 갱신
    // 3) 회복: 반격 명중 → recoverableHealth*recoverFraction 만큼 current로 환원
    // + 재피격 시 회색 소멸: 풀피격(Hit)이면 recoverableHealth=0 (회복기회 상실)
}
```

| Health 필드 | 플레이어 | 보스 | 출처 |
|-------------|----------|------|------|
| maxHealth | 100 | **1200** `// 튜닝 필요(1000~1400)` | feel-params E/F |
| recoverableDuration | 5s | (보스 미사용) | E |
| recoverFraction | 0.35 | — | E |

> **`// 튜닝 필요` 처리(난점 #6):** 보스 maxHealth 1200은 `[SerializeField] [Tooltip("튜닝 범위 1000~1400")]`로 노출 + `[Range(1000,1400)]` 슬라이더 권장. Inspector에서 즉시 조정.

### 5.2 Stamina (Combat, 플레이어 전용)

```csharp
public class Stamina : MonoBehaviour {
    [SerializeField] float maxStamina = 120;
    [SerializeField] float regenRate = 40;          // /s
    [SerializeField] float regenDelay = 1.0f;       // 마지막 소모 후(s)
    float currentStamina; bool exhausted;
    // 0 처리: 회피/공격 불가, 가드는 탈진 상태로 가능(QA 검증 대상)
}
```

| 필드 | 값 | 출처 |
|------|-----|------|
| maxStamina | 120 | feel-params E |
| regenRate | 40/s | E |
| regenDelay | 1.0s | E |
| 달리기 소모 | 8/s | (Locomotion 필드) D |
| 회피/약공/강공 소모 | 25/16/30 | AttackData.staminaCost / Dodge 필드 |

### 5.3 Posture (Combat) — 스태거. 보스만 그로기 사용

```csharp
public class Posture : MonoBehaviour {
    [SerializeField] float maxStagger = 100;
    [SerializeField] float decayRate = 12f;         // /s (// 0순위 튜닝, 범위 8~15)
    [SerializeField] float decayDelay = 1.5f;       // 마지막 적립 후(s)
    [SerializeField] float groggyDuration = 4f;     // 치명타 윈도우(s)
    [SerializeField] bool usesGroggy = true;        // 보스 true / 플레이어 false
    float currentStagger; float lastAddTime;
    public event Action OnGroggyEnter, OnGroggyExit;
    public event Action<float,float> OnStaggerChanged;  // (current,max)
}
```

| Posture 필드 | 보스 | 플레이어 | 출처 |
|--------------|------|----------|------|
| maxStagger | 100 | (포이즈/가드게이지로 대체) | feel-params F |
| decayRate | 12/s `// 0순위 튜닝(8~15)` | — | F |
| decayDelay | 1.5s | — | F |
| groggyDuration | 4s | — | F |
| usesGroggy | true | false | — |

> **플레이어 경직은 Posture 그로기가 아니라 가드게이지/포이즈로 처리** → 플레이어 Posture.usesGroggy=false. 가드게이지는 GuardSystem(§5.4)에서 관리.

### 5.4 GuardSystem (Player) — 가드게이지 + PG 윈도우

```csharp
public class GuardSystem : MonoBehaviour {
    [SerializeField] float maxGuardGauge = 100;
    [SerializeField] float guardRegenRate = 20f;        // /s
    [SerializeField] float guardRegenDelay = 2.0f;      // 비가드·비피격 후(s)
    [SerializeField, Range(0.10f,0.167f)]
        float perfectGuardWindow = 0.133f;              // 8f // 0순위 튜닝(6~10f=0.10~0.167s)
    [SerializeField, Range(0,1)] float normalGuardChip = 0.18f; // 칩 18%
    [SerializeField] float guardBreakStunFrames = 30f;  // 가드브레이크 경직(f)
    [SerializeField] float pgStaggerToEnemy = 18f;      // PG 시 적 스태거 적립
    [SerializeField] float pgEnemyHitstunFrames = 6f;   // PG 시 적 미세경직
    [SerializeField] float pgRecoverFrames = 4f;        // PG 후딜
    float currentGauge; float guardPressedTime;
    public bool IsInPerfectGuardWindow(float now) =>
        (now - guardPressedTime) <= perfectGuardWindow;
}
```

| GuardSystem 필드 | 값 | 출처 |
|------------------|-----|------|
| **perfectGuardWindow** | **0.133s(8f)** `// 0순위 튜닝(6~10f)` | feel-params C |
| maxGuardGauge | 100 | C |
| guardRegenRate / Delay | 20/s / 2.0s | C |
| normalGuardChip | 0.18 | C |
| guardBreakStunFrames | 30f | C |
| pgStaggerToEnemy | 18 | C |
| pgEnemyHitstunFrames | 6f | C |
| pgRecoverFrames | 4f | C |

### 5.5 Dodge/Locomotion 파라미터 (Player)

```csharp
public class PlayerLocomotion : MonoBehaviour {
    [SerializeField] float walkSpeed = 2.5f, strafeSpeed = 2.0f, runSpeed = 5.0f;
    [SerializeField] float rotateSpeed = 720f;       // °/s
    [SerializeField] float runStaminaPerSec = 8f;
    [Header("Dodge")]
    [SerializeField] float dodgeDistance = 3.5f;
    [SerializeField] float dodgeStaminaCost = 25f;
    [SerializeField] float iFrameStartFrame = 3f;    // // 튜닝(AE로 구현, 참고값)
    [SerializeField] float iFrameDuration = 11f;     // // 튜닝(10~13f)
    [SerializeField] float dodgeRecoverFrames = 9f;
}
```
> i-frame은 **Animation Event(`AE_IFrameOn/Off`)** 로 구현. 위 프레임값은 AE 배치 기준·튜닝 참고. `// 튜닝` 항목은 Tooltip+범위로 노출(난점 #6).

### 5.6 LockOn / Critical (Player)

```csharp
public class LockOnSystem : MonoBehaviour {
    [SerializeField] float lockRange = 12f;          // // 튜닝(아레나 의존)
    // Cinemachine 미설치 → 카메라 지향 '추가 필요'. MVP는 Transform LookAt 폴백.
}
public class CriticalAttackSystem : MonoBehaviour {
    [SerializeField] float critRange = 2.5f;
    [SerializeField] float critDamage = 150f;        // 또는 maxHP 12%
    [SerializeField] float critInvulnDuration = 1.5f;
}
```

### 5.7 BossPhaseData (SO) — 페이즈 1개 = 1 에셋

```csharp
[CreateAssetMenu(menuName = "Combat/Boss Phase")]
public class BossPhaseData : ScriptableObject {
    public int phaseIndex;
    public float hpThresholdPercent;     // 다음 페이즈 진입 HP% (P1→P2 = 0.50)
    public AttackPattern[] patterns;     // 이 페이즈 패턴 셋
    public float aggressionCooldown;     // 패턴 간 평균 대기(s)
    public float moveSpeedMult = 1f;     // 페이즈 이동 속도 배율
    public float telegraphMult = 1f;     // 이 페이즈 텔레그래프 배율(P2=0.85) — 난점 #4
    public float phaseTransitionInvuln = 1.5f; // 전환 무적(s)
}
```

| BossPhaseData | P1 | P2 | 출처 |
|---------------|----|----|------|
| hpThresholdPercent | 0.50 | (없음/사망) | boss-patterns 3 |
| aggressionCooldown | 1.2s | **0.8s** | boss-patterns 2P변화표 |
| telegraphMult | 1.0 | **0.85** | boss-patterns(-15%) |
| moveSpeedMult | 1.0 | (↑, 튜닝) | 2P "빠르게" |
| phaseTransitionInvuln | — | 1.5s | boss-patterns 3 |
| patterns | #1~#5 | #1~#8 | boss-patterns 2 |

---

## 6. SO vs 컴포넌트 필드 — 분류 기준

| 데이터 | 위치 | 이유 |
|--------|------|------|
| 공격 데미지/포이즈/스태거/타입 | **AttackData SO** | 공유·튜닝·에셋 관리 |
| 보스 패턴 선택 메타(거리/쿨다운/가중치) | **AttackPattern SO** | 코드 무변경 패턴 추가 |
| 페이즈 임계/쿨다운/속도/텔레그래프배율 | **BossPhaseData SO** | 페이즈=에셋 |
| 최대치(maxHP/Stamina/Gauge/Stagger) | 컴포넌트 `[SerializeField]` | actor·프리팹별 상이 |
| 현재값(currentHP/회색/스태거/게이지) | 컴포넌트 런타임 | 인스턴스 상태 |
| 윈도우 타이밍(PG 8f) | GuardSystem `[SerializeField]` | 입력 기반 타이머(애니 아님) |
| 판정 타이밍(active/i-frame) | **Animation Event** | 애니 동기화(보이는=맞는) |

## 7. `// 튜닝 필요` 미확정값 노출 목록 (난점 #6)

전부 Inspector 노출 + Range/Tooltip로 재컴파일 없이 튜닝:

| 값 | 위치 | 출발값 | 범위 |
|----|------|--------|------|
| 퍼펙트가드 윈도우 | GuardSystem | 0.133s(8f) | 6~10f(0.10~0.167s) **0순위** |
| 스태거 감소율 | Posture(보스) | 12/s | 8~15 **0순위** |
| PG 스태거 적립 | GuardSystem | 18 | 튜닝 |
| 회피 i-frame 지속 | PlayerLocomotion(+AE) | 11f | 10~13f |
| 보스 maxHP | Health(보스) | 1200 | 1000~1400 |
| 락온 거리 | LockOnSystem | 12m | 아레나 의존 |
| 2P 잔상 판정 6f | AttackData(2P 베기) | 6f | 튜닝 |

## 8. 에셋 네이밍 / 폴더

- `SO_Attack_Light1/2/3`, `SO_Attack_Heavy`, `SO_Attack_Boss_Slash` …
- `SO_Pattern_Boss_Slash`, `SO_Pattern_Boss_Grapple` …
- `SO_Phase_Boss_1`, `SO_Phase_Boss_2`
- 폴더: `Assets/_Project/ScriptableObjects/{Attacks, Patterns, Phases, Weapons}/`
