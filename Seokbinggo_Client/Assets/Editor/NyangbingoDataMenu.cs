using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Nyangbingo.Data;

public static class NyangbingoDataMenu
{
    [MenuItem("Nyangbingo/Rebuild Game Data Catalog")]
    private static void RebuildGameDataCatalog()
    {
        const string rootDirectory = "Assets/Data/SO";
        const string assetPath = rootDirectory + "/GameDataCatalog.asset";
        EnsureFolder(rootDirectory);

        var catalog = AssetDatabase.LoadAssetAtPath<GameDataCatalog>(assetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<GameDataCatalog>();
            AssetDatabase.CreateAsset(catalog, assetPath);
        }

        var items = LoadAssets<ItemDefinition>(rootDirectory + "/Items");
        var recipes = LoadAssets<RecipeDefinition>(rootDirectory + "/Recipes");
        var smelting = LoadAssets<SmeltingDefinition>(rootDirectory + "/Smelting");
        var equipment = LoadAssets<EquipmentDefinition>(rootDirectory + "/Equipment");
        var utilities = LoadAssets<UtilityDefinition>(rootDirectory + "/Utilities");
        var yokai = LoadAssets<YokaiDefinition>(rootDirectory + "/Yokai");
        var bosses = LoadAssets<BossDefinition>(rootDirectory + "/Bosses");
        var chests = LoadAssets<ChestDefinition>(rootDirectory + "/Chests");
        var dayEvents = LoadAssets<DayEventDefinition>(rootDirectory + "/DayEvents");

        if (!ValidateAssetIds(items, value => value.Id, "items") ||
            !ValidateAssetIds(recipes, value => value.Id, "recipes") ||
            !ValidateAssetIds(smelting, value => value.Id, "smelting") ||
            !ValidateAssetIds(equipment, value => value.Id, "equipment") ||
            !ValidateAssetIds(utilities, value => value.Id, "utilities") ||
            !ValidateAssetIds(yokai, value => value.Id, "yokai") ||
            !ValidateAssetIds(bosses, value => value.Id, "bosses") ||
            !ValidateAssetIds(chests, value => value.Id, "chests") ||
            !ValidateAssetIds(dayEvents, value => value.Id, "day events"))
            return;

        Debug.Log("[Nyangbingo] Game data catalog source ID validation completed.");

        var serialized = new SerializedObject(catalog);
        SetObjectReferences(serialized.FindProperty("items"), items);
        SetObjectReferences(serialized.FindProperty("recipes"), recipes);
        SetObjectReferences(serialized.FindProperty("smelting"), smelting);
        SetObjectReferences(serialized.FindProperty("equipment"), equipment);
        SetObjectReferences(serialized.FindProperty("utilities"), utilities);
        SetObjectReferences(serialized.FindProperty("yokai"), yokai);
        SetObjectReferences(serialized.FindProperty("bosses"), bosses);
        SetObjectReferences(serialized.FindProperty("chests"), chests);
        SetObjectReferences(serialized.FindProperty("dayEvents"), dayEvents);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Nyangbingo] Game data catalog rebuilt: {items.Length} items, {recipes.Length} recipes, " +
                  $"{smelting.Length} smelting, {equipment.Length} equipment, {utilities.Length} utilities, " +
                  $"{yokai.Length} yokai, {bosses.Length} bosses, {chests.Length} chests, {dayEvents.Length} day events.");
    }

    [MenuItem("Nyangbingo/Validate CSV Data")]
    private static void ValidateCsvData()
    {
        var directory = Path.Combine(Application.dataPath, "Data", "CSV");
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.csv") : new string[0];
        System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
        if (files.Length == 0)
        {
            Debug.LogError("[Nyangbingo] No CSV files were found to validate.");
            return;
        }

        var valid = true;
        foreach (var file in files)
        {
            try
            {
                var rows = NyangbingoCsvUtility.ReadRows(file);
                Debug.Log($"[Nyangbingo] CSV validated: {Path.GetFileName(file)} ({rows.Count} rows)");
            }
            catch (System.Exception exception)
            {
                valid = false;
                Debug.LogError($"[Nyangbingo] CSV validation failed: {Path.GetFileName(file)} - {exception.Message}");
            }
        }

        if (valid)
            Debug.Log($"[Nyangbingo] CSV structural and unique-ID validation completed: {files.Length} files.");
    }

    [MenuItem("Nyangbingo/Reimport Items CSV")]
    private static void ReimportItems()
    {
        const string csvPath = "Assets/Data/CSV/items.csv";
        const string targetDirectory = "Assets/Data/SO/Items";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] items.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Item CSV validation failed: {exception.Message}");
            return;
        }

        var maxStacks = new int[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                string.IsNullOrWhiteSpace(row["displayName"]) ||
                !int.TryParse(row["maxStack"], System.Globalization.NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out maxStacks[i]) || maxStacks[i] < 1)
            {
                Debug.LogError($"[Nyangbingo] Item '{id}' must have a safe ID, display name, and maxStack of at least 1.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Item CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder("Assets/Data/SO"); EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            if (item == null) { item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item, assetPath); }
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = row["displayName"];
            serialized.FindProperty("maxStack").intValue = maxStacks[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] Item CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport Recipes CSV")]
    private static void ReimportRecipes()
    {
        const string csvPath = "Assets/Data/CSV/recipes.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/Recipes";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] recipes.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Recipe CSV validation failed: {exception.Message}");
            return;
        }

        var stations = new Nyangbingo.Core.CraftingStation[rows.Count];
        var outputs = new ItemDefinition[rows.Count];
        var outputAmounts = new int[rows.Count];
        var durations = new float[rows.Count];
        var ingredientsByRecipe = new ItemAmount[rows.Count][];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !System.Enum.TryParse(row["station"], true, out stations[rowIndex]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.CraftingStation), stations[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Recipe '{id}' has an unsafe ID or unknown station '{row["station"]}'.");
                return;
            }

            outputs[rowIndex] = FindItem(itemDirectory, row["output"]);
            if (outputs[rowIndex] == null || outputs[rowIndex].Id != row["output"] ||
                !int.TryParse(row["amount"], System.Globalization.NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out outputAmounts[rowIndex]) || outputAmounts[rowIndex] <= 0 ||
                !float.TryParse(row["durationSeconds"], System.Globalization.NumberStyles.Float,
                    CultureInfo.InvariantCulture, out durations[rowIndex]) || durations[rowIndex] < 0f ||
                float.IsNaN(durations[rowIndex]) || float.IsInfinity(durations[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Recipe '{id}' has an invalid output, amount, or durationSeconds.");
                return;
            }

            var encodedIngredients = row["ingredients"].Split('|');
            if (encodedIngredients.Length == 0 || string.IsNullOrWhiteSpace(row["ingredients"]))
            {
                Debug.LogError($"[Nyangbingo] Recipe '{id}' must have at least one ingredient.");
                return;
            }
            var ingredientIds = new HashSet<string>(System.StringComparer.Ordinal);
            var ingredients = new ItemAmount[encodedIngredients.Length];
            for (var ingredientIndex = 0; ingredientIndex < encodedIngredients.Length; ingredientIndex++)
            {
                var parts = encodedIngredients[ingredientIndex].Split(':');
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !ingredientIds.Add(parts[0]) ||
                    !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Recipe '{id}' has an invalid or duplicate ingredient '{encodedIngredients[ingredientIndex]}'.");
                    return;
                }
                var item = FindItem(itemDirectory, parts[0]);
                if (item == null || item.Id != parts[0])
                {
                    Debug.LogError($"[Nyangbingo] Recipe '{id}' has an unknown ingredient item '{parts[0]}'.");
                    return;
                }
                ingredients[ingredientIndex] = new ItemAmount { item = item, amount = amount };
            }
            ingredientsByRecipe[rowIndex] = ingredients;
        }

        Debug.Log($"[Nyangbingo] Recipe CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(assetPath);
            if (recipe == null) { recipe = ScriptableObject.CreateInstance<RecipeDefinition>(); AssetDatabase.CreateAsset(recipe, assetPath); }
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("station").enumValueIndex = (int)stations[rowIndex];
            SetItemAmount(serialized.FindProperty("output"), outputs[rowIndex], outputAmounts[rowIndex]);
            serialized.FindProperty("durationSeconds").floatValue = durations[rowIndex];
            var ingredients = ingredientsByRecipe[rowIndex];
            var property = serialized.FindProperty("ingredients");
            property.arraySize = ingredients.Length;
            for (var ingredientIndex = 0; ingredientIndex < ingredients.Length; ingredientIndex++)
            {
                SetItemAmount(property.GetArrayElementAtIndex(ingredientIndex),
                    ingredients[ingredientIndex].item, ingredients[ingredientIndex].amount);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(recipe);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("[Nyangbingo] Recipe CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport Smelting CSV")]
    private static void ReimportSmelting()
    {
        const string csvPath = "Assets/Data/CSV/smelting.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/Smelting";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] smelting.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Smelting CSV validation failed: {exception.Message}");
            return;
        }

        var stationKinds = new Nyangbingo.Core.SmeltingStationKind[rows.Count];
        var inputs = new ItemDefinition[rows.Count];
        var fuels = new ItemDefinition[rows.Count];
        var outputs = new ItemDefinition[rows.Count];
        var inputAmounts = new int[rows.Count];
        var fuelAmounts = new int[rows.Count];
        var outputAmounts = new int[rows.Count];
        var durations = new float[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !System.Enum.TryParse(row["station"], true, out stationKinds[i]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.SmeltingStationKind), stationKinds[i]))
            {
                Debug.LogError($"[Nyangbingo] Smelting '{id}' has an unsafe ID or unknown station '{row["station"]}'.");
                return;
            }

            inputs[i] = FindItem(itemDirectory, row["input"]);
            fuels[i] = FindItem(itemDirectory, row["fuel"]);
            outputs[i] = FindItem(itemDirectory, row["output"]);
            if (inputs[i] == null || inputs[i].Id != row["input"] ||
                fuels[i] == null || fuels[i].Id != row["fuel"] ||
                outputs[i] == null || outputs[i].Id != row["output"] ||
                !int.TryParse(row["inputAmount"], System.Globalization.NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out inputAmounts[i]) || inputAmounts[i] <= 0 ||
                !int.TryParse(row["fuelAmount"], System.Globalization.NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out fuelAmounts[i]) || fuelAmounts[i] <= 0 ||
                !int.TryParse(row["outputAmount"], System.Globalization.NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out outputAmounts[i]) || outputAmounts[i] <= 0 ||
                !float.TryParse(row["durationSeconds"], System.Globalization.NumberStyles.Float,
                    CultureInfo.InvariantCulture, out durations[i]) || durations[i] <= 0f ||
                float.IsNaN(durations[i]) || float.IsInfinity(durations[i]))
            {
                Debug.LogError($"[Nyangbingo] Smelting '{id}' has an invalid item, amount, or durationSeconds.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Smelting CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var id = rows[i]["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<SmeltingDefinition>(assetPath);
            if (definition == null) { definition = ScriptableObject.CreateInstance<SmeltingDefinition>(); AssetDatabase.CreateAsset(definition, assetPath); }
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("stationKind").enumValueIndex = (int)stationKinds[i];
            SetItemAmount(serialized.FindProperty("input"), inputs[i], inputAmounts[i]);
            SetItemAmount(serialized.FindProperty("fuel"), fuels[i], fuelAmounts[i]);
            SetItemAmount(serialized.FindProperty("output"), outputs[i], outputAmounts[i]);
            serialized.FindProperty("durationSeconds").floatValue = durations[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("[Nyangbingo] Smelting CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport Equipment CSV")]
    private static void ReimportEquipment()
    {
        const string csvPath = "Assets/Data/CSV/equipment.csv";
        const string targetDirectory = "Assets/Data/SO/Equipment";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] equipment.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Equipment CSV validation failed: {exception.Message}");
            return;
        }

        var slots = new Nyangbingo.Core.EquipmentSlot[rows.Count];
        var accessories = new bool[rows.Count];
        var defenses = new int[rows.Count];
        var movementBonuses = new float[rows.Count];
        var miningCriticalBonuses = new float[rows.Count];
        var temperatureModifiers = new float[rows.Count];
        var fireModifiers = new float[rows.Count];
        var doubleJumps = new bool[rows.Count];
        var visionBonuses = new float[rows.Count];
        var theftBlocks = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !System.Enum.TryParse(row["slot"], true, out slots[i]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.EquipmentSlot), slots[i]) ||
                !bool.TryParse(row["isAccessory"], out accessories[i]))
            {
                Debug.LogError($"[Nyangbingo] Equipment '{id}' has an unsafe ID, slot, or accessory flag.");
                return;
            }

            var accessorySlot = slots[i] == Nyangbingo.Core.EquipmentSlot.AccessoryOne ||
                                slots[i] == Nyangbingo.Core.EquipmentSlot.AccessoryTwo;
            if (accessorySlot != accessories[i] ||
                !int.TryParse(row["defense"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out defenses[i]) || defenses[i] < 0 ||
                !TryParseFiniteFloat(row["movementBonus"], out movementBonuses[i]) ||
                !TryParseFiniteFloat(row["miningCriticalBonus"], out miningCriticalBonuses[i]) ||
                !TryParseFiniteFloat(row["temperatureRiseModifier"], out temperatureModifiers[i]) ||
                !TryParseFiniteFloat(row["fireDamageModifier"], out fireModifiers[i]) ||
                !bool.TryParse(row["grantsDoubleJump"], out doubleJumps[i]) ||
                !TryParseFiniteFloat(row["visionRadiusBonus"], out visionBonuses[i]) ||
                !bool.TryParse(row["blocksInventoryTheft"], out theftBlocks[i]))
            {
                Debug.LogError($"[Nyangbingo] Equipment '{id}' has mismatched slot data or invalid stats.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Equipment CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var id = rows[i]["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("slot").enumValueIndex = (int)slots[i];
            serialized.FindProperty("accessory").boolValue = accessories[i];
            serialized.FindProperty("defense").intValue = defenses[i];
            serialized.FindProperty("movementBonus").floatValue = movementBonuses[i];
            serialized.FindProperty("miningCriticalBonus").floatValue = miningCriticalBonuses[i];
            serialized.FindProperty("temperatureRiseModifier").floatValue = temperatureModifiers[i];
            serialized.FindProperty("fireDamageModifier").floatValue = fireModifiers[i];
            serialized.FindProperty("grantsDoubleJump").boolValue = doubleJumps[i];
            serialized.FindProperty("visionRadiusBonus").floatValue = visionBonuses[i];
            serialized.FindProperty("blocksInventoryTheft").boolValue = theftBlocks[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Equipment CSV reimport completed: {rows.Count} assets.");
    }

    [MenuItem("Nyangbingo/Reimport Utilities CSV")]
    private static void ReimportUtilities()
    {
        const string csvPath = "Assets/Data/CSV/utilities.csv";
        const string targetDirectory = "Assets/Data/SO/Utilities";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] utilities.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Utility CSV validation failed: {exception.Message}");
            return;
        }

        var kinds = new Nyangbingo.Core.UtilityKind[rows.Count];
        var cooldowns = new float[rows.Count];
        var values = new float[rows.Count];
        var consumables = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !System.Enum.TryParse(row["kind"], true, out kinds[i]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.UtilityKind), kinds[i]))
            {
                Debug.LogError($"[Nyangbingo] Utility '{id}' has an unsafe ID or unknown kind '{row["kind"]}'.");
                return;
            }

            if (!TryParseFiniteFloat(row["cooldownSeconds"], out cooldowns[i]) || cooldowns[i] < 0f ||
                !TryParseFiniteFloat(row["value"], out values[i]) || values[i] < 0f ||
                !bool.TryParse(row["consumable"], out consumables[i]))
            {
                Debug.LogError($"[Nyangbingo] Utility '{id}' has invalid cooldown, value, or consumable data.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Utility CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var id = rows[i]["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<UtilityDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UtilityDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("kind").enumValueIndex = (int)kinds[i];
            serialized.FindProperty("cooldownSeconds").floatValue = cooldowns[i];
            serialized.FindProperty("value").floatValue = values[i];
            serialized.FindProperty("consumable").boolValue = consumables[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Utility CSV reimport completed: {rows.Count} assets.");
    }

    [MenuItem("Nyangbingo/Reimport Bosses CSV")]
    private static void ReimportBosses()
    {
        const string csvPath = "Assets/Data/CSV/bosses.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/Bosses";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] bosses.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Boss CSV validation failed: {exception.Message}");
            return;
        }

        var kinds = new Nyangbingo.Core.BossKind[rows.Count];
        var hitPoints = new int[rows.Count];
        var combatSeconds = new float[rows.Count];
        var summonItems = new ItemDefinition[rows.Count];
        var deepAltarRequirements = new bool[rows.Count];
        var forcedDays = new int[rows.Count];
        var guaranteedDrops = new ItemAmount[rows.Count][];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !System.Enum.TryParse(row["kind"], true, out kinds[rowIndex]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.BossKind), kinds[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' has an unsafe ID or unknown kind '{row["kind"]}'.");
                return;
            }

            if (!int.TryParse(row["hitPoints"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHitPoints) ||
                !TryParseFiniteFloat(row["expectedCombatSeconds"], out combatSeconds[rowIndex]) ||
                !bool.TryParse(row["requiresDeepAltar"], out deepAltarRequirements[rowIndex]) ||
                !int.TryParse(row["forcedDay"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out forcedDays[rowIndex]) || parsedHitPoints <= 0 || combatSeconds[rowIndex] <= 0f ||
                forcedDays[rowIndex] < 0)
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' has invalid combat or summon condition data.");
                return;
            }
            hitPoints[rowIndex] = parsedHitPoints;

            summonItems[rowIndex] = FindItem(itemDirectory, row["summonItem"]);
            if (summonItems[rowIndex] == null || summonItems[rowIndex].Id != row["summonItem"])
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' has an unknown summon item '{row["summonItem"]}'. Reimport Items CSV first.");
                return;
            }

            var drops = row["guaranteedDrops"].Split('|');
            if (drops.Length == 0 || string.IsNullOrWhiteSpace(row["guaranteedDrops"]))
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' must have at least one guaranteed drop.");
                return;
            }
            var uniqueDropIds = new HashSet<string>(System.StringComparer.Ordinal);
            var parsedDrops = new ItemAmount[drops.Length];
            for (var dropIndex = 0; dropIndex < drops.Length; dropIndex++)
            {
                var parts = drops[dropIndex].Split(':');
                var dropItem = parts.Length == 2 ? FindItem(itemDirectory, parts[0]) : null;
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !uniqueDropIds.Add(parts[0]) ||
                    dropItem == null || dropItem.Id != parts[0] ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Boss '{id}' has an invalid or duplicate guaranteed drop '{drops[dropIndex]}'.");
                    return;
                }
                parsedDrops[dropIndex] = new ItemAmount { item = dropItem, amount = amount };
            }
            guaranteedDrops[rowIndex] = parsedDrops;
        }

        Debug.Log($"[Nyangbingo] Boss CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var id = rows[rowIndex]["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BossDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("kind").enumValueIndex = (int)kinds[rowIndex];
            serialized.FindProperty("hitPoints").intValue = hitPoints[rowIndex];
            serialized.FindProperty("expectedCombatSeconds").floatValue = combatSeconds[rowIndex];
            serialized.FindProperty("summonItem").objectReferenceValue = summonItems[rowIndex];
            serialized.FindProperty("requiresDeepAltar").boolValue = deepAltarRequirements[rowIndex];
            serialized.FindProperty("forcedDay").intValue = forcedDays[rowIndex];
            var dropsProperty = serialized.FindProperty("guaranteedDrops");
            dropsProperty.arraySize = guaranteedDrops[rowIndex].Length;
            for (var dropIndex = 0; dropIndex < guaranteedDrops[rowIndex].Length; dropIndex++)
                SetItemAmount(dropsProperty.GetArrayElementAtIndex(dropIndex),
                    guaranteedDrops[rowIndex][dropIndex].item, guaranteedDrops[rowIndex][dropIndex].amount);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Boss CSV reimport completed: {rows.Count} assets.");
    }

    [MenuItem("Nyangbingo/Reimport Chests CSV")]
    private static void ReimportChests()
    {
        const string csvPath = "Assets/Data/CSV/chests.csv";
        const string equipmentDirectory = "Assets/Data/SO/Equipment";
        const string targetDirectory = "Assets/Data/SO/Chests";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] chests.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Chest CSV validation failed: {exception.Message}");
            return;
        }

        var regions = new Nyangbingo.Core.ChestRegion[rows.Count];
        var equipmentPools = new EquipmentDefinition[rows.Count][];
        var uniqueRegions = new HashSet<Nyangbingo.Core.ChestRegion>();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !System.Enum.TryParse(row["region"], true, out regions[rowIndex]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.ChestRegion), regions[rowIndex]) ||
                !uniqueRegions.Add(regions[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Chest '{id}' has an unsafe ID, unknown region, or duplicate region '{row["region"]}'.");
                return;
            }

            var equipmentIds = row["equipmentPool"].Split('|');
            var equipmentPool = new EquipmentDefinition[equipmentIds.Length];
            var uniqueIds = new HashSet<string>(System.StringComparer.Ordinal);
            if (equipmentIds.Length == 0 || string.IsNullOrWhiteSpace(row["equipmentPool"]))
            {
                Debug.LogError($"[Nyangbingo] Chest '{id}' must have at least one accessory reward.");
                return;
            }
            for (var poolIndex = 0; poolIndex < equipmentIds.Length; poolIndex++)
            {
                var equipmentId = equipmentIds[poolIndex];
                equipmentPool[poolIndex] = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(
                    $"{equipmentDirectory}/{equipmentId}.asset");
                if (string.IsNullOrWhiteSpace(equipmentId) || !uniqueIds.Add(equipmentId) ||
                    equipmentPool[poolIndex] == null || equipmentPool[poolIndex].Id != equipmentId ||
                    !equipmentPool[poolIndex].IsAccessory ||
                    (equipmentPool[poolIndex].Slot != Nyangbingo.Core.EquipmentSlot.AccessoryOne &&
                     equipmentPool[poolIndex].Slot != Nyangbingo.Core.EquipmentSlot.AccessoryTwo))
                {
                    Debug.LogError($"[Nyangbingo] Chest '{id}' has an invalid accessory pool entry '{equipmentId}'. " +
                                   "Reimport Equipment CSV first.");
                    return;
                }
            }
            equipmentPools[rowIndex] = equipmentPool;
        }

        Debug.Log($"[Nyangbingo] Chest CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var id = rows[rowIndex]["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ChestDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ChestDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("region").enumValueIndex = (int)regions[rowIndex];
            serialized.FindProperty("rewards").arraySize = 0;
            SetObjectReferences(serialized.FindProperty("equipmentPool"), equipmentPools[rowIndex]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Chest CSV reimport completed: {rows.Count} assets.");
    }

    [MenuItem("Nyangbingo/Reimport Day Events CSV")]
    private static void ReimportDayEvents()
    {
        const string csvPath = "Assets/Data/CSV/day-events.csv";
        const string targetDirectory = "Assets/Data/SO/DayEvents";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] day-events.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Day event CSV validation failed: {exception.Message}");
            return;
        }

        var days = new int[rows.Count];
        var maxActive = new int[rows.Count];
        var tearMultipliers = new float[rows.Count];
        var signatureMultipliers = new float[rows.Count];
        var waveOffsets = new float[rows.Count][];
        var compositions = new Nyangbingo.Data.YokaiSpawnAmount[rows.Count][];
        var uniqueDays = new HashSet<int>();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !int.TryParse(row["day"], NumberStyles.Integer, CultureInfo.InvariantCulture, out days[rowIndex]) ||
                days[rowIndex] < 1 || !uniqueDays.Add(days[rowIndex]) ||
                !int.TryParse(row["maxActive"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out maxActive[rowIndex]) || maxActive[rowIndex] < 1 ||
                !TryParseFiniteFloat(row["tearMultiplier"], out tearMultipliers[rowIndex]) ||
                tearMultipliers[rowIndex] < 0f ||
                !TryParseFiniteFloat(row["signatureMultiplier"], out signatureMultipliers[rowIndex]) ||
                signatureMultipliers[rowIndex] < 0f)
            {
                Debug.LogError($"[Nyangbingo] Day event '{id}' has an unsafe ID, duplicate day, or invalid limits/reward multipliers.");
                return;
            }

            var encodedOffsets = row["waveOffsets"].Split('|');
            if (encodedOffsets.Length == 0 || string.IsNullOrWhiteSpace(row["waveOffsets"]))
            {
                Debug.LogError($"[Nyangbingo] Day event '{id}' must have at least one wave offset.");
                return;
            }
            var parsedOffsets = new float[encodedOffsets.Length];
            for (var offsetIndex = 0; offsetIndex < encodedOffsets.Length; offsetIndex++)
            {
                if (!TryParseFiniteFloat(encodedOffsets[offsetIndex], out parsedOffsets[offsetIndex]) ||
                    parsedOffsets[offsetIndex] < 0f ||
                    (offsetIndex > 0 && parsedOffsets[offsetIndex] <= parsedOffsets[offsetIndex - 1]))
                {
                    Debug.LogError($"[Nyangbingo] Day event '{id}' wave offsets must be finite, nonnegative, and strictly increasing gameSeconds.");
                    return;
                }
            }
            waveOffsets[rowIndex] = parsedOffsets;

            var encodedComposition = row["composition"].Split('|');
            if (encodedComposition.Length == 0 || string.IsNullOrWhiteSpace(row["composition"]))
            {
                Debug.LogError($"[Nyangbingo] Day event '{id}' must have at least one yokai composition entry.");
                return;
            }
            var uniqueKinds = new HashSet<Nyangbingo.Core.YokaiKind>();
            var parsedComposition = new Nyangbingo.Data.YokaiSpawnAmount[encodedComposition.Length];
            for (var compositionIndex = 0; compositionIndex < encodedComposition.Length; compositionIndex++)
            {
                var parts = encodedComposition[compositionIndex].Split(':');
                if (parts.Length != 2 ||
                    !System.Enum.TryParse(parts[0], true, out Nyangbingo.Core.YokaiKind kind) ||
                    !System.Enum.IsDefined(typeof(Nyangbingo.Core.YokaiKind), kind) || !uniqueKinds.Add(kind) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Day event '{id}' has an invalid or duplicate composition '{encodedComposition[compositionIndex]}'.");
                    return;
                }
                parsedComposition[compositionIndex] = new Nyangbingo.Data.YokaiSpawnAmount
                {
                    kind = kind,
                    amount = amount
                };
            }
            compositions[rowIndex] = parsedComposition;
        }

        Debug.Log($"[Nyangbingo] Day event CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var id = rows[rowIndex]["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<DayEventDefinition>(assetPath);
            var scriptReferenceMissing = definition != null
                && new SerializedObject(definition).FindProperty("m_Script")?.objectReferenceValue == null;
            if (definition == null || scriptReferenceMissing)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    Debug.LogWarning($"[Nyangbingo] Recreating invalid day event asset: {assetPath}");
                }

                definition = ScriptableObject.CreateInstance<DayEventDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("day").intValue = days[rowIndex];
            serialized.FindProperty("maxActive").intValue = maxActive[rowIndex];
            serialized.FindProperty("tearMultiplier").floatValue = tearMultipliers[rowIndex];
            serialized.FindProperty("signatureMultiplier").floatValue = signatureMultipliers[rowIndex];

            var offsetsProperty = serialized.FindProperty("waveOffsets");
            offsetsProperty.arraySize = waveOffsets[rowIndex].Length;
            for (var offsetIndex = 0; offsetIndex < waveOffsets[rowIndex].Length; offsetIndex++)
                offsetsProperty.GetArrayElementAtIndex(offsetIndex).floatValue = waveOffsets[rowIndex][offsetIndex];

            var compositionProperty = serialized.FindProperty("composition");
            compositionProperty.arraySize = compositions[rowIndex].Length;
            for (var compositionIndex = 0; compositionIndex < compositions[rowIndex].Length; compositionIndex++)
            {
                var element = compositionProperty.GetArrayElementAtIndex(compositionIndex);
                element.FindPropertyRelative("kind").enumValueIndex = (int)compositions[rowIndex][compositionIndex].kind;
                element.FindPropertyRelative("amount").intValue = compositions[rowIndex][compositionIndex].amount;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Day event CSV reimport completed: {rows.Count} assets.");
    }

    [MenuItem("Nyangbingo/Reimport Yokai Stats CSV")]
    private static void ReimportYokaiStats()
    {
        const string csvPath = "Assets/Data/CSV/yokai-stats.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/Yokai";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] yokai-stats.csv was not found."); return; }
        var tearItem = FindItem(itemDirectory, "yokai_tears");
        if (tearItem == null || tearItem.Id != "yokai_tears")
        {
            Debug.LogError("[Nyangbingo] Yokai stats require the 'yokai_tears' item. Reimport Items CSV first.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Yokai stats CSV validation failed: {exception.Message}");
            return;
        }

        var kinds = new Nyangbingo.Core.YokaiKind[rows.Count];
        var hitPoints = new int[rows.Count];
        var moveSpeeds = new float[rows.Count];
        var wallDamage = new float[rows.Count];
        var contactDamage = new int[rows.Count];
        var tearDrops = new int[rows.Count];
        var signatureItems = new ItemDefinition[rows.Count];
        var signatureChances = new float[rows.Count];
        var uniqueKinds = new HashSet<Nyangbingo.Core.YokaiKind>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !TryParseYokaiKind(id, out kinds[i]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.YokaiKind), kinds[i]) || !uniqueKinds.Add(kinds[i]))
            {
                Debug.LogError($"[Nyangbingo] Yokai '{id}' has an unsafe ID or duplicate/unknown YokaiKind mapping.");
                return;
            }

            signatureItems[i] = FindItem(itemDirectory, row["signatureDrop"]);
            if (signatureItems[i] == null || signatureItems[i].Id != row["signatureDrop"])
            {
                Debug.LogError($"[Nyangbingo] Yokai '{id}' has an unknown signature item '{row["signatureDrop"]}'. Reimport Items CSV first.");
                return;
            }

            if (!int.TryParse(row["hp"], NumberStyles.Integer, CultureInfo.InvariantCulture, out hitPoints[i]) ||
                hitPoints[i] <= 0 || !TryParseFiniteFloat(row["moveSpeed"], out moveSpeeds[i]) ||
                moveSpeeds[i] < 0f || !TryParseFiniteFloat(row["wallDps"], out wallDamage[i]) ||
                wallDamage[i] < 0f ||
                !int.TryParse(row["contactDamage"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out contactDamage[i]) || contactDamage[i] < 0 ||
                !int.TryParse(row["tearDrop"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out tearDrops[i]) || tearDrops[i] < 0 ||
                !TryParseFiniteFloat(row["signatureChance"], out signatureChances[i]) ||
                signatureChances[i] < 0f || signatureChances[i] > 1f)
            {
                Debug.LogError($"[Nyangbingo] Yokai '{id}' has invalid combat stats, drop amounts, or signature chance.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Yokai stats CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var id = rows[i]["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<YokaiDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<YokaiDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("kind").enumValueIndex = (int)kinds[i];
            serialized.FindProperty("hitPoints").intValue = hitPoints[i];
            serialized.FindProperty("moveSpeed").floatValue = moveSpeeds[i];
            serialized.FindProperty("wallDamagePerSecond").floatValue = wallDamage[i];
            serialized.FindProperty("contactDamage").intValue = contactDamage[i];
            serialized.FindProperty("tearItem").objectReferenceValue = tearItem;
            serialized.FindProperty("tearDrop").intValue = tearDrops[i];
            serialized.FindProperty("signatureItem").objectReferenceValue = signatureItems[i];
            serialized.FindProperty("signatureChance").floatValue = signatureChances[i];
            serialized.FindProperty("drops").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Yokai stats CSV reimport completed: {rows.Count} assets.");
    }

    private static ItemDefinition FindItem(string directory, string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{directory}/{id}.asset");

    private static bool TryParseFiniteFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
               !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool TryParseYokaiKind(string id, out Nyangbingo.Core.YokaiKind kind)
    {
        var normalizedId = id.Replace("_", string.Empty).Replace("-", string.Empty);
        foreach (Nyangbingo.Core.YokaiKind candidate in System.Enum.GetValues(typeof(Nyangbingo.Core.YokaiKind)))
            if (string.Equals(candidate.ToString(), normalizedId, System.StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }

        kind = default;
        return false;
    }

    private static void SetItemAmount(SerializedProperty property, ItemDefinition item, int amount)
    {
        property.FindPropertyRelative("item").objectReferenceValue = item;
        property.FindPropertyRelative("amount").intValue = amount;
    }

    private static T[] LoadAssets<T>(string directory) where T : UnityEngine.Object
    {
        if (!AssetDatabase.IsValidFolder(directory)) return new T[0];

        var assets = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { directory }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) assets.Add(asset);
        }

        assets.Sort((left, right) => string.CompareOrdinal(
            AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right)));
        return assets.ToArray();
    }

    private static bool ValidateAssetIds<T>(T[] assets, System.Func<T, string> getId, string label)
        where T : UnityEngine.Object
    {
        if (assets == null || getId == null)
        {
            Debug.LogError($"[Nyangbingo] Game data catalog {label} source is missing.");
            return false;
        }

        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        for (var i = 0; i < assets.Length; i++)
        {
            var asset = assets[i];
            if (asset == null)
            {
                Debug.LogError($"[Nyangbingo] Game data catalog {label} contains a null asset.");
                return false;
            }

            var id = getId(asset);
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError($"[Nyangbingo] Game data catalog {label} asset '{AssetDatabase.GetAssetPath(asset)}' has a blank ID.");
                return false;
            }
            if (!ids.Add(id))
            {
                Debug.LogError($"[Nyangbingo] Game data catalog {label} contains duplicate ID '{id}'.");
                return false;
            }
        }

        return true;
    }

    private static void SetObjectReferences<T>(SerializedProperty property, T[] assets) where T : UnityEngine.Object
    {
        property.arraySize = assets.Length;
        for (var i = 0; i < assets.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
