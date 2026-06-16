using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 애니메이션 이벤트(AE) 릴레이. AnimationEvent는 Animator와 같은 GameObject의 컴포넌트만 부를 수 있어,
    /// 모델 자식(Animator 보유)에 붙어 루트 <see cref="PlayerIFrame"/>로 포워딩한다.
    /// 불변식: 이 릴레이는 무적을 강제 해제(닫기)만 하고 다시 열지 않는다 — 타이머 권한(<see cref="DodgeState"/>)과 충돌하지 않는 안전망.
    /// </summary>
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        [Tooltip("포워딩 대상. 보통 루트(Player_PlaceHolder)의 PlayerIFrame. 비우면 부모에서 탐색.")]
        [SerializeField] private PlayerIFrame iframe;

        private void Awake()
        {
            if (iframe == null) iframe = GetComponentInParent<PlayerIFrame>();

            if (iframe == null)
                Debug.LogError("[PlayerAnimationEventRelay] PlayerIFrame 미배선 — AE 안전망이 동작하지 않는다. 루트의 PlayerIFrame을 슬롯에 배선하라.", this);
        }

        /// <summary>
        /// 무적 강제 해제(단방향 닫기). Dodge 클립 회복 프레임의 AnimationEvent로 호출.
        /// 무적창을 열지 않으므로 타이머 권한과 충돌하지 않는다.
        /// </summary>
        public void AE_ForceVulnerable()
        {
            if (iframe != null) iframe.ForceVulnerable();
        }
    }
}
