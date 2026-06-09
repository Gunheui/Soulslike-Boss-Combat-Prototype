using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    /// <summary>
    /// [임시 스캐폴드 — M0-B / T0.3 입력 감지 검증 전용]
    ///
    /// 생성된 <c>PlayerControls</c> 래퍼(PlayerControls.inputactions의 Generate C# Class)의
    /// 6개 액션을 구독해 입력이 들어오는지 Debug.Log로 확인한다.
    ///
    /// 왜 raw 키 폴링이 아니라 "액션 콜백 구독"인가 = intent 분리.
    /// 입력을 의미 단위(Move/Dodge/Guard…)로 받아두면, 이후 입력 버퍼(공격 큐잉)와
    /// 퍼펙트가드 윈도우(guardPressedTime 시각 기준) 판정이 이 의미 계층 위에 올라간다.
    ///
    /// ⚠ M1 / T1.1 PlayerInputReader 구현 시 이 컴포넌트는 삭제·대체된다(영구 코드 아님).
    /// </summary>
    public class InputProbe : MonoBehaviour
    {
        private PlayerControls _controls;

        private void Awake()
        {
            _controls = new PlayerControls();

            // 핸들러는 Awake에서 1회만 등록(중복 구독 방지).
            _controls.Player.Move.performed += OnMove;
            _controls.Player.Move.canceled += OnMove;
            _controls.Player.Dodge.performed += OnDodge;
            _controls.Player.LightAttack.performed += OnLightAttack;
            _controls.Player.HeavyAttack.performed += OnHeavyAttack;
            _controls.Player.Guard.performed += OnGuardPressed;
            _controls.Player.Guard.canceled += OnGuardReleased;
            _controls.Player.LockOn.performed += OnLockOn;
        }

        // 액션맵은 활성화해야 콜백이 들어온다 — 생명주기 필수.
        private void OnEnable() => _controls.Player.Enable();
        private void OnDisable() => _controls.Player.Disable();
        private void OnDestroy() => _controls.Dispose();

        private void OnMove(InputAction.CallbackContext ctx)
            => Debug.Log($"[InputProbe] Move = {ctx.ReadValue<Vector2>()}");

        private void OnDodge(InputAction.CallbackContext ctx) => Debug.Log("[InputProbe] Dodge");
        private void OnLightAttack(InputAction.CallbackContext ctx) => Debug.Log("[InputProbe] LightAttack");
        private void OnHeavyAttack(InputAction.CallbackContext ctx) => Debug.Log("[InputProbe] HeavyAttack (heavy/charge)");
        private void OnGuardPressed(InputAction.CallbackContext ctx) => Debug.Log("[InputProbe] Guard (pressed)");
        private void OnGuardReleased(InputAction.CallbackContext ctx) => Debug.Log("[InputProbe] Guard (released)");
        private void OnLockOn(InputAction.CallbackContext ctx) => Debug.Log("[InputProbe] LockOn");
    }
}
