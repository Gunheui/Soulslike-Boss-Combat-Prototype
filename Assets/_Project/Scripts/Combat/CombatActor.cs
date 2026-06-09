using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// 전투 actor의 루트 식별 컴포넌트. 플레이어/보스 공통.
    ///
    /// 왜 이 컴포넌트가 경계의 핵심인가 = Player 어셈블리는 Boss 어셈블리를 import하지 않는다
    /// (단방향 의존: Player → Combat, Boss → Combat). 그런데 락온은 보스를 가리켜야 한다.
    /// 그래서 LockOn은 보스를 "Boss 타입"이 아니라 Combat이 정의한 <see cref="CombatActor"/>로만
    /// 다룬다 → Player가 Boss를 몰라도 보스를 타겟할 수 있다. 경계를 깨지 않는 의존성 역전.
    ///
    /// ⚠ M1 최소판: 지금은 Team 식별만 한다. M2 T2.2에서 Health/Stamina/Posture/Animator
    /// 참조 캐시를 이 컴포넌트에 추가해, Hitbox/Hurtbox가 actor의 하위 시스템에 접근하는
    /// 단일 진입점이 된다. 지금 그 필드를 넣지 않는 이유 = 과확장 금지(해당 컴포넌트들이
    /// 아직 없음 — DoD가 요구하는 멤버만 둔다).
    /// </summary>
    public class CombatActor : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Player;

        /// <summary>이 actor의 진영. Hitbox 동팀 무시·LockOn 적 선별에 사용.</summary>
        public Team Team => team;
    }
}
