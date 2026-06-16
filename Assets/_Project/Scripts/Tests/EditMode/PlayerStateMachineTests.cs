using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Project.Player;

namespace Project.Tests.EditMode
{
    /// <summary>
    /// M1-A / T1.2 DoD: PlayerStateMachine.ChangeState가 항상 "이전.OnExit() → 새.OnEnter()" 순서로 전이함을 검증.
    /// EditMode라 Start()가 안 돌아 CurrentState가 null에서 출발 — 더미 상태 2개로 전이 경로만 격리해 호출 순서를 기록·검증한다.
    /// </summary>
    public class PlayerStateMachineTests
    {
        /// <summary>호출 순서를 공유 로그에 남기는 더미 상태.</summary>
        private sealed class RecordingState : IPlayerState
        {
            private readonly List<string> _log;
            private readonly string _label;

            public RecordingState(List<string> log, string label)
            {
                _log = log;
                _label = label;
            }

            public void OnEnter() => _log.Add($"{_label}.OnEnter");
            public void Tick() => _log.Add($"{_label}.Tick");
            public void OnExit() => _log.Add($"{_label}.OnExit");
        }

        private GameObject _go;
        private PlayerStateMachine _sm;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestStateMachine");
            _sm = _go.AddComponent<PlayerStateMachine>();
            // Start()는 EditMode에서 호출되지 않으므로 CurrentState는 null로 시작한다.
        }

        [TearDown]
        public void TearDown()
        {
            // EditMode는 Destroy가 아니라 DestroyImmediate.
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void ChangeState_FirstEntry_CallsOnEnter_NoExitOnNull()
        {
            var log = new List<string>();
            var a = new RecordingState(log, "A");

            _sm.ChangeState(a);

            // 첫 전이: 이전 상태가 null이므로 OnExit는 건너뛰고 OnEnter만.
            Assert.AreEqual(new[] { "A.OnEnter" }, log.ToArray());
            Assert.AreSame(a, _sm.CurrentState);
        }

        [Test]
        public void ChangeState_Transition_CallsPrevOnExit_BeforeNextOnEnter()
        {
            var log = new List<string>();
            var a = new RecordingState(log, "A");
            var b = new RecordingState(log, "B");

            _sm.ChangeState(a);   // A.OnEnter
            log.Clear();          // 전이 자체만 보기 위해 초기 진입 로그 비움
            _sm.ChangeState(b);   // 기대: A.OnExit → B.OnEnter

            Assert.AreEqual(new[] { "A.OnExit", "B.OnEnter" }, log.ToArray(),
                "ChangeState는 이전 상태 OnExit를 새 상태 OnEnter보다 먼저 불러야 한다(상태 누수 차단).");
            Assert.AreSame(b, _sm.CurrentState);
        }
    }
}
