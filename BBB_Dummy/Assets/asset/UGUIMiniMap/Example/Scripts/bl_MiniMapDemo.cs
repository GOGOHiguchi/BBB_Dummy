using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class bl_MiniMapDemo : MonoBehaviour
{

    public int MapID = 2;
    public const string MMName = "MMManagerExample";

    public bl_MiniMap[] Maps;
    public Dropdown mapsDropdown;
    private bool Rotation = true;
    private bl_MiniMap CurrentMiniMap;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        MapID = PlayerPrefs.GetInt("MMExampleMapID", 0);

        mapsDropdown.ClearOptions();
        var op = new List<Dropdown.OptionData>();
        foreach (var item in Maps)
        {
            op.Add(new Dropdown.OptionData()
            {
                text = item.name.ToUpper()
            });
        }
        mapsDropdown.AddOptions(op);
        ApplyMap();
    }

    /// <summary>
    /// 
    /// </summary>
    void ApplyMap()
    {
        for (int i = 0; i < Maps.Length; i++)
        {
            Maps[i].SetActive(false);
        }

        Maps[MapID].SetActive(true);
        CurrentMiniMap = Maps[MapID];
        mapsDropdown.value = MapID;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.R))
#endif
        {
            ChangeRotation();
        }

#if ENABLE_INPUT_SYSTEM
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.Escape))
#endif
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
#if ENABLE_INPUT_SYSTEM
        if (keyboard != null && keyboard.numpadEnterKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.KeypadEnter))
#endif
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void ChangeRotation()
    {
        Rotation = !Rotation;
      //  Maps[MapID].GetComponentInChildren<bl_MiniMap>().SetMapRotationMode(Rotation);

    }

    public void SetIconMulti(float v)
    {
        CurrentMiniMap.IconMultiplier = v;
    }

    public void SetGridSize(float v)
    {
        CurrentMiniMap.SetGridSize(v);
    }

    public void SetGrid(bool v)
    {
        CurrentMiniMap.SetActiveGrid(v);
    }

    public void SetDynamicRot(bool v)
    {
      //  CurrentMiniMap.SetMapRotationMode(v);
    }

    public void SetFadeOnFullscreen(bool fadeOn)
    {
        CurrentMiniMap.FadeOnFullScreen = fadeOn;
    }

    public void ChangeMap(int i)
    {
        PlayerPrefs.SetInt("MMExampleMapID", i);
        Maps[i].SetAsActiveMiniMap();
        mapsDropdown.OnDeselect(null);
        MapID = i;
        CurrentMiniMap = Maps[MapID];
    }

    public bl_MiniMap GetActiveMiniMap
    {
        get
        {
            bl_MiniMap m = Maps[MapID].GetComponentInChildren<bl_MiniMap>();
            return m;
        }
    }
}