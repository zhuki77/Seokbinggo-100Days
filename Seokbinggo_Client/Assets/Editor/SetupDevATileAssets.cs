using System.Collections.Generic;
using System.IO;
using System.Text;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
// UnityEngine.Tilemaps.TilemapRenderer와 Nyangbingo.World.TilemapRenderer가 충돌하므로 명시적으로 구분.
using WorldTilemapRenderer = Nyangbingo.World.TilemapRenderer;

namespace Nyangbingo.Editor
{
    /// <summary>
    /// A-01: DevAWorldTest 씬용 임시 타일/스프라이트 에셋을 자동으로 생성하고 씬에 배선한다.
    ///
    /// 반복문 안에서 텍스처 생성 → 임포트 → 즉시 로드를 한 원소씩 순차 처리하면 Unity의 AssetDatabase
    /// 임포트 파이프라인 타이밍 문제로 일부 원소만 간헐적으로 실패하는 현상이 있었다. 이를 근본적으로
    /// 없애기 위해 Unity가 대량 에셋 생성 시 공식적으로 권장하는 <see cref="AssetDatabase.StartAssetEditing"/>/
    /// <see cref="AssetDatabase.StopAssetEditing"/> 배치 처리로 단계를 명확히 분리한다:
    ///   1단계) 모든 PNG 텍스처 파일을 disk에 쓰고 일괄 임포트
    ///   2단계) 모든 텍스처의 TextureImporter 설정을 Sprite로 일괄 전환
    ///   3단계) 모든 Sprite를 로드해 Tile 에셋을 일괄 생성
    /// 각 단계 사이에는 반드시 StopAssetEditing() + Refresh()로 완전히 flush한 뒤 다음 단계로 넘어간다.
    /// </summary>
    public static class SetupDevATileAssets
    {
        private const string SpritesFolder = "Assets/Sprites/Temp";
        private const string TilesFolder = "Assets/Tiles/Temp";
        private const string ScenePath = "Assets/Scenes/DevAWorldTest.unity";
        private const string FallbackElementType = "_unknown_fallback";

        // 콘솔을 오가며 로그를 옮겨적는 대신, 이번 실행의 모든 진단 로그를 프로젝트 루트의 파일로도
        // 그대로 남긴다(Assets 바깥이라 임포트되지 않음) — 실행이 끝나면 이 파일 하나만 열어보면 이번
        // 실행에서 정확히 무슨 일이 있었는지 처음부터 끝까지 전부 확인할 수 있다.
        private static readonly string DiagnosticLogPath =
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "SetupDevATileAssets_LastRun.log");

        [MenuItem("Nyangbingo/Setup Tilemap Rendering In Dev A Scene")]
        public static void SetupTilemapRenderingInDevAScene()
        {
            var diag = new List<string>();
            void L(string msg) { Debug.Log(msg); diag.Add("[LOG] " + msg); }
            void W(string msg) { Debug.LogWarning(msg); diag.Add("[WARN] " + msg); }
            void E(string msg) { Debug.LogError(msg); diag.Add("[ERROR] " + msg); }

            try
            {
                RunSetup(L, W, E);
            }
            finally
            {
                File.WriteAllLines(DiagnosticLogPath, diag, Encoding.UTF8);
                Debug.Log($"[Nyangbingo] A-01: 이번 실행의 전체 로그를 파일로 저장했습니다 — {DiagnosticLogPath}");
            }
        }

