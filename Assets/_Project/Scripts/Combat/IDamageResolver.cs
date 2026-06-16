namespace Project.Combat
{
    /// <summary>
    /// 들어온 공격을 actor의 규칙(플레이어 가드/PG/회피 · 보스 무적/슈퍼아머)으로 판정하는 계약.
    /// 구현체 = PlayerDamageResolver(M4)·BossDamageResolver(M5). M2-A는 계약만.
    /// in 파라미터: 13필드 struct 복사 회피 + 원본 수정 불가를 컴파일러가 보장.
    /// </summary>
    public interface IDamageResolver
    {
        /// <summary>들어온 공격을 이 actor의 규칙으로 판정해 결과를 돌려준다.</summary>
        DamageResult Resolve(in DamageInfo info);
    }
}
