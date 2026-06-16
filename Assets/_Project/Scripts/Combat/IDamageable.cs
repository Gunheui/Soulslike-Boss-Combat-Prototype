namespace Project.Combat
{
    /// <summary>
    /// 판정 결과(<see cref="DamageResult"/>)를 실제 체력 숫자에 반영하는 적용 계약.
    /// ApplyDamage는 DamageInfo가 아니라 판정된 DamageResult를 받는다 — 체력은 결정된 결과만 반영(§4).
    /// 구현체 = Health(M2-B). M2-A는 계약만.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>판정된 결과를 체력에 반영한다(차감·회색 적립·회복 분기).</summary>
        void ApplyDamage(in DamageResult result);

        /// <summary>사망 여부. FSM의 Dead 전이·타겟 해제 등에서 조회.</summary>
        bool IsDead { get; }
    }
}
