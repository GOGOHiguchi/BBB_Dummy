using UnityEngine;

namespace Lovatto.MiniMap
{
    public class bl_MiniMapTarget : MonoBehaviour
    {
        public bool setTargetOnStart = false;
        public Transform target = null;
        [SerializeField] private Transform rotationTarget;

        private void Awake()
        {
            if (setTargetOnStart)
            {
                SetAsTarget();
            }
        }

        private void Start()
        {
            if (setTargetOnStart)
            {
                SetAsTarget();
            }
        }

        public void SetAsTarget()
        {
            bl_MiniMap map = bl_MiniMapUtils.GetMiniMap();
            if (map != null)
            {
                map.SetTarget(this);
            }
        }

        public Transform GetTarget()
        {
            if (target == null) { target = transform; }
            return target;
        }

        public Transform GetRotationTarget()
        {
            return rotationTarget != null ? rotationTarget : GetTarget();
        }

        public static bl_MiniMapTarget Get()
        {
            return FindAnyObjectByType<bl_MiniMapTarget>();
        }
    }
}