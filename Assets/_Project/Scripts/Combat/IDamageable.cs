namespace Project.Combat
{
    /// <summary>
    /// 판정 결과(<see cref="DamageResult"/>)를 실제 체력 숫자에 반영하는 적용 계약.
    ///
    /// 왜 Resolve(판정)와 ApplyDamage(적용)를 분리하나 = 두 책임이 본질적으로 다르기
    /// 때문이다. Resolve는 "이 공격을 어떻게 처리할지 결정"하고(가드인가·PG인가·무적인가),
    /// ApplyDamage는 그 결정에 따라 "체력 숫자를 실제로 바꾼다"(차감·회색 적립·회복).
    /// 하나로 합치면, 체력을 가진 컴포넌트가 가드/회피 규칙까지 떠안아 비대해지고,
    /// 가드 규칙을 바꿀 때마다 체력 코드를 건드려야 한다. 둘을 나누면 Resolver(어떻게
    /// 처리)와 Health(숫자 변경)가 독립적으로 진화한다. ApplyDamage가 DamageInfo가
    /// 아니라 이미 판정된 DamageResult를 받는 것이 이 분리의 핵심 — 체력은 "어떻게
    /// 막았는지"를 다시 따지지 않고, 결정된 결과만 숫자에 반영한다(damage-pipeline §4).
    ///
    /// 구현체 = Health(M2-B, data-model §5.1). 회색 체력 3분기(실데미지/칩적립/회복)를
    /// outcome에 따라 가른다. M2-A에서는 계약만 정의하고 Health 본체는 만들지 않는다.
    ///
    /// 왜 in 파라미터인가 = <see cref="IDamageResolver"/>와 동일 — struct 복사 회피 +
    /// 읽기 전용 보장.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>판정된 결과를 체력에 반영한다(차감·회색 적립·회복 분기).</summary>
        void ApplyDamage(in DamageResult result);

        /// <summary>사망 여부. FSM의 Dead 전이·타겟 해제 등에서 조회.</summary>
        bool IsDead { get; }
    }
}
