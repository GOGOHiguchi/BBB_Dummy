using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public static class CreateScriptOnlyPlayerPrefab
{
    private const string SourcePrefabPath = "Assets/Prefab/player.prefab";
    private const string OutputPrefabPath = "Assets/Prefab/player_characterRoot.prefab";

    [MenuItem("Tools/BBB/Create Script-Only Player Prefab")]
    private static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath) != null)
        {
            EditorUtility.DisplayDialog(
                "Player prefab already exists",
                OutputPrefabPath + " already exists. It was not overwritten.",
                "OK");
            return;
        }

        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            if (root == null)
                throw new InvalidOperationException("Source prefab could not be loaded: " + SourcePrefabPath);

            Transform skeleton = root.transform.Find("Bip001");
            Transform body = root.transform.Find("Body_02_blackTshirt");
            Animator sourceAnimator = root.GetComponent<Animator>();

            if (skeleton == null || body == null || sourceAnimator == null)
                throw new InvalidOperationException(
                    "Expected model objects or Animator were not found. " +
                    "The source prefab hierarchy may have changed.");

            PlayerMovement movement = root.GetComponent<PlayerMovement>();
            EmoteWheelController emote = root.GetComponent<EmoteWheelController>();
            BowlingArmController arm = root.GetComponent<BowlingArmController>();

            if (movement == null || emote == null || arm == null)
                throw new InvalidOperationException(
                    "PlayerMovement, EmoteWheelController, or BowlingArmController is missing.");

            var visualRoot = new GameObject("CharacterVisual");
            visualRoot.layer = root.layer;
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            var modelRoot = new GameObject("CurrentModel");
            modelRoot.layer = root.layer;
            modelRoot.transform.SetParent(visualRoot.transform, false);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;
            modelRoot.transform.localScale = Vector3.one;

            skeleton.SetParent(modelRoot.transform, false);
            body.SetParent(modelRoot.transform, false);

            if (!ComponentUtility.CopyComponent(sourceAnimator) ||
                !ComponentUtility.PasteComponentAsNew(modelRoot))
                throw new InvalidOperationException("Could not move the existing Animator to CurrentModel.");

            Animator modelAnimator = modelRoot.GetComponent<Animator>();
            if (modelAnimator == null)
                throw new InvalidOperationException("Animator copy was not created.");

            UnityEngine.Object.DestroyImmediate(sourceAnimator);

            Transform leftUpperArm = modelRoot
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Bip001 L UpperArm");

            if (leftUpperArm == null)
                throw new InvalidOperationException("Bip001 L UpperArm was not found.");

            SetReference(movement, "animator", modelAnimator);
            SetReference(emote, "animator", modelAnimator);
            SetReference(arm, "animator", modelAnimator);
            SetReference(arm, "leftArmBone", leftUpperArm);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
            if (saved == null)
                throw new InvalidOperationException("Unity failed to save the new prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Player prefab created",
                "Created " + OutputPrefabPath + "\n\n" +
                "The original player.prefab was not changed.\n" +
                "The network/controller root remains intact, and the original model + Animator " +
                "are now under CharacterVisual/CurrentModel.",
                "OK");

            Debug.Log("[BBB] Created script-only player prefab: " + OutputPrefabPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[BBB] Failed to create script-only player prefab. " + ex);
            EditorUtility.DisplayDialog("Prefab creation failed", ex.Message, "OK");
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            throw new InvalidOperationException(
                target.GetType().Name + " does not contain serialized field '" + propertyName + "'.");

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
