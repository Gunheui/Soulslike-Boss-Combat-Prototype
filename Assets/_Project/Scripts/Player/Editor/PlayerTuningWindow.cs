using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.Player.Editor
{
    /// <summary>
    /// 플레이어 전투 튜닝값을 한 창에서 조절하는 에디터 윈도우. 이동/회피/락온/카메라/애니
    /// 파라미터가 컴포넌트 5개에 흩어져 있어, 손맛 반복 조정 시 매번 GameObject·인스펙터를
    /// 오가야 하던 비용을 없앤다.
    ///
    /// <b>런타임 무수정</b> = 새 데이터 구조(ScriptableObject 등)를 만들지 않고, 기존
    /// <c>[SerializeField]</c> 필드를 <see cref="SerializedObject"/>로 그대로 편집한다.
    /// 필드명이 곧 프로퍼티 경로. <see cref="SerializedObject.ApplyModifiedProperties"/>가
    /// Undo·씬 dirty·Play Mode 즉시 반영을 모두 처리한다.
    ///
    /// <b>값 보존</b> = Play Mode 중 조정한 값은 Play 종료 시 Unity가 원복한다(손맛 루프 단절).
    /// Play 종료 직전 leaf 값을 JSON 스냅샷(<c>UserSettings/</c>, git 비추적)으로 떠 두고,
    /// Edit Mode 복귀 시 자동 복원해 영속시킨다(토글 ON 기본).
    /// </summary>
    public class PlayerTuningWindow : EditorWindow
    {
        // --- 섹션 정의(정적). 각 그룹 = 컴포넌트 1개 + 그릴 프로퍼티 경로들. ---
        private readonly struct SectionDef
        {
            public readonly string Label;
            public readonly Type ComponentType;
            public readonly string[] Paths;
            public SectionDef(string label, Type type, string[] paths)
            { Label = label; ComponentType = type; Paths = paths; }
        }

        private static readonly SectionDef[] Defs =
        {
            new SectionDef("이동", typeof(PlayerLocomotion),
                new[] { "walkSpeed", "sprintSpeed", "strafeSpeed", "speedChangeRate", "rotateSpeed", "gravity" }),
            // 회피: rollConfig/backstepConfig는 DodgeConfig struct(timing+distance+moveDuration) — 자식 포함 통째로.
            new SectionDef("회피", typeof(PlayerStateMachine),
                new[] { "rollConfig", "backstepConfig" }),
            new SectionDef("락온", typeof(LockOnSystem),
                new[] { "lockRange", "maxAcquireAngle" }),
            new SectionDef("카메라", typeof(PlayerCameraDriver),
                new[] { "recenterTime", "lockOnDamping", "freeLookDamping", "freeLookRecenterWait", "freeLookRecenterTime", "moveThreshold" }),
            new SectionDef("애니", typeof(PlayerAnimationDriver),
                new[] { "dampTime" }),
        };

        // --- 바인딩된 섹션(런타임). 씬 컴포넌트를 찾아 SerializedObject로 감싼 것. ---
        private sealed class Section
        {
            public SectionDef Def;
            public Component Component;
            public SerializedObject So;
            public bool Foldout;
        }

        private List<Section> _sections;
        private Vector2 _scroll;

        private const string PrefPrefix = "PlayerTuningWindow.";
        private const string AutoPreserveKey = PrefPrefix + "AutoPreserve";

        private static string SnapshotPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "UserSettings", "PlayerTuningSnapshot.json");

        [MenuItem("Tools/Soulslike/플레이어 튜닝")]
        private static void Open()
        {
            var w = GetWindow<PlayerTuningWindow>();
            w.titleContent = new GUIContent("플레이어 튜닝");
            w.minSize = new Vector2(340f, 200f);
            w.Show();
        }

        private void OnEnable()
        {
            Rebind();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        // 씬에서 컴포넌트 5종을 타입으로 찾아 SerializedObject를 새로 만든다.
        // Play 진입/종료 시 씬 오브젝트가 재생성돼 기존 SerializedObject가 무효화되므로 그때마다 재바인딩.
        private void Rebind()
        {
            _sections = new List<Section>(Defs.Length);
            foreach (var def in Defs)
            {
                var comp = FindFirstObjectByType(def.ComponentType) as Component;
                _sections.Add(new Section
                {
                    Def = def,
                    Component = comp,
                    So = comp != null ? new SerializedObject(comp) : null,
                    Foldout = EditorPrefs.GetBool(PrefPrefix + def.Label, true),
                });
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingPlayMode:
                    // Play 종료 직전(원복되기 전) 현재 값을 스냅샷 — 자동 보존이 켜져 있을 때만.
                    if (EditorPrefs.GetBool(AutoPreserveKey, true)) SaveSnapshot();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    Rebind(); // Play에서 빠져나오며 무효화된 SerializedObject 갱신
                    if (EditorPrefs.GetBool(AutoPreserveKey, true)) LoadSnapshot();
                    Repaint();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    Rebind(); // Play 인스턴스로 다시 바인딩
                    Repaint();
                    break;
            }
        }

        private void OnGUI()
        {
            if (_sections == null) Rebind();

            DrawHeader();
            EditorGUILayout.Space(4f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var s in _sections)
                DrawSection(s);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4f);
            DrawPreserveBar();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool playing = EditorApplication.isPlaying;
                GUILayout.Label(playing ? "▶ Play Mode (실시간 반영)" : "■ Edit Mode", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("재탐색", EditorStyles.toolbarButton))
                    Rebind();
            }

            // 누락 컴포넌트 안내
            foreach (var s in _sections)
                if (s.Component == null)
                    EditorGUILayout.HelpBox($"[{s.Def.Label}] {s.Def.ComponentType.Name} 컴포넌트를 씬에서 찾지 못함.", MessageType.Warning);
        }

        private void DrawSection(Section s)
        {
            if (s.Component == null || s.So == null) return;

            bool newFold = EditorGUILayout.Foldout(s.Foldout, s.Def.Label, true, EditorStyles.foldoutHeader);
            if (newFold != s.Foldout)
            {
                s.Foldout = newFold;
                EditorPrefs.SetBool(PrefPrefix + s.Def.Label, newFold);
            }
            if (!s.Foldout) return;

            s.So.Update();
            EditorGUI.indentLevel++;
            foreach (var path in s.Def.Paths)
            {
                var prop = s.So.FindProperty(path);
                if (prop != null)
                    EditorGUILayout.PropertyField(prop, includeChildren: true);
            }
            EditorGUI.indentLevel--;
            // Play Mode면 살아있는 인스턴스에 즉시 반영, Edit Mode면 Undo+씬 dirty 자동 처리.
            s.So.ApplyModifiedProperties();
        }

        private void DrawPreserveBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool auto = EditorPrefs.GetBool(AutoPreserveKey, true);
                bool newAuto = EditorGUILayout.ToggleLeft("Play 종료 시 자동 보존(권장)", auto);
                if (newAuto != auto) EditorPrefs.SetBool(AutoPreserveKey, newAuto);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("현재 값 저장")) SaveSnapshot();
                    using (new EditorGUI.DisabledScope(!File.Exists(SnapshotPath)))
                        if (GUILayout.Button("저장값 불러오기")) LoadSnapshot();
                }
                EditorGUILayout.LabelField("스냅샷", File.Exists(SnapshotPath) ? "UserSettings/PlayerTuningSnapshot.json" : "(없음)", EditorStyles.miniLabel);
            }
        }

        // === 스냅샷 직렬화 ===

        [Serializable]
        private struct Entry
        {
            public string group; // 섹션 라벨 = 그룹 키
            public string path;   // SerializedProperty 전체 경로(중첩 포함, 예: rollConfig.distance)
            public int kind;      // 0=float, 1=int, 2=bool
            public float value;   // bool은 0/1로 저장
        }

        [Serializable]
        private class Snapshot { public List<Entry> entries = new List<Entry>(); }

        // 섹션 leaf 값(float/int/bool)을 전부 떠서 JSON 파일로 기록. 참조 필드는 캡처하지 않는다(씬 참조 보호).
        private void SaveSnapshot()
        {
            if (_sections == null) Rebind();
            var snap = new Snapshot();
            foreach (var s in _sections)
            {
                if (s.Component == null || s.So == null) continue;
                s.So.Update();
                foreach (var path in s.Def.Paths)
                {
                    var prop = s.So.FindProperty(path);
                    if (prop != null) CaptureLeaves(prop, s.Def.Label, snap.entries);
                }
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
                File.WriteAllText(SnapshotPath, EditorJsonUtility.ToJson(snap, true));
                Debug.Log($"[PlayerTuning] 스냅샷 저장 — {snap.entries.Count}개 값 → {SnapshotPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerTuning] 스냅샷 저장 실패: {e.Message}");
            }
        }

        private void LoadSnapshot()
        {
            if (!File.Exists(SnapshotPath)) return;
            var snap = new Snapshot();
            try { EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(SnapshotPath), snap); }
            catch (Exception e) { Debug.LogError($"[PlayerTuning] 스냅샷 읽기 실패: {e.Message}"); return; }

            if (_sections == null) Rebind();
            // 그룹 라벨 → 섹션 매핑
            var byGroup = new Dictionary<string, Section>();
            foreach (var s in _sections) byGroup[s.Def.Label] = s;

            int applied = 0;
            foreach (var e in snap.entries)
            {
                if (!byGroup.TryGetValue(e.group, out var s) || s.So == null) continue;
                var prop = s.So.FindProperty(e.path);
                if (prop == null) continue;
                switch (e.kind)
                {
                    case 0: prop.floatValue = e.value; break;
                    case 1: prop.intValue = Mathf.RoundToInt(e.value); break;
                    case 2: prop.boolValue = e.value != 0f; break;
                }
                applied++;
            }
            // 섹션별로 적용(Undo+dirty). FindProperty가 같은 SerializedObject를 공유하므로 한 번씩 Apply.
            foreach (var s in _sections)
                s.So?.ApplyModifiedProperties();

            Debug.Log($"[PlayerTuning] 스냅샷 복원 — {applied}개 값 적용");
            Repaint();
        }

        // 프로퍼티 서브트리를 깊이우선 순회하며 leaf(float/int/bool)만 entries에 담는다.
        // DodgeConfig(중첩 struct)의 timing.startupSeconds 같은 값도 자동 포함.
        private static void CaptureLeaves(SerializedProperty root, string group, List<Entry> entries)
        {
            // 루트 자체가 leaf면 바로 캡처(NextVisible 순회는 자식이 있는 노드 전용).
            if (root.propertyType != SerializedPropertyType.Generic)
            {
                AddLeaf(root, group, entries);
                return;
            }

            var p = root.Copy();
            var end = root.GetEndProperty();
            while (p.NextVisible(true) && !SerializedProperty.EqualContents(p, end))
                if (p.propertyType != SerializedPropertyType.Generic)
                    AddLeaf(p, group, entries);
        }

        private static void AddLeaf(SerializedProperty p, string group, List<Entry> entries)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                    entries.Add(new Entry { group = group, path = p.propertyPath, kind = 0, value = p.floatValue });
                    break;
                case SerializedPropertyType.Integer:
                    entries.Add(new Entry { group = group, path = p.propertyPath, kind = 1, value = p.intValue });
                    break;
                case SerializedPropertyType.Boolean:
                    entries.Add(new Entry { group = group, path = p.propertyPath, kind = 2, value = p.boolValue ? 1f : 0f });
                    break;
            }
        }
    }
}
