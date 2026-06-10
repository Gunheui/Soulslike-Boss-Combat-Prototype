using UnityEngine;

namespace Project.Player
{
    /// <summary>
    /// 플레이어의 실제 이동 물리. Move/Dodge 상태가 "구동기"로서 호출한다(상태는 전이 판정만,
    /// 물리는 여기 한곳에 응집 — M1-D Dodge도 이 컴포넌트를 재사용).
    ///
    /// 왜 CharacterController인가 = 소울라이크 관례. Root Motion을 끄고 스크립트로 이동시켜야
    /// 전투 애니메이션 이벤트(i-frame/히트박스) 타이밍이 이동 속도와 무관하게 결정적으로 유지된다.
    /// Rigidbody는 물리 틱에 끌려가 이 결정성이 깨지고, 캐릭터 전용 계단/경사 처리도 CC가 내장한다.
    ///
    /// 이동은 항상 <b>카메라 상대(camera-relative)</b>다 — 입력 W는 "월드 +Z"가 아니라 "카메라가
    /// 보는 앞". M1-B free-look 오비탈 카메라와 맞물려야 소울 조작감이 난다.
    /// </summary>
    public class PlayerLocomotion : MonoBehaviour
    {
        [Header("속도 (m/s) — T1.4 튜닝값")]
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float sprintSpeed = 5.0f;
        // strafeSpeed는 락온 스트레이프(M1-E)용. 지금은 미사용 — 락온 타겟이 없어 facing-override가 비활성.
        [SerializeField] private float strafeSpeed = 2.0f;

        [Header("회전")]
        [Tooltip("이동 방향으로 도는 각속도(°/s). free-look에선 진행 방향을 바라보게 회전.")]
        [SerializeField] private float rotateSpeed = 720f;

        [Header("중력")]
        // CharacterController는 중력을 자동 적용하지 않는다 — 직접 누적해 바닥에 붙인다.
        [SerializeField] private float gravity = -20f;

        [Header("참조")]
        [SerializeField] private CharacterController controller;
        [Tooltip("camera-relative 기준. 비우면 Camera.main으로 폴백(Cinemachine Brain이 Main Camera를 구동).")]
        [SerializeField] private Transform cameraTransform;

        // 중력 누적 속도(음수). 접지 시 작은 음수로 리셋해 경사/계단에서 바닥에 밀착시킨다.
        private float _verticalVel;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        /// <summary>
        /// 이동 1프레임. Move 상태가 입력이 있는 동안 매 Tick 호출.
        /// </summary>
        /// <param name="moveInput">PlayerInputReader.MoveIntent (-1~1, 카메라 상대 해석).</param>
        /// <param name="sprint">Sprint 홀드 여부.</param>
        public void Move(Vector2 moveInput, bool sprint)
        {
            Vector3 worldDir = CameraRelative(moveInput);

            // TODO(M1-E): 락온 시 worldDir 그대로 이동하되 회전은 보스 방향 고정(strafe), 속도=strafeSpeed.
            //             지금은 락온 타겟이 없어 free-look 분기만 동작.
            // TODO(M2 T2.x): sprint 중 스태미나 -8/s 소모, 0이면 walkSpeed 강제(현재는 무한 질주).
            float speed = sprint ? sprintSpeed : walkSpeed;

            if (worldDir.sqrMagnitude > 0.0001f)
                RotateToward(worldDir);

            ApplyGravity();
            Vector3 velocity = worldDir * speed + Vector3.up * _verticalVel;
            controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// 수평 이동 정지(중력만 유지). 입력 해제·Idle 진입·Move 탈출 시 호출 — 잔류 속도 누수 차단.
        /// </summary>
        public void Stop()
        {
            ApplyGravity();
            controller.Move(Vector3.up * (_verticalVel * Time.deltaTime));
        }

        /// <summary>
        /// 입력 벡터(-1~1)를 카메라 기준 월드 방향(XZ 평면, 정규화)으로 변환.
        /// 카메라 forward/right를 XZ로 투영(Y 제거)해야 카메라가 아래를 봐도 캐릭터가 땅으로 처박히지 않는다.
        /// Move와 Dodge가 공유하는 변환(회피 방향 산출도 같은 camera-relative 기준).
        /// </summary>
        public Vector3 CameraRelative(Vector2 moveInput)
        {
            Vector3 camFwd = Flatten(cameraTransform.forward);
            Vector3 camRight = Flatten(cameraTransform.right);
            Vector3 worldDir = camFwd * moveInput.y + camRight * moveInput.x;
            return Vector3.ClampMagnitude(worldDir, 1f); // 대각 입력 과속 방지
        }

        /// <summary>
        /// 회피 이동 1프레임. <see cref="Move"/>와 달리 입력이 아니라 Dodge 상태가 정한 고정 방향·속도로
        /// 강제 이동시킨다(구르기/백스텝). 같은 CharacterController·중력을 재사용해 이동 물리를 한곳에 응집.
        /// </summary>
        /// <param name="worldDir">이동 월드 방향(정규화 전제). 방향 구르기=입력방향, 백스텝=-forward.</param>
        /// <param name="speed">이동 속도(m/s). DodgeConfig가 거리/구동시간으로 환산.</param>
        /// <param name="rotateToDir">true면 진행 방향을 바라보게 회전(방향 구르기). 백스텝은 false(바라보는 방향 유지).</param>
        public void DodgeMove(Vector3 worldDir, float speed, bool rotateToDir)
        {
            if (rotateToDir && worldDir.sqrMagnitude > 0.0001f)
                RotateToward(worldDir);

            ApplyGravity();
            Vector3 velocity = worldDir.normalized * speed + Vector3.up * _verticalVel;
            controller.Move(velocity * Time.deltaTime);
        }

        private void RotateToward(Vector3 worldDir)
        {
            Quaternion target = Quaternion.LookRotation(worldDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotateSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            // 접지 상태면 음수로 리셋(0이 아닌 작은 음수라야 다음 프레임 isGrounded가 안정적으로 유지).
            if (controller.isGrounded && _verticalVel < 0f)
                _verticalVel = -2f;
            else
                _verticalVel += gravity * Time.deltaTime;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : v;
        }
    }
}
