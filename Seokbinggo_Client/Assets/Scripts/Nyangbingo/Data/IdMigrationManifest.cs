using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/ID Migration Manifest")]
    public sealed class IdMigrationManifest : ScriptableObject
    {
        [SerializeField] private IdMigrationDefinition[] definitions = Array.Empty<IdMigrationDefinition>();

        public IReadOnlyList<IdMigrationDefinition> Definitions =>
            definitions ?? Array.Empty<IdMigrationDefinition>();
    }
}