        private static void RunSetup(System.Action<string> L, System.Action<string> W, System.Action<string> E)
        {
            L("[Nyangbingo] A-01: DevAWorldTest 씬용 타일 에셋 생성 및 배선을 시작합니다...");

            EnsureFolderExists(SpritesFolder);
            EnsureFolderExists(TilesFolder);

            // 폴백 타일(_unknown_fallback)도 나머지 18개와 완전히 동일한 파이프라인을 거치게 해서
            // 특정 원소만 예외적으로 다르게 처리하다 생기는 버그를 원천적으로 없앤다.
            var elementTypes = new[]
            {
                WorldTileTypes.Dirt, WorldTileTypes.Stone, WorldTileTypes.Coal, WorldTileTypes.Clay,
                WorldTileTypes.StoneMid, WorldTileTypes.IronOre, WorldTileTypes.CopperOre, WorldTileTypes.IceShard,
                WorldTileTypes.StoneDeep, WorldTileTypes.IceSteelOre, WorldTileTypes.FrostEssence,
                WorldTileTypes.Bedrock, WorldTileTypes.RuinWall, WorldTileTypes.IceLake, WorldTileTypes.IceAltar,
                WorldTileTypes.BackgroundDirt, WorldTileTypes.BackgroundStone, WorldTileTypes.BackgroundDeep,
                FallbackElementType
            };

            // ---------------- 1단계: 텍스처(PNG) 일괄 생성 및 임포트 ----------------
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var elementType in elementTypes)
                {
                    WriteTextureFile(elementType, GetColorForElementType(elementType));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
            L($"[Nyangbingo] A-01: 1단계 완료 — 텍스처 {elementTypes.Length}개 임포트.");

            // ---------------- 2단계: TextureImporter를 Sprite로 일괄 전환 ----------------
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var elementType in elementTypes)
                {
                    ConfigureAsSprite(elementType);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            L($"[Nyangbingo] A-01: 2단계 완료 — Sprite 임포트 설정 {elementTypes.Length}개 적용.");

            // ---------------- 3단계: Sprite 로드 + Tile 에셋 일괄 생성 ----------------
            var tileMap = new Dictionary<string, Tile>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var elementType in elementTypes)
                {
                    var texturePath = GetTexturePath(elementType);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                    if (sprite == null)
                    {
                        E($"[Nyangbingo] A-01: [{texturePath}] Sprite 로드 실패! " +
                          $"'{elementType}'의 Tile 에셋을 만들 수 없습니다.");
                        continue;
                    }

                    var tile = CreateTileAsset(elementType, sprite);
                    if (tile != null)
                    {
                        tileMap[elementType] = tile;
                    }
                    else
                    {
                        E($"[Nyangbingo] A-01: [진단] CreateTileAsset('{elementType}')가 null을 반환했습니다.");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            L($"[Nyangbingo] A-01: 3단계 완료 — Tile 에셋 {tileMap.Count}/{elementTypes.Length}개 생성.");

            // ---------------- 씬 배선 ----------------
            // 바로 아래 OpenScene(Single)이 "죽은 참조" 버그의 진짜 원인이었다: 씬을 Single 모드로
            // 전환하면 Unity가 아직 어떤 씬에도 물려있지 않은(막 만든) 임시 객체 참조를 내부적으로
            // 무효화시킨다(Unity의 오버로드된 == 연산자 때문에 실제로는 null이 아니어도
            // "tile == null"이 true가 되는 fake-null 문제) — 그래서 OpenScene 호출 "전에" tileMap을
            // 아무리 신선하게 채워둬도, 이 호출을 지나가는 순간 전부 다시 죽어버렸다. 그래서 재로드는
            // OpenScene을 통과한 "다음"(바로 아래)에서 딱 한 번만 한다 — 그 뒤로는 씬 전환이 전혀
            // 없으므로 참조가 죽을 일이 없다.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                E($"[Nyangbingo] A-01: 씬을 열 수 없습니다: {ScenePath}");
                return;
            }

            foreach (var elementType in elementTypes)
            {
                var reloaded = AssetDatabase.LoadAssetAtPath<Tile>(GetTilePath(elementType));
                if (reloaded != null)
                {
                    tileMap[elementType] = reloaded;
                }
                else
                {
                    E($"[Nyangbingo] A-01: [{GetTilePath(elementType)}] Tile 에셋을 디스크에서 " +
                      "다시 로드하는 데 실패했습니다 — 3단계에서 생성 자체가 안 됐을 수 있습니다.");
                    tileMap.Remove(elementType);
                }
            }

            L($"[Nyangbingo] A-01: [진단] OpenScene 이후 재로드 완료 — tileMap {tileMap.Count}개, 키 목록: " +
              $"{string.Join(", ", tileMap.Keys)}");

            // Object.FindObjectOfType는 비활성 오브젝트를 건너뛰거나 씬 로드 상태에 따라 예상 밖의
            // 인스턴스를 집을 수 있다는 의심을 원천적으로 없애기 위해, "활성 씬의 루트부터 직접 순회"하는
            // 방식으로 명확하게 찾는다 — 어떤 GameObject에서 찾았는지 이름/경로까지 로그로 남긴다.
            var activeScene = SceneManager.GetActiveScene();
            WorldTilemapRenderer renderer = null;
            foreach (var root in activeScene.GetRootGameObjects())
            {
                renderer = root.GetComponentInChildren<WorldTilemapRenderer>(true);
                if (renderer != null) break;
            }

            if (renderer == null)
            {
                E($"[Nyangbingo] A-01: 활성 씬('{activeScene.name}')의 루트 오브젝트들 중에서 " +
                  "TilemapRenderer 컴포넌트를 찾을 수 없습니다! 하이어라키를 확인하세요.");
                return;
            }

            L($"[Nyangbingo] A-01: TilemapRenderer를 찾았습니다 — GameObject '{GetHierarchyPath(renderer.gameObject)}' " +
              $"(씬 '{activeScene.name}').");

            Undo.RecordObject(renderer, "Setup Dev A Tile Visuals");

            L($"[Nyangbingo] A-01: [진단] 배선 루프 시작 직전 — elementTypes {elementTypes.Length}개, tileMap {tileMap.Count}개.");

            var newVisuals = new List<WorldTilemapRenderer.TileVisual>();
            foreach (var elementType in elementTypes)
            {
                if (elementType == FallbackElementType) continue; // 폴백은 tileVisuals가 아니라 별도 필드로 연결.

                var found = tileMap.TryGetValue(elementType, out var tile);
                var isNull = tile == null;
                if (!found || isNull)
                {
                    W($"[Nyangbingo] A-01: '{elementType}'의 Tile 에셋을 로드하지 못해 " +
                      $"tileVisuals에 연결하지 못했습니다. [진단] tileMap에서 찾음={found}, tile==null={isNull}.");
                    continue;
                }

                newVisuals.Add(new WorldTilemapRenderer.TileVisual { elementType = elementType, tile = tile });
                L($"[Nyangbingo] A-01: {elementType} → {tile.name} 연결됨 (assetPath: {AssetDatabase.GetAssetPath(tile)})");
            }

            L($"[Nyangbingo] A-01: [진단] 배선 루프 종료 — newVisuals.Count = {newVisuals.Count}.");

            // 매핑 없는 elementType은 투명 대신 마젠타로 바로 눈에 띄게 — 방어적 폴백(3순위 안전장치).
            tileMap.TryGetValue(FallbackElementType, out var fallbackTile);

            // ---- 1. 데이터 배선 및 더티 표시만 한다 — 여기서는 절대로 RebuildLookupTable()을 호출하지
            //         않는다. SetTileVisualsForEditorSetup은 이제 필드 대입만 하고 캐시 갱신은 하지 않으므로,
            //         "18개가 디스크에 완전히 반영되기 전에 캐시가 먼저 굳어버리는" 시간차 문제 자체가 발생할
            //         여지가 없다.
            renderer.SetTileVisualsForEditorSetup(newVisuals.ToArray(), fallbackTile);
            EditorUtility.SetDirty(renderer);

            // 카메라 설정도 같은 트랜잭션 안에서 같이 더티 표시해둔다(월드 전체가 보이도록 조정).
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                // 월드 크기: 400x160 (기본 config 기준). 카메라를 월드 중앙에 배치하고,
                // 전체 높이가 보이도록 orthographic size 설정(여유 있게 90).
                camera.transform.position = new Vector3(200f, 80f, -10f);
                camera.orthographicSize = 90f;
                EditorUtility.SetDirty(camera);
                L("[Nyangbingo] A-01: 카메라를 월드 중앙(200, 80)으로 이동, Size=90 설정 완료.");
            }
            else
            {
                W("[Nyangbingo] A-01: 씬에서 Main Camera를 찾을 수 없어 카메라 설정을 건너뜁니다.");
            }

