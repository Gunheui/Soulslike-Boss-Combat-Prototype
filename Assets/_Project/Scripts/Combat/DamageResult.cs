namespace Project.Combat
{
    /// <summary>한 타격의 처리 결과. 이후 후처리 분기의 기준 — Health의 데미지/칩/회복 3분기, FSM의 다음 상태 전이(damage-pipeline §4).</summary>
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
    /// 입력(<see cref="DamageInfo"/>)과 분리해 원본 오염을 막는다(damage-pipeline §1·§2 단방향).
    /// 소비 = Health.ApplyDamage(3분기)·FSM(outcome→전이)·Hurtbox 방송(Feel/UI).
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
