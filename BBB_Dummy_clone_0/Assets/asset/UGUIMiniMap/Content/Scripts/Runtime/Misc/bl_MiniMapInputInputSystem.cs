#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lovatto.MiniMap
{
    [CreateAssetMenu(menuName = "Lovatto/MiniMap/Input Handler Input System")]
    public class bl_MiniMapInputInputSystem : bl_MiniMapInputBase
    {
        [SerializeField, Tooltip("Keyboard key that toggles the minimap screen mode.")]
        private Key screenModeKey = Key.M;

        [SerializeField, Tooltip("Keyboard key that zooms the minimap in.")]
        private Key zoomInKey = Key.NumpadPlus;

        [SerializeField, Tooltip("Keyboard key that zooms the minimap out.")]
        private Key zoomOutKey = Key.NumpadMinus;

        public override void Init()
        {
        }

        public override bool IsInputDown(MiniMapInput key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            switch (key)
            {
                case MiniMapInput.ZoomIn:
                    return WasPressedThisFrame(keyboard, zoomInKey);
                case MiniMapInput.ZoomOut:
                    return WasPressedThisFrame(keyboard, zoomOutKey);
                case MiniMapInput.ScreenMode:
                    return WasPressedThisFrame(keyboard, screenModeKey);
                default:
                    return false;
            }
        }

        private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }
    }
}
#endif
