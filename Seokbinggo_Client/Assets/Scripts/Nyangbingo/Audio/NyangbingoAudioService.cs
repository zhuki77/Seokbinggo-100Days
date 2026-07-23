using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Nyangbingo.Audio
{
    public enum AudioCue
    {
        MiningDirtImpact,
        MiningMineralImpact,
        TileBroken,
        ItemAcquired,
        PlayerDamaged,
        YokaiDamaged,
        YokaiKilled,
        MiningCritical,
        CraftingCompleted,
        ChestOpened,
        RaidStarted,
        WallDamaged,
        BossAppearedOrFled,
        EoduksiniBloomed,
        PlayerHeatPanting,
        NapStarted,
        GoalBadgeCompleted
    }

    public enum MusicTrack { Day, Night, Boss }

    [Serializable]
    public struct AudioCueClip
    {
        public AudioCue cue;
        public AudioClip clip;
    }

    public sealed class AudioEventRouter : IDisposable
    {
        public const int P1CueCount = 13;
        public const int P2CueCount = 4;

        private bool disposed;
        private bool isNight;
        private bool baekjungActive;
        private bool bossActive;

        public event Action<AudioCue> CueRequested;
        public event Action<MusicTrack> MusicRequested;
        public event Action<bool> BaekjungPercussionRequested;

        public AudioEventRouter()
        {
            GameEvents.OnDayStart += HandleDayStart;
            GameEvents.OnNightStart += HandleNightStart;
            GameEvents.OnBaekjungStart += HandleBaekjungStart;
            GameEvents.OnBaekjungEnd += HandleBaekjungEnd;
            GameEvents.OnMiningImpact += HandleMiningImpact;
            GameEvents.OnTileBroken += HandleTileBroken;
            GameEvents.OnItemAcquired += HandleItemAcquired;
            GameEvents.OnPlayerDamaged += HandlePlayerDamaged;
            GameEvents.OnYokaiDamaged += HandleYokaiDamaged;
            GameEvents.OnYokaiKilled += HandleYokaiKilled;
            GameEvents.OnMiningCritical += HandleMiningCritical;
            GameEvents.OnCraftingCompleted += HandleCraftingCompleted;
            GameEvents.OnChestOpened += HandleChestOpened;
            GameEvents.OnWallDamaged += HandleWallDamaged;
            GameEvents.OnBossSummoned += HandleBossSummoned;
            GameEvents.OnBossDefeated += HandleBossDefeated;
            GameEvents.OnBossFled += HandleBossFled;
            GameEvents.OnEoduksiniBloomed += HandleEoduksiniBloomed;
            GameEvents.OnPlayerHeatPanting += HandlePlayerHeatPanting;
            GameEvents.OnNapStarted += HandleNapStarted;
            GameEvents.OnGoalBadgeCompleted += HandleGoalBadgeCompleted;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnDayStart -= HandleDayStart;
            GameEvents.OnNightStart -= HandleNightStart;
            GameEvents.OnBaekjungStart -= HandleBaekjungStart;
            GameEvents.OnBaekjungEnd -= HandleBaekjungEnd;
            GameEvents.OnMiningImpact -= HandleMiningImpact;
            GameEvents.OnTileBroken -= HandleTileBroken;
            GameEvents.OnItemAcquired -= HandleItemAcquired;
            GameEvents.OnPlayerDamaged -= HandlePlayerDamaged;
            GameEvents.OnYokaiDamaged -= HandleYokaiDamaged;
            GameEvents.OnYokaiKilled -= HandleYokaiKilled;
            GameEvents.OnMiningCritical -= HandleMiningCritical;
            GameEvents.OnCraftingCompleted -= HandleCraftingCompleted;
            GameEvents.OnChestOpened -= HandleChestOpened;
            GameEvents.OnWallDamaged -= HandleWallDamaged;
            GameEvents.OnBossSummoned -= HandleBossSummoned;
            GameEvents.OnBossDefeated -= HandleBossDefeated;
            GameEvents.OnBossFled -= HandleBossFled;
            GameEvents.OnEoduksiniBloomed -= HandleEoduksiniBloomed;
            GameEvents.OnPlayerHeatPanting -= HandlePlayerHeatPanting;
            GameEvents.OnNapStarted -= HandleNapStarted;
            GameEvents.OnGoalBadgeCompleted -= HandleGoalBadgeCompleted;
        }

        private void HandleDayStart()
        {
            isNight = false;
            bossActive = false;
            baekjungActive = false;
            MusicRequested?.Invoke(MusicTrack.Day);
            BaekjungPercussionRequested?.Invoke(false);
        }

        private void HandleNightStart()
        {
            isNight = true;
            bossActive = false;
            MusicRequested?.Invoke(MusicTrack.Night);
            CueRequested?.Invoke(AudioCue.RaidStarted);
        }

        private void HandleBaekjungStart()
        {
            baekjungActive = true;
            if (isNight && !bossActive) BaekjungPercussionRequested?.Invoke(true);
        }

        private void HandleBaekjungEnd()
        {
            baekjungActive = false;
            BaekjungPercussionRequested?.Invoke(false);
        }

        private void HandleBossSummoned(Nyangbingo.Data.BossDefinition _)
        {
            bossActive = true;
            MusicRequested?.Invoke(MusicTrack.Boss);
            BaekjungPercussionRequested?.Invoke(false);
            CueRequested?.Invoke(AudioCue.BossAppearedOrFled);
        }

        private void HandleBossDefeated(Nyangbingo.Data.BossDefinition _)
        {
            bossActive = false;
            MusicRequested?.Invoke(isNight ? MusicTrack.Night : MusicTrack.Day);
            BaekjungPercussionRequested?.Invoke(isNight && baekjungActive);
        }

        private void HandleBossFled()
        {
            bossActive = false;
            CueRequested?.Invoke(AudioCue.BossAppearedOrFled);
            MusicRequested?.Invoke(isNight ? MusicTrack.Night : MusicTrack.Day);
            BaekjungPercussionRequested?.Invoke(isNight && baekjungActive);
        }

        private void HandleMiningImpact(MiningImpactSurface surface) => CueRequested?.Invoke(
            surface == MiningImpactSurface.Dirt ? AudioCue.MiningDirtImpact : AudioCue.MiningMineralImpact);
        private void HandleTileBroken(Vector3Int _) => CueRequested?.Invoke(AudioCue.TileBroken);
        private void HandleItemAcquired() => CueRequested?.Invoke(AudioCue.ItemAcquired);
        private void HandlePlayerDamaged() => CueRequested?.Invoke(AudioCue.PlayerDamaged);
        private void HandleYokaiDamaged() => CueRequested?.Invoke(AudioCue.YokaiDamaged);
        private void HandleYokaiKilled(Nyangbingo.Data.YokaiDefinition _) => CueRequested?.Invoke(AudioCue.YokaiKilled);
        private void HandleMiningCritical() => CueRequested?.Invoke(AudioCue.MiningCritical);
        private void HandleCraftingCompleted() => CueRequested?.Invoke(AudioCue.CraftingCompleted);
        private void HandleChestOpened() => CueRequested?.Invoke(AudioCue.ChestOpened);
        private void HandleWallDamaged() => CueRequested?.Invoke(AudioCue.WallDamaged);
        private void HandleEoduksiniBloomed() => CueRequested?.Invoke(AudioCue.EoduksiniBloomed);
        private void HandlePlayerHeatPanting() => CueRequested?.Invoke(AudioCue.PlayerHeatPanting);
        private void HandleNapStarted() => CueRequested?.Invoke(AudioCue.NapStarted);
        private void HandleGoalBadgeCompleted() => CueRequested?.Invoke(AudioCue.GoalBadgeCompleted);
    }

    public sealed class NyangbingoAudioService : MonoBehaviour
    {
        public const int SfxChannelCount = 8;
        public const float CrossfadeSeconds = 2f;
        public const string BgmVolumeParameter = "BGMVolume";
        public const string SfxVolumeParameter = "SFXVolume";

        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup bgmOutput;
        [SerializeField] private AudioMixerGroup sfxOutput;
        [SerializeField] private AudioClip dayMusic;
        [SerializeField] private AudioClip nightMusic;
        [SerializeField] private AudioClip bossMusic;
        [SerializeField] private AudioClip baekjungPercussion;
        [SerializeField] private AudioCueClip[] sfxClips = Array.Empty<AudioCueClip>();

        private readonly Dictionary<AudioCue, AudioClip> clipsByCue = new Dictionary<AudioCue, AudioClip>();
        private readonly AudioSource[] sfxSources = new AudioSource[SfxChannelCount];
        private Transform audioHost;
        private AudioSource[] musicSources;
        private AudioSource percussionSource;
        private AudioEventRouter router;
        private int activeMusicSource;
        private int sfxCursor;
        private float fadeElapsed = CrossfadeSeconds;
        private bool initialized;
        private AudioClip runtimeDayFallback;

        public MusicTrack CurrentTrack { get; private set; } = MusicTrack.Day;
        public float BgmVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        private void Awake() => Initialize();

        private void Start()
        {
            EnsureClips();
            EnsureAudioHost();
            PlayTrackNow(CurrentTrack);
            Debug.Log(
                $"[Nyangbingo] Audio BGM status day={(dayMusic != null ? dayMusic.name : "null")} " +
                $"playing={(musicSources != null && musicSources[activeMusicSource] != null && musicSources[activeMusicSource].isPlaying)} " +
                $"listener={(FindAnyObjectByType<AudioListener>() != null)} " +
                $"volume={BgmVolume}");
        }

        private void OnDestroy()
        {
            router?.Dispose();
            if (runtimeDayFallback != null)
            {
                Destroy(runtimeDayFallback);
                runtimeDayFallback = null;
            }
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            BuildClipIndex();
            EnsureAudioHost();
            musicSources = new[] { CreateSource(bgmOutput, true), CreateSource(bgmOutput, true) };
            percussionSource = CreateSource(bgmOutput, true);
            for (var i = 0; i < sfxSources.Length; i++) sfxSources[i] = CreateSource(sfxOutput, false);
            router = new AudioEventRouter();
            router.CueRequested += PlayCue;
            router.MusicRequested += RequestMusic;
            router.BaekjungPercussionRequested += SetBaekjungPercussion;
            PlayTrackNow(MusicTrack.Day);
        }

        public bool TrySetBusVolumes(float bgmNormalized, float sfxNormalized)
        {
            if (!IsNormalized(bgmNormalized) || !IsNormalized(sfxNormalized)) return false;
            BgmVolume = bgmNormalized;
            SfxVolume = sfxNormalized;
            if (audioMixer != null)
            {
                audioMixer.SetFloat(BgmVolumeParameter, NormalizedToDecibels(bgmNormalized));
                audioMixer.SetFloat(SfxVolumeParameter, NormalizedToDecibels(sfxNormalized));
            }

            ApplySourceVolumes(fadeElapsed >= CrossfadeSeconds ? 1f : fadeElapsed / CrossfadeSeconds);
            return true;
        }

        public static float NormalizedToDecibels(float normalized)
            => normalized <= .0001f ? -80f : Mathf.Log10(Mathf.Clamp01(normalized)) * 20f;

        private void Update()
        {
            if (musicSources == null || fadeElapsed >= CrossfadeSeconds) return;
            fadeElapsed = Mathf.Min(CrossfadeSeconds, fadeElapsed + Time.unscaledDeltaTime);
            var t = fadeElapsed / CrossfadeSeconds;
            ApplySourceVolumes(t);
            if (fadeElapsed >= CrossfadeSeconds) musicSources[1 - activeMusicSource].Stop();
        }

        private void LateUpdate()
        {
            // Keep the host glued to the listener so 2D playback never depends on world position.
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null || audioHost == null) return;
            if (audioHost.parent != listener.transform)
                audioHost.SetParent(listener.transform, false);
            audioHost.localPosition = Vector3.zero;
        }

        private void RequestMusic(MusicTrack track) => CrossfadeTo(track);

        private void PlayTrackNow(MusicTrack track)
        {
            CurrentTrack = track;
            EnsureClips();
            var clip = ResolveTrackClip(track);
            if (clip == null || musicSources == null)
            {
                Debug.LogWarning($"[Nyangbingo] BGM clip missing for {track}.");
                return;
            }

            var source = musicSources[activeMusicSource];
            source.clip = clip;
            source.loop = true;
            source.mute = false;
            source.enabled = true;
            source.spatialBlend = 0f;
            source.volume = BgmVolume;
            if (!source.isPlaying) source.Play();
            fadeElapsed = CrossfadeSeconds;
            Debug.Log($"[Nyangbingo] BGM playing {clip.name} on {source.gameObject.name}");
        }

        private void CrossfadeTo(MusicTrack track)
        {
            CurrentTrack = track;
            EnsureClips();
            var clip = ResolveTrackClip(track);
            if (clip == null || musicSources == null) return;
            var current = musicSources[activeMusicSource];
            if (current.clip == clip && current.isPlaying) return;
            activeMusicSource = 1 - activeMusicSource;
            var next = musicSources[activeMusicSource];
            next.clip = clip;
            next.mute = false;
            next.enabled = true;
            next.spatialBlend = 0f;
            next.volume = 0f;
            next.Play();
            fadeElapsed = 0f;
        }

        private void SetBaekjungPercussion(bool enabled)
        {
            if (percussionSource == null) return;
            EnsureClips();
            if (baekjungPercussion == null) return;
            if (!enabled)
            {
                percussionSource.Stop();
                return;
            }
            if (percussionSource.isPlaying) return;
            percussionSource.clip = baekjungPercussion;
            percussionSource.spatialBlend = 0f;
            percussionSource.volume = BgmVolume;
            percussionSource.Play();
        }

        private void PlayCue(AudioCue cue)
        {
            if (!clipsByCue.TryGetValue(cue, out var clip) || clip == null) return;
            AudioSource selected = null;
            for (var i = 0; i < sfxSources.Length; i++)
            {
                var index = (sfxCursor + i) % sfxSources.Length;
                if (sfxSources[index].isPlaying) continue;
                selected = sfxSources[index];
                sfxCursor = (index + 1) % sfxSources.Length;
                break;
            }
            if (selected == null)
            {
                selected = sfxSources[sfxCursor];
                sfxCursor = (sfxCursor + 1) % sfxSources.Length;
            }
            selected.spatialBlend = 0f;
            selected.volume = SfxVolume;
            selected.PlayOneShot(clip, SfxVolume);
        }

        private void BuildClipIndex()
        {
            clipsByCue.Clear();
            if (sfxClips != null)
            {
                for (var i = 0; i < sfxClips.Length; i++)
                    if (sfxClips[i].clip != null && !clipsByCue.ContainsKey(sfxClips[i].cue))
                        clipsByCue.Add(sfxClips[i].cue, sfxClips[i].clip);
            }

            EnsureClips();
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                if (clipsByCue.ContainsKey(cue)) continue;
                var clip = Resources.Load<AudioClip>($"Audio/SFX/{cue}");
                if (clip != null) clipsByCue.Add(cue, clip);
            }
        }

        private void EnsureClips()
        {
            if (dayMusic == null) dayMusic = Resources.Load<AudioClip>("Audio/BGM/Day");
            if (nightMusic == null) nightMusic = Resources.Load<AudioClip>("Audio/BGM/Night");
            if (bossMusic == null) bossMusic = Resources.Load<AudioClip>("Audio/BGM/Boss");
            if (baekjungPercussion == null)
                baekjungPercussion = Resources.Load<AudioClip>("Audio/BGM/BaekjungPercussion");

            // Absolute last resort so silence is never caused by missing assets.
            if (dayMusic == null)
            {
                runtimeDayFallback ??= CreateFallbackLoop("RuntimeDayFallback", 261.63f, 329.63f);
                dayMusic = runtimeDayFallback;
            }
        }

        private AudioClip ResolveTrackClip(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Night: return nightMusic != null ? nightMusic : dayMusic;
                case MusicTrack.Boss: return bossMusic != null ? bossMusic : dayMusic;
                default: return dayMusic;
            }
        }

        private void EnsureAudioHost()
        {
            if (audioHost != null) return;
            var listener = FindAnyObjectByType<AudioListener>();
            var hostObject = new GameObject("NyangbingoAudioHost");
            if (listener != null) hostObject.transform.SetParent(listener.transform, false);
            else hostObject.transform.SetParent(transform, false);
            hostObject.transform.localPosition = Vector3.zero;
            audioHost = hostObject.transform;
        }

        private void ApplySourceVolumes(float fadeT)
        {
            if (musicSources == null) return;
            fadeT = Mathf.Clamp01(fadeT);
            musicSources[activeMusicSource].volume = fadeT * BgmVolume;
            var fadingOut = 1 - activeMusicSource;
            if (musicSources[fadingOut].isPlaying)
                musicSources[fadingOut].volume = (1f - fadeT) * BgmVolume;
            if (percussionSource != null && percussionSource.isPlaying)
                percussionSource.volume = BgmVolume;
        }

        private AudioSource CreateSource(AudioMixerGroup output, bool loop)
        {
            EnsureAudioHost();
            var source = audioHost.gameObject.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = output;
            source.playOnAwake = false;
            source.loop = loop;
            source.ignoreListenerPause = true;
            source.spatialBlend = 0f;
            source.priority = loop ? 32 : 128;
            source.mute = false;
            source.enabled = true;
            return source;
        }

        private static AudioClip CreateFallbackLoop(string name, float freqA, float freqB)
        {
            const int sampleRate = 22050;
            const float seconds = 2f;
            var samples = Mathf.CeilToInt(sampleRate * seconds);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = 0.35f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freqA * t) + Mathf.Sin(2f * Mathf.PI * freqB * t)) *
                          0.5f * envelope;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static bool IsNormalized(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }
}
