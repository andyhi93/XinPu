using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 音樂管理器：負責播放/切換 BGM，可透過 Yarn 指令控制。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum BGMType { 放鬆, 緊張, 難過 }

    [System.Serializable]
    public class BGMEntry
    {
        public BGMType type;
        public AudioClip clip;
        public bool loop = true;
    }

    [Header("音源元件")]
    [Tooltip("留空時會自動在此物件上新增一個 AudioSource")]
    public AudioSource bgmSource;

    [Header("BGM 設定")]
    public List<BGMEntry> bgmList = new List<BGMEntry>
    {
        new BGMEntry { type = BGMType.放鬆 },
        new BGMEntry { type = BGMType.緊張 },
        new BGMEntry { type = BGMType.難過 },
    };

    private BGMType? currentType = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.playOnAwake = false;
    }

    /// <summary>
    /// 播放指定類型的 BGM，若已經在播放同一首則不重新觸發（避免每次對話節點都重播）。
    /// </summary>
    public void PlayBGM(BGMType type)
    {
        BGMEntry entry = bgmList.Find(e => e.type == type);
        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"【音樂管理】尚未設定 {type} 的 BGM 音效檔，請檢查 Inspector 設定。");
            return;
        }

        if (currentType == type && bgmSource.isPlaying) return;

        bgmSource.clip = entry.clip;
        bgmSource.loop = entry.loop;
        bgmSource.Play();
        currentType = type;
    }

    public void StopBGM()
    {
        bgmSource.Stop();
        currentType = null;
    }

    /// <summary>
    /// 供 Yarn 指令使用：切換 BGM。用法：<<play_bgm "放鬆">>
    /// </summary>
    [YarnCommand("play_bgm")]
    public static void PlayBGMCommand(string bgmName)
    {
        if (Instance == null) return;

        if (System.Enum.TryParse(bgmName, true, out BGMType type))
        {
            Instance.PlayBGM(type);
        }
        else
        {
            Debug.LogWarning($"【音樂管理】找不到名為 '{bgmName}' 的 BGM 類型，請使用 放鬆 / 緊張 / 難過。");
        }
    }

    /// <summary>
    /// 供 Yarn 指令使用：停止 BGM。用法：<<stop_bgm>>
    /// </summary>
    [YarnCommand("stop_bgm")]
    public static void StopBGMCommand()
    {
        if (Instance == null) return;
        Instance.StopBGM();
    }
}
