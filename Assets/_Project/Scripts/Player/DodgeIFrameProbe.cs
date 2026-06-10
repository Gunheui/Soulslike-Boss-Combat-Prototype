using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// ⚠ <b>버릴 검증용 프로브</b>(M1-D / T1.5 DoD 라이브 시연 전용). 검증이 끝나면 씬 오브젝트와
    /// 이 스크립트를 함께 제거한다 — 잔류 금지.
    ///
    /// 실 데미지 파이프라인(Hurtbox/Hitbox/Resolver)은 전부 M2다. 그래서 "회피 i-frame이 진짜
    /// 공격을 흘려보내는가"를 지금 눈으로 확인하려고, 가짜 공격 판정으로 쓰는 <b>정지한 Trigger 볼륨</b>이다.
    ///
    /// 원리: i-frame은 플레이어 Hurtbox 콜라이더를 끄는 것(PlayerIFrame)이라, 무적 중엔 Hurtbox가
    /// 이 프로브와 겹쳐도 OnTriggerEnter가 안 불린다 → [HIT] 미카운트. 평상시 통과 = [HIT].
    ///
    /// 셋업: 빈 GameObject + Trigger Collider, 레이어 <b>BossHitbox(8)</b>(Collision Matrix에서 7↔8만
    /// 충돌하도록 M0-A에서 구성), <b>Kinematic Rigidbody</b>(두 Trigger가 콜백을 받으려면 한쪽에 필요).
    /// 플레이어가 이 볼륨을 통과(또는 통과하며 회피)하도록 앞 바닥에 배치.
    /// </summary>
    public class DodgeIFrameProbe : MonoBehaviour
    {
        [Tooltip("이 레이어의 콜라이더만 피격으로 카운트(플레이어 Hurtbox = 7). CharacterController 캡슐 등은 무시.")]
        [SerializeField] private int hurtboxLayer = 7; // PlayerHurtbox (M0-A TagManager)

        private int _hitCount;

        private void OnTriggerEnter(Collider other)
        {
            // Hurtbox 레이어만 집계 — 무적 중엔 Hurtbox 콜라이더가 disabled라 여기 안 들어온다.
            if (other.gameObject.layer != hurtboxLayer) return;

            _hitCount++;
            Debug.Log($"[HIT] 더미 공격 적중 #{_hitCount} — Hurtbox 활성 상태에서 통과 (회피 무적이면 이 로그가 안 떠야 정상)", this);
        }
    }
}
