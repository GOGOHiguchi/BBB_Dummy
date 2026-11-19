#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 2_LobbyシーンのCanvasへキャラクター選択UIを追加するセットアップメニュー。
/// </summary>
public static class CharacterSelectUISetup
{
    private const string LobbyScenePath = "Assets/Scenes/2_Lobby.unity";
    private const string CanvasObjectName = "Canvas";

    [MenuItem("Tools/BBB/Build Character Select UI in 2_Lobby")]
    public static void BuildCharacterSelectUI()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(CanvasObjectName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Character Select Canvas");
        }
        else if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<CharacterSelectUI>() == null)
            Undo.AddComponent<CharacterSelectUI>(canvas.gameObject);

        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("[CharacterSelectUISetup] 2_LobbyのCanvasにキャラクター選択UIを追加しました。Playして表示を確認してください。");
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create UI EventSystem");
            InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            return;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null &&
            eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            InputSystemUIInputModule inputModule = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            inputModule.AssignDefaultActions();
        }
    }
}
#endif
