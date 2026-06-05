# 상태 머신 — 플레이어 FSM + 보스 FSM

> 핵심: 모든 비종단 상태에 시간/이벤트 탈출 전이 보장 → **데드락 차단**(§4 검증 필수).
> 해소 난점: **#1 PG 윈도우 시간 판정·판정 순서**, **#2 그로기/치명타 vs 페이즈 전환 충돌**.
> @60fps. f = 프레임. 플레이어 = State 패턴(클래스), 보스 = enum+switch(경량).

**변경 메모:**
- 2026-06-05 최초 작성 (v1).

---

## 1. 플레이어 FSM (9상태)

각 상태는 `IPlayerState { OnEnter(); Update(); OnExit(); }`. `PlayerStateMachine.ChangeState()`가 OnExit→OnEnter 순서 보장.

| 상태 | 진입 조건 | 진입 액션(OnEnter) | 탈출 액션(OnExit) | 전이 → (조건) | 비고 |
|------|-----------|--------------------|-------------------|----------------|------|
| **Idle** | 입력 없음 | 스태미나 회복 활성, 루트모션 정지 | — | Move(이동입력) · Attack(공격버퍼+스태미나≥16) · Dodge(회피+스태미나≥25) · Guard(가드홀드) · Hit(피격해소=Hit) · 치명타(그로기보스 2.5m내+입력) | 기본 |
| **Move** | 이동입력 | Locomotion 구동(락온 시 스트레이프) | — | Idle(입력해제) · Attack · Dodge · Guard · Hit | 회전 720°/s |
| **Dodge** | 회피입력 + 스태미나≥25 | 스태미나-25, 이동임펄스(3.5m), **i-frame 스케줄(AE)** | i-frame 강제 off(잔류 방지) | Idle/Move(`AE_AttackEnd`/모션종료, 후딜 9f 후) | **i-frame은 `AE_IFrameOn`(3f)~`AE_IFrameOff`(14f)**. 이 구간 Hurtbox.enabled=false |
| **Attack** | 공격버퍼 + 스태미나≥cost | 콤보인덱스 set, 스태미나-, AttackData 주입, Animator 트리거 | Hitbox.Disable, 입력버퍼 클리어 | 다음Attack(콤보윈도우 60~90%내 버퍼) · Idle(모션종료 `AE_AttackEnd`) · Hit(슈퍼아머 없음+피격해소=Hit) | 히트박스는 `AE_HitboxOn/Off` |
| **Guard** | 가드홀드 | 가드자세 Animator, **GuardSystem: guardPressedTime=now 기록(PG 윈도우 기준점)** | 가드자세 해제 | Idle(가드해제) · **피격 시 §2 분기로** · Staggered(가드브레이크) | **여기 머무는 동안 PG 윈도우는 "최근 가드 입력 시각 기준" 타이머** |
| **PerfectGuard** | Guard 상태에서 피격 시 PG 윈도우 내(§2) | 데미지0 확정, 적 Posture+18, 적 미세경직 6f 요청, PG VFX | — | Guard(가드홀드 유지) / Idle(가드해제), **후딜 4f 후** | 보상 상태. 후딜 4f(회피 9f보다 짧음) |
| **Hit** | 피격해소 결과=Hit(가드 외 풀피격) | 히트리액션 Animator, **회색체력 적립(Health.AddRecoverable)**, 입력잠금 | 입력잠금 해제 | Idle(리액션 종료, **타이머/`AE_HitReactEnd`**) | **탈출 전이 필수** |
| **Staggered** | 가드브레이크(게이지0) or 큰 포이즈 or 잡기 다운 | 경직 모션, 입력 완전잠금, staggerTimer 시작 | 입력잠금 해제 | Idle(**staggerTimer 종료** — 가드브레이크 30f / 잡기다운 90f) | **탈출 전이 필수(타이머)** |
| **Dead** | HP≤0 | 사망 모션, 모든 입력 차단, 컴포넌트 비활성 | — | **없음(종단)** | 유일하게 탈출 없는 상태 |

