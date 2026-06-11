using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 애니메이션 이벤트(AE) 릴레이. AnimationEvent는 <b>Animator와 같은 GameObject</b>의 컴포넌트
    /// 함수만 부를 수 있어, 모델 자식(Animator 보유)에 붙어 루트 <see cref="PlayerIFrame"/>로 포워딩한다.
    ///
    /// <b>A안 — i-frame 이중화의 안전망(단방향).</b> 무적창 on/off 권한은 <see cref="DodgeState"/>의
    /// 타이머(<see cref="DodgeTiming"/> 3f~14f, EditMode 검증)가 갖는다. 이 릴레이가 하는 일은
    /// <b>강제 해제(닫기)뿐</b> — 무적창을 다시 열지 않으므로 타이머와 충돌하지 않는다. Dodge 클립의
    /// 회복 프레임에 <see cref="AE_ForceVulnerable"/> 이벤트를 심어, 타이머가 어떤 이유로 OnExit를
    /// 못 타도 무적이 잔류하지 않게 하는 마지막 보강(무적 누수 0의 3차 방어선).
    ///
    /// 방어선 순서: 1차 <c>DodgeState.OnExit → ForceVulnerable</c> / 2차 타이머 IsComplete /
    /// 3차 이 AE. 셋 다 같은 방향(닫기)이라 안전하게 중첩된다.
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
