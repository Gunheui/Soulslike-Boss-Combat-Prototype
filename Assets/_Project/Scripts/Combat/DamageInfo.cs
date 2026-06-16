using UnityEngine;

namespace Project.Combat
{
    /// <summary>공격의 성질. Resolver가 가장 먼저 읽어 PG·일반가드 유효 여부를 가르는 분기 키(damage-pipeline §3.1, 난점 #5).</summary>
    public enum DamageType
    {
        Normal,      // 일반 베기 — 가드/PG 모두 유효
        Unblockable, // 가드불가(적색) — 막아도 관통, PG 무효
        Grab         // 포박 잡기 — 가드/PG 무효, 다운
    }

    /// <summary>
    /// 한 타격의 모든 정보를 담아 공격자 → 방어자로 흐르는 단방향 입력 계약.
    /// 처리 결과는 <see cref="DamageResult"/>로 따로 역류한다(입력·출력 분리, damage-pipeline §1·§2).
    /// 생성 = Hitbox가 명중 시 AttackData(밸런스) + 런타임 source 정보를 합쳐 만든다.
    /// </summary>
    public struct DamageInfo
    {
        public float amount;             // HP 데미지
        public float poiseDamage;        // 경직 누적(= 플레이어 일반가드 게이지 소모량)
        public float staggerDamage;      // 상대 스태거 게이지 적립량
        public DamageType type;          // Normal / Unblockable / Grab — 가드 무효화 분기 키(난점 #5)
        public bool isPerfectGuardable;  // PG 가능 여부(가드불가 공격은 false)
        public Vector3 sourcePos;        // 공격 출처 위치 — 넉백 방향·측면 판정용
        public GameObject source;        // 공격 주체 오브젝트 — 자가 피해 무시 식별
        public Team sourceTeam;          // 공격자 진영 — 동팀 피해 무시 식별
        public float knockback;          // 넉백 거리(m)
        public float hitstunFrames;      // 명중 시 상대에게 줄 경직(f)
        public float dotPerSec;          // 지속 피해(화상 등, 없으면 0)
        public float dotDuration;        // 지속 피해 지속 시간(s)
        public bool isCritical;          // 치명타 여부 — 보스 무적/슈퍼아머를 관통해 고정150 적용(damage-pipeline §3.2 ②)
    }
}
