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
        private readonly PlayerAnimationDriver _anim;   // 전용 Dodge 클립 트리거(표현만, i-frame은 타이머 권한). null 허용.

        // 회피 1회의 진행 상태(OnEnter에서 리셋).
        private float _elapsed;          // 입력 후 경과(초)
        private Vector3 _dodgeDir;       // 고정 이동 방향(월드)
        private bool _rotateToDir;       // 방향 구르기(true) vs 백스텝(false)
        private bool _carryMomentum;     // 종료 시 Move로 인계하면 true → OnExit에서 Stop() 생략(잔류 속도 보존)
        private DodgeConfig _active;     // 이번 회피의 활성 config(OnEnter에서 sm의 최신값 스냅샷)

        public DodgeState(PlayerStateMachine sm, PlayerInputReader input,
                          PlayerLocomotion loco, PlayerIFrame iframe,
                          PlayerAnimationDriver anim = null)
        {
            _sm = sm;
            _input = input;
            _loco = loco;
            _iframe = iframe;
            _anim = anim;
        }

        public void OnEnter()
        {
            Debug.Log("[FSM] → Dodge");
            _elapsed = 0f;
            _carryMomentum = false;   // 인계 플래그 리셋(상태 인스턴스 재사용 — 직전 회피값 잔존 방지)

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

            // 모션별 타이밍 선택: 이동=구르기(긴 무적 0.4s), 정지=백스텝(짧은 무적 0.1s).
            // sm에서 회피 진입 시점에 읽어 스냅샷 — 런타임 Inspector 튜닝이 다음 회피부터 반영된다.
            _active = _rotateToDir ? _sm.RollConfig : _sm.BackstepConfig;

            // facing 정책 = free-look이면 구르는 방향으로 즉시 스냅(전방 다이브 클립과 facing 일치, 반응성).
            //   락온이면 스냅하지 않는다 → DodgeMove의 RotateToward(720°/s)가 보스→회피방향으로 매 프레임
            //   부드럽게 회전(즉시 90/180° 스냅은 딱딱). 락온 타겟·카메라(보스 조준)는 회전과 무관하게 유지.
            if (_rotateToDir && _loco.LockOnTarget == null)
                _loco.SnapFacing(_dodgeDir);

            // 전용 Dodge 클립 1회 재생 — 회피 중 PlanarVelocity 피크가 로코모션 블렌드를 Run으로
            // 밀던 문제를 덮는다(표현만, 무적 타이밍은 아래 타이머가 권한).
            // 몸이 회피 방향을 향하므로(스냅 또는 RotateToward) 방향 회피=전방 다이브, 중립(입력 없음)=백스텝.
            _anim?.PlayDodge(back: !_rotateToDir);

            // TODO(M2 T2.4): 스태미나 -25 소모 + 진입조건 ≥25. 지금은 무한 회피(Stamina 미존재).

            // 무적은 startup(0.05s) 이후부터 — OnEnter 시점(t=0)은 아직 무적 아님. 명시적으로 보장.
            _iframe.SetInvulnerable(_active.timing.IsInvulnerable(0f));
        }

        public void Tick()
        {
            _elapsed += Time.deltaTime;

            // 1) 무적 토글 — 매 프레임 타이밍에 맞춰 동기화(idempotent).
            _iframe.SetInvulnerable(_active.timing.IsInvulnerable(_elapsed));

            // 2) 이동 — 구동 구간은 회피 이동, 이후 후딜은 정지(중력만).
            //    후딜 동안 신규 입력은 읽지 않는다(= 입력 잠금). 회피 전 구간 새 전이 없음.
            if (_active.IsMoving(_elapsed))
                _loco.DodgeMove(_dodgeDir, _active.SpeedAt(_elapsed), _rotateToDir);  // ease-out 감속
            else
                _loco.Stop();

            // 3) 모션 종료 → 다음 상태. 이동 입력이 남아 있으면 Move, 아니면 Idle.
            if (_active.timing.IsComplete(_elapsed))
            {
                if (_input.MoveIntent.sqrMagnitude > 0.01f)
                {
                    // 방향 입력 유지 → 롤 잔류 속도를 Move로 인계(데드 스톱 제거, 소울 표준 연속 블렌드).
                    // ToMove()가 곧 OnExit를 부르므로 그 전에 플래그를 세워야 Stop()이 생략된다.
                    _carryMomentum = true;
                    _sm.ToMove();
                }
                else
                    _sm.ToIdle();
            }
        }

        public void OnExit()
        {
            // i-frame 누수 0의 최종 방어선 — 무적 강제 해제 + Hurtbox 콜라이더 복구.
            // 타이밍이 어긋나거나 외부에서 강제로 상태를 바꿔도 무적이 영구 잔류하지 않는다.
            _iframe.ForceVulnerable();
            // Move 인계 시엔 잔류 롤 속도를 유지(모멘텀 연속). Idle/기타 경계는 방어용 Stop()으로 정지
            //   — Idle은 OnEnter/Tick에서 자체 Stop()으로 미끄럼 감속하므로 여기 생략해도 데드 스톱 없이 자연 정지.
            if (!_carryMomentum) _loco.Stop();

            // 회피 도중 큐잉됐을 수 있는 회피 입력을 흘려보내 후딜 직후 자동 재회피를 막는다.
            _input.ConsumeDodge();
        }
    }
}