            // ---- 2. [가장 중요] AssetDatabase를 디스크에 강제로 Flush하고 "동기적"으로 리프레시한다.
            //         ForceSynchronousImport를 명시해 임포트가 끝날 때까지 이 호출에서 실제로 블로킹되도록
            //         만든다 — 이후 코드가 이어서 실행된다는 것 자체가 임포트가 완전히 끝났다는 보장이 된다.
            AssetDatabase.SaveAssets();
            if (fallbackTile != null)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(fallbackTile), ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // ---- 3. 씬 세이브로 tileVisuals 18개가 실제로 박힌 바이너리를 디스크에 물리적으로 기록한다.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ---- 4. [피날레] 위의 모든 디스크 쓰기가 끝나 씬에 18개가 확실히 존재하는 시점에만, 이제서야
            //         딱 한 번 룩업 테이블을 갱신한다. 이 로그가 이번 실행에서 "진짜 최종 상태"다 — 씬을
            //         여는 순간 Awake()가 자동으로 한 번 더 찍는 "룩업 테이블이 N개로 갱신" 로그는 저장되기
            //         전(이전 실행 결과 기준) 초기화 로그이므로 무시하고, 아래 로그 이후의 값만 신뢰할 것.
            renderer.RebuildLookupTable();

            // SerializedObject로 같은 값을 다시 읽어와 "에디터 직렬화 시스템도 동의하는지" 최종 이중 확인.
            var verify = new SerializedObject(renderer);
            verify.Update();
            var verifyProp = verify.FindProperty("tileVisuals");
            L($"[Nyangbingo] A-01: [최종 확인] SerializedObject 기준 tileVisuals.arraySize = " +
              $"{verifyProp.arraySize} (기대값 {newVisuals.Count}). 서로 다르면 렌더러 인스턴스가 " +
              "잘못 지정된 것입니다.");

