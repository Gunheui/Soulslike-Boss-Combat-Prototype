using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 로코모션 애니 브리지. <see cref="PlayerLocomotion"/>의 이동 속도를 읽어 Animator 블렌드 트리
    /// 파라미터(MoveX/MoveY/Speed)로 흘려보낸다. 한 방향(velocity → Animator)으로만 읽어가므로
    /// 상태/물리 코드는 Animator를 몰라도 된다(M1-C/D 관심사 분리 연장).
    ///
    /// <b>왜 별도 컴포넌트인가</b> = 이동 물리(Locomotion)와 표현(애니)을 분리하면 애니 시스템을
    /// 갈아끼워도 전투 결정성(i-frame/히트박스 타이밍)이 영향받지 않는다. 브리지는 읽기 전용.
    ///
    /// <b>Root Motion OFF</b> = 클립은 in-place(제자리), 실제 이동은 CharacterController가 한다.
    /// Root Motion을 켜면 애니가 위치를 밀어 전투 이벤트 타이밍이 이동 속도에 끌려간다(소울라이크 금기).
    /// </summary>
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("로코모션 클립을 재생할 모델의 Animator(보통 자식). 비우면 자식에서 탐색.")]
        [SerializeField] private Animator animator;
        [Tooltip("속도 단일 진실원. 비우면 같은 GameObject에서 탐색.")]
        [SerializeField] private PlayerLocomotion loco;

        [Header("블렌드 댐핑")]
        [Tooltip("파라미터 보간 시간(초). 급정거/방향전환 시 애니가 튀지 않게 부드럽게 수렴.")]
        [SerializeField] private float dampTime = 0.1f;

        // 문자열 대신 해시로 SetFloat — 매 프레임 호출이라 문자열 룩업 비용 제거.
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (loco == null) loco = GetComponent<PlayerLocomotion>();

            if (animator == null)
                Debug.LogError("[PlayerAnimationDriver] Animator 미배선 — 모델 자식에 Animator가 있어야 로코모션 애니가 재생된다.", this);
            if (loco == null)
                Debug.LogError("[PlayerAnimationDriver] PlayerLocomotion 미배선 — 속도 소스가 없어 애니가 idle에 고정된다.", this);

            // in-place 클립 전제 — 애니가 위치를 밀지 않게 강제(인스펙터 실수 방어).
            if (animator != null) animator.applyRootMotion = false;
        }

        private void Update()
        {
            if (animator == null || loco == null) return;

            // 월드 속도를 캐릭터 facing 기준 로컬로 변환 → 블렌드 트리 축(전/후/좌/우)에 직접 대응.
            // 프리룩: 진행 방향을 바라보므로 local.z≈속력, local.x≈0(전진축). 락온 strafe 방향성은 F3.
            Vector3 local = transform.InverseTransformDirection(loco.PlanarVelocity);

            animator.SetFloat(MoveXHash, local.x, dampTime, Time.deltaTime);
            animator.SetFloat(MoveYHash, local.z, dampTime, Time.deltaTime);
            animator.SetFloat(SpeedHash, loco.PlanarVelocity.magnitude, dampTime, Time.deltaTime);

            // 회피 중엔 PlanarVelocity가 피크(~15 m/s)라 run으로 튄다 — 전용 Dodge 클립+AE는 F3에서.
        }
    }
}
