using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 회피(Dodge) 상태. 가드불가 공격에 대응하는 비상 동사이자 i-frame(무적)의 주체.
    ///
    /// <b>타이머 단독 구동</b>(M1-D): 애니 클립이 없어 AE(AE_IFrameOn/Off·AE_AttackEnd)를 쏠
    /// Animator가 아직 없다. 설계의 "AE + 타이머 이중화" 중 안전한 타이머 arm만 켜고 진행한다.
    /// M1-F에서 클립이 들어오면 AE가 무적 토글/종료를 조기-컷으로 앞당기고, 이 타이머는 폴백으로
    /// 잔존한다(AE 누락 시에도 데드락/무적 누수 0).
    ///
    /// 책임 분리: 이 상태는 "언제 무적이고 언제 끝나는가"(타이밍)만 판정하고, 무적 토글은
    /// <see cref="PlayerIFrame"/>에, 이동 물리는 <see cref="PlayerLocomotion"/>에 위임한다.
    /// </summary>
    public class DodgeState : IPlayerState
    {
        private readonly PlayerStateMachine _sm;
        private readonly PlayerInputReader _input;
        private readonly PlayerLocomotion _loco;
        private readonly PlayerIFrame _iframe;
        private readonly DodgeConfig _config;

        // 회피 1회의 진행 상태(OnEnter에서 리셋).
        private float _elapsed;          // 입력 후 경과(초)
        private Vector3 _dodgeDir;       // 고정 이동 방향(월드)
        private bool _rotateToDir;       // 방향 구르기(true) vs 백스텝(false)

        public DodgeState(PlayerStateMachine sm, PlayerInputReader input,
                          PlayerLocomotion loco, PlayerIFrame iframe, DodgeConfig config)
        {
            _sm = sm;
            _input = input;
            _loco = loco;
            _iframe = iframe;
            _config = config;
        }

        public void OnEnter()
        {
            Debug.Log("[FSM] → Dodge");
            _elapsed = 0f;

            // 방향 산출: 이동 입력이 있으면 그 방향으로 구르고(그 방향을 바라봄),
            // 입력이 없으면 바라보는 방향 유지한 채 뒤로 백스텝.
            Vector2 mv = _input.MoveIntent;
            if (mv.sqrMagnitude > 0.01f)
            {
                _dodgeDir = _loco.CameraRelative(mv);
                _rotateToDir = true;                  // 방향 구르기
            }
            else
            {
                _dodgeDir = -_sm.transform.forward;   // 중립 백스텝
                _rotateToDir = false;
            }

            // TODO(M2 T2.4): 스태미나 -25 소모 + 진입조건 ≥25. 지금은 무한 회피(Stamina 미존재).

            // 무적은 startup(3f) 이후부터 — OnEnter 시점(t=0)은 아직 무적 아님. 명시적으로 보장.
            _iframe.SetInvulnerable(_config.timing.IsInvulnerable(0f));
        }

        public void Tick()
        {
            _elapsed += Time.deltaTime;

            // 1) 무적 토글 — 매 프레임 타이밍에 맞춰 동기화(idempotent).
            _iframe.SetInvulnerable(_config.timing.IsInvulnerable(_elapsed));

            // 2) 이동 — 구동 구간은 회피 이동, 이후 후딜은 정지(중력만).
            //    후딜 9f 동안 신규 입력은 읽지 않는다(= 입력 잠금). 회피 전 구간 새 전이 없음.
            if (_config.IsMoving(_elapsed))
                _loco.DodgeMove(_dodgeDir, _config.SpeedAt(_elapsed), _rotateToDir);  // ease-out 감속
            else
                _loco.Stop();

            // 3) 모션 종료 → 다음 상태. 이동 입력이 남아 있으면 Move, 아니면 Idle.
            if (_config.timing.IsComplete(_elapsed))
            {
                if (_input.MoveIntent.sqrMagnitude > 0.01f)
                    _sm.ToMove();
                else
                    _sm.ToIdle();
            }
        }

        public void OnExit()
        {
            // i-frame 누수 0의 최종 방어선 — 무적 강제 해제 + Hurtbox 콜라이더 복구.
            // 타이밍이 어긋나거나 외부에서 강제로 상태를 바꿔도 무적이 영구 잔류하지 않는다.
            _iframe.ForceVulnerable();
            _loco.Stop();   // 잔류 수평 속도 차단

            // 회피 도중 큐잉됐을 수 있는 회피 입력을 흘려보내 후딜 직후 자동 재회피를 막는다.
            _input.ConsumeDodge();
        }
    }
}
