using UnityEngine;

public class BowlingBallVisual : MonoBehaviour
{
    [SerializeField] private int outlineLayer;

    private int originalLayer;

    private void Awake()
    {
        originalLayer = gameObject.layer;

        outlineLayer = LayerMask.NameToLayer("Ball");
    }

    public void SetOutline(bool enabled)
    {
        if (enabled)
        {
            gameObject.layer = outlineLayer;
        }
        else
        {
            gameObject.layer = originalLayer;
        }
    }
}