using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public enum SEType
{
    ButtonClick,
    ItemUse,
    ItemPickup,
    Jump,
    Attack,
    RocketFire,
    Damage,
    BallThrow,
    GameStart,
    GameEnd
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;
    public AudioMixer audioMixer;

    [Header("Audio Sources")]
    public AudioSource bgmAudioSource;
    public AudioSource seAudioSource;

    [System.Serializable]
    public class SEData
    {
        public SEType type;
        public AudioClip clip;
    }

    [Header("SE一覧")]
    public SEData[] seClips;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private IEnumerator Start()
    {
        yield return null;
        LoadSettings();
    }

    // --- BGM再生・切り替え機能 ---
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        // すでに同じBGMが流れている場合は最初から再生し直さない
        if (bgmAudioSource.isPlaying && bgmAudioSource.clip == clip)
        {
            return;
        }

        bgmAudioSource.clip = clip;
        bgmAudioSource.Play();
    }

    // --- SE再生機能 ---
    public void PlaySE(SEType type)
    {
        foreach (SEData data in seClips)
        {
            if (data.type == type)
            {
                if (data.clip != null)
                {
                    seAudioSource.PlayOneShot(data.clip);
                }

                return;
            }
        }
    }

    // --- 音量設定関係 ---
    public void SetBGMVolume(float volume)
    {
        float dB = volume <= 0 ? -80f : Mathf.Log10(volume) * 20f;
        audioMixer.SetFloat("BGMVolume", dB);
        PlayerPrefs.SetFloat("BGM_VAL", volume);
    }

    public void SetSEVolume(float volume)
    {
        float dB = volume <= 0 ? -80f : Mathf.Log10(volume) * 20f;
        audioMixer.SetFloat("SEVolume", dB);
        PlayerPrefs.SetFloat("SE_VAL", volume);
    }

    private void LoadSettings()
    {
        float bgm = PlayerPrefs.GetFloat("BGM_VAL", 0.50f);
        float se = PlayerPrefs.GetFloat("SE_VAL", 0.50f);

        SetBGMVolume(bgm);
        SetSEVolume(se);
    }
}