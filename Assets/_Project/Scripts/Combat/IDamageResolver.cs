namespace Project.Combat
{
    /// <summary>
    /// "이 공격을 어떻게 막았나/피했나/맞았나"를 actor 종류별로 격리하는 판정 계약.
    ///
    /// 왜 인터페이스인가 = Hitbox/Hurtbox는 전투의 "판정 바디"이고, 플레이어든 보스든
    /// 똑같이 재사용돼야 한다. 그런데 처리 규칙은 actor마다 전혀 다르다 — 플레이어는
    /// 가드·퍼펙트가드·회피로 분기하고, 보스는 무적·슈퍼아머로 분기한다. 이걸 Hurtbox
    /// 안에 if(플레이어)/else(보스)로 박으면, 판정 바디가 actor 종류를 알게 돼 재사용이
    /// 깨진다. 그래서 "Resolve 한 줄"만 인터페이스로 두고, Hurtbox는 소유 actor의
    /// IDamageResolver에게 위임만 한다. Hurtbox는 "맞았다"만 알고, "어떻게 처리할지"는
    /// 구현체가 안다(damage-pipeline §2 핵심 분리 · §3).
    ///
    /// 구현체 = PlayerDamageResolver(M4) · BossDamageResolver(M5). M2-A에서는 계약만
    /// 깔고 구현체는 만들지 않는다(과확장 금지 — 해당 시스템 로직은 후속 마일스톤).
    ///
    /// 왜 in 파라미터인가 = DamageInfo는 13필드 struct라 값 복사 비용이 적지 않다.
    /// in으로 받으면 읽기 전용 참조로 넘어가 복사를 피하면서도, Resolve가 원본을
    /// 수정할 수 없음을 컴파일러가 보장한다(단방향 계약을 타입 레벨로 강제).
    /// </summary>
    public interface IDamageResolver
    {
        /// <summary>들어온 공격을 이 actor의 규칙으로 판정해 결과를 돌려준다.</summary>
        DamageResult Resolve(in DamageInfo info);
    }
}
