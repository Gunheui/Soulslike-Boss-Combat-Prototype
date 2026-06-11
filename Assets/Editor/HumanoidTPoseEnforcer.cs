using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HumanoidTPoseEnforcer
{
    private static Type AvatarSetupTool =>
        typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AvatarSetupTool");

    /// <summary>
    /// Logs the available static method signatures on AvatarSetupTool for diagnostics.
    /// </summary>
    public static string DumpApi()
    {
        Type t = AvatarSetupTool;
        if (t == null) return "AvatarSetupTool not found";
        var sb = new StringBuilder();
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            string n = m.Name;
            if (n.Contains("Pose") || n.Contains("Bone") || n.Contains("Transfer") || n.Contains("Valid"))
            {
                var ps = m.GetParameters();
                sb.Append(n + "(");
                for (int i = 0; i < ps.Length; i++)
                    sb.Append((i > 0 ? ", " : "") + ps[i].ParameterType.Name + " " + ps[i].Name);
                sb.AppendLine(")");
            }
        }
        return sb.ToString();
    }

    public static string DiagnoseEnforce(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        Type ast = AvatarSetupTool;
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        var instance = UnityEngine.Object.Instantiate(root);
        var sb = new StringBuilder();
        try
        {
            var so = new SerializedObject(importer);
            SerializedProperty humanArray = so.FindProperty("m_HumanDescription.m_Human");
            SerializedProperty skeletonArray = so.FindProperty("m_HumanDescription.m_Skeleton");
            sb.AppendLine("humanArray size=" + (humanArray != null ? humanArray.arraySize : -1) +
                          " skeletonArray size=" + (skeletonArray != null ? skeletonArray.arraySize : -1));

            var getModelBones = ast.GetMethod("GetModelBones", BindingFlags.Public | BindingFlags.Static);
            object modelBones = getModelBones.Invoke(null, new object[] { instance.transform, false, null });
            sb.AppendLine("modelBones count=" + (modelBones as System.Collections.IDictionary).Count);

            MethodInfo getHumanBones = null;
            foreach (var m in ast.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (m.Name == "GetHumanBones" && m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(SerializedProperty)) { getHumanBones = m; break; }
            object humanBones = getHumanBones.Invoke(null, new object[] { humanArray, modelBones });
            var arr = humanBones as Array;
            int resolved = 0;
            Type bwt = arr.GetType().GetElementType();
            var boneField = bwt.GetField("bone", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < arr.Length; i++)
            {
                var bw = arr.GetValue(i);
                var bone = boneField.GetValue(bw) as Transform;
                if (bone != null) resolved++;
            }
            sb.AppendLine("humanBones total=" + arr.Length + " resolved(non-null bone)=" + resolved);

            var transferDescToPose = ast.GetMethod("TransferDescriptionToPose", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(SerializedObject), typeof(Transform) }, null);
            transferDescToPose.Invoke(null, new object[] { so, instance.transform });

            var isPoseValid = ast.GetMethod("IsPoseValid", BindingFlags.Public | BindingFlags.Static);
            var getPoseError = ast.GetMethod("GetPoseError", BindingFlags.Public | BindingFlags.Static);
            sb.AppendLine("Before MakePoseValid: IsPoseValid=" + isPoseValid.Invoke(null, new[] { humanBones }) +
                          " error=" + getPoseError.Invoke(null, new[] { humanBones }));

            var makePoseValid = ast.GetMethod("MakePoseValid", BindingFlags.Public | BindingFlags.Static);
            makePoseValid.Invoke(null, new[] { humanBones });
            sb.AppendLine("After MakePoseValid: IsPoseValid=" + isPoseValid.Invoke(null, new[] { humanBones }) +
                          " error=" + getPoseError.Invoke(null, new[] { humanBones }));

            // measure arm after makepose
            var la = (boneField.GetValue(arr.GetValue(FindIndex(arr, boneField, humanArray, "LeftLowerArm"))) as Transform);
            sb.AppendLine("(diagnostic measure skipped)");
        }
        catch (Exception e) { sb.AppendLine("EX: " + e); }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        return sb.ToString();
    }

    private static int FindIndex(Array arr, FieldInfo boneField, SerializedProperty humanArray, string human) { return 0; }

    /// <summary>
    /// Enforces a valid Unity Humanoid T-Pose on the avatar of the model at assetPath and reimports it.
    /// </summary>
    public static string EnforceTPose(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) return "Not a ModelImporter: " + assetPath;
        if (importer.animationType != ModelImporterAnimationType.Human)
            return "Model is not Humanoid: " + assetPath;

        Type ast = AvatarSetupTool;
        if (ast == null) return "AvatarSetupTool reflection failed";

        var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        var instance = UnityEngine.Object.Instantiate(root);
        var sb = new StringBuilder();

        try
        {
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null)
                return "No Animator/Avatar on instance";

            // Reset to bind pose stored in the description first so we align from a known state.
            var so = new SerializedObject(importer);
            SerializedProperty skeletonArray = so.FindProperty("m_HumanDescription.m_Skeleton");
            var transferDescToPose = ast.GetMethod("TransferDescriptionToPose",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SerializedObject), typeof(Transform) }, null);
            transferDescToPose.Invoke(null, new object[] { so, instance.transform });

            Vector3 left = Vector3.left;   // character's left = -X
            Vector3 right = Vector3.right; // character's right = +X
            Vector3 down = Vector3.down;

            // Align arms horizontal (A-pose -> T-pose). Parent-to-child order matters.
            AlignBone(animator, HumanBodyBones.LeftShoulder,  HumanBodyBones.LeftUpperArm, left, sb);
            AlignBone(animator, HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm, left, sb);
            AlignBone(animator, HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,     left, sb);
            AlignBone(animator, HumanBodyBones.LeftHand,      HumanBodyBones.LeftMiddleProximal, left, sb);

            AlignBone(animator, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, right, sb);
            AlignBone(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, right, sb);
            AlignBone(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,      right, sb);
            AlignBone(animator, HumanBodyBones.RightHand,     HumanBodyBones.RightMiddleProximal, right, sb);

            // Ensure legs point straight down.
            AlignBone(animator, HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,  down, sb);
            AlignBone(animator, HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot,      down, sb);
            AlignBone(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, down, sb);
            AlignBone(animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,     down, sb);

            // Write the corrected pose into the importer description skeleton.
            var transferPoseToDesc = ast.GetMethod("TransferPoseToDescription",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SerializedProperty), typeof(Transform) }, null);
            transferPoseToDesc.Invoke(null, new object[] { skeletonArray, instance.transform });

            so.ApplyModifiedProperties();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        importer.SaveAndReimport();
        AssetDatabase.Refresh();
        return "Enforced explicit T-Pose and reimported: " + assetPath + "\n" + sb.ToString();
    }

    private static void AlignBone(Animator animator, HumanBodyBones bone, HumanBodyBones child,
        Vector3 targetWorldDir, StringBuilder log)
    {
        var b = animator.GetBoneTransform(bone);
        var c = animator.GetBoneTransform(child);
        if (b == null || c == null) { log.AppendLine("skip " + bone + " (missing bone/child)"); return; }
        Vector3 cur = c.position - b.position;
        if (cur.sqrMagnitude < 1e-10f) { log.AppendLine("skip " + bone + " (zero len)"); return; }
        Quaternion q = Quaternion.FromToRotation(cur.normalized, targetWorldDir.normalized);
        b.rotation = q * b.rotation; // rotate in world space; children follow
        log.AppendLine("aligned " + bone + " -> " + targetWorldDir);
    }
}
