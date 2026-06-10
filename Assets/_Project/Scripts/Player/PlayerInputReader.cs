using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    /// <summary>
    /// Input System 래퍼(<c>PlayerControls</c>)를 구독해 raw 입력을 전투 의미(intent)로 번역한다.
    /// M0-B의 임시 <c>InputProbe</c>를 대체하는 영구 컴포넌트.
    ///
    /// 왜 raw 키 폴링이 아니라 intent 계층인가 = 이후 두 시스템이 이 계층 위에 올라간다.
    ///  ① 입력 버퍼: 공격 입력을 "큐잉"했다가 콤보 윈도우에서 소비(M3) — 그래서 단발 read가 아니라
    ///     Consume 패턴(읽으면 소비되는 1회성 플래그)으로 노출한다.
    ///  ② 퍼펙트가드 윈도우: 가드를 "누른 시각"에서 8f 안에 맞으면 PG(M4) — 그래서 GuardHeld(불리언)뿐
    ///     아니라 <see cref="GuardPressedTime"/>(눌린 Time.time)을 지금부터 기록해 둔다.
    /// 즉 M1-A는 이 두 미래 기능의 '기준점'만 미리 노출하고, 판정 로직 자체는 해당 마일스톤으로 이월한다.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        private PlayerControls _controls;

        // --- 지속 상태(매 프레임 유효) ---
        /// <summary>이동 입력 벡터(-1~1). Locomotion이 매 프레임 읽음(M1-B).</summary>
        public Vector2 MoveIntent { get; private set; }

        /// <summary>가드 버튼을 누르고 있는가. Guard 상태 유지 판정용(M4).</summary>
        public bool GuardHeld { get; private set; }

        /// <summary>달리기(Sprint) 버튼을 누르고 있는가. Locomotion이 매 프레임 읽어 보행/질주 속도 분기(M1-C). 전용 액션(LeftShift/buttonEast).</summary>
        public bool SprintHeld { get; private set; }

        /// <summary>가드를 마지막으로 "누른" 시각(Time.time). PG 윈도우(now - 이 값 ≤ 8f) 기준점(M4).</summary>
        public float GuardPressedTime { get; private set; }

        // --- 1회성 엣지 입력(읽으면 소비) ---
        private bool _dodgeQueued;
        private bool _attackQueued;

        /// <summary>회피 입력이 큐에 있으면 true 반환 후 소비(엣지 트리거 — 한 번 눌림에 한 번만 회피).</summary>
        public bool ConsumeDodge()
        {
            if (!_dodgeQueued) return false;
            _dodgeQueued = false;
            return true;
        }

        /// <summary>현재 공격 버퍼가 차 있는가(소비 없이 확인만).</summary>
        public bool AttackBuffered => _attackQueued;

        /// <summary>
        /// 공격 입력이 버퍼에 있으면 true 반환 후 소비.
        /// ⚠ M1-A는 "최근 눌림" 단순 플래그다. 모션 종료 전 10f 큐잉·콤보 윈도우 매칭 같은
        /// 진짜 입력 버퍼 로직은 M3 T3.3로 이월(여기선 인터페이스만 선노출).
        /// </summary>
        public bool ConsumeAttack()
        {
            if (!_attackQueued) return false;
            _attackQueued = false;
            return true;
        }

        private void Awake()
        {
            _controls = new PlayerControls();

            // 핸들러는 Awake에서 1회만 등록(중복 구독 방지).
            _controls.Player.Move.performed += OnMove;
            _controls.Player.Move.canceled += OnMove;     // 손 떼면 (0,0)으로 복귀
            _controls.Player.Dodge.performed += OnDodge;
            _controls.Player.LightAttack.performed += OnLightAttack;
            _controls.Player.Guard.performed += OnGuardPressed;
            _controls.Player.Guard.canceled += OnGuardReleased;
            _controls.Player.Sprint.performed += OnSprintPressed;
            _controls.Player.Sprint.canceled += OnSprintReleased;
        }

        private void OnEnable() => _controls.Player.Enable();
        private void OnDisable() => _controls.Player.Disable();
        private void OnDestroy() => _controls.Dispose();

        // --- 콜백: raw → intent 번역 + DoD 검증 로그(확인 후 제거 가능) ---

        private void OnMove(InputAction.CallbackContext ctx)
        {
            MoveIntent = ctx.ReadValue<Vector2>();
            Debug.Log($"[Input] MoveIntent = {MoveIntent}");
        }

        private void OnDodge(InputAction.CallbackContext ctx)
        {
            _dodgeQueued = true;
            Debug.Log("[Input] Dodge queued");
        }

        private void OnLightAttack(InputAction.CallbackContext ctx)
        {
            _attackQueued = true;
            Debug.Log("[Input] Attack buffered");
        }

        private void OnGuardPressed(InputAction.CallbackContext ctx)
        {
            GuardHeld = true;
            GuardPressedTime = Time.time;   // PG 윈도우 기준점 기록
            Debug.Log($"[Input] Guard pressed @ t={GuardPressedTime:F3}");
        }

        private void OnGuardReleased(InputAction.CallbackContext ctx)
        {
            GuardHeld = false;
            Debug.Log("[Input] Guard released");
        }

        private void OnSprintPressed(InputAction.CallbackContext ctx) => SprintHeld = true;
        private void OnSprintReleased(InputAction.CallbackContext ctx) => SprintHeld = false;
    }
}
