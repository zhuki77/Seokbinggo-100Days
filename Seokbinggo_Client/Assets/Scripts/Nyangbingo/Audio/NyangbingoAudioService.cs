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
        private AudioSource[] musicSources;
        private AudioSource percussionSource;
        private AudioEventRouter router;
        private int activeMusicSource;
        private int sfxCursor;
        private float fadeElapsed = CrossfadeSeconds;
        private bool initialized;

        public MusicTrack CurrentTrack { get; private set; } = MusicTrack.Day;
        public float BgmVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        private void Awake() => Initialize();
        private void OnDestroy() => router?.Dispose();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            BuildClipIndex();
            musicSources = new[] { CreateSource(bgmOutput, true), CreateSource(bgmOutput, true) };
            percussionSource = CreateSource(bgmOutput, true);
            for (var i = 0; i < sfxSources.Length; i++) sfxSources[i] = CreateSource(sfxOutput, false);
            router = new AudioEventRouter();
            router.CueRequested += PlayCue;
            router.MusicRequested += RequestMusic;
            router.BaekjungPercussionRequested += SetBaekjungPercussion;
            RequestMusic(MusicTrack.Day);
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
            return true;
        }

        public static float NormalizedToDecibels(float normalized)
            => normalized <= .0001f ? -80f : Mathf.Log10(Mathf.Clamp01(normalized)) * 20f;

        private void Update()
        {
            if (musicSources == null || fadeElapsed >= CrossfadeSeconds) return;
            fadeElapsed = Mathf.Min(CrossfadeSeconds, fadeElapsed + Time.unscaledDeltaTime);
            var t = fadeElapsed / CrossfadeSeconds;
            musicSources[activeMusicSource].volume = t;
            var fadingOut = 1 - activeMusicSource;
            musicSources[fadingOut].volume = 1f - t;
            if (fadeElapsed >= CrossfadeSeconds) musicSources[fadingOut].Stop();
        }

        private void RequestMusic(MusicTrack track)
        {
            CurrentTrack = track;
            var clip = track == MusicTrack.Day ? dayMusic : track == MusicTrack.Night ? nightMusic : bossMusic;
            if (clip == null) return;
            var current = musicSources[activeMusicSource];
            if (current.clip == clip && current.isPlaying) return;
            activeMusicSource = 1 - activeMusicSource;
            var next = musicSources[activeMusicSource];
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            fadeElapsed = 0f;
        }

        private void SetBaekjungPercussion(bool enabled)
        {
            if (percussionSource == null || baekjungPercussion == null) return;
            if (!enabled)
            {
                percussionSource.Stop();
                return;
            }
            if (percussionSource.isPlaying) return;
            percussionSource.clip = baekjungPercussion;
            percussionSource.volume = 1f;
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
            selected.PlayOneShot(clip);
        }

        private void BuildClipIndex()
        {
            clipsByCue.Clear();
            if (sfxClips == null) return;
            for (var i = 0; i < sfxClips.Length; i++)
                if (sfxClips[i].clip != null && !clipsByCue.ContainsKey(sfxClips[i].cue))
                    clipsByCue.Add(sfxClips[i].cue, sfxClips[i].clip);
        }

        private AudioSource CreateSource(AudioMixerGroup output, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = output;
            source.playOnAwake = false;
            source.loop = loop;
            source.ignoreListenerPause = true;
            return source;
        }

        private static bool IsNormalized(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }
}
