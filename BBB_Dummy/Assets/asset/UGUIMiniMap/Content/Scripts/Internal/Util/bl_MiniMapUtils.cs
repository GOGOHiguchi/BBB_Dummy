using UnityEngine;
using System.Runtime.CompilerServices;

public static class bl_MiniMapUtils
{


    /// <summary>
    /// 
    /// </summary>
    /// <param name="viewPoint"></param>
    /// <param name="maxAnchor"></param>
    /// <returns></returns>
    public static Vector3 CalculateMiniMapPosition(Vector3 viewPoint, RectTransform maxAnchor)
    {
        viewPoint = new Vector2((viewPoint.x * maxAnchor.sizeDelta.x) - (maxAnchor.sizeDelta.x * 0.5f),
            (viewPoint.y * maxAnchor.sizeDelta.y) - (maxAnchor.sizeDelta.y * 0.5f));

        return viewPoint;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bl_MiniMap GetMiniMap(int id = 0)
    {
        if (bl_MiniMap.ActiveMiniMap != null) return bl_MiniMap.ActiveMiniMap;

        bl_MiniMap[] allmm = FindObjects<bl_MiniMap>();
        if (id > allmm.Length - 1) return null;
        return allmm[id];
    }

    public static bool IsInLayerMask(int layer, LayerMask layermask)
    {
        return layermask == (layermask | (1 << layer));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <param name="isClamped"></param>
    /// <returns></returns>
    public static float ClampBorders(float value, float min, float max, out bool isClamped)
    {
        if (value < min)
        {
            value = min;
            isClamped = true;
        }
        else
        {
            if (value > max)
            {
                isClamped = true;
                value = max;
            }
            else
            {
                isClamped = false;
            }
        }
        return value;
    }

    private static Camera _renderCamera = null;
    public static Camera RenderCamera
    {
        get
        {
            if (_renderCamera == null)
            {
                _renderCamera = Camera.main;
                if (_renderCamera == null) { _renderCamera = Camera.current; }
            }
            return _renderCamera;
        }
    }

    /// <summary>
    /// Resolves the correct object identifier depending on the Unity version compiled.
    /// </summary>
#if UNITY_6000_4_OR_NEWER
    public static int GetEntityIdSafe(UnityEngine.Object obj)
    {
        return obj.GetEntityId().GetHashCode();
    }
#else
    public static int GetEntityIdSafe(UnityEngine.Object obj)
    {
        return obj.GetInstanceID();
    }
#endif

    /// <summary>
    /// Cross-version safe replacement for FindObjectsByType<T> that eliminates CS0618 warnings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] FindObjects<T>(FindObjectsInactive includeInactive = FindObjectsInactive.Exclude) where T : UnityEngine.Object
    {
#if UNITY_6000_4_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(includeInactive);
#elif UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(includeInactive, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(includeInactive == FindObjectsInactive.Include);
#endif
    }
}