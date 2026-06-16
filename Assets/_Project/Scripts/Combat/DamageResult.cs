namespace Project.Combat
{
    /// <summary>
    /// 한 타격이 "어떻게 처리됐나"의 최종 판정. Resolver가 결정해 방어자 → 공격자/체력으로 흐른다.
    ///
    /// 왜 enum인가 = 처리 결과는 7가지 닫힌 상태이고, 이 한 값이 이후 모든 후처리의
    /// 분기 기준이 된다 — Health는 outcome으로 데미지/칩/회복 3분기를 가르고
    /// (damage-pipeline §4), FSM은 outcome으로 다음 상태(Hit/Staggered 등)를 정한다.
    /// </summary>
    public enum DamageOutcome
    {
        PerfectGuard, // 퍼펙트가드 — 데미지0·칩0, 적 스태거 적립
        Blocked,      // 일반가드 — 칩 데미지(회색 적립), 게이지 소모
        GuardBreak,   // 가드브레이크 — 게이지 고갈, 풀데미지 + 경직
        Dodged,       // 회피 — i-frame으로 무판정(사실상 Resolve 진입 전 컷)
        Hit,          // 피격 — 풀데미지
        Grabbed,      // 포박 — 가드 무효, 다운
        Immune        // 무적 — 데미지0·스태거0(보스 페이즈 전환 등)
    }

    /// <summary>
    /// 방어자(Resolver)가 공격에 어떻게 응답했는지를 담아 역방향으로 돌려주는 결과 계약.
    ///
    /// 왜 결과를 별도 struct로 역류시키나 = 입력(<see cref="DamageInfo"/>)과 출력을 분리하기 위해서다.
    /// DamageInfo는 공격자의 의도(때리려는 양)일 뿐, 실제로 얼마가 들어갔는지는 방어자의
    /// 상태(가드 중인가·PG 윈도우인가·무적인가)에 따라 전혀 달라진다. 그 "실제 결과"를
    /// DamageInfo에 덮어쓰면 원본이 오염되고 흐름이 양방향이 돼 추적이 어려워진다. 그래서
    /// 깨끗한 입력은 그대로 두고, 별도의 작은 응답 struct를 새로 만들어 돌려준다
    /// (damage-pipeline §1·§2의 단방향 원칙). struct인 이유는 DamageInfo와 동일 —
    /// 일회성 값 스냅샷, 복사 안전, GC 압박 0.
    ///
    /// 소비 위치 = Health.ApplyDamage(이 result로 3분기), FSM(outcome→상태 전이),
    /// Hurtbox 방송(Feel/UI).
    /// </summary>
    public struct DamageResult
    {
        public DamageOutcome outcome; // 처리 결과 — 이후 모든 후처리 분기의 기준
        public float finalDamage;     // 실제 적용된 HP 데미지
        public float chipDamage;      // 회색으로 들어간 칩 데미지(일반가드)
        public float recoverableAdded;// 이번 타격으로 회색 영역에 적립된 양
        public bool staggeredTarget;  // 이 피격으로 상대가 스태거됐는지
    }
}
