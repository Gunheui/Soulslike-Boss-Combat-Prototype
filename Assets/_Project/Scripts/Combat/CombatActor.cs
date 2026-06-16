using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// 전투 actor의 루트 식별 + 하위 시스템 진입점(플레이어/보스 공통). 락온은 보스를 이 타입으로만 다룬다.
    /// Hitbox/Hurtbox가 닿을 Animator·판정기·체력을 Awake에서 1회 캐시하며, 결합은 전부 인터페이스로만 한다.
    /// ⚠ Stamina/Posture/Health 등 concrete 접근자는 여기 두지 않는다 — 해당 타입이 생기는 M2-B에서 추가(지금 참조하면 컴파일 깨짐).
    /// </summary>
    public class CombatActor : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Player;

        // 하위 시스템 참조 캐시 — Awake에서 1회 수집. 모델이 자식에 붙는 흔한 셋업이라
        // Animator는 GetComponentInChildren, 나머지는 actor 루트에서 GetComponent.
        private Animator animator;
        private IDamageResolver resolver;
        private IDamageable damageable;

        /// <summary>이 actor의 진영. Hitbox 동팀 무시·LockOn 적 선별에 사용.</summary>
        public Team Team => team;

        /// <summary>모션 구동·Animation Event 소스. 자식 메시에 붙어 있을 수 있어 children에서 탐색.</summary>
        public Animator Animator => animator;

        /// <summary>이 actor의 데미지 판정기(플레이어 가드/PG · 보스 무적/슈퍼아머). 구현체는 M4/M5.</summary>
        public IDamageResolver Resolver => resolver;

        /// <summary>이 actor의 체력 적용 대상. 구현체 Health는 M2-B.</summary>
        public IDamageable Damageable => damageable;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            resolver = GetComponent<IDamageResolver>();
            damageable = GetComponent<IDamageable>();

            // 캐시가 null이어도 LogError를 던지지 않는다 = M2-A 시점엔 Resolver/Damageable
            // 구현체가 아직 없어 null이 "정상"이다. Animator도 셋업 전 프리팹엔 없을 수 있다.
            // 실제 채워짐: Animator(프리팹 셋업) · Damageable(M2-B Health) · Resolver(M4/M5).
            // null 가드(필수 의존 검증)는 그 시스템들이 생기는 마일스톤에서 각자 추가한다.
        }
    }
}
