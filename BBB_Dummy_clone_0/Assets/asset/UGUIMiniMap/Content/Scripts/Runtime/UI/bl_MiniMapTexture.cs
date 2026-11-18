using UnityEngine;
using UnityEngine.EventSystems;

namespace Lovatto.MiniMap
{
    public class bl_MiniMapTexture : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform Area;
        private bl_MiniMap MiniMap;

        /// <summary>
        /// 
        /// </summary>
        void Awake()
        {
            Area = GetComponent<RectTransform>();
            MiniMap = transform.root.GetComponentInChildren<bl_MiniMap>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (MiniMap == null || MiniMap.miniMapCamera == null)
                return;

            bool isSatelliteAiming =
                PlayerMovement.Local != null &&
                PlayerMovement.Local.IsReadyingSatelliteLaser;

            // サテライトレーザー照準中は全体マップからのみ確定可能
            if (isSatelliteAiming && !MiniMap.IsFullScreen)
                return;

            // 通常のマップマーカーだけ AllowMapMarks の設定に従う
            if (!isSatelliteAiming && !MiniMap.AllowMapMarks)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Area,
                eventData.pressPosition,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                localPoint.x /= Area.rect.width;
                localPoint.y /= Area.rect.height;

                Vector2 absolutePosition =
                    new Vector2(localPoint.x * 2f, localPoint.y * 2f);

                Vector2 ms = MiniMap.miniMapCamera.pixelRect.size * 0.5f;

                Vector2 relativeScreen = new Vector2(
                    (absolutePosition.x * ms.x) + ms.x,
                    (absolutePosition.y * ms.y) + ms.y
                );

                SpawnPointer(relativeScreen);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="position"></param>
        private void SpawnPointer(Vector2 position)
        {
            Ray ray = MiniMap.miniMapCamera.ScreenPointToRay(position);

            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            // サテライトレーザー照準中
            if (PlayerMovement.Local != null &&
                PlayerMovement.Local.IsReadyingSatelliteLaser)
            {
                PlayerMovement.Local.RequestSatelliteLaser(hit.point);
                return;
            }

            // 通常のマップマーカー処理
            MiniMap.SetPointMark(hit.point);
            bl_MiniMapEvents.onSelectPosition?.Invoke(hit.point);
        }

        public Vector2 GetRectSize()
        {
            return Area.rect.size;
        }
    }
}