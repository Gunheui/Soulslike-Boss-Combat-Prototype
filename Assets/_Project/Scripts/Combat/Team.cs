namespace Project.Combat
{
    /// <summary>
    /// 전투 진영. 자가/아군 피해 차단과 락온 타겟 식별의 기준.
    ///
    /// 왜 enum인가 = 진영은 닫힌 2값 집합(플레이어 vs 보스)이라 확장 여지가 없고,
    /// Hitbox의 동팀 무시·LockOn의 적 선별이 분기 1회로 끝나기 때문. 보스 1체 MVP엔
    /// 파벌(Faction) 시스템 같은 과설계가 불필요하다.
    /// </summary>
    public enum Team
    {
        Player,
        Enemy
    }
}
