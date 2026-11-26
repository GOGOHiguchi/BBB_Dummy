using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SatelliteLaserSound : MonoBehaviour
{
    [Header("サテライト発射SE設定")]
    [SerializeField] private AudioClip laserClip;
    [Range(0f, 10f)]
    [SerializeField] private float volumeMultiplier = 3.0f; // 最大10倍まで調整可能

    private void Start()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null) return;

        // AudioSource側に設定されているClipがあればそちらを優先使用
        AudioClip clipToPlay = laserClip != null ? laserClip : audioSource.clip;
        if (clipToPlay == null) return;

        // AudioManagerのSEミキサーグループを自動適用（未設定時）
        if (audioSource.outputAudioMixerGroup == null &&
            AudioManager.instance != null &&
            AudioManager.instance.seAudioSource != null)
        {
            audioSource.outputAudioMixerGroup = AudioManager.instance.seAudioSource.outputAudioMixerGroup;
        }

        // PlayOneShotを使うことで1.0倍を超える音量スケールで再生できる
        audioSource.PlayOneShot(clipToPlay, volumeMultiplier);
    }
}