            L($"[Nyangbingo] A-01: TilemapRenderer에 {tileMap.Count - (tileMap.ContainsKey(FallbackElementType) ? 1 : 0)}/" +
              $"{elementTypes.Length - 1}개 타일 항목 연결 완료.");
            L("[Nyangbingo] A-01: DevAWorldTest 씬 타일 에셋 생성 및 배선 완료!");
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            var parent = go.transform.parent;
            while (parent != null)
            {
                sb.Insert(0, parent.name + "/");
                parent = parent.parent;
            }
            return sb.ToString();
        }

        private static void EnsureFolderExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path).Replace('\\', '/');
                var folderName = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static string GetTexturePath(string elementType) => $"{SpritesFolder}/{elementType}_texture.png";
        private static string GetTilePath(string elementType) => $"{TilesFolder}/{elementType}.asset";

        private static void WriteTextureFile(string elementType, Color color)
        {
            var texturePath = GetTexturePath(elementType);
            if (File.Exists(texturePath))
            {
                return;
            }

            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();

            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(texturePath);
            Object.DestroyImmediate(texture);
        }

        private static void ConfigureAsSprite(string elementType)
        {
            var texturePath = GetTexturePath(elementType);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Nyangbingo] A-01: [{texturePath}] TextureImporter를 가져오지 못했습니다 " +
                                "(1단계 임포트가 끝나지 않았을 가능성).");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static Tile CreateTileAsset(string elementType, Sprite sprite)
        {
            var tilePath = GetTilePath(elementType);
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null)
            {
                if (existing.sprite == null)
                {
                    existing.sprite = sprite;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile, tilePath);
            return tile;
        }

        private static Color GetColorForElementType(string elementType)
        {
            // 각 타일별 임시 색상 (디버그용 구분)
            return elementType switch
            {
                WorldTileTypes.Dirt => new Color(0.6f, 0.4f, 0.2f), // 갈색
                WorldTileTypes.Stone => new Color(0.5f, 0.5f, 0.5f), // 회색
                WorldTileTypes.Coal => new Color(0.2f, 0.2f, 0.2f), // 검정
                WorldTileTypes.Clay => new Color(0.8f, 0.6f, 0.4f), // 주황빛 갈색
                WorldTileTypes.StoneMid => new Color(0.4f, 0.4f, 0.4f), // 진회색
                WorldTileTypes.IronOre => new Color(0.7f, 0.5f, 0.4f), // 철색
                WorldTileTypes.CopperOre => new Color(0.8f, 0.4f, 0.2f), // 구리색
                WorldTileTypes.IceShard => new Color(0.7f, 0.9f, 1.0f), // 하늘색
                WorldTileTypes.StoneDeep => new Color(0.3f, 0.3f, 0.3f), // 어두운 회색
                WorldTileTypes.IceSteelOre => new Color(0.6f, 0.8f, 0.9f), // 청회색
                WorldTileTypes.FrostEssence => new Color(0.8f, 0.95f, 1.0f), // 밝은 청색
                WorldTileTypes.Bedrock => new Color(0.1f, 0.1f, 0.1f), // 거의 검정
                WorldTileTypes.RuinWall => new Color(0.6f, 0.55f, 0.5f), // 폐허색
                WorldTileTypes.IceLake => new Color(0.5f, 0.7f, 0.9f), // 물색
                WorldTileTypes.IceAltar => new Color(0.9f, 0.95f, 1.0f), // 신성한 흰색
                WorldTileTypes.BackgroundDirt => new Color(0.4f, 0.3f, 0.15f), // 어두운 갈색
                WorldTileTypes.BackgroundStone => new Color(0.35f, 0.35f, 0.35f), // 어두운 회색
                WorldTileTypes.BackgroundDeep => new Color(0.2f, 0.2f, 0.2f), // 거의 검정
                _ => Color.magenta // 미지정(_unknown_fallback 포함)
            };
        }
    }
}
