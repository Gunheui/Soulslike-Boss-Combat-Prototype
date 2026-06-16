using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// 공격의 성질. 가드 시스템이 "막을 수 있나"를 가장 먼저 가르는 분기 키.
    ///
    /// 왜 enum인가 = 공격 처리의 최상위 갈림(Normal/Unblockable/Grab)은 닫힌 집합이고,
    /// Resolver가 이 한 값을 가장 먼저 읽어 PG·일반가드를 통째로 무효화하기 때문
    /// (damage-pipeline §3.1 ①②, 난점 #5). bool 두 개로 흩뿌리면 "막을 수 없는데
    /// 잡기는 아닌" 같은 모순 조합이 생기지만, enum은 한 값이라 모순 불가능.
    /// </summary>
    public enum DamageType
    {
        Normal,      // 일반 베기 — 가드/PG 모두 유효
        Unblockable, // 가드불가(적색) — 막아도 관통, PG 무효
        Grab         // 포박 잡기 — 가드/PG 무효, 다운
    }

    /// <summary>
    /// 한 타격의 모든 정보를 담아 공격자 → 방어자로 흐르는 단방향 계약.
    ///
    /// 왜 struct(값타입)인가 = DamageInfo 하나는 "한 타격의 한 스냅샷"이다. Hitbox가
    /// 명중 순간 만들어 Resolver까지 넘기는 일회성 데이터라, 값으로 복사돼도 원본이
    /// 오염될 일이 없고(방어자가 건드려도 공격자 쪽 원본 불변) 힙 할당이 없어 전투 중
    /// 매 타격 GC 압박이 0이다. class였다면 매 명중마다 힙 객체가 생겨 가비지가 쌓인다.
    ///
    /// 왜 단방향인가 = 방어자는 이 struct를 절대 수정하지 않는다. "어떻게 처리했나"는
    /// 별도 <see cref="DamageResult"/>를 만들어 역방향으로 돌려준다(damage-pipeline §1·§2).
    /// 입력(DamageInfo)과 출력(DamageResult)을 분리하면 데이터 흐름이 한 방향이라
    /// 추적이 쉽고, 공격자가 보낸 원본과 방어자의 응답이 절대 섞이지 않는다.
    ///
    /// 생성 위치 = Hitbox가 활성 중 명중 시 AttackData(밸런스 수치) + 런타임 정보
    /// (sourcePos/source/sourceTeam)를 합쳐 만든다(damage-pipeline §1 "AttackData → DamageInfo").
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