### 콤보/입력 버퍼 규칙
- `PlayerInputReader`가 공격 입력을 **모션 종료 전 10f부터** 큐잉. Attack 상태가 콤보윈도우(모션 60~90%)에서 버퍼 확인 → 다음 Attack. `AE_AttackEnd`에서 미소비 버퍼 클리어.
- 차지 강공: 입력 홀드로 차지 진입(45f→75f, 데미지 40→70).

---

## 2. 가드 피격 분기 (난점 #1·#5 핵심) — Guard 상태에서 Hurtbox가 피격 수신 시

이 분기는 **`PlayerDamageResolver.Resolve(DamageInfo info)`** 안에서 실행된다. Hurtbox는 분기를 모르고 위임만 한다.

### 2.1 판정 순서 (위에서 아래로, 먼저 매칭되는 곳에서 종료)

```
Resolve(info) 진입 [현재 FSM 상태 + GuardSystem + info 읽음]
 │
 ├─[0] 회피 i-frame 활성(Hurtbox.enabled==false)  → 애초에 Hurtbox.OnTriggerEnter가 안 불림
 │        ※ i-frame은 Resolve 이전 단계에서 컷. Dodge 무적은 "콜라이더 비활성"으로 구현(가장 단순·확실).
 │
 ├─[1] info.type == Unblockable | Grab            → outcome=Hit/Staggered (가드/PG 전부 무효)   ← 난점 #5
 │        ※ 가드불가는 가드 상태든 PG 윈도우든 무조건 관통. 잡기는 Staggered(다운 90f).
 │
 ├─[2] (FSM==Guard or PerfectGuard 진입가능) AND GuardSystem.IsInPerfectGuardWindow(now)
 │        AND info.isPerfectGuardable                → outcome=PerfectGuard (데미지0, 적 스태거+18, 적 경직6f)
 │        ※ PG 윈도우 = (now - guardPressedTime) <= 8f(0.133s). 시간 기반(프레임 누락 견고).
 │
 ├─[3] FSM==Guard AND guardGauge >= info.poiseDamage  → outcome=Blocked (일반가드: 칩18%, 회색적립, 게이지-poise)
 │
 ├─[4] FSM==Guard AND guardGauge < info.poiseDamage   → outcome=GuardBreak (Staggered 30f, 풀데미지 강제피격)
 │
 └─[5] FSM != Guard (Idle/Move/Attack 등 무방비)      → outcome=Hit (풀데미지, 회색 미적립)
```

### 2.2 난점 #1 — PG 윈도우 8f 시간 기반 판정 + i-frame/입력버퍼 겹침 순서 확정

