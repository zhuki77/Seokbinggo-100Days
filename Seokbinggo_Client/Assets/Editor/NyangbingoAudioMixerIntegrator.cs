using System;
using System.Linq;
using System.Reflection;
using Nyangbingo.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

public static class NyangbingoAudioMixerIntegrator
{
    public const string MixerPath = "Assets/Audio/NyangbingoAudio.mixer";
    public const string BgmGroupName = "BGM";
    public const string SfxGroupName = "SFX";
    public const string CreditsPath = "Assets/Audio/CREDITS.md";
    private const string BgmFolder = "Assets/Resources/Audio/BGM";
    private const string SfxFolder = "Assets/Resources/Audio/SFX";

    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Nyangbingo/Audio/Apply Product Audio Mixer")]
    public static void Apply()
    {
        EnsureAudioFolder();
        ConfigureAudioImporters();
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null) mixer = CreateMixer();
        if (mixer == null)
        {
            Debug.LogError("[Nyangbingo] Product audio mixer creation failed.");
            return;
        }

        var controller = (UnityEngine.Object)mixer;
        EnsureGroup(controller, mixer, BgmGroupName);
        EnsureGroup(controller, mixer, SfxGroupName);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceSynchronousImport);

        mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var bgm = FindGroup(mixer, BgmGroupName);
        var sfx = FindGroup(mixer, SfxGroupName);
        if (bgm == null || sfx == null)
        {
            Debug.LogError("[Nyangbingo] Product audio mixer groups are missing after creation.");
            return;
        }

        ExposeVolume(controller, bgm, NyangbingoAudioService.BgmVolumeParameter);
        ExposeVolume(controller, sfx, NyangbingoAudioService.SfxVolumeParameter);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        if (!TryRouteMainGameAudio(mixer, bgm, sfx)) return;
        Debug.Log("[Nyangbingo] Product audio mixer integration completed: Master/BGM/SFX, " +
                  "BGMVolume/SFXVolume exposed, MainGame scene updated.");
    }

    /// <summary>
    /// 기존 MainGame 씬의 오디오 라우팅만 갱신한다. 씬을 재생성하면 인스펙터에 수동 배선된
    /// 납품 HUD·팔레트·제작 UI 참조가 모두 사라지므로 여기서는 절대 재생성하지 않는다.
    /// </summary>
    private static bool TryRouteMainGameAudio(AudioMixer mixer, AudioMixerGroup bgm, AudioMixerGroup sfx)
    {
        var scene = EditorSceneManager.OpenScene(
            NyangbingoSceneBuildSettings.MainGameScenePath, OpenSceneMode.Single);
        var audioService = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<NyangbingoAudioService>(true))
            .FirstOrDefault();
        if (audioService == null)
        {
            Debug.LogError("[Nyangbingo] MainGame 씬에서 NyangbingoAudioService를 찾지 못해 오디오 " +
                           "라우팅을 갱신하지 못했습니다. Main Game/Create or Update Main Game Scene을 " +
                           "실행하면 씬이 재생성되어 수동 UI 배선이 사라지니 주의하세요.");
            return false;
        }

        var serialized = new SerializedObject(audioService);
        serialized.FindProperty("audioMixer").objectReferenceValue = mixer;
        serialized.FindProperty("bgmOutput").objectReferenceValue = bgm;
        serialized.FindProperty("sfxOutput").objectReferenceValue = sfx;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(audioService);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, NyangbingoSceneBuildSettings.MainGameScenePath);
        return true;
    }

    [MenuItem("Nyangbingo/Audio/Validate Product Audio Mixer")]
    public static void Validate()
    {
        var failures = new System.Collections.Generic.List<string>();
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var bgm = FindGroup(mixer, BgmGroupName);
        var sfx = FindGroup(mixer, SfxGroupName);
        if (mixer == null) failures.Add($"Mixer missing: {MixerPath}");
        if (bgm == null) failures.Add("BGM group missing.");
        if (sfx == null) failures.Add("SFX group missing.");
        if (mixer != null && !mixer.GetFloat(NyangbingoAudioService.BgmVolumeParameter, out _))
            failures.Add("BGMVolume is not exposed.");
        if (mixer != null && !mixer.GetFloat(NyangbingoAudioService.SfxVolumeParameter, out _))
            failures.Add("SFXVolume is not exposed.");
        ValidateAudioImporters(failures);
        ValidateRequiredClips(failures);
        if (!System.IO.File.Exists(CreditsPath))
            failures.Add($"Audio provenance manifest missing: {CreditsPath}");

        var scene = EditorSceneManager.OpenScene(
            NyangbingoSceneBuildSettings.MainGameScenePath, OpenSceneMode.Single);
        var service = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<NyangbingoAudioService>(true))
            .FirstOrDefault();
        if (service == null)
        {
            failures.Add("MainGame NyangbingoAudioService missing.");
        }
        else
        {
            var serialized = new SerializedObject(service);
            if (serialized.FindProperty("audioMixer").objectReferenceValue != mixer)
                failures.Add("MainGame audio mixer reference missing.");
            if (serialized.FindProperty("bgmOutput").objectReferenceValue != bgm)
                failures.Add("MainGame BGM output reference missing.");
            if (serialized.FindProperty("sfxOutput").objectReferenceValue != sfx)
                failures.Add("MainGame SFX output reference missing.");
        }

        if (failures.Count > 0)
        {
            Debug.LogError("[Nyangbingo] Product audio mixer validation failed:\n- " +
                           string.Join("\n- ", failures));
            return;
        }
        Debug.Log("[Nyangbingo] Product audio mixer validation passed: " +
                  "Master/BGM/SFX, exposed parameters 2/2, MainGame routing 3/3, " +
                  "streaming BGM and SFX cue coverage complete.");
    }

    private static AudioMixer CreateMixer()
    {
        var controllerType = Type.GetType(
            "UnityEditor.Audio.AudioMixerController, UnityEditor");
        var create = controllerType?.GetMethod(
            "CreateMixerControllerAtPath", StaticMembers);
        var controller = create?.Invoke(null, new object[] { MixerPath });
        return controller as AudioMixer;
    }

    private static void ConfigureAudioImporters()
    {
        ConfigureFolder(BgmFolder, streaming: true);
        ConfigureFolder(SfxFolder, streaming: false);
    }

    private static void ConfigureFolder(string folder, bool streaming)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
            var settings = importer.defaultSampleSettings;
            var targetLoadType = streaming
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.DecompressOnLoad;
            var changed = settings.loadType != targetLoadType ||
                          settings.compressionFormat != AudioCompressionFormat.Vorbis ||
                          !Mathf.Approximately(settings.quality, .7f) ||
                          settings.preloadAudioData == streaming ||
                          importer.loadInBackground != streaming;
            if (!changed) continue;
            settings.loadType = targetLoadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .7f;
            settings.preloadAudioData = !streaming;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = streaming;
            importer.SaveAndReimport();
        }
    }

    private static void ValidateAudioImporters(
        System.Collections.Generic.ICollection<string> failures)
    {
        ValidateFolder(BgmFolder, AudioClipLoadType.Streaming, preload: false, background: true,
            failures);
        ValidateFolder(SfxFolder, AudioClipLoadType.DecompressOnLoad, preload: true, background: false,
            failures);
    }

    private static void ValidateFolder(string folder, AudioClipLoadType loadType, bool preload,
        bool background, System.Collections.Generic.ICollection<string> failures)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
            {
                failures.Add($"Audio importer missing: {path}");
                continue;
            }
            var settings = importer.defaultSampleSettings;
            if (settings.loadType != loadType ||
                settings.compressionFormat != AudioCompressionFormat.Vorbis ||
                settings.preloadAudioData != preload ||
                importer.loadInBackground != background)
                failures.Add($"Audio import policy mismatch: {path}");
        }
    }

    private static void ValidateRequiredClips(
        System.Collections.Generic.ICollection<string> failures)
    {
        foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
        {
            var name = track.ToString();
            if (AssetDatabase.LoadAssetAtPath<AudioClip>($"{BgmFolder}/{name}.wav") == null)
                failures.Add($"BGM clip missing: {name}");
        }
        if (AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"{BgmFolder}/BaekjungPercussion.wav") == null)
            failures.Add("BGM layer missing: BaekjungPercussion");
        foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
        {
            var path = $"{SfxFolder}/{cue}.ogg";
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                failures.Add($"SFX cue clip missing: {cue}");
        }
    }

    private static void EnsureGroup(UnityEngine.Object controller, AudioMixer mixer, string groupName)
    {
        if (FindGroup(mixer, groupName) != null) return;
        var type = controller.GetType();
        var master = type.GetProperty("masterGroup", InstanceMembers)?.GetValue(controller);
        var create = type.GetMethod("CreateNewGroup", InstanceMembers);
        var add = type.GetMethod("AddChildToParent", InstanceMembers);
        var group = create?.Invoke(controller, new object[] { groupName, false });
        if (group == null || master == null || add == null)
            throw new InvalidOperationException($"Unable to create audio mixer group '{groupName}'.");
        add.Invoke(controller, new[] { group, master });
    }

    private static void ExposeVolume(
        UnityEngine.Object controller, AudioMixerGroup group, string parameterName)
    {
        var controllerType = controller.GetType();
        var exposedProperty = controllerType.GetProperty("exposedParameters", InstanceMembers);
        var exposed = exposedProperty?.GetValue(controller) as Array;
        if (ContainsExposedName(exposed, parameterName)) return;

        var groupType = group.GetType();
        var getGuid = groupType.GetMethod("GetGUIDForVolume", InstanceMembers);
        var parameterGuid = getGuid?.Invoke(group, null);
        if (parameterGuid == null)
            throw new InvalidOperationException($"Mixer group '{group.name}' volume GUID is missing.");

        var pathType = controllerType.Assembly.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        if (pathType == null)
            throw new InvalidOperationException("Unity audio group parameter path type is missing.");
        var path = Activator.CreateInstance(
            pathType, InstanceMembers, null, new[] { (object)group, parameterGuid }, null);
        controllerType.GetMethod("AddExposedParameter", InstanceMembers)
            ?.Invoke(controller, new[] { path });

        exposed = exposedProperty?.GetValue(controller) as Array;
        if (exposed == null) throw new InvalidOperationException("Mixer exposed parameter list is missing.");
        for (var index = 0; index < exposed.Length; index++)
        {
            var entry = exposed.GetValue(index);
            var entryType = entry.GetType();
            var guid = entryType.GetField("guid", InstanceMembers)?.GetValue(entry);
            if (!Equals(guid, parameterGuid)) continue;
            entryType.GetField("name", InstanceMembers)?.SetValue(entry, parameterName);
            exposed.SetValue(entry, index);
            exposedProperty?.SetValue(controller, exposed);
            return;
        }
        throw new InvalidOperationException($"Unable to expose mixer parameter '{parameterName}'.");
    }

    private static bool ContainsExposedName(Array exposed, string parameterName)
    {
        if (exposed == null) return false;
        foreach (var entry in exposed)
            if (string.Equals(
                    entry.GetType().GetField("name", InstanceMembers)?.GetValue(entry) as string,
                    parameterName, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName) =>
        mixer?.FindMatchingGroups(groupName)
            .FirstOrDefault(group => string.Equals(group.name, groupName, StringComparison.Ordinal));

    private static void EnsureAudioFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Audio"))
            AssetDatabase.CreateFolder("Assets", "Audio");
    }
}
