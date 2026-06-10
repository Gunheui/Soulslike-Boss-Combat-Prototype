using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 회피 무적(i-frame)의 <b>명시적 소유자</b>. "지금 무적인가"를 한곳에서 켜고 끈다.
    ///
    /// 설계상 i-frame은 데미지 계산을 막는 게 아니라 <b>Hurtbox 콜라이더 자체를 비활성</b>해서
    /// 구현한다(state-machines §2.1 [0]). 콜라이더가 꺼지면 보스 Hitbox의 OnTriggerEnter가
    /// 애초에 안 불려 Resolve 단계까지 가지도 않는다 — 가장 단순하고 확실한 무적.
    ///
    /// ⚠ M1-D 시점엔 실 Hurtbox(M2 T2.6)가 없어 <see cref="hurtboxCollider"/>는 플레이스홀더
    /// Trigger 콜라이더(Player_Placeholder 하위 Hurtbox child)를 가리킨다. M2에서 실 Hurtbox가
    /// 같은 콜라이더에 얹히면 이 토글 메커니즘이 그대로 승계된다(코드 변경 불필요).
    ///
    /// 누수 방지 계약: <see cref="ForceVulnerable"/>를 Dodge.OnExit에서 무조건 호출 → AE 누락/상태
    /// 강제이탈이 있어도 무적이 영구 잔류하지 않는다(영구 무적 버그 차단의 최종 방어선).
    /// </summary>
    public class PlayerIFrame : MonoBehaviour
    {
        [Tooltip("무적 중 비활성화할 Hurtbox 콜라이더. M1-D는 플레이스홀더, M2는 실 Hurtbox 콜라이더.")]
        [SerializeField] private Collider hurtboxCollider;

        // 인스펙터에서 무적 구간을 눈으로 확인하기 위한 디버그 미러(읽기 전용 노출).
        [SerializeField, Tooltip("디버그 표시용 — 현재 무적 여부. 토글 주체는 코드(SetInvulnerable).")]
        private bool isInvulnerable;

        /// <summary>현재 무적인가(= Hurtbox 콜라이더가 꺼져 있는가).</summary>
        public bool IsInvulnerable => isInvulnerable;

        private void Awake()
        {
            // 시작은 항상 피격 가능 상태로 정규화(씬에서 콜라이더가 꺼진 채 저장됐어도 복구).
            ForceVulnerable();
        }

        /// <summary>
        /// 무적 on/off. on이면 Hurtbox 콜라이더를 끈다(피격 미수신), off면 켠다.
        /// Dodge.Tick에서 매 프레임 타이밍에 맞춰 호출(idempotent).
        /// </summary>
        public void SetInvulnerable(bool value)
        {
            isInvulnerable = value;
            if (hurtboxCollider != null)
                hurtboxCollider.enabled = !value;   // 무적 = 콜라이더 비활성
        }

        /// <summary>
        /// 무적 강제 해제 + 콜라이더 복구. Dodge.OnExit에서 무조건 호출 — i-frame 누수 0의 보증.
        /// </summary>
        public void ForceVulnerable() => SetInvulnerable(false);
    }
}
