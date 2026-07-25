using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    public enum IdMigrationDomain
    {
        Item,
        Yokai,
        Boss,
        Smelting
    }

    public enum IdMigrationAction
    {
        Rename,
        RemoveRefund
    }

    [CreateAssetMenu(menuName = "Nyangbingo/Data/ID Migration")]
    public sealed class IdMigrationDefinition : ScriptableObject
    {
        [SerializeField] private string legacyId;
        [SerializeField] private string newId;
        [SerializeField] private IdMigrationDomain domain;
        [SerializeField] private IdMigrationAction action;
        [TextArea][SerializeField] private string note;
        [SerializeField] private string refundItemId;
        [Min(0)][SerializeField] private int refundAmount;

        public string LegacyId => legacyId;
        public string NewId => newId;
        public IdMigrationDomain Domain => domain;
        public IdMigrationAction Action => action;
        public string Note => note;
        public string RefundItemId => refundItemId;
        public int RefundAmount => refundAmount;
        public string Key => $"{domain}:{legacyId}";
    }

    public sealed class IdMigrationPolicy
    {
        private readonly Dictionary<string, IdMigrationDefinition> definitionsByKey =
            new Dictionary<string, IdMigrationDefinition>(StringComparer.Ordinal);

        public IdMigrationPolicy(IReadOnlyList<IdMigrationDefinition> definitions)
        {
            IsValid = definitions != null && definitions.Count > 0;
            if (definitions == null) return;

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.LegacyId) ||
                    !definitionsByKey.TryAdd(definition.Key, definition))
                {
                    IsValid = false;
                    continue;
                }

                if (definition.Action == IdMigrationAction.Rename)
                {
                    if (string.IsNullOrWhiteSpace(definition.NewId) ||
                        !string.IsNullOrEmpty(definition.RefundItemId) || definition.RefundAmount != 0)
                        IsValid = false;
                }
                else if (definition.Action == IdMigrationAction.RemoveRefund)
                {
                    if (!string.IsNullOrEmpty(definition.NewId) ||
                        string.IsNullOrWhiteSpace(definition.RefundItemId) || definition.RefundAmount <= 0)
                        IsValid = false;
                }
                else IsValid = false;
            }
        }

        public bool IsValid { get; private set; }
        public int RuleCount => definitionsByKey.Count;

        public IdMigrationDefinition Find(IdMigrationDomain domain, string legacyId)
        {
            if (string.IsNullOrWhiteSpace(legacyId)) return null;
            definitionsByKey.TryGetValue(Key(domain, legacyId), out var definition);
            return definition;
        }

        public string Migrate(IdMigrationDomain domain, string id)
        {
            var definition = Find(domain, id);
            if (definition == null) return id;
            return definition.Action == IdMigrationAction.RemoveRefund ? string.Empty : definition.NewId;
        }

        private static string Key(IdMigrationDomain domain, string legacyId) => $"{domain}:{legacyId}";
    }

    public static class IdMigrationRuntime
    {
        private const string ResourcePath = "Nyangbingo/IdMigrationManifest";
        private const int OfficialRuleCount = 26;

        public static IdMigrationPolicy LoadOfficial()
        {
            var manifest = Resources.Load<IdMigrationManifest>(ResourcePath);
            if (manifest == null)
                throw new InvalidOperationException("The official ID migration manifest is missing.");

            var policy = new IdMigrationPolicy(manifest.Definitions);
            if (!policy.IsValid || policy.RuleCount != OfficialRuleCount)
                throw new InvalidOperationException("The official ID migration manifest is invalid.");
            return policy;
        }
    }
}
