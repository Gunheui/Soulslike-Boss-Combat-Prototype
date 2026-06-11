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
        [Tooltip("락온 중 이동 속도. 타겟을 바라본 채 옆/뒤로 도는 strafe — 보행보다 느려 신중한 거리 조절.")]
        [SerializeField] private float strafeSpeed = 2.0f;

        [Header("가속")]
        // 물리 속도가 가속의 주체 — 애니 블렌드는 이 속도를 읽기만 한다(종속). 둘이 같은 곡선을
        // 공유해야 발이 안 미끄러진다(Unity Starter Assets ThirdPersonController 관례).
        [Tooltip("가속/감속 수렴 속도. 클수록 빠르게 목표 속도 도달(Unity 관례, 10≈0.3s). 물리·애니 공용 곡선.")]
        [SerializeField] private float speedChangeRate = 10f;

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

        // 이번 프레임의 수평 이동 속도(월드 XZ, m/s). Move/Stop/DodgeMove가 갱신한다.
        private Vector3 _planarVelocity;

        /// <summary>
        /// 이번 프레임 수평 이동 속도(월드 XZ, m/s). <see cref="PlayerAnimationDriver"/>가 읽어
        /// 로코모션 애니 파라미터를 구동하는 <b>속도 단일 진실원</b> — 쓰기는 이 컴포넌트만 한다.
        /// 중력(Y)은 제외(애니는 수평 이동만 본다).
        /// </summary>
        public Vector3 PlanarVelocity => _planarVelocity;

        /// <summary>
        /// 락온 타겟(없으면 null). <see cref="LockOnSystem"/>이 설정한다. null이 아니면 이동/정지/회피 전반에서
        /// 진행 방향이 아니라 이 타겟을 바라본다(facing-override) — strafe·백스텝의 기준 = "타겟을 본 채".
        /// 이 컴포넌트가 '어디를 보는가'의 단일 진실이라, Move/Dodge 상태 코드는 락온을 몰라도 된다.
        /// </summary>
        public Transform LockOnTarget { get; set; }

        /// <summary>
        /// 락온 중 sprint 질주 여부(이번 프레임, Move가 갱신). 락온 sprint는 strafe를 깨고 진행 방향으로
        /// 몸을 돌려 달린다(엘든링/P의 거짓 패턴 — 락온은 카메라/타겟팅만 고정, 이동 잠금 아님).
        /// <see cref="PlayerAnimationDriver"/>가 읽어 strafe/전방달리기 서브트리를 고른다.
        /// </summary>
        public bool IsLockOnSprint { get; private set; }

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

            // TODO(M2 T2.x): sprint 중 스태미나 -8/s 소모, 0이면 walkSpeed 강제(현재는 무한 질주).
            float speed;
            IsLockOnSprint = LockOnTarget != null && sprint && worldDir.sqrMagnitude > 0.0001f;
            if (IsLockOnSprint)
            {
                // 락온 sprint = strafe를 깨고 진행 방향으로 몸을 돌려 질주(free-look 달리기와 동일 경로).
                //  락온 타겟·카메라는 유지(카메라 pivot이 타겟 방향 기준이라 몸 회전 무관). sprint를 떼면
                //  아래 strafe 분기의 FaceTarget이 보스로 재정렬한다.
                speed = sprintSpeed;
                RotateToward(worldDir);
            }
            else if (LockOnTarget != null)
            {
                // 락온 = strafe. 이동 방향과 무관하게 타겟을 바라본 채 옆/뒤로 돈다.
                speed = strafeSpeed;
                FaceTarget();
            }
            else
            {
                speed = sprint ? sprintSpeed : walkSpeed;
                if (worldDir.sqrMagnitude > 0.0001f)
                    RotateToward(worldDir);   // free-look은 진행 방향을 바라봄
            }

            // 목표 속력으로 램프(Unity Starter Assets 패턴) — 방향은 즉시, 속력만 가속.
            // worldDir이 아날로그 입력 크기를 보존(부분 스틱=부분 속도). 물리·애니 공용 곡선.
            float curSpeed = _planarVelocity.magnitude;
            float newSpeed = Mathf.Lerp(curSpeed, speed, Time.deltaTime * speedChangeRate);
            _planarVelocity = worldDir * newSpeed;   // 애니 브리지가 읽는 단일 진실원

            ApplyGravity();
            Vector3 velocity = _planarVelocity + Vector3.up * _verticalVel;
            controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// 수평 이동 정지(중력만 유지). 입력 해제·Idle 진입·Move 탈출 시 호출 — 잔류 속도 누수 차단.
        /// </summary>
        public void Stop()
        {
            IsLockOnSprint = false;   // Move 외 구동기로 넘어오면 락온 sprint 종료 — 애니가 strafe로 복귀
            // 정지 중에도 락온이면 타겟을 계속 바라본다 — 멈춰 선 채로도 보스를 마주봐야 백스텝/strafe 기준이 선다.
            if (LockOnTarget != null) FaceTarget();

            // 0으로 램프 감속 — 진행 방향 유지한 채 속력만 줄여 자연스러운 미끄럼 정지(급정거 아님).
            // Idle이 매 Tick Stop()을 호출하므로 감속이 Idle에서 완주한다. 애니도 같이 idle로 수렴.
            float curSpeed = _planarVelocity.magnitude;
            if (curSpeed > 0.01f)
                _planarVelocity = _planarVelocity.normalized
                    * Mathf.Lerp(curSpeed, 0f, Time.deltaTime * speedChangeRate);
            else
                _planarVelocity = Vector3.zero;

            ApplyGravity();
            // 감속 중 잔여 수평 속도도 controller.Move에 실어야 글라이드가 적용된다(기존엔 중력만 이동).
            Vector3 velocity = _planarVelocity + Vector3.up * _verticalVel;
            controller.Move(velocity * Time.deltaTime);
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
            IsLockOnSprint = false;   // 회피 진입 시 락온 sprint 종료(표현은 Dodge 클립이 덮는다)
            // 방향 회피는 락온/free-look 공통으로 회피 방향을 바라보며 회전한다 — 몸이 회피 방향으로
            //   돌아 전방 다이브 클립과 모션이 맞는다. 락온이어도 RotateToward로 부드럽게 돌고(OnEnter에서
            //   즉시 스냅은 안 함), 회피가 끝나면 다음 상태의 FaceTarget이 보스로 재정렬한다.
            //   락온 카메라는 보스를 직접 조준(RotationComposer)해 몸 회전과 무관하게 보스를 화면에 유지.
            // 중립 백스텝(입력 없음)만 락온 시 보스를 본 채 물러난다(-forward 이동).
            if (rotateToDir && worldDir.sqrMagnitude > 0.0001f)
                RotateToward(worldDir);
            else if (LockOnTarget != null)
                FaceTarget();

            _planarVelocity = worldDir.normalized * speed;   // 회피 이동도 애니 브리지로 노출
            ApplyGravity();
            Vector3 velocity = _planarVelocity + Vector3.up * _verticalVel;
            controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// 지정 방향으로 즉시 회전(스냅, XZ 평면). 회피 진입처럼 "그 프레임에 이미 그 방향을 보고
        /// 있어야 하는" 동작용 — RotateToward(720°/s)로는 180° 전환에 0.25s가 걸려 첫 구간을
        /// 이전 방향을 본 채 굴러 어색하다.
        /// </summary>
        public void SnapFacing(Vector3 worldDir)
        {
            Vector3 flat = Flatten(worldDir);
            if (flat.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }

        // 락온 타겟을 향해 회전(XZ 평면). 타겟이 머리 위/발밑이어도 캐릭터가 기울지 않도록 Y를 평탄화.
        private void FaceTarget()
        {
            Vector3 dir = Flatten(LockOnTarget.position - transform.position);
            if (dir.sqrMagnitude > 0.0001f)
                RotateToward(dir);
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
