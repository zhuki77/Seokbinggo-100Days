using System;
using System.Collections.Generic;

namespace Nyangbingo.Crafting
{
    /// <summary>
    /// v46 장비 진화: crafting-tree materials 첫 토큰이 원본 id이면 진화.
    /// 원본 1개를 반드시 소모한다(6-14).
    /// </summary>
    public static class EvolutionCraft
    {
        public const int SmithyUnlockStage = 4;

        public static bool IsEvolutionRecipe(string sourceItemId, IReadOnlyList<string> materialIds)
        {
            if (string.IsNullOrWhiteSpace(sourceItemId) || materialIds == null || materialIds.Count == 0)
                return false;
            return string.Equals(materialIds[0], sourceItemId, StringComparison.Ordinal);
        }

        public static bool CanEvolve(
            string sourceItemId,
            IReadOnlyList<(string id, int count)> materials,
            Func<string, int, bool> hasItems)
        {
            if (hasItems == null || materials == null || materials.Count == 0) return false;
            if (!IsEvolutionRecipe(sourceItemId, ExtractIds(materials))) return false;
            for (var i = 0; i < materials.Count; i++)
            {
                var (id, count) = materials[i];
                if (string.IsNullOrWhiteSpace(id) || count <= 0) return false;
                if (!hasItems(id, count)) return false;
            }

            return true;
        }

        public static bool TryEvolve(
            string sourceItemId,
            IReadOnlyList<(string id, int count)> materials,
            Func<string, int, bool> hasItems,
            Func<string, int, bool> tryRemove,
            Action<string> grantResult,
            string resultItemId)
        {
            if (grantResult == null || string.IsNullOrWhiteSpace(resultItemId) || tryRemove == null)
                return false;
            if (!CanEvolve(sourceItemId, materials, hasItems)) return false;

            for (var i = 0; i < materials.Count; i++)
            {
                var (id, count) = materials[i];
                if (!tryRemove(id, count)) return false;
            }

            grantResult(resultItemId);
            return true;
        }

        public static bool IsSmithyUnlocked(int seokbinggoStage) =>
            seokbinggoStage >= SmithyUnlockStage;

        private static List<string> ExtractIds(IReadOnlyList<(string id, int count)> materials)
        {
            var ids = new List<string>(materials.Count);
            for (var i = 0; i < materials.Count; i++)
                ids.Add(materials[i].id);
            return ids;
        }
    }
}
