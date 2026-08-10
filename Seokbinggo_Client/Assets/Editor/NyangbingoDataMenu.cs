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
        var modules = LoadAssets<ModuleDefinition>(rootDirectory + "/Modules");
        var mineralTiers = LoadAssets<MineralTierDefinition>(rootDirectory + "/MineralTiers");
        var sealWhitelist = LoadAssets<SealWhitelistDefinition>(rootDirectory + "/SealWhitelist");
        var idMigrations = LoadAssets<IdMigrationDefinition>(rootDirectory + "/IdMigrations");
        var dayCurves = LoadAssets<DayCurveDefinition>(rootDirectory + "/DayCurves");
        var globals = LoadAssets<GlobalDefinition>(rootDirectory + "/Globals");
        var smelting = LoadAssets<SmeltingDefinition>(rootDirectory + "/Smelting");
        var equipment = LoadAssets<EquipmentDefinition>(rootDirectory + "/Equipment");
        var utilities = LoadAssets<UtilityDefinition>(rootDirectory + "/Utilities");
        var combatProfiles = LoadAssets<CombatProfileDefinition>(rootDirectory + "/CombatProfiles");
        var yokai = LoadAssets<YokaiDefinition>(rootDirectory + "/Yokai");
        var bosses = LoadAssets<BossDefinition>(rootDirectory + "/Bosses");
        var chests = LoadAssets<ChestDefinition>(rootDirectory + "/Chests");
        var dayEvents = LoadAssets<DayEventDefinition>(rootDirectory + "/DayEvents");

        if (!ValidateAssetIds(items, value => value.Id, "items") ||
            !ValidateAssetIds(recipes, value => value.Id, "recipes") ||
            !ValidateAssetIds(modules, value => value.Id, "modules") ||
            !ValidateAssetIds(mineralTiers, value => value.Id, "mineral tiers") ||
            !ValidateAssetIds(sealWhitelist, value => value.Element, "seal whitelist") ||
            !ValidateAssetIds(idMigrations, value => value.Key, "ID migrations") ||
            !ValidateAssetIds(dayCurves, value => value.Id, "day curves") ||
            !ValidateAssetIds(globals, value => value.Key, "globals") ||
            !ValidateAssetIds(smelting, value => value.Id, "smelting") ||
            !ValidateAssetIds(equipment, value => value.Id, "equipment") ||
            !ValidateAssetIds(utilities, value => value.Id, "utilities") ||
            !ValidateAssetIds(combatProfiles, value => value.Id, "combat profiles") ||
            !ValidateAssetIds(yokai, value => value.Id, "yokai") ||
            !ValidateAssetIds(bosses, value => value.Id, "bosses") ||
            !ValidateAssetIds(chests, value => value.Id, "chests") ||
            !ValidateAssetIds(dayEvents, value => value.Id, "day events"))
            return;

        Debug.Log("[Nyangbingo] Game data catalog source ID validation completed.");

        var serialized = new SerializedObject(catalog);
        SetObjectReferences(serialized.FindProperty("items"), items);
        SetObjectReferences(serialized.FindProperty("recipes"), recipes);
        SetObjectReferences(serialized.FindProperty("modules"), modules);
        SetObjectReferences(serialized.FindProperty("mineralTiers"), mineralTiers);
        SetObjectReferences(serialized.FindProperty("sealWhitelist"), sealWhitelist);
        SetObjectReferences(serialized.FindProperty("idMigrations"), idMigrations);
        SetObjectReferences(serialized.FindProperty("dayCurves"), dayCurves);
        SetObjectReferences(serialized.FindProperty("globals"), globals);
        SetObjectReferences(serialized.FindProperty("smelting"), smelting);
        SetObjectReferences(serialized.FindProperty("equipment"), equipment);
        SetObjectReferences(serialized.FindProperty("utilities"), utilities);
        SetObjectReferences(serialized.FindProperty("combatProfiles"), combatProfiles);
        SetObjectReferences(serialized.FindProperty("yokai"), yokai);
        SetObjectReferences(serialized.FindProperty("bosses"), bosses);
        SetObjectReferences(serialized.FindProperty("chests"), chests);
        SetObjectReferences(serialized.FindProperty("dayEvents"), dayEvents);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Nyangbingo] Game data catalog rebuilt: {items.Length} items, {recipes.Length} recipes, " +
                  $"{modules.Length} modules, {mineralTiers.Length} mineral tiers, {smelting.Length} smelting, " +
                  $"{sealWhitelist.Length} seal rules, " +
                  $"{idMigrations.Length} ID migrations, " +
                  $"{dayCurves.Length} day curves, " +
                  $"{globals.Length} globals, " +
                  $"{equipment.Length} equipment, {utilities.Length} utilities, " +
                  $"{combatProfiles.Length} combat profiles, " +
                  $"{yokai.Length} yokai, {bosses.Length} bosses, {chests.Length} chests, {dayEvents.Length} day events.");
    }

    [MenuItem("Nyangbingo/Reimport v29 Data Bundle")]
    public static void ReimportV29DataBundle()
    {
        ReimportItems();
        ReimportRecipes();
        ReimportModules();
        ReimportGlobals();
        ReimportSealWhitelist();

        const string rootDirectory = "Assets/Data/SO";
        var items = LoadAssets<ItemDefinition>(rootDirectory + "/Items");
        var recipes = LoadAssets<RecipeDefinition>(rootDirectory + "/Recipes");
        var globals = LoadAssets<GlobalDefinition>(rootDirectory + "/Globals");
        var sealRules = LoadAssets<SealWhitelistDefinition>(rootDirectory + "/SealWhitelist");
        var wallpaper = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(
            rootDirectory + "/Recipes/wallpaper.asset");
        var jangdok = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(
            rootDirectory + "/Modules/jangdok.asset");
        // v29 baseline was 86/54/93/23. P2 adds seokbinggo_s1~s6 + smithy (items 93+) and WaveNight/turret globals (93+).
        if (items.Length < 93 || recipes.Length != 54 || globals.Length < 93 || sealRules.Length != 23 ||
            wallpaper == null || wallpaper.Output.item == null || wallpaper.Output.item.Id != "wallpaper" ||
            wallpaper.Output.amount != 16 || jangdok == null || jangdok.Role != "보관함 40슬롯(v29 확정)")
        {
            Debug.LogError(
                $"[Nyangbingo] v29 data bundle reimport failed its item/recipe/global/seal, wallpaper x16, " +
                $"or jangdok 40-slot check (items={items.Length}, recipes={recipes.Length}, " +
                $"globals={globals.Length}, seal={sealRules.Length}).");
            return;
        }

        RebuildGameDataCatalog();
        Debug.Log($"[Nyangbingo] v29 data bundle reimport completed: {items.Length} items, {recipes.Length} recipes, " +
                  $"{globals.Length} globals, {sealRules.Length} seal rules, wallpaper output 16, jangdok storage 40.");
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
                var rows = NyangbingoCsvUtility.ReadRows(file,
                    mergeUnquotedTrailingNote: Path.GetFileName(file) == "globals.csv");
                Debug.Log($"[Nyangbingo] CSV validated: {Path.GetFileName(file)} ({rows.Count} rows)");
            }
            catch (System.Exception exception)
            {
                valid = false;
                Debug.LogError($"[Nyangbingo] CSV validation failed: {Path.GetFileName(file)} - {exception.Message}");
            }
        }

        if (!valid) return;

        Debug.Log($"[Nyangbingo] CSV structural and unique-ID validation completed: {files.Length} files.");
        try
        {
            if (NyangbingoV24DataValidator.IsV24DataSet(directory))
                Debug.Log($"[Nyangbingo] v24 cross-file validation completed: " +
                          NyangbingoV24DataValidator.Validate(directory) + ".");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] v24 cross-file validation failed: {exception.Message}");
        }
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

        if (rows.Count == 0)
        {
            Debug.LogError("[Nyangbingo] Item CSV must contain at least one data row.");
            return;
        }

        var v24Schema = HasColumns(rows[0], "id", "name_ko", "category", "max_stack", "mvp_scope", "note");
        var legacySchema = HasColumns(rows[0], "id", "displayName", "maxStack");
        if (v24Schema == legacySchema)
        {
            Debug.LogError("[Nyangbingo] Item CSV must use either the legacy id/displayName/maxStack schema " +
                           "or the v24 id/name_ko/category/max_stack/mvp_scope/note schema.");
            return;
        }

        var displayNames = new string[rows.Count];
        var maxStacks = new int[rows.Count];
        var categories = new ItemCategory[rows.Count];
        var mvpScopes = new ItemMvpScope[rows.Count];
        var notes = new string[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            displayNames[i] = row[v24Schema ? "name_ko" : "displayName"];
            notes[i] = v24Schema ? row["note"] : string.Empty;
            var maxStackText = row[v24Schema ? "max_stack" : "maxStack"];

            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                string.IsNullOrWhiteSpace(displayNames[i]) ||
                !int.TryParse(maxStackText, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxStacks[i]))
            {
                Debug.LogError($"[Nyangbingo] Item '{id}' must have a safe ID, display name, and integer max stack.");
                return;
            }

            if (!v24Schema)
            {
                if (maxStacks[i] < 1 && !(id == "bare_claw" && maxStacks[i] == 0))
                {
                    Debug.LogError($"[Nyangbingo] Legacy item '{id}' must have maxStack of at least 1, except bare_claw at 0.");
                    return;
                }
                categories[i] = ItemCategory.Unspecified;
                mvpScopes[i] = ItemMvpScope.Unspecified;
                continue;
            }

            if (!IsSnakeCaseId(id) ||
                !System.Enum.TryParse(row["category"], true, out categories[i]) ||
                categories[i] == ItemCategory.Unspecified ||
                !System.Enum.TryParse(row["mvp_scope"], true, out mvpScopes[i]) ||
                mvpScopes[i] == ItemMvpScope.Unspecified)
            {
                Debug.LogError($"[Nyangbingo] v24 item '{id}' must use a snake_case ID, known category, and A/B MVP scope.");
                return;
            }

            var expectedMaxStack = id == "bare_claw" ? 0 :
                categories[i] == ItemCategory.Tool || categories[i] == ItemCategory.Weapon ||
                categories[i] == ItemCategory.Equipment ? 1 : 99;
            if (maxStacks[i] != expectedMaxStack)
            {
                Debug.LogError($"[Nyangbingo] v24 item '{id}' has max stack {maxStacks[i]}; expected {expectedMaxStack} " +
                               $"for category {categories[i]}.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Item CSV semantic validation completed: {rows.Count} rows " +
                  $"({(v24Schema ? "v24" : "legacy")} schema).");
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
            serialized.FindProperty("displayName").stringValue = displayNames[i];
            serialized.FindProperty("maxStack").intValue = maxStacks[i];
            serialized.FindProperty("category").enumValueIndex = (int)categories[i];
            serialized.FindProperty("mvpScope").enumValueIndex = (int)mvpScopes[i];
            serialized.FindProperty("note").stringValue = notes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] Item CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport Recipes CSV")]
    private static void ReimportRecipes()
    {
        const string legacyCsvPath = "Assets/Data/CSV/recipes.csv";
        const string v24CsvPath = "Assets/Data/CSV/crafting-tree.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/Recipes";
        var hasLegacyCsv = File.Exists(legacyCsvPath);
        var hasV24Csv = File.Exists(v24CsvPath);
        if (hasLegacyCsv == hasV24Csv)
        {
            Debug.LogError("[Nyangbingo] Exactly one recipe source is required: legacy recipes.csv or v24 crafting-tree.csv.");
            return;
        }
        var csvPath = hasV24Csv ? v24CsvPath : legacyCsvPath;
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

        if (rows.Count == 0)
        {
            Debug.LogError("[Nyangbingo] Recipe CSV must contain at least one data row.");
            return;
        }

        var v24Schema = HasColumns(rows[0], "id", "type", "item_ko", "station_id", "materials",
            "craft_time_sec", "mvp_scope", "note");
        var hasOutputCount = v24Schema && rows[0].ContainsKey("output_count");
        var legacySchema = HasColumns(rows[0], "id", "station", "output", "amount", "ingredients",
            "durationSeconds");
        if (v24Schema == legacySchema)
        {
            Debug.LogError("[Nyangbingo] Recipe CSV does not match the selected legacy or v24 schema.");
            return;
        }

        var stations = new Nyangbingo.Core.CraftingStation[rows.Count];
        var outputs = new ItemDefinition[rows.Count];
        var outputAmounts = new int[rows.Count];
        var durations = new float[rows.Count];
        var ingredientsByRecipe = new ItemAmount[rows.Count][];
        var recipeTypes = new RecipeType[rows.Count];
        var mvpScopes = new ItemMvpScope[rows.Count];
        var notes = new string[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            var stationId = row[v24Schema ? "station_id" : "station"];
            var stationParsed = v24Schema
                ? TryParseCraftingStationId(stationId, out stations[rowIndex])
                : System.Enum.TryParse(stationId, true, out stations[rowIndex]) &&
                  System.Enum.IsDefined(typeof(Nyangbingo.Core.CraftingStation), stations[rowIndex]);
            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                (v24Schema && !IsSnakeCaseId(id)) || !stationParsed)
            {
                Debug.LogError($"[Nyangbingo] Recipe '{id}' has an unsafe ID or unknown station '{stationId}'.");
                return;
            }

            var outputId = v24Schema ? id : row["output"];
            outputAmounts[rowIndex] = v24Schema && !hasOutputCount ? 1 : 0;
            outputs[rowIndex] = FindItem(itemDirectory, outputId);
            if (outputs[rowIndex] == null || outputs[rowIndex].Id != outputId ||
                ((hasOutputCount || !v24Schema) &&
                 (!int.TryParse(row[hasOutputCount ? "output_count" : "amount"], NumberStyles.Integer,
                     CultureInfo.InvariantCulture, out outputAmounts[rowIndex]) || outputAmounts[rowIndex] <= 0)) ||
                !float.TryParse(row[v24Schema ? "craft_time_sec" : "durationSeconds"], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out durations[rowIndex]) || durations[rowIndex] < 0f ||
                float.IsNaN(durations[rowIndex]) || float.IsInfinity(durations[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Recipe '{id}' has an invalid output, amount, or durationSeconds.");
                return;
            }

            if (v24Schema)
            {
                if (!System.Enum.TryParse(row["type"], true, out recipeTypes[rowIndex]) ||
                    recipeTypes[rowIndex] == RecipeType.Unspecified ||
                    !System.Enum.TryParse(row["mvp_scope"], true, out mvpScopes[rowIndex]) ||
                    mvpScopes[rowIndex] == ItemMvpScope.Unspecified)
                {
                    Debug.LogError($"[Nyangbingo] v24 recipe '{id}' has an unknown type or MVP scope.");
                    return;
                }
                notes[rowIndex] = row["note"];
            }
            else
            {
                recipeTypes[rowIndex] = RecipeType.Unspecified;
                mvpScopes[rowIndex] = ItemMvpScope.Unspecified;
                notes[rowIndex] = string.Empty;
            }

            var ingredientsText = row[v24Schema ? "materials" : "ingredients"];
            var encodedIngredients = ingredientsText.Split(v24Schema ? ',' : '|');
            if (encodedIngredients.Length == 0 || string.IsNullOrWhiteSpace(ingredientsText))
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

        Debug.Log($"[Nyangbingo] Recipe CSV semantic validation completed: {rows.Count} rows " +
                  $"({(v24Schema ? hasOutputCount ? "v26" : "v24" : "legacy")} schema).");
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
            serialized.FindProperty("type").enumValueIndex = (int)recipeTypes[rowIndex];
            serialized.FindProperty("mvpScope").enumValueIndex = (int)mvpScopes[rowIndex];
            serialized.FindProperty("note").stringValue = notes[rowIndex];
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

    [MenuItem("Nyangbingo/Reimport Modules CSV")]
    private static void ReimportModules()
    {
        const string csvPath = "Assets/Data/CSV/modules.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/Modules";
        if (!File.Exists(csvPath))
        {
            Debug.LogError("[Nyangbingo] modules.csv was not found.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Module CSV validation failed: {exception.Message}");
            return;
        }

        if (rows.Count == 0 || !HasColumns(rows[0], "id", "name_ko", "item_id", "role", "materials",
                "build_time_sec", "priority"))
        {
            Debug.LogError("[Nyangbingo] modules.csv does not match the official v24.1 schema.");
            return;
        }

        var items = new ItemDefinition[rows.Count];
        var materialsByModule = new ItemAmount[rows.Count][];
        var buildTimes = new float[rows.Count];
        var priorities = new ModulePriority[rows.Count];
        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            items[rowIndex] = FindItem(itemDirectory, row["item_id"]);
            if (!IsSnakeCaseId(id) || !ids.Add(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                string.IsNullOrWhiteSpace(row["name_ko"]) || string.IsNullOrWhiteSpace(row["role"]) ||
                items[rowIndex] == null || items[rowIndex].Id != row["item_id"] ||
                !float.TryParse(row["build_time_sec"], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out buildTimes[rowIndex]) || buildTimes[rowIndex] <= 0f ||
                float.IsNaN(buildTimes[rowIndex]) || float.IsInfinity(buildTimes[rowIndex]) ||
                !System.Enum.TryParse(row["priority"], false, out priorities[rowIndex]) ||
                !System.Enum.IsDefined(typeof(ModulePriority), priorities[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Module '{id}' has invalid identity, item, time, or priority data.");
                return;
            }

            var encodedMaterials = row["materials"].Split(',');
            var materialIds = new HashSet<string>(System.StringComparer.Ordinal);
            var materials = new ItemAmount[encodedMaterials.Length];
            if (encodedMaterials.Length == 0 || string.IsNullOrWhiteSpace(row["materials"]))
            {
                Debug.LogError($"[Nyangbingo] Module '{id}' must have at least one material.");
                return;
            }

            for (var materialIndex = 0; materialIndex < encodedMaterials.Length; materialIndex++)
            {
                var parts = encodedMaterials[materialIndex].Split(':');
                var material = parts.Length == 2 ? FindItem(itemDirectory, parts[0]) : null;
                if (parts.Length != 2 || !materialIds.Add(parts[0]) || material == null || material.Id != parts[0] ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Module '{id}' has invalid material '{encodedMaterials[materialIndex]}'.");
                    return;
                }
                materials[materialIndex] = new ItemAmount { item = material, amount = amount };
            }
            materialsByModule[rowIndex] = materials;
        }

        Debug.Log($"[Nyangbingo] Module CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ModuleDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = row["name_ko"];
            serialized.FindProperty("item").objectReferenceValue = items[rowIndex];
            serialized.FindProperty("role").stringValue = row["role"];
            serialized.FindProperty("buildTimeSeconds").floatValue = buildTimes[rowIndex];
            serialized.FindProperty("priority").enumValueIndex = (int)priorities[rowIndex];
            var materialsProperty = serialized.FindProperty("materials");
            var materials = materialsByModule[rowIndex];
            materialsProperty.arraySize = materials.Length;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                SetItemAmount(materialsProperty.GetArrayElementAtIndex(materialIndex),
                    materials[materialIndex].item, materials[materialIndex].amount);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] Module CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport Mineral Tiers CSV")]
    private static void ReimportMineralTiers()
    {
        const string csvPath = "Assets/Data/CSV/mineral-tiers.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/MineralTiers";
        if (!File.Exists(csvPath))
        {
            Debug.LogError("[Nyangbingo] mineral-tiers.csv was not found.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Mineral tier CSV validation failed: {exception.Message}");
            return;
        }

        if (rows.Count == 0 || !HasColumns(rows[0], "resource_id", "name_ko", "layer", "depth_min",
                "depth_max", "min_claw_tier", "gate_type", "claw_t1_sec", "claw_t2_sec", "claw_t3_sec",
                "freq_per_100tiles", "use_ko", "gate_ko"))
        {
            Debug.LogError("[Nyangbingo] mineral-tiers.csv does not match the official v24.1 schema.");
            return;
        }

        var resources = new ItemDefinition[rows.Count];
        var layers = new MineralLayer[rows.Count];
        var minimumDepths = new int[rows.Count];
        var maximumDepths = new int[rows.Count];
        var minimumClawTiers = new int[rows.Count];
        var gates = new MiningGateType[rows.Count];
        var tierOneSeconds = new float[rows.Count];
        var tierTwoSeconds = new float[rows.Count];
        var tierThreeSeconds = new float[rows.Count];
        var frequencies = new float[rows.Count];
        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["resource_id"];
            resources[i] = FindItem(itemDirectory, id);
            var parsed = IsSnakeCaseId(id) && ids.Add(id) && resources[i] != null && resources[i].Id == id &&
                         !string.IsNullOrWhiteSpace(row["name_ko"]) &&
                         !string.IsNullOrWhiteSpace(row["use_ko"]) && !string.IsNullOrWhiteSpace(row["gate_ko"]) &&
                         TryParseMineralLayer(row["layer"], out layers[i]) &&
                         int.TryParse(row["depth_min"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out minimumDepths[i]) && minimumDepths[i] >= 0 &&
                         int.TryParse(row["depth_max"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out maximumDepths[i]) && maximumDepths[i] >= minimumDepths[i] &&
                         int.TryParse(row["min_claw_tier"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out minimumClawTiers[i]) && minimumClawTiers[i] >= 1 && minimumClawTiers[i] <= 3 &&
                         System.Enum.TryParse(row["gate_type"], true, out gates[i]) &&
                         System.Enum.IsDefined(typeof(MiningGateType), gates[i]) &&
                         TryParseFiniteFloat(row["claw_t1_sec"], out tierOneSeconds[i]) &&
                         TryParseFiniteFloat(row["claw_t2_sec"], out tierTwoSeconds[i]) &&
                         TryParseFiniteFloat(row["claw_t3_sec"], out tierThreeSeconds[i]) &&
                         TryParseFiniteFloat(row["freq_per_100tiles"], out frequencies[i]) && frequencies[i] >= 0f;
            if (!parsed || tierTwoSeconds[i] <= 0f || tierThreeSeconds[i] <= 0f ||
                (minimumClawTiers[i] == 1 ? tierOneSeconds[i] <= 0f : tierOneSeconds[i] != -1f))
            {
                Debug.LogError($"[Nyangbingo] Mineral tier '{id}' has invalid identity, layer, gate, or numeric data.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Mineral tier CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["resource_id"];
            var assetPath = $"{targetDirectory}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<MineralTierDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MineralTierDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = row["name_ko"];
            serialized.FindProperty("resource").objectReferenceValue = resources[i];
            serialized.FindProperty("layer").enumValueIndex = (int)layers[i];
            serialized.FindProperty("minimumDepth").intValue = minimumDepths[i];
            serialized.FindProperty("maximumDepth").intValue = maximumDepths[i];
            serialized.FindProperty("minimumClawTier").intValue = minimumClawTiers[i];
            serialized.FindProperty("gateType").enumValueIndex = (int)gates[i];
            serialized.FindProperty("clawTierOneSeconds").floatValue = tierOneSeconds[i];
            serialized.FindProperty("clawTierTwoSeconds").floatValue = tierTwoSeconds[i];
            serialized.FindProperty("clawTierThreeSeconds").floatValue = tierThreeSeconds[i];
            serialized.FindProperty("frequencyPerHundredTiles").floatValue = frequencies[i];
            serialized.FindProperty("usageDescription").stringValue = row["use_ko"];
            serialized.FindProperty("gateDescription").stringValue = row["gate_ko"];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] Mineral tier CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport Seal Whitelist CSV")]
    private static void ReimportSealWhitelist()
    {
        const string csvPath = "Assets/Data/CSV/seal-whitelist.csv";
        const string targetDirectory = "Assets/Data/SO/SealWhitelist";
        if (!File.Exists(csvPath))
        {
            Debug.LogError("[Nyangbingo] seal-whitelist.csv was not found.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath, mergeUnquotedTrailingNote: true);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Seal whitelist CSV validation failed: {exception.Message}");
            return;
        }

        if (rows.Count == 0 || !HasColumns(rows[0], "element", "seals", "note"))
        {
            Debug.LogError("[Nyangbingo] seal-whitelist.csv does not match the official v24.1 schema.");
            return;
        }

        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        var seals = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var element = row["element"];
            if (string.IsNullOrWhiteSpace(element) || !ids.Add(element) ||
                (row["seals"] != "0" && row["seals"] != "1") || string.IsNullOrWhiteSpace(row["note"]))
            {
                Debug.LogError($"[Nyangbingo] Seal whitelist row {i + 1} has an invalid element, flag, or note.");
                return;
            }
            seals[i] = row["seals"] == "1";
        }

        Debug.Log($"[Nyangbingo] Seal whitelist CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var assetPath = $"{targetDirectory}/rule_{i + 1:00}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<SealWhitelistDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<SealWhitelistDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("element").stringValue = rows[i]["element"];
            serialized.FindProperty("seals").boolValue = seals[i];
            serialized.FindProperty("note").stringValue = rows[i]["note"];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] Seal whitelist CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport ID Migration CSV")]
    private static void ReimportIdMigrations()
    {
        const string csvPath = "Assets/Data/CSV/id-migration.csv";
        const string targetDirectory = "Assets/Data/SO/IdMigrations";
        const string manifestDirectory = "Assets/Resources/Nyangbingo";
        const string manifestPath = manifestDirectory + "/IdMigrationManifest.asset";
        if (!File.Exists(csvPath))
        {
            Debug.LogError("[Nyangbingo] id-migration.csv was not found.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath, mergeUnquotedTrailingNote: true);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] ID migration CSV validation failed: {exception.Message}");
            return;
        }

        if (rows.Count != 27 || !HasColumns(rows[0], "legacy_id", "new_id", "domain", "action", "note"))
        {
            Debug.LogError("[Nyangbingo] id-migration.csv must contain the official 27-row v24.1 schema.");
            return;
        }

        var domains = new IdMigrationDomain[rows.Count];
        var actions = new IdMigrationAction[rows.Count];
        var keys = new HashSet<string>(System.StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var legacyId = row["legacy_id"];
            var newId = row["new_id"];
            var parsed = IsSnakeCaseId(legacyId) && TryParseIdMigrationDomain(row["domain"], out domains[i]) &&
                         TryParseIdMigrationAction(row["action"], out actions[i]) &&
                         keys.Add($"{domains[i]}:{legacyId}");
            if (!parsed || actions[i] == IdMigrationAction.Rename && !IsSnakeCaseId(newId) ||
                actions[i] == IdMigrationAction.RemoveRefund &&
                (legacyId != "fox_rain_charm" || !string.IsNullOrEmpty(newId) ||
                 !row["note"].Contains("yokai_tear:3")))
            {
                Debug.LogError($"[Nyangbingo] ID migration row {i + 1} has an invalid key, target, or action.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] ID migration CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        var definitions = new IdMigrationDefinition[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var assetPath = $"{targetDirectory}/migration_{i + 1:00}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<IdMigrationDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<IdMigrationDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("legacyId").stringValue = rows[i]["legacy_id"];
            serialized.FindProperty("newId").stringValue = rows[i]["new_id"];
            serialized.FindProperty("domain").enumValueIndex = (int)domains[i];
            serialized.FindProperty("action").enumValueIndex = (int)actions[i];
            serialized.FindProperty("note").stringValue = rows[i]["note"];
            var refunds = actions[i] == IdMigrationAction.RemoveRefund;
            serialized.FindProperty("refundItemId").stringValue = refunds ? "yokai_tear" : string.Empty;
            serialized.FindProperty("refundAmount").intValue = refunds ? 3 : 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            definitions[i] = definition;
        }

        EnsureFolder("Assets/Resources");
        EnsureFolder(manifestDirectory);
        var manifest = AssetDatabase.LoadAssetAtPath<IdMigrationManifest>(manifestPath);
        if (manifest == null)
        {
            manifest = ScriptableObject.CreateInstance<IdMigrationManifest>();
            AssetDatabase.CreateAsset(manifest, manifestPath);
        }
        var manifestSerialized = new SerializedObject(manifest);
        SetObjectReferences(manifestSerialized.FindProperty("definitions"), definitions);
        manifestSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manifest);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] ID migration CSV reimport and runtime manifest build completed.");
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
        var batchCapacities = new int[rows.Count];
        var notes = new string[rows.Count];
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
                !int.TryParse(row["batchCapacity"], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out batchCapacities[i]) || batchCapacities[i] <= 0 ||
                !float.TryParse(row["durationSeconds"], System.Globalization.NumberStyles.Float,
                    CultureInfo.InvariantCulture, out durations[i]) || durations[i] <= 0f ||
                float.IsNaN(durations[i]) || float.IsInfinity(durations[i]))
            {
                Debug.LogError($"[Nyangbingo] Smelting '{id}' has an invalid item, amount, or durationSeconds.");
                return;
            }
            notes[i] = row["note"];
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
            serialized.FindProperty("batchCapacity").intValue = batchCapacities[i];
            serialized.FindProperty("note").stringValue = notes[i];
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
        var doubleJumpRatios = new float[rows.Count];
        var visionBonuses = new float[rows.Count];
        var theftBlocks = new bool[rows.Count];
        var setIds = new string[rows.Count];
        var setTemperatureModifiers = new float[rows.Count];
        var setFireModifiers = new float[rows.Count];
        var verbIds = new string[rows.Count];
        var usageLimits = new int[rows.Count];
        var activations = new string[rows.Count];
        var setMembers = new Dictionary<string, List<int>>(System.StringComparer.Ordinal);
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
                !TryParseFiniteFloat(row["doubleJumpHeightRatio"], out doubleJumpRatios[i]) ||
                !TryParseFiniteFloat(row["visionRadiusBonus"], out visionBonuses[i]) ||
                !bool.TryParse(row["blocksInventoryTheft"], out theftBlocks[i]) ||
                !TryParseFiniteFloat(row["setTemperatureRiseModifier"], out setTemperatureModifiers[i]) ||
                !TryParseFiniteFloat(row["setFireDamageModifier"], out setFireModifiers[i]))
            {
                Debug.LogError($"[Nyangbingo] Equipment '{id}' has mismatched slot data or invalid stats.");
                return;
            }

            setIds[i] = row["setId"] == "none" ? string.Empty : row["setId"].Trim();
            verbIds[i] = row.TryGetValue("verb_id", out var verbRaw) ? (verbRaw ?? string.Empty).Trim() : string.Empty;
            if (!row.TryGetValue("usage_limit_per_day", out var usageRaw) || string.IsNullOrWhiteSpace(usageRaw))
                usageLimits[i] = 0;
            else if (!int.TryParse(usageRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out usageLimits[i]) ||
                     usageLimits[i] < 0)
            {
                Debug.LogError($"[Nyangbingo] Equipment '{id}' has invalid usage_limit_per_day.");
                return;
            }

            activations[i] = row.TryGetValue("activation_condition", out var activationRaw) &&
                             !string.IsNullOrWhiteSpace(activationRaw)
                ? activationRaw.Trim()
                : "None";
            if (Nyangbingo.Core.ArtifactVerbParsing.ParseActivation(activations[i]) ==
                    Nyangbingo.Core.ArtifactActivationCondition.None &&
                !string.Equals(activations[i], "None", System.StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(activations[i]))
            {
                Debug.LogError($"[Nyangbingo] Equipment '{id}' has invalid activation_condition.");
                return;
            }

            if (!string.IsNullOrEmpty(verbIds[i]) &&
                Nyangbingo.Core.ArtifactVerbParsing.ParseVerb(verbIds[i]) == Nyangbingo.Core.ArtifactVerbId.None)
            {
                Debug.LogError($"[Nyangbingo] Equipment '{id}' has invalid verb_id '{verbIds[i]}'.");
                return;
            }

            var jumpRatioMatches = doubleJumps[i] ? doubleJumpRatios[i] > 0f : doubleJumpRatios[i] == 0f;
            var hasSet = !string.IsNullOrEmpty(setIds[i]);
            if (!jumpRatioMatches || doubleJumpRatios[i] < 0f ||
                (accessories[i] && hasSet) ||
                (!hasSet && (!Mathf.Approximately(setTemperatureModifiers[i], 0f) ||
                             !Mathf.Approximately(setFireModifiers[i], 0f))))
            {
                Debug.LogError($"[Nyangbingo] Equipment '{id}' has invalid jump or set-bonus data.");
                return;
            }

            if (hasSet)
            {
                if (!setMembers.TryGetValue(setIds[i], out var members))
                {
                    members = new List<int>();
                    setMembers.Add(setIds[i], members);
                }
                members.Add(i);
            }
        }

        foreach (var pair in setMembers)
        {
            var members = pair.Value;
            var slotsFound = new HashSet<Nyangbingo.Core.EquipmentSlot>();
            var referenceIndex = members[0];
            for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
            {
                var index = members[memberIndex];
                slotsFound.Add(slots[index]);
                if (!Mathf.Approximately(setTemperatureModifiers[index], setTemperatureModifiers[referenceIndex]) ||
                    !Mathf.Approximately(setFireModifiers[index], setFireModifiers[referenceIndex]))
                {
                    Debug.LogError($"[Nyangbingo] Equipment set '{pair.Key}' has inconsistent bonus values.");
                    return;
                }
            }
            if (members.Count != 3 || slotsFound.Count != 3 ||
                !slotsFound.Contains(Nyangbingo.Core.EquipmentSlot.Head) ||
                !slotsFound.Contains(Nyangbingo.Core.EquipmentSlot.Body) ||
                !slotsFound.Contains(Nyangbingo.Core.EquipmentSlot.Feet))
            {
                Debug.LogError($"[Nyangbingo] Equipment set '{pair.Key}' must contain exactly Head, Body, and Feet.");
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
            serialized.FindProperty("doubleJumpHeightRatio").floatValue = doubleJumpRatios[i];
            serialized.FindProperty("visionRadiusBonus").floatValue = visionBonuses[i];
            serialized.FindProperty("blocksInventoryTheft").boolValue = theftBlocks[i];
            serialized.FindProperty("setId").stringValue = setIds[i];
            serialized.FindProperty("setTemperatureRiseModifier").floatValue = setTemperatureModifiers[i];
            serialized.FindProperty("setFireDamageModifier").floatValue = setFireModifiers[i];
            var verbProperty = serialized.FindProperty("verbId");
            if (verbProperty != null) verbProperty.stringValue = verbIds[i];
            var usageProperty = serialized.FindProperty("usageLimitPerDay");
            if (usageProperty != null) usageProperty.intValue = usageLimits[i];
            var activationProperty = serialized.FindProperty("activationCondition");
            if (activationProperty != null) activationProperty.stringValue = activations[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Equipment CSV reimport completed: {rows.Count} assets.");
    }

    /// <summary>
    /// accessories.csv에만 있고 equipment SO가 없는 id를 AccessoryOne(수치 0)으로 생성.
    /// 런타임 정본은 equipment.csv — 이 메뉴는 기획 원장 동기화 보조.
    /// </summary>
    [MenuItem("Nyangbingo/Reimport Accessories As Equipment")]
    private static void ReimportAccessoriesAsEquipment()
    {
        const string accessoriesPath = "Assets/Data/CSV/accessories.csv";
        const string equipmentDirectory = "Assets/Data/SO/Equipment";
        if (!File.Exists(accessoriesPath))
        {
            Debug.LogError("[Nyangbingo] accessories.csv was not found.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(accessoriesPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Accessories CSV validation failed: {exception.Message}");
            return;
        }

        EnsureFolder(equipmentDirectory);
        var created = 0;
        var skipped = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var id = rows[i]["id"];
            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Debug.LogError($"[Nyangbingo] Accessory '{id}' has an unsafe ID.");
                return;
            }

            var assetPath = $"{equipmentDirectory}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(assetPath) != null)
            {
                skipped++;
                continue;
            }

            var definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            AssetDatabase.CreateAsset(definition, assetPath);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("slot").enumValueIndex = (int)Nyangbingo.Core.EquipmentSlot.AccessoryOne;
            serialized.FindProperty("accessory").boolValue = true;
            serialized.FindProperty("defense").intValue = 0;
            serialized.FindProperty("movementBonus").floatValue = 0f;
            serialized.FindProperty("miningCriticalBonus").floatValue = 0f;
            serialized.FindProperty("temperatureRiseModifier").floatValue = 0f;
            serialized.FindProperty("fireDamageModifier").floatValue = 0f;
            serialized.FindProperty("grantsDoubleJump").boolValue = false;
            serialized.FindProperty("doubleJumpHeightRatio").floatValue = 0f;
            serialized.FindProperty("visionRadiusBonus").floatValue = 0f;
            serialized.FindProperty("blocksInventoryTheft").boolValue = false;
            serialized.FindProperty("setId").stringValue = string.Empty;
            serialized.FindProperty("setTemperatureRiseModifier").floatValue = 0f;
            serialized.FindProperty("setFireDamageModifier").floatValue = 0f;
            var verbProperty = serialized.FindProperty("verbId");
            if (verbProperty != null) verbProperty.stringValue = string.Empty;
            var usageProperty = serialized.FindProperty("usageLimitPerDay");
            if (usageProperty != null) usageProperty.intValue = 0;
            var activationProperty = serialized.FindProperty("activationCondition");
            if (activationProperty != null) activationProperty.stringValue = "None";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Accessories-as-equipment import completed: created={created}, skippedExisting={skipped}.");
    }

    [MenuItem("Nyangbingo/Reimport Player Combat CSV")]
    private static void ReimportPlayerCombat()
    {
        const string csvPath = "Assets/Data/CSV/player-combat.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/CombatProfiles";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] player-combat.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        try { rows = NyangbingoCsvUtility.ReadRows(csvPath); }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Player combat CSV validation failed: {exception.Message}");
            return;
        }

        var expectedIds = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "bare_claw", "iron_claw", "icesteel_claw", "dokkaebi_club",
            FanItemIds.Cheolseon, "frostclaw_gauntlet", FanItemIds.Hapjukseon
        };
        var ids = new string[rows.Count];
        var tiers = new string[rows.Count];
        var hasBasicAttacks = new bool[rows.Count];
        var attackDamages = new int[rows.Count];
        var attacksPerSecond = new float[rows.Count];
        var damagePerSecond = new float[rows.Count];
        var knockbacks = new float[rows.Count];
        var ranges = new float[rows.Count];
        var arcs = new float[rows.Count];
        var multiTargets = new bool[rows.Count];
        var hitsWalls = new bool[rows.Count];
        var notes = new string[rows.Count];
        var seenIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ids[i] = row["item_id"];
            tiers[i] = row["tier"];
            notes[i] = row["verify_note"];
            hasBasicAttacks[i] = row["attacks_per_sec"] != "-";
            var item = FindItem(itemDirectory, ids[i]);
            if (!expectedIds.Contains(ids[i]) || !seenIds.Add(ids[i]) || item == null || item.Id != ids[i] ||
                string.IsNullOrWhiteSpace(tiers[i]) ||
                !int.TryParse(row["attack_dmg"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out attackDamages[i]) || attackDamages[i] < 0 ||
                (hasBasicAttacks[i] && (!TryParseFiniteFloat(row["attacks_per_sec"], out attacksPerSecond[i]) ||
                                        attacksPerSecond[i] <= 0f)) ||
                !TryParseFiniteFloat(row["dps"], out damagePerSecond[i]) || damagePerSecond[i] < 0f ||
                !TryParseFiniteFloat(row["knockback_tiles"], out knockbacks[i]) || knockbacks[i] < 0f ||
                !TryParseFiniteFloat(row["range_tiles"], out ranges[i]) || ranges[i] <= 0f ||
                !TryParseFiniteFloat(row["arc_deg"], out arcs[i]) || arcs[i] < 1f || arcs[i] > 180f ||
                (row["multi_target"] != "0" && row["multi_target"] != "1") ||
                (row["hits_walls"] != "0" && row["hits_walls"] != "1"))
            {
                Debug.LogError($"[Nyangbingo] Combat profile '{ids[i]}' has an invalid ID, item reference, or stat.");
                return;
            }
            multiTargets[i] = row["multi_target"] == "1";
            hitsWalls[i] = row["hits_walls"] == "1";
            if ((!hasBasicAttacks[i] && (attackDamages[i] != 0 || damagePerSecond[i] != 0f)) ||
                (hasBasicAttacks[i] && (attackDamages[i] <= 0 ||
                    !Mathf.Approximately(damagePerSecond[i], attackDamages[i] * attacksPerSecond[i]))))
            {
                Debug.LogError($"[Nyangbingo] Combat profile '{ids[i]}' has an inconsistent basic-attack DPS contract.");
                return;
            }
        }
        if (rows.Count != expectedIds.Count || seenIds.Count != expectedIds.Count)
        {
            Debug.LogError($"[Nyangbingo] Player combat CSV must contain exactly {expectedIds.Count} official profiles.");
            return;
        }

        Debug.Log($"[Nyangbingo] Player combat CSV semantic validation completed: {rows.Count} profiles.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var assetPath = $"{targetDirectory}/{ids[i]}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<CombatProfileDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CombatProfileDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = ids[i];
            serialized.FindProperty("tier").stringValue = tiers[i];
            serialized.FindProperty("hasBasicAttack").boolValue = hasBasicAttacks[i];
            serialized.FindProperty("attackDamage").intValue = attackDamages[i];
            serialized.FindProperty("attacksPerSecond").floatValue = attacksPerSecond[i];
            serialized.FindProperty("damagePerSecond").floatValue = damagePerSecond[i];
            serialized.FindProperty("knockbackTiles").floatValue = knockbacks[i];
            serialized.FindProperty("rangeTiles").floatValue = ranges[i];
            serialized.FindProperty("arcDegrees").floatValue = arcs[i];
            serialized.FindProperty("multiTarget").boolValue = multiTargets[i];
            serialized.FindProperty("hitsWalls").boolValue = hitsWalls[i];
            serialized.FindProperty("note").stringValue = notes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Player combat CSV reimport completed: {rows.Count} assets.");
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
        const string dropsPath = "Assets/Data/CSV/drops.csv";
        const string itemDirectory = "Assets/Data/SO/Items";
        const string targetDirectory = "Assets/Data/SO/Bosses";
        if (!File.Exists(csvPath)) { Debug.LogError("[Nyangbingo] bosses.csv was not found."); return; }
        if (!File.Exists(dropsPath)) { Debug.LogError("[Nyangbingo] drops.csv was not found."); return; }
        List<Dictionary<string, string>> rows;
        List<Dictionary<string, string>> dropRows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
            dropRows = NyangbingoCsvUtility.ReadRows(dropsPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Boss CSV validation failed: {exception.Message}");
            return;
        }

        var bossDropRows = new Dictionary<string, Dictionary<string, string>>(System.StringComparer.Ordinal);
        var dropSourceKeys = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var dropRow in dropRows)
        {
            var sourceId = dropRow["source_id"];
            var sourceType = dropRow["source_type"];
            if (string.IsNullOrWhiteSpace(sourceId) ||
                (sourceType != "boss" && sourceType != "yokai") ||
                !dropSourceKeys.Add(sourceType + "\n" + sourceId))
            {
                Debug.LogError($"[Nyangbingo] drops.csv has an invalid or duplicate source '{sourceType}:{sourceId}'.");
                return;
            }
            if (sourceType == "boss") bossDropRows.Add(sourceId, dropRow);
        }

        var kinds = new Nyangbingo.Core.BossKind[rows.Count];
        var displayNames = new string[rows.Count];
        var hitPoints = new int[rows.Count];
        var combatSeconds = new float[rows.Count];
        var summonItems = new ItemDefinition[rows.Count];
        var summonMaterials = new ItemAmount[rows.Count][];
        var summonStations = new Nyangbingo.Core.CraftingStation[rows.Count];
        var recommendedDays = new string[rows.Count];
        var deepAltarRequirements = new bool[rows.Count];
        var forcedDays = new int[rows.Count];
        var guaranteedDrops = new ItemAmount[rows.Count][];
        var wallDamageDefault = new float[rows.Count];
        var wallDamageIce = new float[rows.Count];
        var wallDamageIronWall = new float[rows.Count];
        var contactDamage = new int[rows.Count];
        var specialDescriptions = new string[rows.Count];
        var telegraphSeconds = new float[rows.Count];
        var specialShapes = new BossSpecialShape[rows.Count];
        var specialRanges = new float[rows.Count];
        var specialArcs = new float[rows.Count];
        var specialDamage = new int[rows.Count];
        var specialDurations = new float[rows.Count];
        var specialTicks = new float[rows.Count];
        var specialKnockbacks = new float[rows.Count];
        var specialCooldowns = new float[rows.Count];
        var fireTags = new bool[rows.Count];
        var aimLocks = new bool[rows.Count];
        var mvpScopes = new ItemMvpScope[rows.Count];
        var seenBossIds = new HashSet<string>(System.StringComparer.Ordinal);
        if (rows.Count != 4)
        {
            Debug.LogError($"[Nyangbingo] bosses.csv must contain exactly four official bosses, but found {rows.Count}.");
            return;
        }
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var id = row["id"];
            switch (id)
            {
                case "king_dokkaebi": kinds[rowIndex] = Nyangbingo.Core.BossKind.GoblinChief; break;
                case "mother_bulgasari": kinds[rowIndex] = Nyangbingo.Core.BossKind.MotherBulgasari; break;
                case "imugi": kinds[rowIndex] = Nyangbingo.Core.BossKind.Imugi; break;
                case "gangcheol_boss": kinds[rowIndex] = Nyangbingo.Core.BossKind.Gangcheori; break;
                default:
                    Debug.LogError($"[Nyangbingo] Boss '{id}' is not one of the four official boss IDs.");
                    return;
            }
            displayNames[rowIndex] = row["name_ko"];
            recommendedDays[rowIndex] = row["recommended_day"];
            specialDescriptions[rowIndex] = row["special_ko"];
            deepAltarRequirements[rowIndex] = id == "imugi";
            forcedDays[rowIndex] = id == "gangcheol_boss" ? 30 : 0;
            if (!IsSnakeCaseId(id) || !seenBossIds.Add(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                string.IsNullOrWhiteSpace(displayNames[rowIndex]) ||
                string.IsNullOrWhiteSpace(recommendedDays[rowIndex]) ||
                string.IsNullOrWhiteSpace(specialDescriptions[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' has invalid identity or display data.");
                return;
            }
            if (!row.TryGetValue("drops_ko", out var dropsKo) || string.IsNullOrWhiteSpace(dropsKo) ||
                row.ContainsKey("guaranteedDrops"))
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' must keep display-only drops_ko and no machine drop column.");
                return;
            }

            if (!int.TryParse(row["hp"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHitPoints) ||
                !TryParseEngageSeconds(row["engage_sec_check"], out combatSeconds[rowIndex]) ||
                !TryParseFiniteFloat(row["wall_dps_default"], out wallDamageDefault[rowIndex]) ||
                !TryParseFiniteFloat(row["wall_dps_ice"], out wallDamageIce[rowIndex]) ||
                !TryParseFiniteFloat(row["wall_dps_iron_wall"], out wallDamageIronWall[rowIndex]) ||
                !int.TryParse(row["contact_dmg"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out contactDamage[rowIndex]) ||
                !TryParseFiniteFloat(row["tele_sec"], out telegraphSeconds[rowIndex]) ||
                !System.Enum.TryParse(row["shape"], true, out specialShapes[rowIndex]) ||
                !System.Enum.IsDefined(typeof(BossSpecialShape), specialShapes[rowIndex]) ||
                !TryParseFiniteFloat(row["range_tiles"], out specialRanges[rowIndex]) ||
                !TryParseFiniteFloat(row["arc_deg"], out specialArcs[rowIndex]) ||
                !int.TryParse(row["special_dmg_per_hit"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out specialDamage[rowIndex]) ||
                !TryParseFiniteFloat(row["duration_sec"], out specialDurations[rowIndex]) ||
                !TryParseFiniteFloat(row["tick_sec"], out specialTicks[rowIndex]) ||
                !TryParseFiniteFloat(row["knockback_tiles"], out specialKnockbacks[rowIndex]) ||
                !TryParseFiniteFloat(row["cd_sec"], out specialCooldowns[rowIndex]) ||
                (row["fire_tag"] != "0" && row["fire_tag"] != "1") ||
                (row["aim_lock"] != "0" && row["aim_lock"] != "1") ||
                !System.Enum.TryParse(row["mvp_scope"], true, out mvpScopes[rowIndex]) ||
                !System.Enum.IsDefined(typeof(ItemMvpScope), mvpScopes[rowIndex]) ||
                mvpScopes[rowIndex] == ItemMvpScope.Unspecified ||
                parsedHitPoints <= 0 || combatSeconds[rowIndex] <= 0f || wallDamageDefault[rowIndex] < 0f ||
                wallDamageIce[rowIndex] < 0f || wallDamageIronWall[rowIndex] < 0f || contactDamage[rowIndex] < 0 ||
                telegraphSeconds[rowIndex] < 0f || specialRanges[rowIndex] <= 0f ||
                specialArcs[rowIndex] < 0f || specialArcs[rowIndex] > 180f || specialDamage[rowIndex] <= 0 ||
                specialDurations[rowIndex] < 0f || specialTicks[rowIndex] < 0f || specialKnockbacks[rowIndex] < 0f ||
                specialCooldowns[rowIndex] <= 0f ||
                (specialDurations[rowIndex] == 0f) != (specialTicks[rowIndex] == 0f) ||
                (specialShapes[rowIndex] == BossSpecialShape.Cone && specialArcs[rowIndex] <= 0f))
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' has invalid extended combat data.");
                return;
            }
            hitPoints[rowIndex] = parsedHitPoints;
            fireTags[rowIndex] = row["fire_tag"] == "1";
            aimLocks[rowIndex] = row["aim_lock"] == "1";

            summonItems[rowIndex] = FindItem(itemDirectory, row["summon_item_id"]);
            if (summonItems[rowIndex] == null || summonItems[rowIndex].Id != row["summon_item_id"] ||
                !TryParseCraftingStationId(row["station_id"], out summonStations[rowIndex]))
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' has an unknown summon item or station. Reimport Items CSV first.");
                return;
            }

            var encodedMaterials = row["summon_materials"].Split(',');
            var parsedMaterials = new ItemAmount[encodedMaterials.Length];
            var uniqueMaterialIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (var materialIndex = 0; materialIndex < encodedMaterials.Length; materialIndex++)
            {
                var parts = encodedMaterials[materialIndex].Split(':');
                var material = parts.Length == 2 ? FindItem(itemDirectory, parts[0]) : null;
                if (parts.Length != 2 || material == null || material.Id != parts[0] ||
                    !uniqueMaterialIds.Add(parts[0]) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Boss '{id}' has invalid summon material '{encodedMaterials[materialIndex]}'.");
                    return;
                }
                parsedMaterials[materialIndex] = new ItemAmount { item = material, amount = amount };
            }
            summonMaterials[rowIndex] = parsedMaterials;

            if (!bossDropRows.TryGetValue(id, out var dropRow) ||
                !int.TryParse(dropRow["tears"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tears) ||
                !int.TryParse(dropRow["tears_bonus"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var tearsBonus) || tears <= 0 || tearsBonus != 0 ||
                !TryParseFiniteFloat(dropRow["sig_rate"], out var signatureRate) ||
                !Mathf.Approximately(signatureRate, 1f) || dropRow["sig_condition"] != "none")
            {
                Debug.LogError($"[Nyangbingo] Boss '{id}' has an invalid drops.csv reward contract.");
                return;
            }
            var encodedDrops = new List<string>
            {
                $"yokai_tear:{tears}",
                $"{dropRow["sig_drop_id"]}:1"
            };
            if (!string.IsNullOrWhiteSpace(dropRow["extra_drops"]))
                encodedDrops.AddRange(dropRow["extra_drops"].Split(','));
            var uniqueDropIds = new HashSet<string>(System.StringComparer.Ordinal);
            var parsedDrops = new ItemAmount[encodedDrops.Count];
            for (var dropIndex = 0; dropIndex < encodedDrops.Count; dropIndex++)
            {
                var parts = encodedDrops[dropIndex].Split(':');
                var dropItem = parts.Length == 2 ? FindItem(itemDirectory, parts[0]) : null;
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !uniqueDropIds.Add(parts[0]) ||
                    dropItem == null || dropItem.Id != parts[0] ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Boss '{id}' has an invalid or duplicate drops.csv reward '{encodedDrops[dropIndex]}'.");
                    return;
                }
                parsedDrops[dropIndex] = new ItemAmount { item = dropItem, amount = amount };
            }
            guaranteedDrops[rowIndex] = parsedDrops;
        }

        if (bossDropRows.Count != rows.Count)
        {
            Debug.LogError("[Nyangbingo] drops.csv boss sources do not exactly match bosses.csv IDs.");
            return;
        }

        Debug.Log($"[Nyangbingo] Boss and drop CSV semantic validation completed: {rows.Count} bosses.");
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
            serialized.FindProperty("displayName").stringValue = displayNames[rowIndex];
            serialized.FindProperty("kind").enumValueIndex = (int)kinds[rowIndex];
            serialized.FindProperty("hitPoints").intValue = hitPoints[rowIndex];
            serialized.FindProperty("expectedCombatSeconds").floatValue = combatSeconds[rowIndex];
            serialized.FindProperty("summonItem").objectReferenceValue = summonItems[rowIndex];
            var summonMaterialsProperty = serialized.FindProperty("summonMaterials");
            summonMaterialsProperty.arraySize = summonMaterials[rowIndex].Length;
            for (var materialIndex = 0; materialIndex < summonMaterials[rowIndex].Length; materialIndex++)
                SetItemAmount(summonMaterialsProperty.GetArrayElementAtIndex(materialIndex),
                    summonMaterials[rowIndex][materialIndex].item, summonMaterials[rowIndex][materialIndex].amount);
            serialized.FindProperty("summonStation").enumValueIndex = (int)summonStations[rowIndex];
            serialized.FindProperty("recommendedDay").stringValue = recommendedDays[rowIndex];
            serialized.FindProperty("requiresDeepAltar").boolValue = deepAltarRequirements[rowIndex];
            serialized.FindProperty("forcedDay").intValue = forcedDays[rowIndex];
            serialized.FindProperty("wallDamageDefault").floatValue = wallDamageDefault[rowIndex];
            serialized.FindProperty("wallDamageIce").floatValue = wallDamageIce[rowIndex];
            serialized.FindProperty("wallDamageIronWall").floatValue = wallDamageIronWall[rowIndex];
            serialized.FindProperty("contactDamage").intValue = contactDamage[rowIndex];
            serialized.FindProperty("specialDescription").stringValue = specialDescriptions[rowIndex];
            serialized.FindProperty("telegraphSeconds").floatValue = telegraphSeconds[rowIndex];
            serialized.FindProperty("specialShape").enumValueIndex = (int)specialShapes[rowIndex];
            serialized.FindProperty("specialRangeTiles").floatValue = specialRanges[rowIndex];
            serialized.FindProperty("specialArcDegrees").floatValue = specialArcs[rowIndex];
            serialized.FindProperty("specialDamagePerHit").intValue = specialDamage[rowIndex];
            serialized.FindProperty("specialDurationSeconds").floatValue = specialDurations[rowIndex];
            serialized.FindProperty("specialTickSeconds").floatValue = specialTicks[rowIndex];
            serialized.FindProperty("specialKnockbackTiles").floatValue = specialKnockbacks[rowIndex];
            serialized.FindProperty("specialCooldownSeconds").floatValue = specialCooldowns[rowIndex];
            serialized.FindProperty("specialHasFireTag").boolValue = fireTags[rowIndex];
            serialized.FindProperty("specialAimLocks").boolValue = aimLocks[rowIndex];
            serialized.FindProperty("mvpScope").enumValueIndex = (int)mvpScopes[rowIndex];
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
        const string itemDirectory = "Assets/Data/SO/Items";
        const string equipmentDirectory = "Assets/Data/SO/Equipment";
        const string targetDirectory = "Assets/Data/SO/Chests";
        const string worldConfigPath = "Assets/Data/SO/WorldGenerationConfig.asset";
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

        var ids = new string[rows.Count];
        var regions = new Nyangbingo.Core.ChestRegion[rows.Count];
        var spawnCounts = new int[rows.Count];
        var equipmentPools = new EquipmentDefinition[rows.Count][];
        var rewards = new ItemAmount[rows.Count][];
        var notes = new string[rows.Count];
        var uniqueRegions = new HashSet<Nyangbingo.Core.ChestRegion>();
        var totalSpawnCount = 0;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            switch (row["pool"])
            {
                case "지상 폐허": ids[rowIndex] = "ruins_chest"; regions[rowIndex] = Nyangbingo.Core.ChestRegion.Ruins; break;
                case "상층": ids[rowIndex] = "upper_chest"; regions[rowIndex] = Nyangbingo.Core.ChestRegion.Upper; break;
                case "중층": ids[rowIndex] = "middle_chest"; regions[rowIndex] = Nyangbingo.Core.ChestRegion.Middle; break;
                case "심층": ids[rowIndex] = "deep_chest"; regions[rowIndex] = Nyangbingo.Core.ChestRegion.Deep; break;
                default:
                    Debug.LogError($"[Nyangbingo] Chest pool '{row["pool"]}' is unknown.");
                    return;
            }
            if (!uniqueRegions.Add(regions[rowIndex]) ||
                !int.TryParse(row["count"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out spawnCounts[rowIndex]) || spawnCounts[rowIndex] <= 0)
            {
                Debug.LogError($"[Nyangbingo] Chest pool '{row["pool"]}' has a duplicate region or invalid count.");
                return;
            }
            totalSpawnCount += spawnCounts[rowIndex];
            notes[rowIndex] = row["note"];

            var equipmentIds = row["accessory_pool"].Split('|');
            var equipmentPool = new EquipmentDefinition[equipmentIds.Length];
            var uniqueIds = new HashSet<string>(System.StringComparer.Ordinal);
            if (equipmentIds.Length == 0 || string.IsNullOrWhiteSpace(row["accessory_pool"]))
            {
                Debug.LogError($"[Nyangbingo] Chest '{ids[rowIndex]}' must have at least one accessory reward.");
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
                    Debug.LogError($"[Nyangbingo] Chest '{ids[rowIndex]}' has an invalid accessory pool entry '{equipmentId}'. " +
                                   "Reimport Equipment CSV first.");
                    return;
                }
            }
            equipmentPools[rowIndex] = equipmentPool;

            var encodedRewards = row["bonus_items"].Split(',');
            var parsedRewards = new ItemAmount[encodedRewards.Length];
            var uniqueRewardIds = new HashSet<string>(System.StringComparer.Ordinal);
            if (encodedRewards.Length == 0 || string.IsNullOrWhiteSpace(row["bonus_items"]))
            {
                Debug.LogError($"[Nyangbingo] Chest '{ids[rowIndex]}' must have at least one bonus item.");
                return;
            }
            for (var rewardIndex = 0; rewardIndex < encodedRewards.Length; rewardIndex++)
            {
                var parts = encodedRewards[rewardIndex].Split(':');
                var item = parts.Length == 2 ? FindItem(itemDirectory, parts[0]) : null;
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) ||
                    !uniqueRewardIds.Add(parts[0]) || item == null || item.Id != parts[0] ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Chest '{ids[rowIndex]}' has an invalid bonus item '{encodedRewards[rewardIndex]}'. " +
                                   "Reimport Items CSV first.");
                    return;
                }
                parsedRewards[rewardIndex] = new ItemAmount { item = item, amount = amount };
            }
            rewards[rowIndex] = parsedRewards;
        }

        if (rows.Count != 4 || uniqueRegions.Count != 4 || totalSpawnCount != 20)
        {
            Debug.LogError($"[Nyangbingo] Chest CSV must define four regional pools and exactly 20 chests; found {rows.Count} pools and {totalSpawnCount} chests.");
            return;
        }

        var worldConfig = AssetDatabase.LoadAssetAtPath<Nyangbingo.World.WorldGenerationConfig>(worldConfigPath);
        if (worldConfig == null)
        {
            Debug.LogError("[Nyangbingo] WorldGenerationConfig asset is missing.");
            return;
        }

        Debug.Log($"[Nyangbingo] Chest CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var id = ids[rowIndex];
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
            serialized.FindProperty("spawnCount").intValue = spawnCounts[rowIndex];
            var rewardsProperty = serialized.FindProperty("rewards");
            rewardsProperty.arraySize = rewards[rowIndex].Length;
            for (var rewardIndex = 0; rewardIndex < rewards[rowIndex].Length; rewardIndex++)
                SetItemAmount(rewardsProperty.GetArrayElementAtIndex(rewardIndex),
                    rewards[rowIndex][rewardIndex].item, rewards[rowIndex][rewardIndex].amount);
            SetObjectReferences(serialized.FindProperty("equipmentPool"), equipmentPools[rowIndex]);
            serialized.FindProperty("note").stringValue = notes[rowIndex];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        var worldConfigSerialized = new SerializedObject(worldConfig);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            switch (regions[rowIndex])
            {
                case Nyangbingo.Core.ChestRegion.Ruins:
                    worldConfigSerialized.FindProperty("chestCountRuins").intValue = spawnCounts[rowIndex]; break;
                case Nyangbingo.Core.ChestRegion.Upper:
                    worldConfigSerialized.FindProperty("chestCountUpper").intValue = spawnCounts[rowIndex]; break;
                case Nyangbingo.Core.ChestRegion.Middle:
                    worldConfigSerialized.FindProperty("chestCountMiddle").intValue = spawnCounts[rowIndex]; break;
                case Nyangbingo.Core.ChestRegion.Deep:
                    worldConfigSerialized.FindProperty("chestCountDeep").intValue = spawnCounts[rowIndex]; break;
            }
        }
        worldConfigSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(worldConfig);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Nyangbingo] Chest CSV reimport completed: {rows.Count} assets.");
    }

    [MenuItem("Nyangbingo/Reimport Globals CSV")]
    private static void ReimportGlobals()
    {
        const string csvPath = "Assets/Data/CSV/globals.csv";
        const string targetDirectory = "Assets/Data/SO/Globals";
        if (!File.Exists(csvPath))
        {
            Debug.LogError("[Nyangbingo] globals.csv was not found.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath, mergeUnquotedTrailingNote: true);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Globals CSV validation failed: {exception.Message}");
            return;
        }

        if (rows.Count < 93 || !HasColumns(rows[0], "key", "value", "unit", "note"))
        {
            Debug.LogError("[Nyangbingo] globals.csv must contain at least the v29 official 93 rows (WaveNight keys may add more).");
            return;
        }

        var textUnits = new HashSet<string>(System.StringComparer.Ordinal)
            { "ore:ingot", "recipe", "rule", "scope", "file", "curve", "list", "ref", "mult" };
        var integerUnits = new HashSet<string>(System.StringComparer.Ordinal)
            { "count", "day", "gauge", "hp", "person", "px" };
        var keys = new HashSet<string>(System.StringComparer.Ordinal);
        var numeric = new Dictionary<string, float>(System.StringComparer.Ordinal);
        var values = new Dictionary<string, string>(System.StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var key = row["key"];
            var value = row["value"];
            var unit = row["unit"];
            var isWaveTimes = key == "baekjung_wave_times_sec";
            var valid = IsSnakeCaseId(key) && keys.Add(key) && !string.IsNullOrWhiteSpace(value) &&
                        !string.IsNullOrWhiteSpace(unit) && !string.IsNullOrWhiteSpace(row["note"]);
            if (valid && unit == "bool") valid = value == "0" || value == "1";
            else if (valid && !textUnits.Contains(unit) && !isWaveTimes)
            {
                valid = TryParseFiniteFloat(value, out var parsed);
                if (valid)
                {
                    numeric[key] = parsed;
                    if (integerUnits.Contains(unit)) valid = Mathf.Approximately(parsed, Mathf.Round(parsed));
                }
            }

            if (!valid)
            {
                Debug.LogError($"[Nyangbingo] Global row {i + 1} has an invalid key, value, unit, or note.");
                return;
            }
            values[key] = value;
        }

        var derivedValuesValid = numeric.TryGetValue("day_length_sec", out var dayLength) &&
                                 numeric.TryGetValue("night_length_sec", out var nightLength) &&
                                 numeric.TryGetValue("day_total_sec", out var dayTotal) &&
                                 Mathf.Approximately(dayLength + nightLength, dayTotal) &&
                                 numeric.TryGetValue("seal_window_rx", out var sealRadiusX) &&
                                 numeric.TryGetValue("seal_window_ry", out var sealRadiusY) &&
                                 numeric.TryGetValue("seal_cap", out var sealCap) &&
                                 Mathf.Approximately((2f * sealRadiusX + 1f) * (2f * sealRadiusY + 1f), sealCap) &&
                                  numeric.TryGetValue("mvp_days", out var mvpDays) &&
                                  numeric.TryGetValue("total_days", out var totalDays) && mvpDays <= totalDays &&
                                  numeric.TryGetValue("wallpaper_coverage", out var wallpaperCoverage) &&
                                  Mathf.Approximately(wallpaperCoverage, 100f) &&
                                  numeric.TryGetValue("wallpaper_coldsource_bonus", out var wallpaperBonus) &&
                                  Mathf.Approximately(wallpaperBonus, 25f) &&
                                  values.TryGetValue("wallpaper_remove_rule", out var wallpaperRemoveRule) &&
                                  wallpaperRemoveRule == "restore_original" &&
                                  values.TryGetValue(GlobalKeys.BossFieldYokai, out var bossFieldYokai) &&
                                  bossFieldYokai == "freeze_resume" &&
                                  numeric.TryGetValue(GlobalKeys.CaveMaxHeight, out var caveMaxHeight) &&
                                  Mathf.Approximately(caveMaxHeight, 12f) &&
                                  values.TryGetValue(GlobalKeys.FurnitureMvpScope, out var furnitureScope) &&
                                  furnitureScope == "B" &&
                                  numeric.TryGetValue(GlobalKeys.InventorySlots, out var inventorySlots) &&
                                  Mathf.Approximately(inventorySlots, 50f) &&
                                  values.TryGetValue(GlobalKeys.ActiveSlotRule, out var activeSlotRule) &&
                                  activeSlotRule == "weapon_or_tool_1" &&
                                  numeric.TryGetValue(GlobalKeys.JangdokStorageSlots, out var jangdokSlots) &&
                                  Mathf.Approximately(jangdokSlots, 40f);
        if (!derivedValuesValid)
        {
            Debug.LogError("[Nyangbingo] Globals derived day, seal-window, or survival values are inconsistent.");
            return;
        }

        Debug.Log($"[Nyangbingo] Globals CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var assetPath = $"{targetDirectory}/{row["key"]}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<GlobalDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<GlobalDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("key").stringValue = row["key"];
            serialized.FindProperty("value").stringValue = row["value"];
            serialized.FindProperty("unit").stringValue = row["unit"];
            serialized.FindProperty("note").stringValue = row["note"];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] Globals CSV reimport completed.");
    }

    [MenuItem("Nyangbingo/Reimport Day Curve CSV")]
    private static void ReimportDayCurve()
    {
        const string csvPath = "Assets/Data/CSV/day-curve.csv";
        const string targetDirectory = "Assets/Data/SO/DayCurves";
        if (!File.Exists(csvPath))
        {
            Debug.LogError("[Nyangbingo] day-curve.csv was not found.");
            return;
        }

        List<Dictionary<string, string>> rows;
        try
        {
            rows = NyangbingoCsvUtility.ReadRows(csvPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Nyangbingo] Day curve CSV validation failed: {exception.Message}");
            return;
        }

        if (rows.Count != 30 || !HasColumns(rows[0], "day", "heat_stage", "day_fire_dmg_per_sec",
                "night_yokai_count", "yokai_wall_dmg", "pace_seal_pct", "pace_mineral_tier",
                "max_active", "spawn_composition", "spawn_mult", "drop_mult", "event_id"))
        {
            Debug.LogError("[Nyangbingo] day-curve.csv must contain the official 30-row v24.1 schema.");
            return;
        }

        var heatStages = new int[rows.Count];
        var fireDamage = new float[rows.Count];
        var nightCounts = new int[rows.Count];
        var wallDamage = new float[rows.Count];
        var sealPace = new float[rows.Count];
        var mineralTiers = new int[rows.Count];
        var maxActive = new int[rows.Count];
        var spawnMultipliers = new float[rows.Count];
        var dropMultipliers = new float[rows.Count];
        var compositions = new DayCurveSpawnAmount[rows.Count][];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var expectedDay = rowIndex + 1;
            var parsed = int.TryParse(row["day"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) &&
                         day == expectedDay &&
                         int.TryParse(row["heat_stage"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out heatStages[rowIndex]) && heatStages[rowIndex] >= 1 && heatStages[rowIndex] <= 3 &&
                         TryParseFiniteFloat(row["day_fire_dmg_per_sec"], out fireDamage[rowIndex]) &&
                         fireDamage[rowIndex] >= 0f &&
                         int.TryParse(row["night_yokai_count"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out nightCounts[rowIndex]) && nightCounts[rowIndex] >= 0 &&
                         TryParseFiniteFloat(row["yokai_wall_dmg"], out wallDamage[rowIndex]) &&
                         wallDamage[rowIndex] >= 0f &&
                         TryParseFiniteFloat(row["pace_seal_pct"], out sealPace[rowIndex]) &&
                         sealPace[rowIndex] >= 0f && sealPace[rowIndex] <= 100f &&
                         int.TryParse(row["pace_mineral_tier"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out mineralTiers[rowIndex]) && mineralTiers[rowIndex] >= 1 && mineralTiers[rowIndex] <= 3 &&
                         int.TryParse(row["max_active"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out maxActive[rowIndex]) && maxActive[rowIndex] >= 0 &&
                         TryParseFiniteFloat(row["spawn_mult"], out spawnMultipliers[rowIndex]) &&
                         spawnMultipliers[rowIndex] >= 0f &&
                         TryParseFiniteFloat(row["drop_mult"], out dropMultipliers[rowIndex]) &&
                         dropMultipliers[rowIndex] >= 0f;
            if (!parsed)
            {
                Debug.LogError($"[Nyangbingo] Day curve row {rowIndex + 1} has invalid day or numeric data.");
                return;
            }

            var encodedComposition = row["spawn_composition"].Split(',');
            var uniqueKinds = new HashSet<Nyangbingo.Core.YokaiKind>();
            var parsedComposition = new DayCurveSpawnAmount[encodedComposition.Length];
            var total = 0;
            for (var compositionIndex = 0; compositionIndex < encodedComposition.Length; compositionIndex++)
            {
                var parts = encodedComposition[compositionIndex].Split(':');
                if (parts.Length != 2 || !TryParseYokaiKind(parts[0], out var kind) || !uniqueKinds.Add(kind) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                    amount <= 0)
                {
                    Debug.LogError($"[Nyangbingo] Day curve day {expectedDay} has invalid spawn composition.");
                    return;
                }
                parsedComposition[compositionIndex] = new DayCurveSpawnAmount { kind = kind, amount = amount };
                total += amount;
            }
            compositions[rowIndex] = parsedComposition;

            var expectedTotal = nightCounts[rowIndex] * spawnMultipliers[rowIndex];
            var eventValid = string.IsNullOrEmpty(row["event_id"]) ||
                             expectedDay == 15 && row["event_id"] == "baekjung" ||
                             expectedDay == 30 && row["event_id"] == "gangcheol_boss";
            if (!Mathf.Approximately(total, expectedTotal) || maxActive[rowIndex] != total || !eventValid)
            {
                Debug.LogError($"[Nyangbingo] Day curve day {expectedDay} has inconsistent spawn or event data.");
                return;
            }
        }

        Debug.Log($"[Nyangbingo] Day curve CSV semantic validation completed: {rows.Count} rows.");
        EnsureFolder(targetDirectory);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var assetPath = $"{targetDirectory}/day_{rowIndex + 1:00}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<DayCurveDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<DayCurveDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("day").intValue = rowIndex + 1;
            serialized.FindProperty("heatStage").intValue = heatStages[rowIndex];
            serialized.FindProperty("dayFireDamagePerSecond").floatValue = fireDamage[rowIndex];
            serialized.FindProperty("nightYokaiCount").intValue = nightCounts[rowIndex];
            serialized.FindProperty("yokaiWallDamage").floatValue = wallDamage[rowIndex];
            serialized.FindProperty("paceSealPercent").floatValue = sealPace[rowIndex];
            serialized.FindProperty("paceMineralTier").intValue = mineralTiers[rowIndex];
            serialized.FindProperty("maxActive").intValue = maxActive[rowIndex];
            serialized.FindProperty("spawnMultiplier").floatValue = spawnMultipliers[rowIndex];
            serialized.FindProperty("dropMultiplier").floatValue = dropMultipliers[rowIndex];
            serialized.FindProperty("eventId").stringValue = rows[rowIndex]["event_id"];
            var compositionProperty = serialized.FindProperty("spawnComposition");
            compositionProperty.arraySize = compositions[rowIndex].Length;
            for (var i = 0; i < compositions[rowIndex].Length; i++)
            {
                var element = compositionProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("kind").enumValueIndex = (int)compositions[rowIndex][i].kind;
                element.FindPropertyRelative("amount").intValue = compositions[rowIndex][i].amount;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Nyangbingo] Day curve CSV reimport completed.");
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
        var tearItem = FindItem(itemDirectory, "yokai_tear");
        if (tearItem == null || tearItem.Id != "yokai_tear")
        {
            Debug.LogError("[Nyangbingo] Yokai stats require the 'yokai_tear' item. Reimport Items CSV first.");
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
        var displayNames = new string[rows.Count];
        var appearanceHints = new string[rows.Count];
        var codexNotes = new string[rows.Count];
        var hitPoints = new int[rows.Count];
        var moveSpeeds = new float[rows.Count];
        var wallDamageDefault = new float[rows.Count];
        var wallDamageIce = new float[rows.Count];
        var wallDamageIronWall = new float[rows.Count];
        var contactDamage = new int[rows.Count];
        var contactDamageNoLantern = new int[rows.Count];
        var damageTakenMultipliers = new float[rows.Count];
        var damageTakenConditions = new YokaiDamageTakenCondition[rows.Count];
        var stealSlots = new int[rows.Count];
        var stealMaxItems = new int[rows.Count];
        var tearDrops = new int[rows.Count];
        var signatureItems = new ItemDefinition[rows.Count];
        var signatureChances = new float[rows.Count];
        var spawnTracks = new YokaiSpawnTrack[rows.Count];
        var raidFleesAtDawn = new bool[rows.Count];
        var uniqueKinds = new HashSet<Nyangbingo.Core.YokaiKind>();
        if (rows.Count != 5)
        {
            Debug.LogError($"[Nyangbingo] yokai-stats.csv must contain exactly five official yokai, but found {rows.Count}.");
            return;
        }
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row["id"];
            displayNames[i] = row["name_ko"];
            appearanceHints[i] = row["appear_from"];
            codexNotes[i] = row["note"];
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !TryParseYokaiKind(id, out kinds[i]) ||
                !System.Enum.IsDefined(typeof(Nyangbingo.Core.YokaiKind), kinds[i]) || !uniqueKinds.Add(kinds[i]) ||
                string.IsNullOrWhiteSpace(displayNames[i]) || string.IsNullOrWhiteSpace(appearanceHints[i]) ||
                string.IsNullOrWhiteSpace(codexNotes[i]))
            {
                Debug.LogError($"[Nyangbingo] Yokai '{id}' has invalid identity or codex display data.");
                return;
            }

            signatureItems[i] = FindItem(itemDirectory, row["sig_drop_id"]);
            if (signatureItems[i] == null || signatureItems[i].Id != row["sig_drop_id"])
            {
                Debug.LogError($"[Nyangbingo] Yokai '{id}' has an unknown signature item '{row["sig_drop_id"]}'. Reimport Items CSV first.");
                return;
            }

            if (!int.TryParse(row["hp"], NumberStyles.Integer, CultureInfo.InvariantCulture, out hitPoints[i]) ||
                hitPoints[i] <= 0 || !TryParseFiniteFloat(row["move_tiles_per_sec"], out moveSpeeds[i]) ||
                moveSpeeds[i] < 0f ||
                !TryParseFiniteFloat(row["wall_dps_default"], out wallDamageDefault[i]) ||
                wallDamageDefault[i] < 0f ||
                !TryParseFiniteFloat(row["wall_dps_ice"], out wallDamageIce[i]) || wallDamageIce[i] < 0f ||
                !TryParseFiniteFloat(row["wall_dps_iron_wall"], out wallDamageIronWall[i]) ||
                wallDamageIronWall[i] < 0f ||
                !int.TryParse(row["contact_dmg"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out contactDamage[i]) || contactDamage[i] < 0 ||
                !int.TryParse(row["contact_dmg_no_lantern"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out contactDamageNoLantern[i]) || contactDamageNoLantern[i] < 0 ||
                !TryParseFiniteFloat(row["dmg_taken_mult"], out damageTakenMultipliers[i]) ||
                damageTakenMultipliers[i] <= 0f ||
                !TryParseYokaiDamageTakenCondition(row["dmg_taken_condition"], out damageTakenConditions[i]) ||
                !int.TryParse(row["steal_slots"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out stealSlots[i]) || stealSlots[i] < 0 ||
                !int.TryParse(row["steal_max_items"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out stealMaxItems[i]) || stealMaxItems[i] < 0 ||
                ((stealSlots[i] == 0) != (stealMaxItems[i] == 0)) ||
                !int.TryParse(row["tears"], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out tearDrops[i]) || tearDrops[i] < 0 ||
                !TryParseFiniteFloat(row["sig_rate"], out signatureChances[i]) ||
                signatureChances[i] < 0f || signatureChances[i] > 1f ||
                !TryParseYokaiSpawnTracks(row["spawn_track"], out spawnTracks[i]) ||
                (spawnTracks[i] & YokaiSpawnTrack.Raid) == 0 ||
                !TryParseDawnFlee(row["dawn_flee"], spawnTracks[i], out raidFleesAtDawn[i]) ||
                !raidFleesAtDawn[i] ||
                ((spawnTracks[i] & YokaiSpawnTrack.Resident) != 0 && kinds[i] != Nyangbingo.Core.YokaiKind.Eoduksini))
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
            serialized.FindProperty("displayName").stringValue = displayNames[i];
            serialized.FindProperty("appearanceHint").stringValue = appearanceHints[i];
            serialized.FindProperty("codexNote").stringValue = codexNotes[i];
            serialized.FindProperty("kind").enumValueIndex = (int)kinds[i];
            serialized.FindProperty("hitPoints").intValue = hitPoints[i];
            serialized.FindProperty("moveSpeed").floatValue = moveSpeeds[i];
            serialized.FindProperty("wallDamageDefault").floatValue = wallDamageDefault[i];
            serialized.FindProperty("wallDamageIce").floatValue = wallDamageIce[i];
            serialized.FindProperty("wallDamageIronWall").floatValue = wallDamageIronWall[i];
            serialized.FindProperty("contactDamage").intValue = contactDamage[i];
            serialized.FindProperty("contactDamageNoLantern").intValue = contactDamageNoLantern[i];
            serialized.FindProperty("damageTakenMultiplier").floatValue = damageTakenMultipliers[i];
            serialized.FindProperty("damageTakenCondition").enumValueIndex = (int)damageTakenConditions[i];
            serialized.FindProperty("stealSlots").intValue = stealSlots[i];
            serialized.FindProperty("stealMaxItems").intValue = stealMaxItems[i];
            serialized.FindProperty("tearItem").objectReferenceValue = tearItem;
            serialized.FindProperty("tearDrop").intValue = tearDrops[i];
            serialized.FindProperty("signatureItem").objectReferenceValue = signatureItems[i];
            serialized.FindProperty("signatureChance").floatValue = signatureChances[i];
            serialized.FindProperty("spawnTracks").intValue = (int)spawnTracks[i];
            serialized.FindProperty("raidFleesAtDawn").boolValue = raidFleesAtDawn[i];
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

    private static bool TryParseEngageSeconds(string text, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var equalsIndex = text.LastIndexOf('=');
        if (equalsIndex < 0 || equalsIndex == text.Length - 1) return false;
        var secondsIndex = text.IndexOf('초', equalsIndex + 1);
        if (secondsIndex < 0) secondsIndex = text.Length;
        var encodedValue = text.Substring(equalsIndex + 1, secondsIndex - equalsIndex - 1).Trim();
        return TryParseFiniteFloat(encodedValue, out value) && value > 0f;
    }

    private static bool TryParseYokaiDamageTakenCondition(string value, out YokaiDamageTakenCondition condition)
    {
        switch (value)
        {
            case "none": condition = YokaiDamageTakenCondition.None; return true;
            case "lantern_radius": condition = YokaiDamageTakenCondition.LanternRadius; return true;
            case "steal_only": condition = YokaiDamageTakenCondition.StealOnly; return true;
            default: condition = default; return false;
        }
    }

    private static bool TryParseYokaiSpawnTracks(string value, out YokaiSpawnTrack tracks)
    {
        tracks = YokaiSpawnTrack.None;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var encodedTracks = value.Split('+');
        for (var i = 0; i < encodedTracks.Length; i++)
        {
            switch (encodedTracks[i])
            {
                case "raid": tracks |= YokaiSpawnTrack.Raid; break;
                case "resident": tracks |= YokaiSpawnTrack.Resident; break;
                default: tracks = YokaiSpawnTrack.None; return false;
            }
        }
        return tracks != YokaiSpawnTrack.None;
    }

    private static bool TryParseDawnFlee(string value, YokaiSpawnTrack tracks, out bool raidFlees)
    {
        if (value == "1")
        {
            raidFlees = true;
            return (tracks & YokaiSpawnTrack.Raid) != 0;
        }
        if ((tracks & YokaiSpawnTrack.Resident) != 0 && value.StartsWith("raid만 1", System.StringComparison.Ordinal))
        {
            raidFlees = true;
            return (tracks & YokaiSpawnTrack.Raid) != 0;
        }
        raidFlees = false;
        return false;
    }

    private static bool HasColumns(Dictionary<string, string> row, params string[] columns)
    {
        if (row == null || columns == null) return false;
        for (var i = 0; i < columns.Length; i++)
            if (!row.ContainsKey(columns[i])) return false;
        return true;
    }

    private static bool IsSnakeCaseId(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] == '_' || value[value.Length - 1] == '_') return false;
        var previousUnderscore = false;
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            var underscore = character == '_';
            if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && !underscore)
                return false;
            if (underscore && previousUnderscore) return false;
            previousUnderscore = underscore;
        }
        return true;
    }

    private static bool TryParseCraftingStationId(string value, out Nyangbingo.Core.CraftingStation station)
    {
        switch (value)
        {
            case "none": station = Nyangbingo.Core.CraftingStation.None; return true;
            case "workbench": station = Nyangbingo.Core.CraftingStation.Workbench; return true;
            case "furnace": station = Nyangbingo.Core.CraftingStation.Furnace; return true;
            case "ice_anvil": station = Nyangbingo.Core.CraftingStation.IceAnvil; return true;
            case "blast_furnace": station = Nyangbingo.Core.CraftingStation.Foundry; return true;
            default: station = default; return false;
        }
    }

    private static bool TryParseMineralLayer(string value, out MineralLayer layer)
    {
        switch (value)
        {
            case "지상(밤)": layer = MineralLayer.SurfaceNight; return true;
            case "지상 폐허(밤)": layer = MineralLayer.SurfaceRuinNight; return true;
            case "지하 상층 T1": layer = MineralLayer.UndergroundUpper; return true;
            case "지하 중층 T2": layer = MineralLayer.UndergroundMiddle; return true;
            case "지하 심층 T3": layer = MineralLayer.UndergroundDeep; return true;
            default: layer = default; return false;
        }
    }

    private static bool TryParseIdMigrationDomain(string value, out IdMigrationDomain domain)
    {
        switch (value)
        {
            case "item": domain = IdMigrationDomain.Item; return true;
            case "yokai": domain = IdMigrationDomain.Yokai; return true;
            case "boss": domain = IdMigrationDomain.Boss; return true;
            case "smelting": domain = IdMigrationDomain.Smelting; return true;
            default: domain = default; return false;
        }
    }

    private static bool TryParseIdMigrationAction(string value, out IdMigrationAction action)
    {
        switch (value)
        {
            case "rename": action = IdMigrationAction.Rename; return true;
            case "remove_refund": action = IdMigrationAction.RemoveRefund; return true;
            default: action = default; return false;
        }
    }

    private static bool TryParseYokaiKind(string id, out Nyangbingo.Core.YokaiKind kind)
    {
        switch (id)
        {
            case "club": kind = Nyangbingo.Core.YokaiKind.ClubGoblin; return true;
            case "bulgasari": kind = Nyangbingo.Core.YokaiKind.Bulgasari; return true;
            case "yakwang": kind = Nyangbingo.Core.YokaiKind.Yagwanggwi; return true;
            case "eoduksini": kind = Nyangbingo.Core.YokaiKind.Eoduksini; return true;
            case "gangcheol": kind = Nyangbingo.Core.YokaiKind.Gangcheori; return true;
            default: kind = default; return false;
        }
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
