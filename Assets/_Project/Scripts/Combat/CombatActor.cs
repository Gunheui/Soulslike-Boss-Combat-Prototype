using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// 전투 actor의 루트 식별 + 하위 시스템 진입점. 플레이어/보스 공통.
    ///
    /// 왜 이 컴포넌트가 경계의 핵심인가 = Player 어셈블리는 Boss 어셈블리를 import하지 않는다
    /// (단방향 의존: Player → Combat, Boss → Combat). 그런데 락온은 보스를 가리켜야 한다.
    /// 그래서 LockOn은 보스를 "Boss 타입"이 아니라 Combat이 정의한 <see cref="CombatActor"/>로만
    /// 다룬다 → Player가 Boss를 몰라도 보스를 타겟할 수 있다. 경계를 깨지 않는 의존성 역전.
    ///
    /// M2 확장(여기) = Hitbox/Hurtbox가 명중 시 "이 actor의 Animator·판정기·체력"에 닿아야 하는데,
    /// 매번 GetComponent를 호출하면 비싸고 호출처가 actor 내부 구조를 알게 된다. 그래서
    /// CombatActor가 Awake에서 한 번 캐시해, actor의 하위 시스템에 접근하는 단일 진입점이 된다.
    /// 결합은 전부 인터페이스(<see cref="IDamageResolver"/>/<see cref="IDamageable"/>)로만 — 판정
    /// 바디가 플레이어·보스 concrete 타입을 모르게 해 재사용을 지킨다(damage-pipeline §2).
    ///
    /// ⚠ Stamina/Posture/Health 같은 concrete 타입 접근자는 여기 두지 않는다 = M2-A 시점엔
    /// 그 컴포넌트들이 아직 없어(참조하면 컴파일이 깨진다) + 과확장 금지. 이 actor가 외부에
    /// 노출할 필요가 있는 concrete 접근자(예: Stamina/Posture)는 M2-B에서 그 타입들이
    /// 생긴 뒤 추가한다. 지금은 인터페이스 캐시까지만.
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
            // Unity의 GetComponent는 인터페이스 조회를 지원한다 → concrete 타입을 몰라도 캐시 가능.
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