- **측정 방식:** PG는 프레임 카운트가 아니라 **시각 델타**. `GuardSystem.guardPressedTime`(가드 버튼 눌린 `Time.time`) 기록 → 피격 시점 `now`에서 `now - guardPressedTime <= 0.133s(8f)` 이면 PG. **튜닝값(6~10f)은 SO/Inspector 노출**(난점 #6) → 재컴파일 없이 윈도우 조정.
- **"가드 입력 시각 ~ 피격 판정 델타":** 보스 Hitbox.OnTriggerEnter → DamageInfo 생성 → 플레이어 Hurtbox.TakeDamage → Resolve가 호출되는 시점이 곧 "피격 판정 시각". 이 한 프레임 안에서 델타 평가 → **판정과 입력이 같은 타임라인**(애니 이벤트로 켜진 보스 히트박스 = 보이는 칼 = 판정). "보이는 것 = 맞는 것" 보장.
- **겹침 우선순위(중요):**
  1. **i-frame이 최우선.** 회피 무적은 Hurtbox 콜라이더 비활성으로 구현 → Resolve 자체가 안 불린다. 즉 Dodge 중에는 PG/가드 분기가 존재하지 않음(상호배타). 회피 i-frame(11f, 3~14f)이 보스 판정(6~10f)을 덮으면 회피 우선 — 단 designer 의도대로 **딜레이드 어택(#2 내려찍기)은 i-frame 종료 후 판정**되도록 보스 AE 타이밍을 늦춰 회피 남용을 차단(QA 검증 §튜닝4).
  2. **그 다음 가드불가(#5).** i-frame이 아니면, 가드불가는 PG보다 먼저 평가 → 가드/PG 무효.
  3. **그 다음 PG → 일반가드 → 가드브레이크.** 위 [2]→[3]→[4] 순.
  4. **입력 버퍼는 공격 전용**(다음 Attack 큐). 가드/PG 판정과 무관 → 충돌 없음. 단, 버퍼된 공격이 있어도 피격 해소(Hit/Staggered)가 발생하면 **해소가 우선**, 버퍼는 클리어.
- **PG 실패 → 일반가드 폴백:** 윈도우를 놓쳤어도 FSM==Guard면 [3]에서 일반가드로 흡수(칩18%+게이지). designer 명세 "늦으면 일반가드" 그대로. PG는 스태미나 0, 폴백된 일반가드 피격만 게이지/스태미나(포이즈 비례) 소모.

---

## 3. 보스 FSM (6상태, 경량 enum+switch)

`BossBrain.Tick()`이 매 프레임 currentState로 switch. 상태별 OnEnter/Tick/OnExit 메서드.

| 상태 | 진입 조건 | 진입 액션(OnEnter) | 탈출 전이 → (조건) | 비고 |
|------|-----------|--------------------|--------------------|------|
| **Idle** | 전투 시작 전 / 패턴 쿨다운 중 | 대기 애니, 쿨다운 타이머 | Approach(플레이어 감지 & 쿨다운 종료) | 진입점 |
| **Approach** | 거리 > 패턴 사거리 | NavMesh 이동/스텝(페이즈 속도배율) | Attack(사거리 진입 & 쿨다운0) · Idle(플레이어 소실) | AI Navigation |
| **Attack** | 사거리 + 쿨다운0 | **PatternSelector로 AttackPattern 선택**, Animator 트리거, Hitbox `AE` 스케줄 | Recover(패턴 모션 종료 `AE_PatternEnd`) · **Staggered(스태거 만탱, §3.1)** · **PhaseTransition(보류 후, §3.2)** | 패턴 중 피격은 슈퍼아머 규칙(§3.3) |
| **Recover** | 패턴 종료 | 펀시 윈도우(후딜, 패턴별 recoverTime) | Approach/Idle(후딜 종료) · Staggered(만탱) · PhaseTransition(보류 해제 시) | 플레이어 반격 구간 |
| **Staggered** | 스태거 게이지 만탱(100) | 그로기 모션(흰점멸+무릎), **치명타 가능 플래그 on**, groggyTimer(4s) | Recover(**groggyTimer 종료** OR **치명타 피격**) | **탈출 필수(타이머)**. 종료 시 스태거 0 리셋 |
| **PhaseTransition** | HP 임계(50%) 도달 **AND 전환 게이트 통과(§3.2)** | **무적 on(Hurtbox/Resolver Immune) + 슈퍼아머**, 후퇴 2m, 포효, 패턴셋 교체, **스태거 0 리셋** | Approach(연출 1.5s 종료, 무적 off) | 1회성 |

### 3.1 스태거→그로기→치명타 흐름
- `Posture.AddStagger`로 만탱(100) → `OnGroggyEnter` 방송 → BossBrain이 **현재 어느 상태든 Staggered로 전이**(Attack/Recover/Approach 우선 인터럽트). PhaseTransition·이미 Staggered 중이면 무시.
- groggyTimer(4s) 내 플레이어 치명타 입력(2.5m) → `CriticalAttackSystem`이 보스에 고정150 DamageInfo(type=특수, Resolver가 무조건 적용) → Staggered 탈출 → Recover. 미사용 시 4s 후 Recover + **스태거 0 리셋**.

### 3.2 난점 #2 — 그로기/치명타 vs 페이즈 전환 충돌 해소 (상태 우선순위 확정)

**규칙: "연출 우선 완료 후 전환." 그로기 중 전환 무적 진입 금지(치명타 보상 박탈 방지).**

전환은 즉시 일어나지 않고 **게이트를 통과해야** 한다. `BossPhaseManager.RequestPhaseTransition()`는 다음을 검사:

```
HP가 50% 이하로 떨어짐 (어떤 데미지로든. 치명타 데미지 포함)
 │
 ▼ pendingPhaseTransition = true  (즉시 전환 아님 — "예약")
 │
 매 Tick에서 게이트 검사:
 ├─ currentState == Staggered (그로기 중)         → 보류. groggyTimer/치명타로 Staggered 탈출까지 대기
 ├─ 플레이어 CriticalAttackSystem.IsPlaying (치명타 연출 중) → 보류. 연출 종료까지 대기
 ├─ currentState == Attack 이고 Hitbox 활성 중      → 현재 타 종료(AE_HitboxOff)까지 보류(어중간한 캔슬 방지, 권장)
 └─ 그 외(Approach/Recover/Idle, 안전 시점)         → PhaseTransition 진입, pending=false
```

- **상태 우선순위(높음→낮음):** `Dead > PhaseTransition(진입 후) > Staggered/그로기 > 치명타 연출 > Attack(판정 중) > Recover/Approach/Idle`.
  → 단 **PhaseTransition은 "진입 후"에만 최상위.** 진입 전(pending)에는 Staggered/치명타가 우선 → 연출이 먼저 끝난다. 이게 designer 명세의 핵심.
- **결과:** 치명타로 HP가 50% 아래로 내려가도 → 치명타 연출(1.5s) 완료 → (이미 Staggered였으면 그로기도 정리) → 그 다음 PhaseTransition 진입(무적1.5s). **치명타 보상 정상 지급 후 전환.** designer 엣지케이스 그대로 구현.
- **상호배타 보장:** PhaseTransition 진입 순간 무적 on → 그 시점부터 들어오는 피격은 Resolver가 Immune 처리(데미지0). 그로기와 무적이 동시에 켜지는 구간은 게이트로 원천 차단.

### 3.3 슈퍼아머 규칙(Attack 중 피격)
- 보스는 Attack 모션 중 기본 **슈퍼아머**(경직 없음, 포이즈만 누적). 단 PG로 누적된 스태거가 만탱이면 Staggered로 인터럽트(§3.1). 즉 보스는 평타 피격엔 안 흔들리고 **스태거 만탱에만 무너진다** → designer "PG로 버텨 무너뜨린다" 루프 보장.

### 3.4 보스 패턴 선택 로직 (Attack OnEnter)
```
SelectPattern(dist, phase, lastPattern):
  후보 = phase.patterns 중 (minRange<=dist<=maxRange) AND (쿨다운0) AND (!=lastPattern)
  선택 = selectionWeight 가중 랜덤
  - 근거리: #1 횡베기 / #3 방패밀치기 / (#2 내려찍기) / P2: #6 잡기·#8 광분4연
  - 중거리: #4 돌진베기
  - P2: #7 화염사선, #1→#7 캔슬 연계
  직전 패턴 제외로 단조로움 방지. 후보 없으면 Approach 복귀.
```

---

## 4. 데드락 검증 섹션 (필수)

### 4.1 플레이어 — 모든 비종단 상태 탈출 보장

| 상태 | 탈출 수단 | 보장 근거 | 데드락 위험 | 차단책 |
|------|-----------|-----------|-------------|--------|
| Idle/Move | 입력/피격 | 항상 입력 가능 | 없음 | — |
| Dodge | 모션종료 타이머 / `AE_AttackEnd` | 시간 기반 | AE 누락 시 멈춤 | **타이머 폴백 필수**(AE+시간 이중) |
| Attack | `AE_AttackEnd` 또는 모션길이 타이머 | 시간 기반 | AE 누락 | **타이머 폴백 필수** |
| Guard | 가드해제 입력 / 피격분기 | 입력 항상 가능 | 없음(입력으로 항상 탈출) | — |
| PerfectGuard | 후딜4f 타이머 → Guard/Idle | 시간 기반 | — | 타이머 |
| **Hit** | 리액션 종료 타이머/`AE_HitReactEnd` | **시간 기반** | **AE 누락 시 영구 경직** | **타이머 폴백 필수(예: 모션길이+여유)** |
| **Staggered** | staggerTimer(30f/90f) | **순수 타이머** | 타이머 미설정 시 영구 | **OnEnter에서 타이머 강제 설정** |
| Dead | 없음(종단) | — | 정상 | — |

- **i-frame 누수 차단:** Dodge.OnExit에서 Hurtbox.enabled=true 강제 복구 → i-frame off AE 누락돼도 무적이 영구 남지 않음.
- **회피 중 피격 무시 확인:** Dodge i-frame 구간 Hurtbox.enabled=false → OnTriggerEnter 미발생 → Resolve 미호출. 정상.

### 4.2 보스 — 모든 비종단 상태 탈출 보장

| 상태 | 탈출 수단 | 데드락 위험 | 차단책 |
|------|-----------|-------------|--------|
| Idle/Approach | 거리/감지 조건 | 플레이어 사망 시 멈춤 | 플레이어 Dead 감지 → Idle 유지(정상, 전투종료) |
| Attack | `AE_PatternEnd`/모션 타이머 / 인터럽트(Staggered/Phase) | **AE 누락 시 멈춤**, 플레이어 사망으로 패턴 중단 | **타이머 폴백 필수**. 패턴 끊겨도 Recover로 정리(OnExit에서 Hitbox.Disable) |
| Recover | recoverTime 타이머 | 미설정 | OnEnter 타이머 강제 |
| **Staggered** | groggyTimer(4s) **OR** 치명타 | **치명타 안 와도** | **groggyTimer가 항상 탈출**(치명타는 조기탈출일 뿐) |
| **PhaseTransition** | 연출 1.5s 타이머 | **연출 중 상태 꼬임** | **무적+입력무시로 격리**, 1.5s 타이머가 항상 종료 → Approach |
| Dead | 종단 | — | — |

### 4.3 교차(플레이어↔보스) 데드락 시나리오 점검

1. **그로기 중 페이즈 임계 도달:** §3.2 게이트가 보류 → 그로기 정리 후 전환. **동시 진입 불가**(상호배타). ✔
2. **치명타 연출 중 보스가 또 공격?** 치명타 연출 1.5s는 플레이어 무적 + 보스는 Staggered(그로기, 무방비) → 보스 공격 불가. 모순 없음. ✔
3. **PhaseTransition 무적 중 플레이어 공격:** Resolver Immune → 데미지0(designer "통과"). 보스 스태거도 안 쌓임(만탱 인터럽트 불가) → 무적 중 그로기 진입 불가. ✔
4. **잡기 다운(90f) 중 페이즈 전환?** 보스 PhaseTransition은 보스 상태, 플레이어 Staggered(다운)는 플레이어 상태 — 독립. 각자 타이머로 탈출. 교착 없음. ✔
5. **양쪽 동시 사망 가능성:** 보스 치명타 데미지로 보스 HP 0 + 플레이어 DoT로 0 → 각자 Dead(종단). 게임오버/클리어 우선순위는 매니저가 판정(범위 외, MVP는 보스 Dead=클리어 우선). ✔

> **공통 안전수칙(engineer 필수 준수):** 모든 "모션 종료" 탈출은 **Animation Event + 시간 타이머 이중화**. AE만 의존하면 클립 교체/누락 시 영구 경직 → 데드락. OnEnter에서 항상 폴백 타이머 설정.
