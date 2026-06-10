using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    /// <summary>
    /// 마우스 free-look용 커서 락. CinemachineInputAxisController가 마우스 delta로 카메라를
    /// 오비탈 회전시키려면 커서가 화면 안에 잡혀 있어야 한다(언락 상태면 커서가 창 밖으로 새고
    /// 델타가 끊김). Editor Play 중 Inspector 클릭이 필요할 때를 위해 Esc로 토글 언락.
    ///
    /// M1-B 테스트 편의용 — 정식 일시정지/메뉴 시스템(M8)이 들어오면 그쪽으로 흡수될 임시 컴포넌트.
    /// </summary>
    public class CameraCursorLock : MonoBehaviour
    {
        private void Start() => SetLocked(true);

        private void Update()
        {
            // 새 Input System 경로(레거시 Input 클래스 미사용). 키보드 없으면(빌드 환경) 스킵.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetLocked(Cursor.lockState != CursorLockMode.Locked);
        }

        private static void SetLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
