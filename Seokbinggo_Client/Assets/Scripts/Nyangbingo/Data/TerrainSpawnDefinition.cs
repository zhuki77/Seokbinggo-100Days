using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Terrain Spawn")]
    public sealed class TerrainSpawnDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string terrainId;
        [SerializeField] private string terrainDisplayName;
        [SerializeField] private string yokaiId;
        [SerializeField] private string yokaiDisplayName;
        [SerializeField] private int weight;
        [SerializeField] private bool implemented;
        [TextArea][SerializeField] private string note;
        [SerializeField] private string[] terrainResourceIds = Array.Empty<string>();
        public string Id => id;
        public string TerrainId => terrainId ?? string.Empty;
        public string TerrainDisplayName => terrainDisplayName ?? string.Empty;
        public string YokaiId => yokaiId ?? string.Empty;
        public string YokaiDisplayName => yokaiDisplayName ?? string.Empty;
        public int Weight => weight;
        public bool Implemented => implemented;
        public string Note => note ?? string.Empty;
        public IReadOnlyList<string> TerrainResourceIds => terrainResourceIds ?? Array.Empty<string>();
    }
}
