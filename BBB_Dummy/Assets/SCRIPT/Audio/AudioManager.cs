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
    GameEnd,
    StunLoop // ★追加：スタン中のループ音
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
        [Range(0f, 10f)] public float volume = 1.0f; // 0〜10倍の個別音量調整
    }

    [Header("SE一覧")]
    public SEData[] seClips;

    // 自分専用のループSE再生用スピーカー（スタン中など）
    private AudioSource loopSeSource;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // ★BGMが鳴り始める前に、一番最初に保存済みの音量設定を適用する
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private IEnumerator Start()
    {
        // AudioMixerの初期化タイミング対策として1フレーム後にも再適用
        yield return null;
        LoadSettings();
    }

    // --- BGM再生・切り替え機能 ---
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        // 再生前に念のため最新の設定音量を反映
        LoadSettings();

        // すでに同じBGMが流れている場合は最初から再生し直さない
        if (bgmAudioSource.isPlaying && bgmAudioSource.clip == clip)
        {
            return;
        }

        bgmAudioSource.clip = clip;
        bgmAudioSource.Play();
    }

    // --- SE再生機能（単発・自分だけ） ---
    public void PlaySE(SEType type)
    {
        foreach (SEData data in seClips)
        {
            if (data.type == type)
            {
                if (data.clip != null && seAudioSource != null)
                {
                    seAudioSource.PlayOneShot(data.clip, data.volume);
                }

                return;
            }
        }
    }

    // --- ★追加：ループSE再生機能（スタン中など・自分だけ） ---
    public void PlayLoopSE(SEType type)
    {
        foreach (SEData data in seClips)
        {
            if (data.type == type)
            {
                PlayLoopSE(data.clip, data.volume);
                return;
            }
        }
    }

    public void PlayLoopSE(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;

        if (loopSeSource == null)
        {
            loopSeSource = gameObject.AddComponent<AudioSource>();
            if (seAudioSource != null)
            {
                loopSeSource.outputAudioMixerGroup = seAudioSource.outputAudioMixerGroup;
            }
            loopSeSource.loop = true;
            loopSeSource.spatialBlend = 0f; // 2D再生（自分だけにクリアに聞こえる）
        }

        // AudioSource.volume の仕様（最大1.0）に合わせて反映
        loopSeSource.volume = Mathf.Clamp01(volume);

        if (loopSeSource.isPlaying && loopSeSource.clip == clip) return;

        loopSeSource.clip = clip;
        loopSeSource.Play();
    }

    // --- ★追加：ループSE停止機能 ---
    public void StopLoopSE()
    {
        if (loopSeSource != null && loopSeSource.isPlaying)
        {
            loopSeSource.Stop();
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