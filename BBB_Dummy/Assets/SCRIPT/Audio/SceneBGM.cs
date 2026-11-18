using UnityEngine;

public class SceneBGM : MonoBehaviour
{
    [Header("このシーンで流すBGM")]
    public AudioClip sceneBGM;

    void Start()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayBGM(sceneBGM);
        }
    }
}