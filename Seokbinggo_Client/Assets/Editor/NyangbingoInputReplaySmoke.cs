using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nyangbingo.Core;
using Nyangbingo.UI;
using Nyangbingo.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class NyangbingoInputReplaySmoke
{
    private const string Armed = "Nyangbingo.QA.ReplayArmed";
    public const string SaveKey = "Nyangbingo.QA.SaveDirectory";
    private const string OriginalScene = "Nyangbingo.QA.OriginalScene";
    static NyangbingoInputReplaySmoke() => EditorApplication.playModeStateChanged += OnPlayState;

    [MenuItem("Nyangbingo/QA/Run Isolated Input Smoke (3 passes)")]
    public static void Run() => StartRun(false);

    [MenuItem("Nyangbingo/QA/Measure First Workbench (normal input)")]
    public static void MeasureWorkbench() => StartRun(true);

    [MenuItem("Nyangbingo/QA/Measure First Furnace (normal input)")]
    public static void MeasureFurnace() => StartRun(true, true);

    [MenuItem("Nyangbingo/QA/Resume Furnace from Earned QA Save")]
    public static void ResumeFurnace() => StartRun(true, true, true);

    [MenuItem("Nyangbingo/QA/Resume Gathered Materials Ascent")]
    public static void ResumeAscent() => StartRun(true, true, true, true);

    [MenuItem("Nyangbingo/QA/Resume Workbench Return and Furnace")]
    public static void ResumeReturn() => StartRun(true, true, true, false, true);

    [MenuItem("Nyangbingo/QA/Install Earned Furnace and Smelt")]
    public static void InstallAndSmelt() => StartRun(true, true, true, false, false, true);

    [MenuItem("Nyangbingo/QA/Craft First Iron Claw from Earned Save")]
    public static void CraftIronClaw() => StartRun(true, true, true, false, false, false, true);

    [MenuItem("Nyangbingo/QA/Deep Mining and Foundry from Earned Claw")]
    public static void DeepMining() => StartRun(true, true, true, false, false, false, false, true);

    [MenuItem("Nyangbingo/QA/Resume Deep Mining from Partial Save")]
    public static void ResumeDeepMining() => StartRun(true, true, true, false, false, false, false, true, true);

    [MenuItem("Nyangbingo/QA/Install Earned Foundry and Smelt Ice Steel")]
    public static void SmeltIceSteel() => StartRun(true, true, true, false, false, true, false, false, false, true);

    [MenuItem("Nyangbingo/QA/Craft Ice Anvil from Earned Ice Steel")]
    public static void CraftIceAnvil() => StartRun(true, true, true, false, false, false, false, true, false, false, true);

    [MenuItem("Nyangbingo/QA/Install Earned Ice Anvil and Reload")]
    public static void InstallIceAnvil() => StartRun(true, resume: true, smelt: true, installAnvil: true);
    [MenuItem("Nyangbingo/QA/Natural First Combat from Installed Anvil")]
    public static void NaturalCombat() => StartRun(true, resume: true, combat: true);
    [MenuItem("Nyangbingo/QA/Repeat Natural Combat from Earned Night")]
    public static void RepeatNaturalCombat() => StartRun(true, resume: true, combat: true, combatNight: true);
    [MenuItem("Nyangbingo/QA/Gather Satba Materials from Earned Combat")]
    public static void GatherSatbaMaterials() => StartRun(true, resume: true, harvest: true);

    [MenuItem("Nyangbingo/QA/Gather Gate Materials From Repaired Shelter")]
    public static void GatherGateFromRepairedShelter()
    {
        GatherDoorMaterials();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repaired-rest-3\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ShelterGateGather",true);
    }

    [MenuItem("Nyangbingo/QA/Continue Partial Shelter Gate Gathering")]
    public static void ContinueShelterGateGather()
    {
        GatherGateFromRepairedShelter();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\shelter-gate-gather-partial-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ShelterGateResume",true);
    }

    [MenuItem("Nyangbingo/QA/Inspect Storage After Gate Outing")]
    public static void InspectStorageAfterGateOuting()
    {
        GatherGateFromRepairedShelter();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\shelter-gate-gather-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.GateStorageInspect",true);
    }

    [MenuItem("Nyangbingo/QA/Gather Ice Core Materials from Earned Anvil")]
    public static void GatherIceCoreMaterials() => StartRun(true, resume: true, coreGather: true);

    [MenuItem("Nyangbingo/QA/Craft Ice Core from Earned Ice Shards")]
    public static void CraftIceCore() => StartRun(true, resume: true, deep: true, coreCraft: true);

    [MenuItem("Nyangbingo/QA/Install Earned Ice Core and Inspect")]
    public static void InstallIceCore() => StartRun(true, resume: true, smelt: true, foundrySmelt: true, installCore: true);

    [MenuItem("Nyangbingo/QA/Craft Seal Wall from Earned Core")]
    public static void CraftSealWall() => StartRun(true, resume: true, wallCraft: true);

    [MenuItem("Nyangbingo/QA/Repair First Core Leak Normally")]
    public static void RepairCoreLeak()
    {
        StartRun(true, resume: true, wallCraft: true);
        SessionState.SetBool("Nyangbingo.QA.WallRepair", true);
    }

    [MenuItem("Nyangbingo/QA/Repair Core Leaks Continuously")]
    public static void RepairCoreLeaksContinuously()
    {
        RepairCoreLeak();
        SessionState.SetBool("Nyangbingo.QA.WallChain", true);
    }

    [MenuItem("Nyangbingo/QA/Gather Door Materials from Repaired Core")]
    public static void GatherDoorMaterials() => StartRun(true, resume: true, harvest: true, doorGather: true);

    [MenuItem("Nyangbingo/QA/Gather Storage Shelter Door And Bed Materials")]
    public static void GatherStorageShelterMaterials()
    {
        GatherDoorMaterials();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-open-natural-dawn-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageShelterGather",true);
    }

    [MenuItem("Nyangbingo/QA/Gather Replacement Door Materials After Actual Invasion")]
    public static void GatherInvasionReplacementDoor()
    {
        GatherDoorMaterials();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-invasion-recool-3\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.InvasionDoorGather",true);
    }

    [MenuItem("Nyangbingo/QA/Gather Replacement Door Materials After Floor Repair")]
    public static void GatherReplacementAfterFloorRepair()
    {
        GatherInvasionReplacementDoor();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-invasion-floor-repair-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.AfterStorageFloor",true);
    }

    [MenuItem("Nyangbingo/QA/Ascend To Observed Replacement Tree And Gather")]
    public static void AscendReplacementTree()
    {
        GatherReplacementAfterFloorRepair();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repair-gather-failed-2\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ReplacementTreeAscent",true);
    }

    [MenuItem("Nyangbingo/QA/Repair Actual Storage Invasion Floor Normally")]
    public static void RepairStorageInvasionFloor()
    {
        StartRun(true,resume:true,wallCraft:true);
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-invasion-recool-3\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageFloorRepair",true);
    }

    [MenuItem("Nyangbingo/QA/Gather Door Ice from Earned Wood and Hemp")]
    public static void GatherDoorIce() => StartRun(true, resume: true, coreGather: true, doorIce: true);

    [MenuItem("Nyangbingo/QA/Gather Storage Shelter Door Ice")]
    public static void GatherStorageDoorIce()
    {
        GatherDoorIce();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-shelter-bed-use-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageDoorIce",true);
    }

    [MenuItem("Nyangbingo/QA/Gather Missing Ice For Actual Invasion Door Repair")]
    public static void GatherReplacementDoorIce()
    {
        GatherDoorIce();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repair-tree-ascent-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorIce",true);
    }

    [MenuItem("Nyangbingo/QA/Resume Replacement Ice With Visible Tile Aim")]
    public static void ResumeReplacementDoorIce()
    {
        GatherReplacementDoorIce();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repair-ice-failed-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ReplacementIceResume",true);
    }

    [MenuItem("Nyangbingo/QA/Return Storage Ice And Craft Door")]
    public static void CraftStorageShelterDoor()
    {
        CraftDoor();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-shelter-door-ice-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageDoorCraft",true);
    }

    [MenuItem("Nyangbingo/QA/Return And Craft Replacement Invasion Door")]
    public static void CraftReplacementDoor()
    {
        CraftDoor();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repair-ice-gather-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorCraft",true);
    }

    [MenuItem("Nyangbingo/QA/Finish Partial Replacement Door Return")]
    public static void FinishReplacementDoorReturn()
    {
        CraftReplacementDoor();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repair-door-return-partial-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorResume",true);
    }

    [MenuItem("Nyangbingo/QA/Return and Craft Door from Earned Materials")]
    public static void CraftDoor() => StartRun(true, resume: true, wallCraft: true, doorCraft: true);

    [MenuItem("Nyangbingo/QA/Return And Craft Storage From Earned Materials")]
    public static void CraftEarnedStorage()
    {
        CraftDoor();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\door-ice-gather-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageCraft",true);
    }

    [MenuItem("Nyangbingo/QA/Use Earned Storage Through Natural Dawn")]
    public static void UseEarnedStorage()
    {
        CraftEarnedStorage();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-earned-craft-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageCraft",false);
        SessionState.SetBool("Nyangbingo.QA.StorageUse",true);
    }

    [MenuItem("Nyangbingo/QA/Restore Earned Storage Ice And UI")]
    public static void RestoreEarnedStorage()
    {
        UseEarnedStorage();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-open-natural-dawn-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageUse",false);
        SessionState.SetBool("Nyangbingo.QA.StorageRestore",true);
    }

    [MenuItem("Nyangbingo/QA/Install Earned Door and Toggle")]
    public static void InstallDoor() => StartRun(true, resume: true, wallCraft: true, doorInstall: true);

    [MenuItem("Nyangbingo/QA/Install Storage Shelter Door")]
    public static void InstallStorageShelterDoor()
    {
        InstallDoor();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-shelter-door-craft-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageDoorInstall",true);
    }

    [MenuItem("Nyangbingo/QA/Install Replacement Invasion Door And Inspect Seal")]
    public static void InstallReplacementDoor()
    {
        InstallStorageShelterDoor();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repair-door-craft-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorInstall",true);
    }

    [MenuItem("Nyangbingo/QA/Build Earned Door Shelter Boundary")]
    public static void BuildDoorShelter() => StartRun(true, resume: true, wallCraft: true, enclosure: true);

    [MenuItem("Nyangbingo/QA/Enclose Earned Storage Shelter")]
    public static void EncloseStorageShelter()
    {
        BuildDoorShelter();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-shelter-door-install-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageEnclose",true);
    }

    [MenuItem("Nyangbingo/QA/Restore Earned Shelter Door States")]
    public static void RestoreShelterDoor() => StartRun(true, resume: true, wallCraft: true, restoreShelter: true);

    [MenuItem("Nyangbingo/QA/Return and Craft Earned Satba")]
    public static void CraftSatba() => StartRun(true, resume: true, wallCraft: true, satbaCraft: true);

    [MenuItem("Nyangbingo/QA/Return And Craft Bed From Earned Materials")]
    public static void CraftBed() => StartRun(true, resume: true, wallCraft: true, satbaCraft: true, bedCraft: true);

    [MenuItem("Nyangbingo/QA/Return Storage Shelter Materials And Craft Bed")]
    public static void CraftStorageShelterBed()
    {
        CraftBed();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-shelter-materials-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageBedCraft",true);
    }

    [MenuItem("Nyangbingo/QA/Install Earned Bed And Inspect Rest")]
    public static void InstallBed() => StartRun(true, resume: true, wallCraft: true, bedUse: true);

    [MenuItem("Nyangbingo/QA/Use Earned Bed In Warm Position")]
    public static void UseWarmBed()
    {
        InstallBed();
        SessionState.SetBool("Nyangbingo.QA.BedWarm", true);
    }

    [MenuItem("Nyangbingo/QA/Install Storage Shelter Bed And Rest")]
    public static void UseStorageShelterBed()
    {
        UseWarmBed();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-shelter-bed-craft-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageBedUse",true);
    }

    [MenuItem("Nyangbingo/QA/Verify Sealed Storage Across Normal Bed Rest")]
    public static void RestSealedStorage()
    {
        UseWarmBed();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-shelter-enclosed-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StoragePreserve",true);
    }

    [MenuItem("Nyangbingo/QA/Verify Repaired Storage Across Next Normal Dawn")]
    public static void RestRepairedStorage()
    {
        RestSealedStorage();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-repair-door-install-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageRecovered",true);
    }

    [MenuItem("Nyangbingo/QA/Observe Storage Shelter First Invasion Naturally")]
    public static void ObserveStorageInvasion()
    {
        UseWarmBed();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\shelter-safety-guide-fixed-3\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageInvasion",true);
        SessionState.SetBool("Nyangbingo.QA.InvasionOvernight",true);
    }

    [MenuItem("Nyangbingo/QA/Recool Actual Storage Invasion Damage And Continue")]
    public static void RecoolStorageInvasion()
    {
        UseWarmBed();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\storage-first-invasion-natural-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.StorageRecool",true);
    }

    [MenuItem("Nyangbingo/QA/Rest With Active Smelting And Restore Autosave")]
    public static void RestAutosave()
    {
        UseWarmBed();
        SessionState.SetBool("Nyangbingo.QA.BedAutoOnly", true);
    }

    [MenuItem("Nyangbingo/QA/Reach First Invasion By Ordinary Bed Rest")]
    public static void ReachFirstInvasion()
    {
        UseWarmBed();
        SessionState.SetBool("Nyangbingo.QA.BedInvasion", true);
    }

    [MenuItem("Nyangbingo/QA/Observe Full First Invasion Night Without Time Acceleration")]
    public static void ObserveFirstInvasionNight()
    {
        ReachFirstInvasion();
        SessionState.SetBool("Nyangbingo.QA.InvasionOvernight", true);
    }

    [MenuItem("Nyangbingo/QA/Continue Actual First Invasion Damage Autosave")]
    public static void ContinueInvasionDamage() => StartRun(true, resume: true, wallCraft: true, invasionRestore: true);

    [MenuItem("Nyangbingo/QA/Repair Actual First Invasion Floor Damage")]
    public static void RepairInvasionDamage()
    {
        ContinueInvasionDamage();
        SessionState.SetBool("Nyangbingo.QA.InvasionRepair", true);
    }

    [MenuItem("Nyangbingo/QA/Use Earned Satba Confirm Cancel and Summon")]
    public static void UseSatba() => StartRun(true, resume: true, wallCraft: true, satbaUse: true);

    [MenuItem("Nyangbingo/QA/Approach and Fight Earned Satba Boss")]
    public static void ApproachSatbaBoss()
    {
        UseSatba();
        SessionState.SetBool("Nyangbingo.QA.BossApproach", true);
    }

    [MenuItem("Nyangbingo/QA/Reach Surface Before Earned Satba Fight")]
    public static void SurfaceSatbaBoss()
    {
        ApproachSatbaBoss();
        SessionState.SetBool("Nyangbingo.QA.BossSurface", true);
    }

    [MenuItem("Nyangbingo/QA/Observe Earned Boss Death Recovery")]
    public static void BossDeathRecovery()
    {
        SurfaceSatbaBoss();
        SessionState.SetBool("Nyangbingo.QA.BossDeathRecovery", true);
    }

    [MenuItem("Nyangbingo/QA/Verify Normal and Boss Pause Save Hint")]
    public static void BossSaveHint()
    {
        UseSatba();
        SessionState.SetBool("Nyangbingo.QA.BossSaveHint", true);
    }

    [MenuItem("Nyangbingo/QA/Wait Natural Boss Dawn and Save")]
    public static void BossNaturalDawn()
    {
        BossSaveHint();
        SessionState.SetBool("Nyangbingo.QA.BossDawn", true);
    }

    [MenuItem("Nyangbingo/QA/Restore Natural Dawn Earned Save")]
    public static void RestoreNaturalDawn() => StartRun(true, resume: true, dawnRestore: true);

    [MenuItem("Nyangbingo/QA/Collect And Restore Earned King Rewards")]
    public static void CollectKingRewards() => StartRun(true, resume: true, kingRewards: true);

    [MenuItem("Nyangbingo/QA/Gather T3 Materials After Earned King Victory")]
    public static void GatherT3AfterKing() => StartRun(true, resume: true, deep: true, t3Gather: true);

    [MenuItem("Nyangbingo/QA/Craft T3 From Earned Post King Materials")]
    public static void CraftT3AfterKing() => StartRun(true, resume: true, t3Craft: true);

    [MenuItem("Nyangbingo/QA/Craft T3 From Actual Day30 Dawn Materials")]
    public static void CraftT3Day30() => StartRun(true, resume: true, t3Craft: true, day30T3: true);

    [MenuItem("Nyangbingo/QA/Fight Natural Night With Earned T3")]
    public static void FightEarnedT3() => StartRun(true, resume: true, combat: true, t3Combat: true);

    [MenuItem("Nyangbingo/QA/Repeat Earned T3 Natural Night Combat")]
    public static void RepeatEarnedT3Combat() => StartRun(true, resume: true, combat: true, combatNight: true, t3Combat: true);

    [MenuItem("Nyangbingo/QA/Reach Day15 Baekjung With Earned Bed")]
    public static void ReachBaekjungWithEarnedBed()
    {
        StartRun(true, resume: true, invasionRestore: true);
        SessionState.SetBool("Nyangbingo.QA.BaekjungReach", true);
    }

    [MenuItem("Nyangbingo/QA/Observe Natural Baekjung Waves Dawn And Continue")]
    public static void ObserveNaturalBaekjung()
    {
        ReachBaekjungWithEarnedBed();
        SessionState.SetBool("Nyangbingo.QA.BaekjungOvernight", true);
    }

    [MenuItem("Nyangbingo/QA/Continue Earned Baekjung Dawn")]
    public static void ContinueEarnedBaekjungDawn()
    {
        StartRun(true, resume: true, invasionRestore: true);
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day15-natural-waves-dawn-1\baekjung-natural-dawn-autosave.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.BaekjungRestore", true);
        SessionState.SetBool("Nyangbingo.QA.InvasionRestore", false);
    }

    [MenuItem("Nyangbingo/QA/Reach Day16 Invasion From Natural Dawn")]
    public static void ReachSecondInvasion()
    {
        ContinueEarnedBaekjungDawn();
        SessionState.SetBool("Nyangbingo.QA.Day16Invasion", true);
    }

    [MenuItem("Nyangbingo/QA/Observe Saved Day16 Invasion Until Natural Dawn")]
    public static void ObserveSecondInvasion()
    {
        ContinueEarnedBaekjungDawn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day16-invasion-entry-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.SecondInvasionNight", true);
        SessionState.SetBool("Nyangbingo.QA.BaekjungRestore", false);
    }

    [MenuItem("Nyangbingo/QA/Observe Saved Day26 Invasion Until Natural Dawn")]
    public static void ObserveDay26Invasion()
    {
        ObserveSecondInvasion();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day26-rest-route-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetInt("Nyangbingo.QA.ObservedInvasionDay", 26);
    }

    [MenuItem("Nyangbingo/QA/Reach Day18 Residents From Earned Day17")]
    public static void ReachDay18Residents()
    {
        ContinueEarnedBaekjungDawn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day16-natural-night-dawn-1\day17-natural-dawn-autosave.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.Day18Residents", true);
        SessionState.SetBool("Nyangbingo.QA.BaekjungRestore", false);
    }

    [MenuItem("Nyangbingo/QA/Approach Day18 Gangcheori Normally")]
    public static void ApproachDay18Gangcheori()
    {
        ContinueEarnedBaekjungDawn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day18-resident-entry-1\bed-dawn-autosave.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriApproach", true);
        SessionState.SetBool("Nyangbingo.QA.BaekjungRestore", false);
    }

    [MenuItem("Nyangbingo/QA/Approach Day18 Gangcheori With Earned Healing")]
    public static void ApproachDay18GangcheoriPrepared()
    {
        ApproachDay18Gangcheori();
        SessionState.SetBool("Nyangbingo.QA.GangcheoriPrepared", true);
    }

    [MenuItem("Nyangbingo/QA/Inspect Nearby Gangcheori Save Continue")]
    public static void InspectNearbyGangcheoriContinue()
    {
        ContinueNearGangcheoriCheckpoint();
    }

    [MenuItem("Nyangbingo/QA/Approach And Fight Gangcheori Without Reload")]
    public static void ApproachAndFightGangcheori()
    {
        ApproachDay18GangcheoriPrepared();
        SessionState.SetBool("Nyangbingo.QA.GangcheoriFight", true);
    }

    private static void ContinueNearGangcheoriCheckpoint()
    {
        ContinueEarnedBaekjungDawn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day18-gangcheori-approach-healing-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriNearRestore", true);
        SessionState.SetBool("Nyangbingo.QA.BaekjungRestore", false);
    }

    [MenuItem("Nyangbingo/QA/Continue Gangcheori Victory And Collect Remaining Tear")]
    public static void ContinueGangcheoriVictory()
    {
        ContinueEarnedBaekjungDawn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day18-gangcheori-continuous-combat-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriVictoryRestore", true);
        SessionState.SetBool("Nyangbingo.QA.BaekjungRestore", false);
    }

    [MenuItem("Nyangbingo/QA/Inspect Restored Codex Layout And Scroll")]
    public static void InspectRestoredCodexLayout()
    {
        ContinueGangcheoriVictory();
        SessionState.SetBool("Nyangbingo.QA.CodexLayout", true);
    }

    [MenuItem("Nyangbingo/QA/Return Gangcheori Loot To Workbench Normally")]
    public static void ReturnGangcheoriLoot()
    {
        StartGangcheoriLootReturn();
    }

    [MenuItem("Nyangbingo/QA/Finish Earned Gangcheori Return Checkpoint")]
    public static void FinishGangcheoriReturn()
    {
        StartGangcheoriLootReturn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day18-loot-return-partial-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
    }

    [MenuItem("Nyangbingo/QA/Inspect Earned Return Reward Crafting")]
    public static void InspectEarnedReturnRewardCrafting()
    {
        StartGangcheoriLootReturn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day18-loot-return-finish-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.ReturnRecipeAudit", true);
    }

    [MenuItem("Nyangbingo/QA/Reach Day23 Night From Earned Gangcheori Return")]
    public static void ReachDay23FromReturn()
    {
        InspectEarnedReturnRewardCrafting();
        SessionState.SetBool("Nyangbingo.QA.ReturnRecipeAudit", false);
        SessionState.SetBool("Nyangbingo.QA.Day23Route", true);
    }

    [MenuItem("Nyangbingo/QA/Reach Day26 Invasion From Earned Day23")]
    public static void ReachDay26FromDay23()
    {
        ReachDay23FromReturn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day23-rest-route-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetInt("Nyangbingo.QA.LateRouteTarget", 26);
    }

    [MenuItem("Nyangbingo/QA/Reach Day27 Theme Night From Natural Dawn")]
    public static void ReachDay27FromDawn()
    {
        ReachDay23FromReturn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day26-natural-night-dawn-1\day27-natural-dawn-autosave.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetInt("Nyangbingo.QA.LateRouteTarget", 27);
    }

    [MenuItem("Nyangbingo/QA/Reach Day30 Dawn From Earned Day27 Night")]
    public static void ReachDay30Dawn()
    {
        ReachDay23FromReturn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day27-theme-entry-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetInt("Nyangbingo.QA.LateRouteTarget", 30);
    }

    [MenuItem("Nyangbingo/QA/Inspect Normal Day30 Imugi Night")]
    public static void InspectDay30ImugiNight()
    {
        ReachDay30Dawn();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day30-dawn-route-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.ImugiNight", true);
    }

    [MenuItem("Nyangbingo/QA/Fight Normal Day30 Imugi With Earned Healing")]
    public static void FightDay30Imugi()
    {
        InspectDay30ImugiNight();
        SessionState.SetBool("Nyangbingo.QA.ImugiFight", true);
    }

    [MenuItem("Nyangbingo/QA/Attempt Full Normal Day30 Imugi Fight")]
    public static void AttemptFullDay30Imugi()
    {
        FightDay30Imugi();
        SessionState.SetBool("Nyangbingo.QA.ImugiFullFight", true);
    }

    [MenuItem("Nyangbingo/QA/Fight Imugi With T3 Crafted On Actual Day30 Route")]
    public static void FightDay30ImugiT3()
    {
        AttemptFullDay30Imugi();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day30-t3-craft-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.ImugiEarnedT3", true);
    }

    [MenuItem("Nyangbingo/QA/Collect Earned Imugi Rewards And Continue")]
    public static void CollectEarnedImugiRewards()
    {
        ContinueGangcheoriVictory();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day30-imugi-result-save-fixed-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriVictoryRestore", false);
        SessionState.SetBool("Nyangbingo.QA.ImugiLoot", true);
    }

    [MenuItem("Nyangbingo/QA/Inspect Earned Imugi Reward Equipment")]
    public static void InspectEarnedImugiRewardEquipment()
    {
        CollectEarnedImugiRewards();
        SessionState.SetBool("Nyangbingo.QA.ImugiRewardEquipment", true);
    }

    [MenuItem("Nyangbingo/QA/Compare Earned Yeouiju And T3 Combat")]
    public static void CompareEarnedRewardCombat()
    {
        CollectEarnedImugiRewards();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\imugi-equipment-hint-fixed-3\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.ImugiLoot", false);
        SessionState.SetBool("Nyangbingo.QA.RewardCombat", true);
    }

    [MenuItem("Nyangbingo/QA/Return Imugi Rewards To Earned Workshop")]
    public static void ReturnImugiRewardsToWorkshop()
    {
        CompareEarnedRewardCombat();
        SessionState.SetBool("Nyangbingo.QA.RewardCombat", false);
        SessionState.SetBool("Nyangbingo.QA.CoolerReturn", true);
    }

    [MenuItem("Nyangbingo/QA/Recover Missing Cooler Ore And Return")]
    public static void RecoverMissingCoolerOre()
    {
        ReturnImugiRewardsToWorkshop();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\imugi-workshop-return-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.CoolerOre",true);
    }

    [MenuItem("Nyangbingo/QA/Craft Cooler From Earned Imugi And Recovered Ore")]
    public static void CraftEarnedCooler()
    {
        ReturnImugiRewardsToWorkshop();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\cooler-ore-return-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.CoolerReturn",false);
        SessionState.SetBool("Nyangbingo.QA.CoolerCraft",true);
    }

    [MenuItem("Nyangbingo/QA/Install Normally Crafted Cooler")]
    public static void InstallEarnedCooler()
    {
        CraftEarnedCooler();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\cooler-earned-craft-1\nyangbingo-save-0.json";
        File.Copy(source,Path.Combine(SessionState.GetString(SaveKey,""),"nyangbingo-save-0.json"),true);
        SessionState.SetBool("Nyangbingo.QA.CoolerCraft",false);
        SessionState.SetBool("Nyangbingo.QA.CoolerInstall",true);
    }

    private static void StartGangcheoriLootReturn()
    {
        ContinueGangcheoriVictory();
        var source = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day18-victory-restore-1\nyangbingo-save-0.json";
        File.Copy(source, Path.Combine(SessionState.GetString(SaveKey, ""), "nyangbingo-save-0.json"), true);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriVictoryRestore", false);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriReturn", true);
    }

    [MenuItem("Nyangbingo/QA/Observe Natural Dawn Autosave And Continue")]
    public static void NaturalDawnAutosave()
    {
        CollectKingRewards();
        SessionState.SetBool("Nyangbingo.QA.NaturalAutosave", true);
    }

    [MenuItem("Nyangbingo/QA/Preserve Earned Satba Until Next Night")]
    public static void PrepareNextNight()
    {
        UseSatba();
        SessionState.SetBool("Nyangbingo.QA.NextNight", true);
    }

    [MenuItem("Nyangbingo/QA/Fight Earned Boss From Fresh Night")]
    public static void FightFreshNight()
    {
        StartRun(true, resume: true, wallCraft: true, satbaUse: true, freshNight: true);
        SessionState.SetBool("Nyangbingo.QA.BossApproach", true);
        SessionState.SetBool("Nyangbingo.QA.BossSurface", true);
        SessionState.SetBool("Nyangbingo.QA.BossColliderAim", true);
        SessionState.SetBool("Nyangbingo.QA.BossOpeningRetreat", true);
    }

    [MenuItem("Nyangbingo/QA/Observe Earned Boss Collider Aim")]
    public static void BossColliderAim()
    {
        SurfaceSatbaBoss();
        SessionState.SetBool("Nyangbingo.QA.BossColliderAim", true);
    }

    [MenuItem("Nyangbingo/QA/Retreat Through Earned Boss Opening")]
    public static void BossOpeningRetreat()
    {
        BossColliderAim();
        SessionState.SetBool("Nyangbingo.QA.BossOpeningRetreat", true);
    }

    [MenuItem("Nyangbingo/QA/Verify Earned Boss Opening Hint")]
    public static void BossOpeningHint()
    {
        UseSatba();
        SessionState.SetBool("Nyangbingo.QA.BossOpeningHint", true);
    }

    private static void StartRun(bool workbench, bool furnace = false, bool resume = false, bool ascent = false, bool bridge = false, bool smelt = false, bool tool = false, bool deep = false, bool resumeDeep = false, bool foundrySmelt = false, bool anvil = false, bool installAnvil = false, bool combat = false, bool combatNight = false, bool harvest = false, bool coreGather = false, bool coreCraft = false, bool installCore = false, bool wallCraft = false, bool doorGather = false, bool doorIce = false, bool doorCraft = false, bool doorInstall = false, bool enclosure = false, bool restoreShelter = false, bool satbaCraft = false, bool satbaUse = false, bool dawnRestore = false, bool freshNight = false, bool kingRewards = false, bool bedCraft = false, bool bedUse = false, bool invasionRestore = false, bool t3Gather = false, bool t3Craft = false, bool t3Combat = false, bool day30T3 = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop play mode before starting isolated QA.");
        SessionState.SetBool("Nyangbingo.QA.WallRepair", enclosure);
        SessionState.SetBool("Nyangbingo.QA.Enclosure", enclosure);
        SessionState.SetBool("Nyangbingo.QA.RestoreShelter", restoreShelter);
        SessionState.SetBool("Nyangbingo.QA.SatbaCraft", satbaCraft);
        SessionState.SetBool("Nyangbingo.QA.SatbaUse", satbaUse);
        SessionState.SetBool("Nyangbingo.QA.BossApproach", false);
        SessionState.SetBool("Nyangbingo.QA.BossSurface", false);
        SessionState.SetBool("Nyangbingo.QA.BossDeathRecovery", false);
        SessionState.SetBool("Nyangbingo.QA.BossSaveHint", false);
        SessionState.SetBool("Nyangbingo.QA.BossDawn", false);
        SessionState.SetBool("Nyangbingo.QA.DawnRestore", dawnRestore);
        SessionState.SetBool("Nyangbingo.QA.BossColliderAim", false);
        SessionState.SetBool("Nyangbingo.QA.BossOpeningRetreat", false);
        SessionState.SetBool("Nyangbingo.QA.BossOpeningHint", false);
        SessionState.SetBool("Nyangbingo.QA.NextNight", false);
        SessionState.SetBool("Nyangbingo.QA.FreshNight", freshNight);
        SessionState.SetBool("Nyangbingo.QA.KingRewards", kingRewards);
        SessionState.SetBool("Nyangbingo.QA.NaturalAutosave", false);
        SessionState.SetBool("Nyangbingo.QA.BedCraft", bedCraft);
        SessionState.SetBool("Nyangbingo.QA.BedUse", bedUse);
        SessionState.SetBool("Nyangbingo.QA.BedWarm", false);
        SessionState.SetBool("Nyangbingo.QA.BedAutoOnly", false);
        SessionState.SetBool("Nyangbingo.QA.BedInvasion", false);
        SessionState.SetBool("Nyangbingo.QA.InvasionOvernight", false);
        SessionState.SetBool("Nyangbingo.QA.InvasionRestore", invasionRestore);
        SessionState.SetBool("Nyangbingo.QA.InvasionRepair", false);
        SessionState.SetBool("Nyangbingo.QA.T3Gather", t3Gather);
        SessionState.SetBool("Nyangbingo.QA.T3Craft", t3Craft);
        SessionState.SetBool("Nyangbingo.QA.Day30T3", day30T3);
        SessionState.SetBool("Nyangbingo.QA.T3Combat", t3Combat);
        SessionState.SetBool("Nyangbingo.QA.BaekjungReach", false);
        SessionState.SetBool("Nyangbingo.QA.BaekjungOvernight", false);
        SessionState.SetBool("Nyangbingo.QA.BaekjungRestore", false);
        SessionState.SetBool("Nyangbingo.QA.Day16Invasion", false);
        SessionState.SetBool("Nyangbingo.QA.SecondInvasionNight", false);
        SessionState.SetInt("Nyangbingo.QA.ObservedInvasionDay", 16);
        SessionState.SetBool("Nyangbingo.QA.Day18Residents", false);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriApproach", false);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriPrepared", false);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriFight", false);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriNearRestore", false);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriVictoryRestore", false);
        SessionState.SetBool("Nyangbingo.QA.CodexLayout", false);
        SessionState.SetBool("Nyangbingo.QA.GangcheoriReturn", false);
        SessionState.SetBool("Nyangbingo.QA.ReturnRecipeAudit", false);
        SessionState.SetBool("Nyangbingo.QA.Day23Route", false);
        SessionState.SetInt("Nyangbingo.QA.LateRouteTarget", 23);
        SessionState.SetBool("Nyangbingo.QA.ImugiNight", false);
        SessionState.SetBool("Nyangbingo.QA.ImugiFight", false);
        SessionState.SetBool("Nyangbingo.QA.ImugiFullFight", false);
        SessionState.SetBool("Nyangbingo.QA.ImugiEarnedT3", false);
        SessionState.SetBool("Nyangbingo.QA.ImugiLoot", false);
        SessionState.SetBool("Nyangbingo.QA.ImugiRewardEquipment", false);
        SessionState.SetBool("Nyangbingo.QA.RewardCombat", false);
        SessionState.SetBool("Nyangbingo.QA.CoolerReturn", false);
        SessionState.SetBool("Nyangbingo.QA.CoolerOre", false);
        SessionState.SetBool("Nyangbingo.QA.CoolerCraft", false);
        SessionState.SetBool("Nyangbingo.QA.CoolerInstall", false);
        SessionState.SetBool("Nyangbingo.QA.StorageCraft", false);
        SessionState.SetBool("Nyangbingo.QA.StorageUse", false);
        SessionState.SetBool("Nyangbingo.QA.StorageRestore", false);
        SessionState.SetBool("Nyangbingo.QA.StorageShelterGather", false);
        SessionState.SetBool("Nyangbingo.QA.ShelterGateGather", false);
        SessionState.SetBool("Nyangbingo.QA.ShelterGateResume", false);
        SessionState.SetBool("Nyangbingo.QA.GateStorageInspect", false);
        SessionState.SetBool("Nyangbingo.QA.InvasionDoorGather", false);
        SessionState.SetBool("Nyangbingo.QA.StorageFloorRepair", false);
        SessionState.SetBool("Nyangbingo.QA.AfterStorageFloor", false);
        SessionState.SetBool("Nyangbingo.QA.ReplacementTreeAscent", false);
        SessionState.SetBool("Nyangbingo.QA.StorageBedCraft", false);
        SessionState.SetBool("Nyangbingo.QA.StorageBedUse", false);
        SessionState.SetBool("Nyangbingo.QA.StorageDoorIce", false);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorIce", false);
        SessionState.SetBool("Nyangbingo.QA.ReplacementIceResume", false);
        SessionState.SetBool("Nyangbingo.QA.StorageDoorCraft", false);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorCraft", false);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorResume", false);
        SessionState.SetBool("Nyangbingo.QA.StorageDoorInstall", false);
        SessionState.SetBool("Nyangbingo.QA.ReplacementDoorInstall", false);
        SessionState.SetBool("Nyangbingo.QA.StorageEnclose", false);
        SessionState.SetBool("Nyangbingo.QA.StoragePreserve", false);
        SessionState.SetBool("Nyangbingo.QA.StorageRecovered", false);
        SessionState.SetBool("Nyangbingo.QA.StorageInvasion", false);
        SessionState.SetBool("Nyangbingo.QA.StorageRecool", false);
        SessionState.SetBool("Nyangbingo.QA.WallChain", false);
        for (var i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes before QA; no scenes were changed.");
        if (SceneManager.sceneCount != 1 || string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            throw new InvalidOperationException("QA requires one saved scene so it can restore the workspace.");
        // This checkpoint was earned by normal input in furnace-access-2, not generated with items.
        var earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-8c73f89b9d9644b0853f8263fa21e391", "nyangbingo-save-0.json");
        if (ascent) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-09ecdcc548074f05823fba3fdcb96f86", "nyangbingo-save-0.json");
        if (bridge) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-bcded799ba134144be99558059708d73", "nyangbingo-save-0.json");
        if (smelt) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-143b6c81f34448c4b08ebcafc5a2ac03", "nyangbingo-save-0.json");
        if (tool) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-61fe852dcb7148649076d934f60e7247", "nyangbingo-save-0.json");
        if (deep) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-942fa3546ec640db89b15c4ab2739244", "nyangbingo-save-0.json");
        if (resumeDeep) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-c2f29f6926614b9bb34dc2dc6418cb1f", "nyangbingo-save-0.json");
        if (foundrySmelt) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-075ce162ca654687b33f3ed56470b38e", "nyangbingo-save-0.json");
        if (anvil) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-eb28863d390d483dbeb2289cf6c02a0a", "nyangbingo-save-0.json");
        if (installAnvil) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-5a39e0f4e1384dd1854475789ff74a93", "nyangbingo-save-0.json");
        if (combat) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-15b2f0d2954046609491d91339cd4775", "nyangbingo-save-0.json");
        if (combatNight) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-273fe81c59104c81bf7d6f2e68cd33d1", "natural-night-checkpoint.json");
        if (harvest) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-ed47d3b6406649cfa9ce1e69fc442de9", "nyangbingo-save-0.json");
        if (coreGather) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-15b2f0d2954046609491d91339cd4775", "nyangbingo-save-0.json");
        if (coreCraft) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-5f66c00d7ae64388b12171eef72560d8", "nyangbingo-save-0.json");
        if (installCore) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-8743c3a5b3584c419ff593f48bef1c1c", "nyangbingo-save-0.json");
        if (wallCraft) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-12fe1dfb246a42059400dc526ffa9a58", "nyangbingo-save-0.json");
        if (doorGather) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-ee382cab2bb34854bf5e6f69f39a3b3b", "nyangbingo-save-0.json");
        if (doorIce) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-d21c4b691e744a5d8ee9c35d62b79927", "nyangbingo-save-0.json");
        if (doorCraft) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-0f5e977716474e1eaa74acd6f8b24b0a", "nyangbingo-save-0.json");
        if (doorInstall) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-661b983067e047ffa9f5957504424304", "nyangbingo-save-0.json");
        if (enclosure) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-25e403c420fe47b9b647e3a406c43b16", "nyangbingo-save-0.json");
        if (restoreShelter) earnedSave = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-6f4fbe153e1c43fb8b8078287b5d8df3", "nyangbingo-save-0.json");
        if (satbaCraft) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\satba-gather-fixed-3\nyangbingo-save-0.json";
        if (satbaUse) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\first-satba-craft-1\nyangbingo-save-0.json";
        if (dawnRestore) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\boss-natural-dawn-save-1\nyangbingo-save-0.json";
        if (freshNight) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\next-night-preparation-1\nyangbingo-save-0.json";
        if (kingRewards) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\fresh-night-king-victory-1\nyangbingo-save-0.json";
        if (bedCraft) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\door-material-gather-1\nyangbingo-save-0.json";
        if (bedUse) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\first-bed-craft-1\nyangbingo-save-0.json";
        if (invasionRestore) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\first-invasion-full-night-1\first-invasion-dawn-autosave.json";
        if (t3Gather) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\king-reward-recovery-1\nyangbingo-save-0.json";
        if (t3Craft) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\post-king-t3-gather-return-1\nyangbingo-save-0.json";
        if (t3Combat) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\post-king-t3-craft-1\nyangbingo-save-0.json";
        if (t3Combat && combatNight) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\post-king-t3-combat-1\natural-night-checkpoint.json";
        if (day30T3) earnedSave = @"C:\Users\ba960\Documents\Codex\2026-09-28\d-learnuinty-seokbinggo-100days\outputs\qa-repeat\day30-dawn-route-1\nyangbingo-save-0.json";
        if (resume && !File.Exists(earnedSave))
            throw new InvalidOperationException("Earned QA checkpoint is missing; run from New Game instead.");
        var directory = Path.Combine(Path.GetTempPath(), "NyangbingoInputQA-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (resume) File.Copy(earnedSave, Path.Combine(directory, "nyangbingo-save-0.json"));
        SessionState.SetString(OriginalScene, SceneManager.GetActiveScene().path);
        SessionState.SetString(SaveKey, directory);
        SessionState.SetBool("Nyangbingo.QA.Workbench", workbench);
        SessionState.SetBool("Nyangbingo.QA.Furnace", furnace);
        SessionState.SetBool("Nyangbingo.QA.Resume", resume);
        SessionState.SetBool("Nyangbingo.QA.Ascent", ascent);
        SessionState.SetBool("Nyangbingo.QA.Bridge", bridge);
        SessionState.SetBool("Nyangbingo.QA.Smelt", smelt);
        SessionState.SetBool("Nyangbingo.QA.Tool", tool);
        SessionState.SetBool("Nyangbingo.QA.Deep", deep);
        SessionState.SetBool("Nyangbingo.QA.FoundrySmelt", foundrySmelt);
        SessionState.SetBool("Nyangbingo.QA.Anvil", anvil);
        SessionState.SetBool("Nyangbingo.QA.InstallAnvil", installAnvil);
        SessionState.SetBool("Nyangbingo.QA.Combat", combat);
        SessionState.SetBool("Nyangbingo.QA.Harvest", harvest);
        SessionState.SetBool("Nyangbingo.QA.DoorGather", doorGather);
        SessionState.SetBool("Nyangbingo.QA.DoorIce", doorIce);
        SessionState.SetBool("Nyangbingo.QA.DoorCraft", doorCraft);
        SessionState.SetBool("Nyangbingo.QA.DoorInstall", doorInstall);
        SessionState.SetBool("Nyangbingo.QA.CoreGather", coreGather);
        SessionState.SetBool("Nyangbingo.QA.CoreCraft", coreCraft);
        SessionState.SetBool("Nyangbingo.QA.InstallCore", installCore);
        SessionState.SetBool("Nyangbingo.QA.WallCraft", wallCraft);
        SessionState.SetString("Nyangbingo.QA.CombatSource", combatNight ? "natural-combat-first-loot-1 natural-night-checkpoint" : "ice-anvil-install-visual-4 daytime checkpoint");
        if (t3Combat) SessionState.SetString("Nyangbingo.QA.CombatSource", "post-king-t3-craft-1 normally crafted T3 daytime checkpoint");
        if (t3Combat && combatNight) SessionState.SetString("Nyangbingo.QA.CombatSource", "post-king-t3-combat-1 naturally reached night checkpoint");
        SessionState.SetString("Nyangbingo.QA.DeepSource", anvil ? "first-icesteel-3 earned checkpoint" : resumeDeep ? "deep-mining-1 partial earned checkpoint" : "first-iron-claw-1 earned checkpoint");
        SessionState.SetBool(Armed, true);
        File.WriteAllText(Path.Combine(directory, "result.json"), "{\"status\":\"starting\"}");
        Debug.Log("[InputQA] Isolated output: " + directory);
        EditorSceneManager.OpenScene("Assets/Scenes/ReleaseScenes/Title.unity");
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Armed, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var host = new GameObject("Editor-only input QA");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<NyangbingoInputReplayDriver>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            GameplayInput.EndReplay();
            SessionState.SetBool(Armed, false);
            SessionState.EraseString(SaveKey);
            SessionState.EraseBool("Nyangbingo.QA.Workbench");
            SessionState.EraseBool("Nyangbingo.QA.Furnace");
            SessionState.EraseBool("Nyangbingo.QA.Resume");
            SessionState.EraseBool("Nyangbingo.QA.Ascent");
            SessionState.EraseBool("Nyangbingo.QA.Bridge");
            SessionState.EraseBool("Nyangbingo.QA.Smelt");
            SessionState.EraseBool("Nyangbingo.QA.Tool");
            SessionState.EraseBool("Nyangbingo.QA.Deep");
            SessionState.EraseBool("Nyangbingo.QA.FoundrySmelt");
            SessionState.EraseBool("Nyangbingo.QA.Anvil");
            SessionState.EraseBool("Nyangbingo.QA.InstallAnvil");
            SessionState.EraseBool("Nyangbingo.QA.Combat");
            SessionState.EraseBool("Nyangbingo.QA.Harvest");
            SessionState.EraseBool("Nyangbingo.QA.CoreGather");
            SessionState.EraseBool("Nyangbingo.QA.CoreCraft");
            SessionState.EraseBool("Nyangbingo.QA.InstallCore");
            SessionState.EraseBool("Nyangbingo.QA.WallCraft");
            SessionState.EraseString("Nyangbingo.QA.DeepSource");
            var original = SessionState.GetString(OriginalScene, "");
            SessionState.EraseString(OriginalScene);
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        }
    }
}

[DefaultExecutionOrder(-10000)]
public sealed class NyangbingoInputReplayDriver : MonoBehaviour
{
    [Serializable] private sealed class Result
    {
        public string status;
        public string scope = "Editor smoke: UI callback start/trait selection; replayed gameplay keys. Not a full or novice playthrough.";
        public float wallSeconds;
        public string[] checks;
        public string[] findings;
    }
    private readonly List<string> checks = new();
    private readonly List<string> findings = new();
    private int phase, pass;
    private int shortcutCheck;
    private int furnaceTargetIndex;
    private static readonly KeyCode[] PanelSmokeKeys =
        { KeyCode.Tab, KeyCode.Tab, KeyCode.I, KeyCode.I, KeyCode.G, KeyCode.G, KeyCode.J, KeyCode.J };
    private float started, phaseStarted;
    private Vector3 origin;
    private MainGamePlayerController player;
    private bool finished;
    private bool replayStarted;
    private bool previousBackground;
    private string directory;
    private TileService tiles;
    private Vector3Int mineCell;
    private int mined;
    private float miningStarted;
    private float pausedGameTime;
    private bool workbenchRun;
    private float controlStarted;
    private int gatheringCount;
    private int lastInventoryTotal;
    private int routeRecoveries;
    private int placementProbe;
    private Vector3 placementPoint;
    private Nyangbingo.Save.PlacedObjectRecord placedWorkbench;
    private int savedDirt, savedStone;
    private bool furnaceRun;
    private float furnaceStarted, furnaceLastProgress;
    private Vector2 furnaceRouteAim;
    private int furnaceInventorySignature = -1;
    private bool furnaceReturning;
    private bool resumeEarnedSave;
    private bool ascentRun;
    private bool bridgeRun, furnaceCrafted;
    private bool smeltRun, smeltPassed;
    private Nyangbingo.Save.PlacedObjectRecord placedFurnace;
    private int smeltOreBefore, smeltCoalBefore;
    private float smeltStarted;
    private int smeltNavigationAttempts;
    private bool toolRun, toolPassed;
    private bool deepRun, deepPassed;
    private bool coreGatherRun, coreGatherPassed;
    private bool DoorIceRun => SessionState.GetBool("Nyangbingo.QA.DoorIce", false);
    private int IceGatherGoal => DoorIceRun ? 4 : 20;
    private int PreservedIceCount => SessionState.GetBool("Nyangbingo.QA.StorageRecovered",false)?2:3;
    private float PreservedMeltRemainder => SessionState.GetBool("Nyangbingo.QA.StorageRecovered",false)?0f:.25f;
    private bool DoorCraftRun => SessionState.GetBool("Nyangbingo.QA.DoorCraft", false);
    private bool DoorInstallRun => SessionState.GetBool("Nyangbingo.QA.DoorInstall", false);
    private int doorToggleCycles;
    private bool EnclosureRun => SessionState.GetBool("Nyangbingo.QA.Enclosure", false);
    private bool RestoreShelterRun => SessionState.GetBool("Nyangbingo.QA.RestoreShelter", false);
    private bool SatbaCraftRun => SessionState.GetBool("Nyangbingo.QA.SatbaCraft", false);
    private bool BedCraftRun => SessionState.GetBool("Nyangbingo.QA.BedCraft", false);
    private bool BedUseRun => SessionState.GetBool("Nyangbingo.QA.BedUse", false);
    private bool bedSleepAllowed;
    private bool BedWarmRun => SessionState.GetBool("Nyangbingo.QA.BedWarm", false);
    private bool BedAutoOnlyRun => SessionState.GetBool("Nyangbingo.QA.BedAutoOnly", false);
    private bool BedInvasionRun => SessionState.GetBool("Nyangbingo.QA.BedInvasion", false);
    private bool InvasionOvernightRun => SessionState.GetBool("Nyangbingo.QA.InvasionOvernight", false);
    private bool InvasionRestoreRun => SessionState.GetBool("Nyangbingo.QA.InvasionRestore", false);
    private bool InvasionRepairRun => SessionState.GetBool("Nyangbingo.QA.InvasionRepair", false);
    private bool T3GatherRun => SessionState.GetBool("Nyangbingo.QA.T3Gather", false);
    private bool T3CraftRun => SessionState.GetBool("Nyangbingo.QA.T3Craft", false);
    private bool CoolerCraftRun => SessionState.GetBool("Nyangbingo.QA.CoolerCraft", false);
    private int coolerPlacementIndex;
    private bool CoolerInstallRun => SessionState.GetBool("Nyangbingo.QA.CoolerInstall", false);
    private string CoolerPlacementId => StorageUseRun ? "jangdok" : CoolerInstallRun ? "ice_crystal_cooler" : coolerPlacementIndex == 0 ? "furnace" : "ice_anvil";
    private bool StorageUseRun => SessionState.GetBool("Nyangbingo.QA.StorageUse",false);
    private bool StorageRestoreRun => SessionState.GetBool("Nyangbingo.QA.StorageRestore",false);
    private string storageTestId;
    private int storageTestDay;
    private bool T3CombatRun => SessionState.GetBool("Nyangbingo.QA.T3Combat", false);
    private bool BaekjungReachRun => SessionState.GetBool("Nyangbingo.QA.BaekjungReach", false);
    private bool BaekjungOvernightRun => SessionState.GetBool("Nyangbingo.QA.BaekjungOvernight", false);
    private bool BaekjungRestoreRun => SessionState.GetBool("Nyangbingo.QA.BaekjungRestore", false);
    private bool Day16InvasionRun => SessionState.GetBool("Nyangbingo.QA.Day16Invasion", false);
    private bool SecondInvasionNightRun => SessionState.GetBool("Nyangbingo.QA.SecondInvasionNight", false);
    private int ObservedInvasionDay => SessionState.GetInt("Nyangbingo.QA.ObservedInvasionDay", 16);
    private float secondInvasionSavedHeat;
    private bool Day18ResidentsRun => SessionState.GetBool("Nyangbingo.QA.Day18Residents", false);
    private bool GangcheoriApproachRun => SessionState.GetBool("Nyangbingo.QA.GangcheoriApproach", false);
    private float gangApproachLogAt;
    private bool GangcheoriPreparedRun => SessionState.GetBool("Nyangbingo.QA.GangcheoriPrepared", false);
    private bool GangcheoriFightRun => SessionState.GetBool("Nyangbingo.QA.GangcheoriFight", false);
    private float gangFightStarted = -1f;
    private int gangLastHp = -1;
    private bool GangcheoriNearRestoreRun => SessionState.GetBool("Nyangbingo.QA.GangcheoriNearRestore", false);
    private bool GangcheoriVictoryRestoreRun => SessionState.GetBool("Nyangbingo.QA.GangcheoriVictoryRestore", false);
    private bool gangApproachDescending;
    private float gangApproachStarted;
    private Vector2 gangApproachLastPosition;
    private int transitionEventSystemPeak;
    private string transitionEventSystemSignature;
    private int baekjungObservedWaves, baekjungObservationBucket = -1;
    private bool t3DamageObserved, t3SlowObserved;
    private int invasionObservationBucket = -1;
    private int bedOutputBefore;
    private float bedClockBefore;
    private int bedDayBefore, bedRestCount, bedSavedDay;
    private bool bedNightBefore;
    private bool SatbaUseRun => SessionState.GetBool("Nyangbingo.QA.SatbaUse", false);
    private bool BossApproachRun => SessionState.GetBool("Nyangbingo.QA.BossApproach", false);
    private bool BossSurfaceRun => SessionState.GetBool("Nyangbingo.QA.BossSurface", false);
    private bool BossDeathRecoveryRun => SessionState.GetBool("Nyangbingo.QA.BossDeathRecovery", false);
    private bool BossSaveHintRun => SessionState.GetBool("Nyangbingo.QA.BossSaveHint", false);
    private bool BossDawnRun => SessionState.GetBool("Nyangbingo.QA.BossDawn", false);
    private bool DawnRestoreRun => SessionState.GetBool("Nyangbingo.QA.DawnRestore", false);
    private bool BossColliderAimRun => SessionState.GetBool("Nyangbingo.QA.BossColliderAim", false);
    private bool BossOpeningRetreatRun => SessionState.GetBool("Nyangbingo.QA.BossOpeningRetreat", false);
    private bool BossOpeningHintRun => SessionState.GetBool("Nyangbingo.QA.BossOpeningHint", false);
    private bool NextNightRun => SessionState.GetBool("Nyangbingo.QA.NextNight", false);
    private bool FreshNightRun => SessionState.GetBool("Nyangbingo.QA.FreshNight", false);
    private bool KingRewardsRun => SessionState.GetBool("Nyangbingo.QA.KingRewards", false);
    private bool NaturalAutosaveRun => SessionState.GetBool("Nyangbingo.QA.NaturalAutosave", false);
    private bool bossOpeningEndedObserved, bossFirstDamageObserved, bossReapproach;
    private bool naturalBossEnded, naturalBossDefeated;
    private float dawnWaitStarted, dawnLogAt;
    private float bossFightStarted, bossLogAt, bossAimLogAt;
    private int shelterRestoreStage;

    private void CheckRestoredShelterDoor(bool open)
    {
        var cell = new Vector3Int(296, 120, 0);
        var environment = FindAnyObjectByType<MainGameEnvironmentState>();
        Require(tiles.IsDoorOpen(cell) == open, "door logical openness matches expected state");
        Require(environment.IsRecognizedBarrier(cell) == !open &&
            environment.IsRecognizedBarrier(cell + Vector3Int.up) == !open,
            "both door cells agree with expected seal state");
        var inspection = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position);
        Require(inspection.HasCore && inspection.InRange && inspection.Sealed == !open &&
            inspection.CoreDelta == (open ? -5 : -10), "earned room actual cooling follows door state");
        checks.Add($"Shelter stage={shelterRestoreStage}, open={open}, sealed={inspection.Sealed}, delta={inspection.CoreDelta}.");
    }
    private bool StorageCraftRun => SessionState.GetBool("Nyangbingo.QA.StorageCraft", false);
    private int storageStoneBefore, storageDirtBefore;
    private string WallRecipe => StorageCraftRun ? "jangdok" : BedCraftRun || BedUseRun ? "nest_bed" : SatbaCraftRun ? "ssireum_satba" : DoorCraftRun || DoorInstallRun ? "door" : "insul_wall";
    private bool coreCraftRun, coreCraftPassed;
    private bool installCoreRun;
    private bool wallCraftRun, wallCraftPassed;
    private int wallsRepaired;
    private int wallBefore;
    private RoomTempService.ShelterInspection coreInspectionBefore;
    private int coreIceBefore;
    private int deepOreBefore;
    private string deepSource;
    private bool foundrySmeltRun;
    private bool anvilRun, anvilFoundryStage, anvilPassed;
    private bool installAnvilRun, anvilReloaded;
    private bool combatRun, combatPassed;
    private bool harvestRun, harvestPassed;
    private bool doorGatherRun;
    private bool StorageShelterGatherRun => SessionState.GetBool("Nyangbingo.QA.StorageShelterGather",false);
    private int HarvestWoodGoal => SessionState.GetBool("Nyangbingo.QA.ShelterGateGather",false) ? 5 : StorageShelterGatherRun ? 12 : doorGatherRun ? 6 : 5;
    private int HarvestHempGoal => SessionState.GetBool("Nyangbingo.QA.ShelterGateGather",false) ? 10 : StorageShelterGatherRun ? 8 : doorGatherRun ? 4 : 10;
    private object harvestTarget;
    private string harvestKind;
    private Vector3 harvestAim;
    private float harvestTargetStarted, harvestPickupStarted, harvestJumpHeldUntil, harvestDiagnosticAt;
    private int harvestWood = -1, harvestHemp = -1;
    private string combatSource;
    private float combatElevation, combatAscentStarted;
    private int combatPlacementRetries;
    private int combatTearsBefore, combatTargetHealth;
    private Nyangbingo.Yokai.YokaiBrain combatTarget;
    private float combatJumpAt, combatStarted;
    private int healingReturnPhase, healingHealthBefore, healingCountBefore;
    private string healingItemId;
    private int healingSourceSlot;
    private int DeepOreRequired => deepOreBefore + (anvilRun ? 6 : 2);
    private string ProgressionRecipe => CoolerCraftRun ? "ice_crystal_cooler" : T3CraftRun ? "icesteel_claw" : coreCraftRun ? "ice_core" : anvilRun ? "ice_anvil" : deepRun ? "blast_furnace" : "iron_claw";
    private string SmeltBuilding => installCoreRun ? "ice_core" : installAnvilRun ? "ice_anvil" : foundrySmeltRun ? "blast_furnace" : "furnace";
    private string SmeltOre => foundrySmeltRun ? "icesteel_ore" : "iron_ore";
    private string SmeltIngot => foundrySmeltRun ? "icesteel_ingot" : "iron_ingot";
    private float diagnosticAt;
    private bool GangcheoriReturnRun => SessionState.GetBool("Nyangbingo.QA.GangcheoriReturn", false);
    private string AscentMaterial => deepRun ? "dirt" : "stone";
    // These earned deep checkpoints keep a non-placeable mushroom stack in slot 7;
    // slot 8 is no longer empty after copper pickups.
    private KeyCode MiningSlotKey => SessionState.GetBool("Nyangbingo.QA.CoolerOre",false) || deepRun || combatRun || coreGatherRun || doorGatherRun || DoorCraftRun || BossApproachRun || GangcheoriReturnRun ? KeyCode.Alpha7 : KeyCode.Alpha8;
    private float toolStageStarted;
    private string toolSmeltId, toolOreId, toolIngotId;
    private int toolIngotBefore, toolNavigationAttempts;
    private float bridgeDirection;
    private Vector3Int ascentCell;
    private int ascentStoneBefore, ascentSteps;
    private Nyangbingo.Inventory.Inventory placementAuditInventory;
    private int placementAuditCount, placementAuditRemoved, placementAuditAdded, placementAuditEvents;

    private void BeginPlacementAudit()
    {
        EndPlacementAudit();
        placementAuditInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
        placementAuditCount = placementAuditInventory.Count(AscentMaterial);
        placementAuditRemoved = placementAuditAdded = placementAuditEvents = 0;
        Require(tiles.GetTile(ascentCell).IsAir, "placement audit starts with an empty target cell");
        placementAuditInventory.Changed += ObservePlacementInventory;
        GameEvents.OnTilePlaced += ObservePlacedCell;
    }

    private void ObservePlacementInventory()
    {
        var current = placementAuditInventory.Count(AscentMaterial);
        var delta = current - placementAuditCount;
        if (delta < 0) placementAuditRemoved -= delta;
        else placementAuditAdded += delta;
        placementAuditCount = current;
    }

    private void ObservePlacedCell(Vector3Int cell)
    {
        if (cell == ascentCell) placementAuditEvents++;
    }

    private void VerifyPlacementAudit(string label)
    {
        EndPlacementAudit();
        var material = tiles.GetTile(ascentCell).elementType;
        Require(placementAuditEvents == 1 && placementAuditRemoved == 1 && material == AscentMaterial,
            $"{label}: target={ascentCell}, material={material}, placedEvents={placementAuditEvents}, " +
            $"removed={placementAuditRemoved}, pickups={placementAuditAdded}, total={placementAuditCount}");
    }

    private void EndPlacementAudit()
    {
        if (placementAuditInventory != null) placementAuditInventory.Changed -= ObservePlacementInventory;
        GameEvents.OnTilePlaced -= ObservePlacedCell;
        placementAuditInventory = null;
    }

    private void TickNaturalHarvest()
    {
        if (player.IsDead) throw new Exception("Natural material gathering ended in death.");
        var inventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
        var wood = inventory.Count("wood");
        var hemp = inventory.Count("hemp_stalk");
        if (wood != harvestWood || hemp != harvestHemp)
        {
            checks.Add($"Surface resources at {Time.realtimeSinceStartup - controlStarted:F3}s: wood={wood}/{HarvestWoodGoal}, hemp={hemp}/{HarvestHempGoal}, player={player.transform.position}");
            harvestWood = wood; harvestHemp = hemp;
        }
        if (wood >= HarvestWoodGoal && hemp >= HarvestHempGoal)
        {
            harvestPassed = true;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "satba-materials-gathered.png"));
            Sample(); Next(420); return;
        }
        if (Time.realtimeSinceStartup - controlStarted > 240f)
        { checks.Add("Surface collection reached 240-second observation bound; saving partial materials."); Sample(); Next(420); return; }
        var decorations = FindAnyObjectByType<MainGameWorldDecorationRenderer>();
        bool Available(object target, string kind) => (bool)typeof(MainGameWorldDecorationRenderer)
            .GetMethod(kind == "wood" ? "IsTreeAvailable" : "IsHempAvailable", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(decorations, new[] { target });
        if (harvestTarget != null && !Available(harvestTarget, harvestKind))
        {
            if (harvestPickupStarted == 0f)
            {
                harvestPickupStarted = Time.realtimeSinceStartup;
                checks.Add($"Natural {harvestKind} target became unavailable at {Time.realtimeSinceStartup - controlStarted:F3}s; walking to check drops. Availability alone does not prove harvest or pickup.");
            }
            if (Time.realtimeSinceStartup - harvestPickupStarted < 1.3f)
            {
                var pickupDx = harvestAim.x - player.transform.position.x;
                Sample(Mathf.Abs(pickupDx) > .25f ? Mathf.Sign(pickupDx) : 0f); return;
            }
            harvestTarget = null;
        }
        if (harvestTarget == null)
        {
            harvestKind = wood < HarvestWoodGoal ? "wood" : "hemp_stalk";
            var dictionary = (System.Collections.IDictionary)typeof(MainGameWorldDecorationRenderer)
                .GetField(harvestKind == "wood" ? "treePatches" : "hempPatches", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(decorations);
            var closest = float.PositiveInfinity;
            foreach (var candidate in dictionary.Values)
            {
                if (!Available(candidate, harvestKind)) continue;
                var cell = (Vector3Int)candidate.GetType().GetField("SupportCell").GetValue(candidate);
                var point = tiles.GetCellCenterWorld(cell + Vector3Int.up);
                var distance = Vector2.Distance(point, player.transform.position);
                if (distance >= closest) continue;
                closest = distance; harvestTarget = candidate; harvestAim = point;
            }
            if (harvestTarget == null)
            { checks.Add("No available natural " + harvestKind + " target; saving partial progress."); Sample(); Next(420); return; }
            harvestTargetStarted = Time.realtimeSinceStartup;
            harvestPickupStarted = 0f;
            checks.Add($"Natural {harvestKind} target at {harvestAim}, player={player.transform.position}");
        }
        var harvestTargetLimit = SessionState.GetBool("Nyangbingo.QA.ShelterGateResume",false) ? 90f : 25f;
        if (Time.realtimeSinceStartup - harvestTargetStarted > harvestTargetLimit)
        {
            checks.Add($"Natural collection target not reached/harvested in {harvestTargetLimit} seconds: {harvestKind}, target={harvestAim}, player={player.transform.position}. Saving partial route, not a game softlock verdict.");
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "surface-route-blocked.png"));
            Sample(); Next(420); return;
        }
        var delta = harvestAim - player.transform.position;
        var move = Mathf.Abs(delta.x) > 1.1f ? Mathf.Sign(delta.x) : 0f;
        if ((move != 0f || delta.y > 1.5f) && player.IsGrounded && Time.realtimeSinceStartup >= combatJumpAt)
        {
            harvestJumpHeldUntil = Time.realtimeSinceStartup + .45f;
            combatJumpAt = Time.realtimeSinceStartup + 1f;
        }
        var jump = Time.realtimeSinceStartup < harvestJumpHeldUntil;
        var mine = delta.sqrMagnitude <= 12f;
        GameplayInput.Sample(move, Camera.main.WorldToScreenPoint(harvestAim), mine, false,
            jump ? new[] { KeyCode.Space } : Array.Empty<KeyCode>());
        if (Time.realtimeSinceStartup >= harvestDiagnosticAt)
        {
            harvestDiagnosticAt = Time.realtimeSinceStartup + 2f;
            var palette = FindAnyObjectByType<MainGameTilePaletteController>();
            var treeHit = decorations.TryResolveTreeMiningTarget(player.transform.position,
                MainGamePlayerController.SnapAttackFeedbackDirection((Vector2)PlayerField("facing")),
                (float)PlayerField("miningReach"), out var treeId, out var treeCell);
            var targetArgs = new object[] { tiles, default(Vector3Int) };
            var targetKind = typeof(MainGamePlayerController).GetMethod("ResolveMiningWorldTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, targetArgs);
            checks.Add($"Harvest target diagnostic: treeHit={treeHit}, id={treeId}, cell={treeCell}, kind={targetKind}, resolvedCell={targetArgs[1]}, renderer={harvestTarget.GetType().GetField("Renderer").GetValue(harvestTarget)}, reach={PlayerField("miningReach")}");
            checks.Add($"Harvest diagnostic: position={player.transform.position}, facing={PlayerField("facing")}, primary={mine}, ui={GameplayInput.IsPointerOverUi()}, placement={palette.ShouldBlockPrimaryForPlacement}, active={PlayerField("miningActive")}, tree={PlayerField("miningTreeId")}, elapsed={PlayerField("miningElapsedSeconds")}, required={PlayerField("miningRequiredSeconds")}");
        }
    }

    private object PlayerField(string name) => typeof(MainGamePlayerController)
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(player);

    private void Start()
    {
        started = phaseStarted = Time.realtimeSinceStartup;
        directory = SessionState.GetString(NyangbingoInputReplaySmoke.SaveKey, "");
        workbenchRun = SessionState.GetBool("Nyangbingo.QA.Workbench", false);
        furnaceRun = SessionState.GetBool("Nyangbingo.QA.Furnace", false);
        resumeEarnedSave = SessionState.GetBool("Nyangbingo.QA.Resume", false);
        ascentRun = SessionState.GetBool("Nyangbingo.QA.Ascent", false);
        bridgeRun = SessionState.GetBool("Nyangbingo.QA.Bridge", false);
        smeltRun = SessionState.GetBool("Nyangbingo.QA.Smelt", false);
        toolRun = SessionState.GetBool("Nyangbingo.QA.Tool", false);
        deepRun = SessionState.GetBool("Nyangbingo.QA.Deep", false);
        foundrySmeltRun = SessionState.GetBool("Nyangbingo.QA.FoundrySmelt", false);
        anvilRun = SessionState.GetBool("Nyangbingo.QA.Anvil", false);
        installAnvilRun = SessionState.GetBool("Nyangbingo.QA.InstallAnvil", false);
        combatRun = SessionState.GetBool("Nyangbingo.QA.Combat", false);
        harvestRun = SessionState.GetBool("Nyangbingo.QA.Harvest", false);
        doorGatherRun = SessionState.GetBool("Nyangbingo.QA.DoorGather", false);
        coreGatherRun = SessionState.GetBool("Nyangbingo.QA.CoreGather", false);
        coreCraftRun = SessionState.GetBool("Nyangbingo.QA.CoreCraft", false);
        installCoreRun = SessionState.GetBool("Nyangbingo.QA.InstallCore", false);
        wallCraftRun = SessionState.GetBool("Nyangbingo.QA.WallCraft", false);
        combatSource = SessionState.GetString("Nyangbingo.QA.CombatSource", "unknown");
        deepSource = SessionState.GetString("Nyangbingo.QA.DeepSource", "unspecified checkpoint");
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
    }
    private void Update()
    {
        if (finished) return;
        try
        {
            // Runtime subsystem registration can run after editor play-state callbacks.
            // Arm input on the first actual gameplay Update, not during the transition.
            if (!replayStarted)
            {
                GameplayInput.BeginReplay();
                replayStarted = true;
                started = phaseStarted = Time.realtimeSinceStartup;
            }
            if (phase != 1283 && phase != 1211 && phase != 1213 && phase != 1236 && Time.realtimeSinceStartup - phaseStarted > (phase == 935 ? 1800f : phase == 1121 ? 650f : phase == 1001 ? 180f : phase == 1101 || phase == 1070 || phase == 1012 && InvasionOvernightRun ? 1200f : phase == 750 || phase == 747 || phase == 761 || phase == 902 || phase == 914 ? 1200f : phase == 615 ? (T3CraftRun ? 180f : 120f) : 60f))
                throw new Exception("Timed out at phase " + phase);
            if (BaekjungRestoreRun)
            {
                var activeEvents = FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Where(e => e.isActiveAndEnabled).ToArray();
                transitionEventSystemPeak = Math.Max(transitionEventSystemPeak, activeEvents.Length);
                var signature = string.Join(",", activeEvents.Select(e => e.gameObject.scene.name + "/" + e.name).OrderBy(n => n));
                if (signature != transitionEventSystemSignature)
                {
                    transitionEventSystemSignature = signature;
                    checks.Add($"EventSystem frame sample: phase={phase}, active={activeEvents.Length}, scenes={signature}");
                }
            }
            Tick();
            if (!finished && Time.realtimeSinceStartup - diagnosticAt > 15f && !string.IsNullOrEmpty(directory))
            {
                diagnosticAt = Time.realtimeSinceStartup;
                File.WriteAllText(Path.Combine(directory, "live.json"), JsonUtility.ToJson(new Result
                { status = "running", scope = $"Live phase {phase}; player {(player != null ? player.transform.position.ToString() : "loading")}",
                    wallSeconds = Time.realtimeSinceStartup - started, checks = checks.ToArray(), findings = findings.ToArray() }, true));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "live.png"));
            }
        }
        catch (Exception error) { Finish("failed", error.ToString()); }
    }
    private void Next(int next)
    {
        phase = next;
        phaseStarted = Time.realtimeSinceStartup;
    }
    private void Sample(float movement = 0f, params KeyCode[] keys) =>
        GameplayInput.Sample(movement, new Vector3(Screen.width * .5f, Screen.height * .5f), false, false, keys);
    private void SampleUse() => GameplayInput.Sample(0f,
        new Vector3(Screen.width * .5f, Screen.height * .5f), false, true);
    private void Require(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks.Add($"{(pass < 3 ? "pass " + (pass + 1) : "post-loop")}: {label}");
    }
    private void Tick()
    {
        if ((anvilRun || combatRun || harvestRun || coreGatherRun || coreCraftRun || SatbaCraftRun || T3GatherRun || GangcheoriPreparedRun || GangcheoriReturnRun) && player != null && !player.IsDead && (phase == 401 || phase == 460 || phase == 480 || phase == 750 || phase == 761 || phase == 1160 || GangcheoriPreparedRun && phase == 1121) &&
            ((Nyangbingo.Combat.Health)PlayerField("health")).Current <= 60)
        {
            var healingSlots = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots;
            healingSourceSlot = -1;
            for (var i = 0; i < healingSlots.Count; i++)
                if (healingSlots[i].amount > 0 && PlayerHealthRecoveryService.IsSupportedHealingItemId(healingSlots[i].itemId))
                { healingSourceSlot = i; break; }
            if (healingSourceSlot < 0)
            {
                if (phase == 1160) throw new Exception("Imugi fight exhausted earned healing; no victory or boss-time save claim.");
                checks.Add("All earned healing supplies exhausted; saving partial progress, not declaring a game softlock.");
                Sample();
                Next(GangcheoriPreparedRun || GangcheoriReturnRun ? 1122 : 420);
                return;
            }
            healingItemId = healingSlots[healingSourceSlot].itemId;
            healingReturnPhase = phase;
            Sample(0f, healingSourceSlot < 8 ? (KeyCode)((int)KeyCode.Alpha1 + healingSourceSlot) : KeyCode.Tab);
            Next(healingSourceSlot < 8 ? 718 : 712);
            return;
        }
        switch (phase)
        {
            case 0:
                Sample();
                var title = FindAnyObjectByType<TitleShellController>();
                if (title == null || Time.realtimeSinceStartup - phaseStarted < 1f) return;
                if (resumeEarnedSave)
                {
                    Require(title.TryContinue(), "Continue loads a copy of the normally earned checkpoint identified in result scope");
                    Next(450);
                    break;
                }
                title.RequestNewGame(); // Same public callback as the New Game button.
                Next(1);
                break;
            case 1:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                var choice = GameObject.Find("Trait_trait_melee");
                if (player == null || !player.IsInitialized || choice == null) return;
                if (SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                var visual = player.transform.Find("Visual").GetComponent<SpriteRenderer>();
                var worldTiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                var blockHeight = worldTiles.GetCellWorldBounds(worldTiles.WorldToCell(player.transform.position)).size.y;
                Require(Mathf.Abs(visual.bounds.size.y / blockHeight - 2f) < .04f,
                    $"player visual is two blocks high (width {visual.bounds.size.x / blockHeight:F3}, height {visual.bounds.size.y / blockHeight:F3}, scale {visual.transform.localScale.x:F3})");
                Require(Vector3.Distance(player.transform.localScale, Vector3.one) < .001f,
                    "physics root scale remains unchanged");
                var collider = player.GetComponent<BoxCollider2D>();
                Require(Mathf.Abs(visual.bounds.min.y - (player.transform.position.y + collider.offset.y - collider.size.y * .5f)) < .02f,
                    "visual feet align with collider bottom");
                Require(player.transform.Find("AttackVisual").localScale == Vector3.one,
                    "attack art does not inherit character scaling");
                var hint = choice.transform.parent.Find("Hint").GetComponent<RectTransform>();
                var buttonRect = choice.GetComponent<RectTransform>();
                Require(buttonRect.anchoredPosition.y + buttonRect.rect.height / 2f <=
                    hint.anchoredPosition.y - hint.rect.height / 2f, "trait hint does not overlap first button");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "trait-selection.png"));
                Next(14);
                break;
            case 14:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                GameObject.Find("Trait_trait_melee").GetComponent<Button>().onClick.Invoke();
                Next(2);
                break;
            case 2:
                Sample();
                if (Time.timeScale <= 0f || SceneTransitionRequest.IsLoadingSceneLoaded() ||
                    Time.realtimeSinceStartup - phaseStarted < .5f) return;
                if (pass == 0) ScreenCapture.CaptureScreenshot(Path.Combine(directory, "player-two-blocks.png"));
                origin = player.transform.position;
                if (workbenchRun)
                {
                    controlStarted = Time.realtimeSinceStartup;
                    checks.Add("timing start: first visible controllable gameplay; bot knows recipe and inspects local tiles");
                    Next(40);
                }
                else Next(230);
                break;
            case 230:
                Sample();
                Require(GameObject.Find("ShelterGuide").GetComponent<Text>().text.Contains("작업대"), "shelter guide starts with current workbench step");
                GameObject.Find("ShelterGuideToggle").GetComponent<Button>().onClick.Invoke();
                Next(231); break;
            case 231:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .15f) return;
                Require(GameObject.Find("ShelterGuide") == null, "shelter guide collapses via UI callback");
                if (pass == 0) ScreenCapture.CaptureScreenshot(Path.Combine(directory, "shelter-guide-collapsed.png"));
                Next(233); break;
            case 233:
                Sample();
                GameObject.Find("ShelterGuideToggle").GetComponent<Button>().onClick.Invoke();
                Next(232); break;
            case 232:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .15f) return;
                Require(GameObject.Find("ShelterGuide") != null, "shelter guide reopens via UI callback");
                if (pass == 0) ScreenCapture.CaptureScreenshot(Path.Combine(directory, "shelter-guide-expanded.png"));
                Next(3); break;
            case 40:
                Sample();
                var inventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                var dirt = inventory.Count("dirt");
                var stone = inventory.Count("stone");
                if (dirt + stone != lastInventoryTotal)
                {
                    lastInventoryTotal = dirt + stone;
                    checks.Add($"pickup at {Time.realtimeSinceStartup - controlStarted:F3}s: dirt={dirt}, stone={stone}");
                }
                if (dirt >= 8 && stone >= 12)
                {
                    checks.Add($"all workbench materials at {Time.realtimeSinceStartup - controlStarted:F3}s");
                    Next(44);
                    break;
                }
                if (Time.realtimeSinceStartup - controlStarted > 180f || gatheringCount >= 80)
                    throw new Exception($"Local gathering strategy exhausted: dirt={dirt}, stone={stone}; not proof of a game softlock.");
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                var current = tiles.WorldToCell(player.transform.position);
                var gatherTargets = new List<Vector3Int>();
                for (var dx = -3; dx <= 3; dx++)
                for (var dy = -3; dy <= 1; dy++)
                {
                    var candidate = current + new Vector3Int(dx, dy, 0);
                    if (!tiles.InBounds(candidate)) continue;
                    var material = tiles.GetTile(candidate).elementType;
                    if (material != WorldTileTypes.Dirt && material != WorldTileTypes.Stone) continue;
                    var point = (Vector2)tiles.GetCellCenterWorld(candidate);
                    if (!MainGamePlayerController.TryPickMiningCell(tiles, player.transform.position,
                        point, (point - (Vector2)player.transform.position).normalized, 4f, out var picked) || picked != candidate) continue;
                    gatherTargets.Add(candidate);
                }
                if (gatherTargets.Count == 0)
                {
                    if (++routeRecoveries > 6)
                        throw new Exception("No local mineable dirt/stone after bounded movement recovery; not proof of a game softlock.");
                    checks.Add($"route recovery {routeRecoveries}: ordinary right movement/jump input");
                    Next(49);
                    break;
                }
                mineCell = gatherTargets.OrderBy(c =>
                    ((tiles.GetTile(c).elementType == WorldTileTypes.Dirt && dirt < 8) ||
                     (tiles.GetTile(c).elementType == WorldTileTypes.Stone && stone < 12) ? 0f : 100f) +
                    (tiles.GetCellCenterWorld(c) - player.transform.position).sqrMagnitude).First();
                Next(41);
                break;
            case 41:
                if (tiles.GetTile(mineCell).IsAir)
                {
                    Sample();
                    gatheringCount++;
                    Next(42);
                    break;
                }
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(mineCell)), true, false);
                if (Time.realtimeSinceStartup - phaseStarted > 8f)
                    throw new Exception($"Selected tile timeout: target={mineCell}, player={player.transform.position}, " +
                        $"miningCell={PlayerField("miningCell")}, active={PlayerField("miningActive")}, " +
                        $"elapsed={PlayerField("miningElapsedSeconds")}, required={PlayerField("miningRequiredSeconds")}, " +
                        $"reach={PlayerField("miningReach")}, scale={Time.timeScale}, " +
                        $"uiBlocked={GameplayInput.IsPointerOverUi()}, gaugeBlocked={MainGameHudController.BlocksWorldPrimaryInput}, " +
                        $"hotbar={FindAnyObjectByType<MainGameTilePaletteController>()?.SelectedItemId}");
                break;
            case 42:
                // Walk toward the broken cell so drops outside magnet range can be collected.
                // No transforms, inventory, or pickup APIs are modified by the route planner.
                var pickupDelta = tiles.GetCellCenterWorld(mineCell).x - player.transform.position.x;
                Sample(Mathf.Abs(pickupDelta) > .25f ? Mathf.Sign(pickupDelta) : 0f);
                if (Time.realtimeSinceStartup - phaseStarted >= 1f) Next(40);
                break;
            case 49:
                Sample(1f, KeyCode.Space);
                if (Time.realtimeSinceStartup - phaseStarted >= .75f) Next(40);
                break;
            case 44: Sample(0f, KeyCode.C); Next(45); break;
            case 45: Sample(); Next(46); break;
            case 46: Sample(0f, KeyCode.E); Next(47); break;
            case 47:
                Sample();
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("workbench") == 1,
                    "normal recipe UI creates exactly one workbench from mined/picked-up resources");
                checks.Add($"first workbench crafted at {Time.realtimeSinceStartup - controlStarted:F3}s; tiles mined={gatheringCount}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "first-workbench.png"));
                Next(48);
                break;
            case 48:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var craftUi = FindAnyObjectByType<MainGameCraftingUiController>();
                ((Button)typeof(MainGameCraftingUiController).GetField("collectButton",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(craftUi)).onClick.Invoke();
                Next(50);
                break;
            case 50:
                Sample();
                Require(FindAnyObjectByType<MainGameTurretRuntime>().IsPlacementPreviewActive,
                    "crafting install button enters ordinary placement preview");
                Next(51);
                break;
            case 51:
                Sample();
                if (placementProbe >= 81) throw new Exception("No valid local workbench installation point; route expansion required.");
                var placementOrigin = tiles.WorldToCell(player.transform.position);
                var offset = new Vector3Int(placementProbe % 9 - 4, placementProbe / 9 - 4, 0);
                placementProbe++;
                placementPoint = tiles.GetCellCenterWorld(placementOrigin + offset);
                Next(52);
                break;
            case 52:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false);
                if (Time.realtimeSinceStartup - phaseStarted < .1f) return;
                Next(FindAnyObjectByType<MainGameTurretRuntime>().IsPlacementPreviewValid ? 53 : 51);
                break;
            case 53:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, true);
                Next(54);
                break;
            case 54:
                Sample();
                var installed = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects()
                    .Where(r => r.definitionId == "workbench").ToArray();
                Require(installed.Length == 1, "left-click placement creates exactly one workbench record");
                placedWorkbench = installed[0];
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("workbench") == 0,
                    "placement consumes the crafted workbench");
                checks.Add($"workbench installed at {Time.realtimeSinceStartup - controlStarted:F3}s: {placedWorkbench.position}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "workbench-installed.png"));
                Next(55);
                break;
            case 55:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Next(56);
                break;
            case 56: Sample(0f, KeyCode.Escape); Next(57); break;
            case 57:
                Sample();
                var shellUi = FindAnyObjectByType<MainGameShellUiController>();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Pause, "pause opened before save");
                savedDirt = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("dirt");
                savedStone = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("stone");
                ((Button)typeof(MainGameShellUiController).GetField("pauseSaveButton",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shellUi)).onClick.Invoke();
                Next(58);
                break;
            case 58:
                Sample();
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var diskSave) &&
                    diskSave.placedObjectRecords.Any(r => r.objectId == placedWorkbench.objectId),
                    "manual save persists installed workbench to isolated disk save");
                var gameShell = FindAnyObjectByType<GameShellController>();
                Require(gameShell.RequestReturnToTitle() && gameShell.Confirm(), "normal return-to-title callbacks accepted");
                Next(59);
                break;
            case 59:
                Sample();
                var continueTitle = FindAnyObjectByType<TitleShellController>();
                if (continueTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(continueTitle.TryContinue(), "title Continue loads the isolated saved game");
                Next(60);
                break;
            case 60:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() ||
                    Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var restored = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects()
                    .Where(r => r.objectId == placedWorkbench.objectId).ToArray();
                Require(restored.Length == 1 && restored[0].definitionId == "workbench" &&
                    Vector2.Distance(restored[0].position, placedWorkbench.position) < .01f,
                    "scene reload restores the same workbench identity and position without duplication");
                var restoredInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                Require(restoredInventory.Count("dirt") == savedDirt && restoredInventory.Count("stone") == savedStone &&
                    restoredInventory.Count("workbench") == 0, "scene reload preserves remaining materials and consumed workbench");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "workbench-restored.png"));
                Next(61);
                break;
            case 61:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Next(300);
                break;
            case 300:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedWorkbench.position), false, false, KeyCode.E);
                Next(301);
                break;
            case 301:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .4f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput,
                    "right-click on restored workbench opens station crafting UI");
                var stationUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var stationRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController)
                    .GetField("filteredRecipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stationUi);
                var furnaceIndex = stationRecipes.FindIndex(r => r.Id == "furnace");
                Require(furnaceIndex >= 0, "furnace recipe exists in runtime recipe list");
                var stationButtons = (Button[])typeof(MainGameCraftingUiController)
                    .GetField("craftingListButtons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stationUi);
                Require(stationButtons[furnaceIndex].gameObject.activeInHierarchy,
                    "furnace recipe is included in installed-workbench UI filter");
                furnaceTargetIndex = furnaceIndex;
                Next(306);
                break;
            case 306:
                var navigationUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var navigationIndex = (int)typeof(MainGameCraftingUiController)
                    .GetField("selectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(navigationUi);
                if (navigationIndex == furnaceTargetIndex) { Sample(); Next(302); }
                else
                {
                    Sample(0f, navigationIndex < furnaceTargetIndex ? KeyCode.DownArrow : KeyCode.UpArrow);
                    Next(307);
                }
                break;
            case 307:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted >= .1f) Next(306);
                break;
            case 302:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .4f) return;
                var selectedFurnace = (Nyangbingo.Data.RecipeDefinition)typeof(MainGameCraftingUiController)
                    .GetMethod("CurrentRecipe", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(FindAnyObjectByType<MainGameCraftingUiController>(), null);
                Require(selectedFurnace?.Id == "furnace", "selected UI recipe identity is furnace");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "next-furnace-recipe.png"));
                Next(303);
                break;
            case 303: Sample(0f, KeyCode.E); Next(304); break;
            case 304:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .4f) return;
                if (furnaceReturning) { Next(412); break; }
                var furnaceUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var furnaceMessage = (string)typeof(MainGameCraftingUiController)
                    .GetField("message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(furnaceUi);
                Require(furnaceMessage.StartsWith("재료 부족:"), "furnace failure explains materials: " + furnaceMessage);
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("furnace") == 0,
                    "unaffordable furnace recipe grants no furnace");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "next-furnace-missing.png"));
                Next(305);
                break;
            case 305:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                if (furnaceRun) { Next(400); break; }
                Finish("passed", "Gather/craft/install/save/reload and restored-workbench right-click/furnace recipe visibility passed. Furnace material gathering, actual furnace crafting, OS process restart and bosses untested.");
                break;
            case 400:
                Sample(0f, KeyCode.Escape);
                furnaceStarted = furnaceLastProgress = Time.realtimeSinceStartup;
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                Next(401);
                break;
            case 450:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() ||
                    Time.realtimeSinceStartup - phaseStarted < 1f) return;
                if (SessionState.GetBool("Nyangbingo.QA.ImugiLoot", false))
                {
                    controlStarted = Time.realtimeSinceStartup;
                    Next(1180); break;
                }
                if (SessionState.GetBool("Nyangbingo.QA.RewardCombat", false))
                {
                    controlStarted = combatStarted = Time.realtimeSinceStartup;
                    checks.Add("Actual equipped reward save: normal Yeouiju attacks then Q to earned T3, read-only enemy HP/slow. No grants, forced enemies, HP or time changes; no resave claim.");
                    Next(1210); break;
                }
                if (SessionState.GetBool("Nyangbingo.QA.CoolerReturn", false))
                {
                    controlStarted = Time.realtimeSinceStartup;
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    checks.Add("Earned imugi-equipment-hint-fixed-3: return to dropped original workbench beside blast furnace; normal movement/pickup/save only. No ore grant or cooler completion claim.");
                    Next(SessionState.GetBool("Nyangbingo.QA.CoolerOre",false) ? 1235 : 1230); break;
                }
                if (StorageRestoreRun)
                {
                    controlStarted=Time.realtimeSinceStartup;
                    tiles=FindAnyObjectByType<MainGameBootstrap>().TileService;
                    placedWorkbench=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="workbench");
                    Next(1284);break;
                }
                if(SessionState.GetBool("Nyangbingo.QA.GateStorageInspect",false)) { Next(1310);break; }
                if (StorageUseRun)
                {
                    controlStarted=Time.realtimeSinceStartup;
                    tiles=FindAnyObjectByType<MainGameBootstrap>().TileService;
                    placedWorkbench=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="workbench");
                    Next(1240);break;
                }
                if (CoolerInstallRun)
                {
                    controlStarted = Time.realtimeSinceStartup;
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ice_crystal_cooler")==1,"normally crafted cooler present");
                    Next(1240); break;
                }
                if (CoolerCraftRun)
                {
                    controlStarted = Time.realtimeSinceStartup;
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("icesteel_ore")==10,"cooler source has ten normally recovered ore");
                    checks.Add("Cooler from recovered earned route: normal inventory placement furnace/anvil, five icesteel and three copper ingots, sixty-game-second cooler craft, ordinary save/Continue. No grants or completion/time injection.");
                    Next(1240); break;
                }
                placedWorkbench = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects()
                    .First(r => r.definitionId == "workbench");
                controlStarted = Time.realtimeSinceStartup;
                if (BaekjungReachRun) { Next(1060); break; }
                if (T3CraftRun)
                {
                    placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "blast_furnace");
                    anvilFoundryStage = true;
                    if (SessionState.GetBool("Nyangbingo.QA.Day30T3", false))
                    {
                        var day30Materials = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                        Require(day30Materials.Count("icesteel_ore") == 19 && day30Materials.Count("coal") == 48 && day30Materials.Count("frost_essence") == 8 && day30Materials.Count("icesteel_claw") == 0 && day30Materials.Count("icesteel_ingot") == 0, "actual day30 dawn materials: ore19 coal48 frost8 no T3 or ingots");
                        checks.Add("Source day30-dawn-route-1: normal five ingot smelting and T3 craft before Imugi. Same earned progression branch; no grants, time skips or merged inventory.");
                    }
                    else checks.Add("Actual post-king gather/return save: normal five ingot smelting and icesteel_claw crafting, then save and Continue. No grants/time skips.");
                    Next(600); break;
                }
                if (InvasionRestoreRun) { tiles = FindAnyObjectByType<MainGameBootstrap>().TileService; Next(1020); break; }
                if (SessionState.GetBool("Nyangbingo.QA.StorageRecool",false)) { tiles = FindAnyObjectByType<MainGameBootstrap>().TileService; Next(1300); break; }
                if (KingRewardsRun) { tiles = FindAnyObjectByType<MainGameBootstrap>().TileService; Next(NaturalAutosaveRun ? 1000 : 950); break; }
                if (BedUseRun) { tiles = FindAnyObjectByType<MainGameBootstrap>().TileService; Next(SessionState.GetBool("Nyangbingo.QA.StorageInvasion",false) || SessionState.GetBool("Nyangbingo.QA.StorageRecovered",false) ? 1295 : SessionState.GetBool("Nyangbingo.QA.StoragePreserve",false) ? 1296 : BedWarmRun ? 986 : 810); break; }
                if (NextNightRun) { dawnWaitStarted = controlStarted; Next(935); break; }
                if (GangcheoriNearRestoreRun) { Next(1124); break; }
                if (GangcheoriVictoryRestoreRun) { Next(1130); break; }
                if (GangcheoriReturnRun) { Next(1140); break; }
                if (GangcheoriApproachRun) { Next(1120); break; }
                if (Day18ResidentsRun) { Next(1110); break; }
                if (SecondInvasionNightRun) { Next(1100); break; }
                if (BaekjungRestoreRun) { Next(1080); break; }
                if (DawnRestoreRun) { Next(920); break; }
                if (wallCraftRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    if (SessionState.GetBool("Nyangbingo.QA.StorageDoorInstall",false)) { Next(1292); break; }
                    if (SatbaUseRun)
                    {
                        if (BossSaveHintRun)
                        {
                            if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                            Next(909); break;
                        }
                        if (BossSurfaceRun)
                        {
                            combatAscentStarted = Time.realtimeSinceStartup;
                            combatElevation = tiles.FindSurfaceNaturalY(tiles.WorldToCell(player.transform.position).x) + 1.38f;
                            Next(748); break;
                        }
                        Next(890); break;
                    }
                    if (RestoreShelterRun) { Next(870); break; }
                    if (SatbaCraftRun)
                    {
                        furnaceReturning = true;
                        furnaceStarted = furnaceLastProgress = controlStarted;
                        checks.Add(BedCraftRun ? "Normal return and nest_bed craft; actual earned source is identified in result scope. No merged inventories." : "Return from satba-gather-fixed-3 with earned club1 wood5 hemp10, then ordinary recipe input and wait.");
                        Next(880); break;
                    }
                    if (EnclosureRun) { Next(860); break; }
                    if (DoorCraftRun)
                    {
                        toolStageStarted = controlStarted;
                        checks.Add(StorageCraftRun ? "Return from actual door-ice-gather-1 and craft jangdok with earned wood6 stone8 dirt10; keep ice for later storage test. No grants or merged inventories." : "Return from door-ice-gather-1 and craft door with normal earned wood/hemp/ice; no grants.");
                        Next(466); break;
                    }
                    checks.Add("Craft insul_wall from installed-core normal save; no grants, time changes or completion injection.");
                    Next(810); break;
                }
                if (installCoreRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    coreInspectionBefore = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position);
                    Require(!coreInspectionBefore.HasCore, "earned core has not yet been installed");
                    checks.Add("Install ice_core from first-ice-core-craft-1 normally earned checkpoint, inspect real cooling and title-Continue restoration.");
                    Next(500); break;
                }
                if (coreCraftRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "furnace");
                    toolStageStarted = controlStarted;
                    checks.Add("Return from ice-core-gather-fixed-1, normally smelt two iron ingots and craft ice_core. Ordinary scaffold/mining/movement, no grants or teleport.");
                    Next(466); break;
                }
                if (coreGatherRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    furnaceStarted = furnaceLastProgress = controlStarted;
                    checks.Add($"Normal ice gathering to {IceGatherGoal}; source is identified in result scope. Preserve earned inventory. No crafting claim.");
                    Next(451); break;
                }
                if (harvestRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    if (doorGatherRun)
                    {
                        if(SessionState.GetBool("Nyangbingo.QA.AfterStorageFloor",false)) Require(tiles.GetTile(new Vector3Int(297,119,0)).elementType=="insul_wall","normal Continue preserves repaired storage floor");
                        combatElevation = tiles.FindSurfaceNaturalY(tiles.WorldToCell(player.transform.position).x) + 1.38f;
                        toolStageStarted = Time.realtimeSinceStartup;
                        if(SessionState.GetBool("Nyangbingo.QA.ReplacementTreeAscent",false)) combatElevation=Mathf.Max(combatElevation,tiles.FindSurfaceNaturalY(244)+1.38f);
                        if(SessionState.GetBool("Nyangbingo.QA.ShelterGateGather",false)) combatElevation=Mathf.Max(combatElevation,tiles.FindSurfaceNaturalY(244)+1.38f);
                        checks.Add($"Door resource route: normal ascent to {combatElevation}, then wood{HarvestWoodGoal} hemp{HarvestHempGoal}. Actual source identified in scope; ice and crafting remain separate.");
                        Next(760); break;
                    }
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("club_shard") == 1,
                        "normal combat checkpoint retains one earned club shard");
                    checks.Add("Gather wood5 and hemp10 from natural surface decorations with ordinary movement/mining/pickup. No crafting or boss claim yet.");
                    Next(760); break;
                }
                if (combatRun)
                {
                    if (T3CombatRun)
                    {
                        var t3Profile = ((Nyangbingo.Combat.MeleeArcAttack)PlayerField("attack")).CombatProfile;
                        Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("icesteel_claw") == 1 && t3Profile.Id == "icesteel_claw", "earned T3 is actual active combat profile");
                        checks.Add($"T3 profile damage={t3Profile.AttackDamage}, range={t3Profile.RangeTiles}, arc={t3Profile.ArcDegrees}; actual enemy effects remain to test.");
                    }
                    combatTearsBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("yokai_tear");
                    combatStarted = Time.realtimeSinceStartup;
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    combatElevation = tiles.FindSurfaceNaturalY(tiles.WorldToCell(player.transform.position).x) + 1.38f;
                    combatAscentStarted = Time.realtimeSinceStartup;
                    checks.Add($"Combat from {combatSource}. Wait underground only if not already night, then ordinary ascent to surface elevation {combatElevation:F2}. No time skip, forced spawn, damage injection or grants.");
                    Next(747);
                    break;
                }
                if (toolRun || deepRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects()
                        .Single(r => r.definitionId == "furnace");
                    furnaceStarted = furnaceLastProgress = controlStarted;
                    if (T3GatherRun) checks.Add("Actual king reward save: gather to icesteel_ore10 frost_essence2, then return to existing workbench. No T3 crafting claim; no grants or time changes.");
                    if (!T3GatherRun) checks.Add(deepRun
                        ? $"Deep mining segment resumes {deepSource}. Obtain {(anvilRun ? 6 : 2)} natural ice-steel ore, return, and craft {ProgressionRecipe}."
                        : "Iron-claw segment resumes first-smelting-3. Gather missing iron, return, smelt and craft through normal inputs. Claw tier depends on inventory, not active-slot equip.");
                    if (deepRun)
                    {
                        Require((int)typeof(MainGamePlayerController).GetMethod("ResolveMiningClawTier", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(player, null) == 2, "earned claw restores actual T2 mining tier");
                        deepOreBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("icesteel_ore");
                    }
                    Next(451);
                    break;
                }
                if (smeltRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    checks.Add(foundrySmeltRun ? "Installation/smelting resumes first-foundry-1 normally crafted save." : "Installation/smelting segment resumes normally crafted first-furnace-1 save.");
                    Next(500);
                    break;
                }
                if (bridgeRun)
                {
                    furnaceStarted = controlStarted;
                    furnaceReturning = true;
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    checks.Add("Horizontal return segment from normal ascent checkpoint; no teleport or item grant.");
                    Next(480);
                    break;
                }
                if (ascentRun)
                {
                    tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                    checks.Add("Ascent segment starts from normally mined furnace-route-resume-3 save. Ordinary mining, number-key selection, jump and click placement only.");
                    Next(460);
                    break;
                }
                checks.Add("Segment timing starts at continued gameplay, not New Game. Source furnace-access-2 saved after normally earned workbench installation at 47.794 seconds.");
                Next(300);
                break;
            case 451:
                Sample(0f, MiningSlotKey);
                Next(452);
                break;
            case 452:
                Sample();
                Next(401);
                break;
            case 500:
                if (foundrySmeltRun || installAnvilRun)
                {
                    placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == (installAnvilRun ? "blast_furnace" : "furnace"));
                    GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedFurnace.position), false, false, KeyCode.E);
                    Next(700);
                    break;
                }
                Sample(0f, MiningSlotKey);
                Next(501);
                break;
            case 501:
                Sample();
                Require(FindAnyObjectByType<MainGameTurretRuntime>().IsPlacementPreviewActive,
                    "earned station opens placement preview through hotbar or installation button");
                Next(502);
                break;
            case 502:
                Sample();
                if (placementProbe >= 81) throw new Exception("No valid local furnace placement; excavation may be needed.");
                placementPoint = tiles.GetCellCenterWorld(tiles.WorldToCell(player.transform.position) +
                    new Vector3Int(placementProbe % 9 - 4, placementProbe / 9 - 4, 0));
                placementProbe++;
                if (BedWarmRun && !FindAnyObjectByType<MainGameRuntimeServices>().Bed.CanSleep(placementPoint, out _, out _)) break;
                if (Vector2.Distance(player.transform.position, placementPoint) <= 2.3f) Next(503);
                break;
            case 503:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false);
                if (Time.realtimeSinceStartup - phaseStarted >= .1f)
                    Next(FindAnyObjectByType<MainGameTurretRuntime>().IsPlacementPreviewValid ? 504 : 502);
                break;
            case 504:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, true);
                Next(505);
                break;
            case 505:
                Sample();
                if (BedUseRun)
                {
                    placementPoint = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "nest_bed").position;
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("nest_bed") == 0, "bed placement consumes the normally earned item");
                    checks.Add($"Bed installed at {placementPoint} after {Time.realtimeSinceStartup - controlStarted:F3}s.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "bed-installed.png"));
                    if (BedAutoOnlyRun)
                    {
                        placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "furnace");
                        bedOutputBefore = FindAnyObjectByType<MainGameRuntimeServices>().StationProduction.PendingCount("iron_ingot") + FindAnyObjectByType<MainGameRuntimeServices>().Furnace.Completed.Where(r => r.item.Id == "iron_ingot").Sum(r => r.amount);
                        toolNavigationAttempts = 0;
                        Next(600); break;
                    }
                    Next(970); break;
                }
                placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects()
                    .Single(r => r.definitionId == SmeltBuilding);
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(SmeltBuilding) == 0,
                    "furnace placement consumes one earned item");
                checks.Add($"{SmeltBuilding} installed after {Time.realtimeSinceStartup - controlStarted:F3}s at {placedFurnace.position}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "furnace-installed.png"));
                Next(installCoreRun ? 800 : 506);
                break;
            case 800:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var coreState = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position);
                Require(coreState.HasCore && coreState.InRange, "normally installed core covers actual player position");
                Require(coreState.CoreDelta == (coreState.Sealed ? -10 : -5), "installed core matches current configured cooling rules");
                checks.Add($"Core state: sealed={coreState.Sealed}, delta={coreState.CoreDelta}, leak={coreState.HasLeak} {coreState.Leak}; room={FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.Resolve(player.transform.position)}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "ice-core-cooling-guide.png"));
                Next(420);
                break;
            case 810:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedWorkbench.position), false, false, KeyCode.E);
                Next(811); break;
            case 811:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput, "normal workbench opens for seal wall");
                var wallUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var wallRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController).GetField("filteredRecipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wallUi);
                var wallIndex = wallRecipes.FindIndex(r => r.Id == WallRecipe);
                var selectedWall = (int)typeof(MainGameCraftingUiController).GetField("selectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wallUi);
                Require(wallIndex >= 0 && ++toolNavigationAttempts < 128, "bounded seal wall recipe navigation");
                if (selectedWall != wallIndex) { bridgeDirection = selectedWall < wallIndex ? 1f : -1f; Next(812); break; }
                if (BedUseRun)
                {
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("nest_bed") == 1, "normally crafted bed available for installation");
                    Next(816); break;
                }
                if (DoorInstallRun)
                {
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("door") == 1, "normally crafted door available for installation");
                    wallBefore = 0;
                    wallCraftPassed = true;
                    Next(816); break;
                }
                var wallInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                savedStone = wallInventory.Count(DoorCraftRun || SatbaCraftRun ? "wood" : "stone"); savedDirt = wallInventory.Count(DoorCraftRun || SatbaCraftRun ? "hemp_stalk" : "dirt"); wallBefore = wallInventory.Count(WallRecipe);
                if(StorageCraftRun){storageStoneBefore=wallInventory.Count("stone");storageDirtBefore=wallInventory.Count("dirt");}
                coreIceBefore = wallInventory.Count(SatbaCraftRun ? "club_shard" : "ice_shard");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "seal-wall-ready.png"));
                Next(813); break;
            case 812:
                Sample(0f, bridgeDirection > 0f ? KeyCode.DownArrow : KeyCode.UpArrow);
                Next(811); break;
            case 813:
                Sample(0f, KeyCode.E); toolStageStarted = Time.realtimeSinceStartup;
                Next(814); break;
            case 814:
                Sample();
                var wallServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(wallServices.StationProduction.Export().Any(s => s.jobs.Count > 0 && s.jobs[0].recipeId == WallRecipe), "normal E starts requested building craft");
                if(StorageCraftRun)
                {
                    Require(wallServices.PlayerInventory.Count("wood")==savedStone-6 && wallServices.PlayerInventory.Count("stone")==storageStoneBefore-8 && wallServices.PlayerInventory.Count("dirt")==storageDirtBefore-10,"jangdok consumes ordinary wood6 stone8 dirt10");
                    Next(815);break;
                }
                Require(BedCraftRun ? wallServices.PlayerInventory.Count("wood") == savedStone - 6 && wallServices.PlayerInventory.Count("hemp_stalk") == savedDirt - 4 : SatbaCraftRun ? wallServices.PlayerInventory.Count("wood") == savedStone - 5 && wallServices.PlayerInventory.Count("hemp_stalk") == savedDirt - 10 && wallServices.PlayerInventory.Count("club_shard") == coreIceBefore - 1 : DoorCraftRun ? wallServices.PlayerInventory.Count("wood") == savedStone - 6 && wallServices.PlayerInventory.Count("hemp_stalk") == savedDirt - 4 && wallServices.PlayerInventory.Count("ice_shard") == coreIceBefore - 4 : wallServices.PlayerInventory.Count("stone") == savedStone - 2 && wallServices.PlayerInventory.Count("dirt") == savedDirt - 1,
                    BedCraftRun ? "bed consumes exactly wood6 hemp4" : SatbaCraftRun ? "satba consumes wood5 hemp10 club1" : DoorCraftRun ? "door consumes wood6 hemp4 ice4" : "seal wall consumes exactly two stone and one dirt");
                Next(815); break;
            case 815:
                Sample();
                if (FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(WallRecipe) != wallBefore + 1) return;
                checks.Add($"{WallRecipe} crafted after {Time.realtimeSinceStartup - toolStageStarted:F3}s; segment {Time.realtimeSinceStartup - controlStarted:F3}s.");
                wallCraftPassed = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "seal-wall-crafted.png"));
                if (InvasionRepairRun) { Next(1031); break; }
                Next(DoorCraftRun || SatbaCraftRun ? 414 : 816); break;
            case 816:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                if (SessionState.GetBool("Nyangbingo.QA.WallRepair", false))
                {
                    if (!EnclosureRun) mineCell = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position).Leak;
                    placementPoint = tiles.GetCellCenterWorld(mineCell);
                    checks.Add($"Repair target {mineCell}: {tiles.GetTile(mineCell).elementType}.");
                    Next(829); break;
                }
                ((Button)typeof(MainGameCraftingUiController).GetField("collectButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameCraftingUiController>())).onClick.Invoke();
                placementProbe = 0;
                Next(BedUseRun ? 501 : 817); break;
            case 817:
                Sample();
                Require(FindAnyObjectByType<MainGameTilePaletteController>().IsForegroundPlacementActive, "seal wall installation button opens ordinary tile preview");
                if(SessionState.GetBool("Nyangbingo.QA.StorageFloorRepair",false))
                {
                    Require(placementProbe++<3,"destroyed storage floor accepts normal wall placement");
                    placementPoint=tiles.GetCellCenterWorld(new Vector3Int(297,119,0));
                    Next(818);break;
                }
                if (SessionState.GetBool("Nyangbingo.QA.StorageDoorInstall",false))
                {
                    Require(placementProbe++ < 3,"planned storage door position accepts normal placement");
                    placementPoint=tiles.GetCellCenterWorld(new Vector3Int(296,120,0));
                    Next(818);break;
                }
                if (placementProbe >= 81) throw new Exception("No valid local seal-wall position; normal excavation required.");
                placementPoint = tiles.GetCellCenterWorld(tiles.WorldToCell(player.transform.position) + new Vector3Int(placementProbe % 9 - 4, placementProbe / 9 - 4, 0));
                placementProbe++;
                if (Vector2.Distance(player.transform.position, placementPoint) <= 2.3f) Next(818);
                break;
            case 818:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false);
                if (Time.realtimeSinceStartup - phaseStarted < .15f) return;
                Next((bool)typeof(MainGameTilePaletteController).GetField("foregroundPlacementValid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameTilePaletteController>()) ? 819 : 817);
                break;
            case 819:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, true);
                Next(820); break;
            case 820:
                Sample();
                Require(tiles.GetTile(tiles.WorldToCell(placementPoint)).elementType == WallRecipe, "normal building placement creates requested tile at preview position");
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(WallRecipe) == wallBefore, "normal building placement consumes exactly one earned item");
                checks.Add($"Seal wall installed at {placementPoint}; segment {Time.realtimeSinceStartup - controlStarted:F3}s.");
                Next(821); break;
            case 821:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var wallInspection = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position);
                checks.Add($"After normal wall placement: sealed={wallInspection.Sealed}, delta={wallInspection.CoreDelta}, leak={wallInspection.HasLeak} {wallInspection.Leak}.");
                if (!wallInspection.Sealed) Require(GameObject.Find("ShelterGuide")?.GetComponent<Text>()?.text.Contains("직접 놓은 흙·돌") == true, "unsealed guide distinguishes placed soil from natural seal boundary");
                wallsRepaired++;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "seal-wall-installed.png"));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"seal-repair-{wallsRepaired:00}.png"));
                Next(822); break;
            case 822:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                if (DoorInstallRun) { Next(840); break; }
                if (EnclosureRun) { Next(861); break; }
                if (SessionState.GetBool("Nyangbingo.QA.WallChain", false))
                {
                    var nextRepair = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position);
                    checks.Add($"Continuous repair {wallsRepaired}: player={player.transform.position}, next={nextRepair.Leak}, sealed={nextRepair.Sealed}.");
                    if (!nextRepair.Sealed && nextRepair.HasLeak && wallsRepaired < 20 &&
                        Vector2.Distance(player.transform.position, tiles.GetCellCenterWorld(nextRepair.Leak)) < 3f)
                    {
                        toolNavigationAttempts = 0;
                        Next(836); break;
                    }
                    checks.Add("Continuous repair checkpoint: sealed or local reach/count boundary reached; no completion assumed.");
                }
                Next(420); break;
            case 880:
                Sample(0f, KeyCode.Alpha7);
                Next(401); break;
            case 881:
                Sample(Mathf.Sign(placedWorkbench.position.x - player.transform.position.x), KeyCode.Space);
                if (Mathf.Abs(player.transform.position.x - placedWorkbench.position.x) < 1f || Time.realtimeSinceStartup - phaseStarted > .6f) Next(401);
                break;
            case 870:
                Sample(Mathf.Abs(player.transform.position.x - 298f) > .1f ? Mathf.Sign(298f - player.transform.position.x) : 0f);
                if (Mathf.Abs(player.transform.position.x - 298f) < .15f) Next(871);
                else if (Time.realtimeSinceStartup - phaseStarted > 3f) throw new Exception("Cannot approach earned door normally.");
                break;
            case 871:
                Sample(0f, KeyCode.Alpha7);
                CheckRestoredShelterDoor(false);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "shelter-restored-closed-initial.png"));
                Next(872); break;
            case 872:
                if (Time.realtimeSinceStartup - phaseStarted < .6f) { Sample(); return; }
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(new Vector3(296.5f, 120.5f)), false, false, KeyCode.E);
                Next(873); break;
            case 873:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                CheckRestoredShelterDoor(true);
                shelterRestoreStage = 1;
                Next(420); break;
            case 874:
                Sample();
                var shelterTitle = FindAnyObjectByType<TitleShellController>();
                if (shelterTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(shelterTitle.TryContinue(), "title Continue restores earned shelter");
                Next(875); break;
            case 875:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                CheckRestoredShelterDoor(shelterRestoreStage == 1);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, shelterRestoreStage == 1 ? "shelter-restored-open.png" : "shelter-restored-closed.png"));
                Next(shelterRestoreStage == 1 ? 876 : 878); break;
            case 876:
                if (Time.realtimeSinceStartup - phaseStarted < .6f) { Sample(); return; }
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(new Vector3(296.5f, 120.5f)), false, false, KeyCode.E);
                Next(877); break;
            case 877:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                CheckRestoredShelterDoor(false);
                shelterRestoreStage = 2;
                Next(420); break;
            case 878:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                Finish("passed", "Earned complete room: both door cells, normal open-close cooling -5/-10 and title Continue after each state passed. OS restart and novice understanding unverified.");
                break;
            case 1300:
                Sample(Mathf.Abs(player.transform.position.x-299.5f)>.1f?Mathf.Sign(299.5f-player.transform.position.x):0f);
                if(Mathf.Abs(player.transform.position.x-299.5f)>.15f) break;
                var recoolObjects=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects();
                placementPoint=recoolObjects.Single(x=>x.definitionId=="ice_core").position;
                storageTestId=recoolObjects.Single(x=>x.definitionId=="jangdok").objectId;
                var recoolSource=FindAnyObjectByType<MainGameRuntimeServices>();
                Require(recoolSource.Invasion.CanRecoolNow && Mathf.Approximately(recoolSource.Invasion.TemperatureRiseCelsius,.5f),"actual day7 damage restores pending half-degree heat");
                Require(recoolSource.PlayerInventory.Count("ice_shard")==3,"actual source has three earned bag ice");
                Require(recoolSource.JangdokStorage.TryGet(storageTestId,out var sourceIce) && sourceIce.Count("ice_shard")==2,"actual dawn loss restores two stored ice");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"recool-before.png"));
                Next(1301);break;
            case 1301:
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(1302);break;
            case 1302:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<1f) break;
                var recoolAfter=FindAnyObjectByType<MainGameRuntimeServices>();
                Require(!recoolAfter.Invasion.HasPendingRecool && recoolAfter.Invasion.TemperatureRiseCelsius==0,"ordinary core right click removes actual invasion heat");
                Require(recoolAfter.PlayerInventory.Count("ice_shard")==2,"half-degree recool consumes exactly one bag ice");
                Require(recoolAfter.StorageTemperature.TryGetStatus(storageTestId,out var recoolTemp,out _) && Mathf.Approximately(recoolTemp,-5f),"recool alone returns leaky storage to minus5 not frozen minus10");
                Require(recoolAfter.JangdokStorage.TryGet(storageTestId,out var recoolIce) && recoolIce.Count("ice_shard")==2,"recool does not refund melted stored ice");
                checks.Add($"Normal recool completed: elapsed={Time.realtimeSinceStartup-controlStarted:F3}s; heat=0; bagIce=2; storedIce=2; temperature={recoolTemp}.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"recool-after.png"));
                Next(1303);break;
            case 1303:
                Sample(0f,KeyCode.Escape);Next(1304);break;
            case 1304:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().Screen==GameShellScreen.Pause,"recool opens ordinary pause");
                ((Button)typeof(MainGameShellUiController).GetField("pauseSaveButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>())).onClick.Invoke();
                Next(1305);break;
            case 1305:
                Sample();
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _,out var recoolDisk) && recoolDisk.invasionTemperatureRise==0 && recoolDisk.day==7,"ordinary save persists removed heat on day7");
                var recoolShell=FindAnyObjectByType<GameShellController>();
                Require(recoolShell.RequestReturnToTitle() && recoolShell.Confirm(),"ordinary Title return after recool");
                Next(1306);break;
            case 1306:
                Sample();
                if(SceneManager.GetActiveScene().name!="Title" || Time.realtimeSinceStartup-phaseStarted<1f) break;
                Require(FindAnyObjectByType<TitleShellController>().TryContinue(),"normal Continue after recool");
                Next(1307);break;
            case 1307:
                Sample();
                if(SceneManager.GetActiveScene().name!="MainGame" || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup-phaseStarted<8f) break;
                var recoolRestored=FindAnyObjectByType<MainGameRuntimeServices>();
                Require(!recoolRestored.Invasion.HasPendingRecool && recoolRestored.PlayerInventory.Count("ice_shard")==2,"Continue preserves recool and exact bag cost");
                Require(recoolRestored.JangdokStorage.TryGet(storageTestId,out var recoolRestoredIce) && recoolRestoredIce.Count("ice_shard")==2,"Continue preserves stored ice without refund");
                Require(recoolRestored.StorageTemperature.TryGetStatus(storageTestId,out var recoolRestoredTemp,out _) && Mathf.Approximately(recoolRestoredTemp,-5f),"Continue preserves leaky minus5 storage");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"recool-continued.png"));
                Next(1308);break;
            case 1308:
                Sample();if(Time.realtimeSinceStartup-phaseStarted<.6f)break;
                Finish("passed","Actual invasion heat removed by normal core right click, one earned bag ice consumed, ordinary save and Continue preserved heat0/bag2/stored2/leaky minus5. No wall repair, restored freezing, repeat invasion or novice proof.");break;
            case 1310:
                Sample();
                if(SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup-phaseStarted<3f) break;
                var outingServices=FindAnyObjectByType<MainGameRuntimeServices>();
                var outingJar=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="jangdok");
                Require(outingServices.StorageTemperature.TryGetStatus(outingJar.objectId,out var outingTemp,out var outingBand),"actual outing storage temperature is available");
                Require(outingServices.JangdokStorage.TryGet(outingJar.objectId,out var outingIce) && outingIce.Count("ice_shard")==2,"outbound save restores two stored ice before next dawn");
                checks.Add($"Outing storage actual temperature={outingTemp}; band={outingBand}; ice={outingIce.Count("ice_shard")}; heatPending={outingServices.Invasion.HasPendingRecool}; player={player.transform.position}.");
                var outingGuide=GameObject.Find("ShelterGuide")?.GetComponent<Text>();
                Require(outingGuide!=null && outingGuide.isActiveAndEnabled && outingGuide.text.StartsWith("저장고 미밀폐 · 문·벽·지붕을 점검하세요.") && outingGuide.text.Contains("직접 놓은 흙·돌은 밀폐 벽이 아닙니다.") && outingGuide.text.Contains("보관함의 실제 온도"),"outside leaky guide prioritizes boundary inspection without claiming inventory loss");
                checks.Add("Outing guide: "+outingGuide.text);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"outing-storage-inspected.png"));
                Next(1311);break;
            case 1311:
                Sample();if(Time.realtimeSinceStartup-phaseStarted<1f)break;
                Finish("passed","Read-only storage and guide inspection after normal Continue of actual outing save. No repair, jar transfer, time skip or new save; no daily preservation claim.");break;
            case 1296:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<3f) break;
                var coldGuide=GameObject.Find("ShelterGuide")?.GetComponent<Text>();
                Require(coldGuide!=null && coldGuide.isActiveAndEnabled && coldGuide.text.StartsWith("저체온 위험 · 먼저 따뜻한 곳으로 이동하세요."),"actual sealed cold guide prioritizes warming over expansion");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"shelter-cold-safety-guide.png"));
                Next(1295);break;
            case 1295:
                Sample(Mathf.Abs(player.transform.position.x-301.4f)>.1f?Mathf.Sign(301.4f-player.transform.position.x):0f);
                if(Mathf.Abs(player.transform.position.x-301.4f)<.15f)
                {
                    var preserveObjects=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects();
                    placementPoint=preserveObjects.Single(x=>x.definitionId=="nest_bed").position;
                    storageTestId=preserveObjects.Single(x=>x.definitionId=="jangdok").objectId;
                    var preserveServices=FindAnyObjectByType<MainGameRuntimeServices>();
                    Require(preserveServices.StorageTemperature.TryGetStatus(storageTestId,out var preserveTemp,out _) && preserveTemp<=-10,"actual stored ice is in frozen-temperature storage before rest");
                    Require(preserveServices.JangdokStorage.TryGet(storageTestId,out var preserveInventory) && preserveInventory.Count("ice_shard")==PreservedIceCount && Mathf.Approximately(preserveInventory.Slots.Where(x=>x.itemId=="ice_shard").Sum(x=>x.storageMeltRemainder),PreservedMeltRemainder),"source retains expected actual stored ice and melt remainder");
                    checks.Add($"Before sealed storage rest: temperature={preserveTemp}; ice={PreservedIceCount}; remainder={PreservedMeltRemainder}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}s.");
                    Next(970);
                }
                else if(Time.realtimeSinceStartup-phaseStarted>6f) throw new Exception($"Cannot reach warm bed normally from sealed room; player={player.transform.position}.");
                break;
            case 1292:
                var approachObstacles = new[] { new Vector3Int(298,121,0),new Vector3Int(298,120,0) };
                var approachObstacle = approachObstacles.FirstOrDefault(c => !tiles.GetTile(c).IsAir);
                if(approachObstacle != default)
                {
                    Require(tiles.GetTile(approachObstacle).elementType=="stone" && !tiles.GetTile(approachObstacle).isNaturalTerrain,"approach cleanup targets only previously placed stone");
                    mineCell=approachObstacle;Next(1293);break;
                }
                var doorApproachMove = Mathf.Abs(player.transform.position.x - 298.1f) > .1f ? Mathf.Sign(298.1f - player.transform.position.x) : 0f;
                if ((Time.realtimeSinceStartup-phaseStarted)% .8f < .4f) Sample(doorApproachMove,KeyCode.Space); else Sample(doorApproachMove);
                if(Mathf.Abs(player.transform.position.x - 298.1f) < .15f) { toolNavigationAttempts=0;Next(810); }
                else if(Time.realtimeSinceStartup-phaseStarted > 10f) throw new Exception($"Cannot approach planned storage door with ordinary movement; player={player.transform.position}.");
                break;
            case 1293:
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(mineCell)),false,false,KeyCode.Alpha7);
                if(Time.realtimeSinceStartup-phaseStarted < .2f) break;
                var cleanupArgs = new object[] { tiles,default(Vector3Int) };
                var cleanupKind = typeof(MainGamePlayerController).GetMethod("ResolveMiningWorldTarget",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(player,cleanupArgs);
                Require(cleanupKind.ToString()=="Tile" && (Vector3Int)cleanupArgs[1]==mineCell,"actual mining target is scaffold tile, not storage or core");
                Next(1294);break;
            case 1294:
                if(tiles.GetTile(mineCell).IsAir)
                {
                    Sample();
                    var cleanupObjects=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects();
                    Require(cleanupObjects.Any(x=>x.definitionId=="jangdok") && cleanupObjects.Any(x=>x.definitionId=="ice_core"),"normal scaffold cleanup preserves storage and core");
                    checks.Add($"Normal scaffold cleanup at {mineCell}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}s.");
                    Next(1292);break;
                }
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(mineCell)),true,false);
                if(Time.realtimeSinceStartup-phaseStarted>6f) throw new Exception("Normal scaffold cleanup did not clear selected stone.");
                break;
            case 860:
                var enclosureWorkX=SessionState.GetBool("Nyangbingo.QA.StorageEnclose",false)?300f:299f;
                Sample(Mathf.Abs(player.transform.position.x - enclosureWorkX) > .1f ? Mathf.Sign(enclosureWorkX - player.transform.position.x) : 0f);
                if (Mathf.Abs(player.transform.position.x - enclosureWorkX) <= .15f) Next(861);
                else if (Time.realtimeSinceStartup - phaseStarted > 3f) throw new Exception("Cannot reach shelter work position normally.");
                break;
            case 861:
                Sample();
                var boundaryCells = new List<Vector3Int>();
                var boundaryRight=SessionState.GetBool("Nyangbingo.QA.StorageEnclose",false)?303:302;
                for (var bx = 296; bx <= boundaryRight; bx++) boundaryCells.Add(new Vector3Int(bx, 122, 0));
                for (var by = 120; by <= 121; by++) boundaryCells.Add(new Vector3Int(boundaryRight, by, 0));
                for (var bx = 296; bx <= boundaryRight; bx++) boundaryCells.Add(new Vector3Int(bx, 119, 0));
                var boundaryEnvironment = FindAnyObjectByType<MainGameEnvironmentState>();
                var unresolved = boundaryCells.Where(c =>
                {
                    var t = tiles.GetTile(c);
                    return !t.isNaturalTerrain && t.elementType != "insul_wall" && t.elementType != "iron_insul_wall" && t.elementType != "roof" && !boundaryEnvironment.IsRecognizedBarrier(c);
                }).ToArray();
                if (unresolved.Length == 0) { Next(862); break; }
                Require(wallsRepaired < 20, "planned shelter boundary remains bounded");
                mineCell = unresolved[0];
                checks.Add($"Planned shelter boundary remaining={unresolved.Length}, next={mineCell}, tile={tiles.GetTile(mineCell).elementType}.");
                toolNavigationAttempts = 0;
                Next(810); break;
            case 862:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var completedShelter = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position);
                checks.Add($"Planned boundary finished: sealed={completedShelter.Sealed}, delta={completedShelter.CoreDelta}, leak={completedShelter.HasLeak} {completedShelter.Leak}.");
                wallCraftPassed = completedShelter.Sealed && completedShelter.CoreDelta == -10;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "shelter-boundary-finished.png"));
                Next(863); break;
            case 863:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                Next(420); break;
            case 840:
                Sample(0f, KeyCode.Alpha7);
                var installedDoorCell = tiles.WorldToCell(placementPoint);
                Require(!tiles.IsDoorOpen(installedDoorCell), "newly installed door is closed");
                Require(FindAnyObjectByType<MainGameEnvironmentState>().IsRecognizedBarrier(installedDoorCell), "closed door participates in seal boundary registry");
                Next(841); break;
            case 841:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(842); break;
            case 842:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(tiles.IsDoorOpen(tiles.WorldToCell(placementPoint)), "normal right click opens earned door");
                Require(!FindAnyObjectByType<MainGameEnvironmentState>().IsRecognizedBarrier(tiles.WorldToCell(placementPoint)), "open door no longer seals");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"door-open-{doorToggleCycles + 1}.png"));
                Next(843); break;
            case 843:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Next(844); break;
            case 844:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(845); break;
            case 845:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(!tiles.IsDoorOpen(tiles.WorldToCell(placementPoint)), "normal right click closes earned door");
                Require(FindAnyObjectByType<MainGameEnvironmentState>().IsRecognizedBarrier(tiles.WorldToCell(placementPoint)), "closed door returns to seal boundary registry");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"door-closed-{++doorToggleCycles}.png"));
                Next(846); break;
            case 846:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Next(doorToggleCycles < 3 ? 841 : 847); break;
            case 847:
                Sample(-1f);
                if (Time.realtimeSinceStartup - phaseStarted < 1.2f) return;
                Require(player.transform.position.x > placementPoint.x + .45f, "closed door blocks actual leftward player movement");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "door-blocked.png"));
                Next(848); break;
            case 848:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(849); break;
            case 849:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(tiles.IsDoorOpen(tiles.WorldToCell(placementPoint)), "door opened for physical crossing");
                Next(850); break;
            case 850:
                Sample(-1f);
                if (player.transform.position.x < placementPoint.x - .8f)
                {
                    checks.Add($"Actual outward door crossing at player={player.transform.position}.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "door-crossed-out.png"));
                    Next(851); break;
                }
                if (Time.realtimeSinceStartup - phaseStarted > 3f) throw new Exception("Open door did not permit outward crossing.");
                break;
            case 851:
                Sample(1f);
                if (player.transform.position.x > placementPoint.x + 1.2f)
                {
                    checks.Add($"Actual return door crossing at player={player.transform.position}.");
                    Next(852); break;
                }
                if (Time.realtimeSinceStartup - phaseStarted > 3f) throw new Exception("Open door did not permit return crossing.");
                break;
            case 852:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(853); break;
            case 853:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Require(!tiles.IsDoorOpen(tiles.WorldToCell(placementPoint)), "door closes after actual return crossing");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "door-returned-closed.png"));
                Next(854); break;
            case 854:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                if(SessionState.GetBool("Nyangbingo.QA.ReplacementDoorInstall",false))
                {
                    var repairSealServices=FindAnyObjectByType<MainGameRuntimeServices>();
                    var repairJar=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="jangdok");
                    var repairSeal=repairSealServices.RoomTemperature.InspectShelter(repairJar.position);
                    repairSealServices.StorageTemperature.TryGetStatus(repairJar.objectId,out var repairJarTemp,out _);
                    Require(repairSealServices.JangdokStorage.TryGet(repairJar.objectId,out var repairJarIce) && repairJarIce.Count("ice_shard")==2,"replacement door route preserves two stored ice");
                    checks.Add($"Replacement door seal: sealed={repairSeal.Sealed}; coreDelta={repairSeal.CoreDelta}; jarTemperature={repairJarTemp}; heat={repairSealServices.Invasion.TemperatureRiseCelsius}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}.");
                }
                Next(420); break;
            case 836:
                // Move over an already repaired floor before excavating the current footing.
                var nextFloor = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position).Leak;
                if (Mathf.Abs(nextFloor.x + .5f - player.transform.position.x) < .75f && nextFloor.y < player.transform.position.y)
                {
                    var coreX = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "ice_core").position.x;
                    Sample(Mathf.Sign(coreX - player.transform.position.x));
                    if (Time.realtimeSinceStartup - phaseStarted > 3f) throw new Exception("Could not move safely off next leak floor.");
                    break;
                }
                Sample();
                Next(810); break;
            case 829:
                Sample(0f, KeyCode.Escape);
                Next(830); break;
            case 830:
                Sample(0f, KeyCode.Alpha7);
                Next(831); break;
            case 831:
                Require(FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Any(r => r.definitionId == "ice_core"), "repair mining preserves installed core");
                if (tiles.GetTile(mineCell).IsAir) { Sample(); Next(832); break; }
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), true, false);
                if (Time.realtimeSinceStartup - phaseStarted > 12f) throw new Exception("Normal leak excavation failed; inspect mining target and reach.");
                break;
            case 832:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedWorkbench.position), false, false, KeyCode.E);
                Next(833); break;
            case 833:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                var repairUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var repairRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController).GetField("filteredRecipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(repairUi);
                var repairIndex = (int)typeof(MainGameCraftingUiController).GetField("selectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(repairUi);
                var repairTarget = repairRecipes.FindIndex(r => r.Id == "insul_wall");
                Require(repairTarget >= 0 && ++toolNavigationAttempts < 128, "repair wall recipe remains available");
                if (repairIndex != repairTarget) { bridgeDirection = repairIndex < repairTarget ? 1 : -1; Next(834); break; }
                ((Button)typeof(MainGameCraftingUiController).GetField("collectButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(repairUi)).onClick.Invoke();
                Next(835); break;
            case 834:
                Sample(0f, bridgeDirection > 0 ? KeyCode.DownArrow : KeyCode.UpArrow);
                Next(833); break;
            case 835:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false);
                if (Time.realtimeSinceStartup - phaseStarted < .2f) return;
                Require((bool)typeof(MainGameTilePaletteController).GetField("foregroundPlacementValid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameTilePaletteController>()), "excavated leak accepts earned seal wall");
                Next(819); break;
            case 801:
                Sample();
                var coreTitle = FindAnyObjectByType<TitleShellController>();
                if (coreTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(coreTitle.TryContinue(), "normal title Continue reloads installed ice core");
                Next(802); break;
            case 802:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 2f) return;
                var loadedCore = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Where(r => r.objectId == placedFurnace.objectId).ToArray();
                Require(loadedCore.Length == 1 && loadedCore[0].definitionId == "ice_core" && Vector2.Distance(loadedCore[0].position, placedFurnace.position) < .01f,
                    "Continue restores same ice core identity and position once");
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ice_core") == 0, "Continue does not duplicate consumed core item");
                var loadedInspection = FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position);
                Require(loadedInspection.HasCore && loadedInspection.InRange, "core cooling coverage survives Continue");
                Next(803); break;
            case 803:
                Sample();
                // State restoration can precede the transition camera's final rendered frame.
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var restoredGuide = GameObject.Find("ShelterGuide")?.GetComponent<Text>();
                if (restoredGuide == null || !restoredGuide.text.Contains("코어 효과"))
                {
                    if (Time.realtimeSinceStartup - phaseStarted < 8f) return;
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "ice-core-restored-guide-failure.png"));
                    throw new Exception("Restored core exists but HUD guide did not update: " + restoredGuide?.text);
                }
                Require(true, "restored HUD guide reflects installed core rather than empty startup inventory");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "ice-core-restored.png"));
                Next(804); break;
            case 804:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                Finish("passed", "Normal ice-core installation, current cooling coverage and title Continue restored. Enclosure construction, novice understanding and OS process restart remain unverified.");
                break;
            case 506:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedFurnace.position), false, false, KeyCode.E);
                Next(507);
                break;
            case 507:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput, "right click opens installed furnace");
                Next(installAnvilRun ? 730 : 508);
                break;
            case 508:
                Sample(0f, KeyCode.Q);
                Next(509);
                break;
            case 509:
                Sample();
                var smeltUi = FindAnyObjectByType<MainGameCraftingUiController>();
                Require(smeltUi.FurnaceSmeltingViewActive, "Q switches furnace to smelting");
                var selectedSmelt = (Nyangbingo.Data.SmeltingDefinition)typeof(MainGameCraftingUiController)
                    .GetMethod("CurrentSmelting", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(smeltUi, null);
                if (selectedSmelt.Id != (foundrySmeltRun ? "smelt_icesteel" : "smelt_iron"))
                {
                    Require(++smeltNavigationAttempts <= 4, "smelting recipe navigation remains bounded");
                    Next(510); break;
                }
                smeltOreBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(SmeltOre);
                smeltCoalBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("coal");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "iron-smelting-ui.png"));
                Next(511);
                break;
            case 510:
                Sample(0f, KeyCode.RightArrow);
                Next(509);
                break;
            case 511:
                Sample(0f, KeyCode.E);
                smeltStarted = Time.realtimeSinceStartup;
                Next(512);
                break;
            case 512:
                Sample();
                var smeltServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(smeltServices.PlayerInventory.Count(SmeltOre) == smeltOreBefore - 2 &&
                    smeltServices.PlayerInventory.Count("coal") == smeltCoalBefore - (foundrySmeltRun ? 2 : 1),
                    $"normal smelt consumes two {SmeltOre} and {(foundrySmeltRun ? 2 : 1)} coal");
                Next(513);
                break;
            case 513:
                Sample();
                var smeltingRuntime = FindAnyObjectByType<MainGameRuntimeServices>();
                if ((foundrySmeltRun ? smeltingRuntime.Foundry : smeltingRuntime.Furnace).Completed.Count == 0)
                {
                    if (Time.realtimeSinceStartup - phaseStarted > (foundrySmeltRun ? 58f : 40f)) throw new Exception("Smelting exceeded the bounded real-time wait.");
                    return;
                }
                checks.Add($"{SmeltIngot} smelting completed after {Time.realtimeSinceStartup - smeltStarted:F3}s; awaiting separate collection.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "iron-awaiting-collection.png"));
                Next(514);
                break;
            case 514:
                Sample();
                ((Button)typeof(MainGameCraftingUiController).GetField("collectButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameCraftingUiController>())).onClick.Invoke();
                Next(515);
                break;
            case 515:
                Sample();
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(SmeltIngot) == 1,
                    $"normal collect button moves one {SmeltIngot} to inventory");
                smeltPassed = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "iron-collected.png"));
                Next(414);
                break;
            case 480:
                Sample();
                if (GangcheoriReturnRun && player.IsDead) throw new Exception("Normal loot return ended in death.");
                if (GangcheoriReturnRun && Time.realtimeSinceStartup - toolStageStarted > 300f) { checks.Add("Return horizontal300s limit; save partial progress."); Next(1122); break; }
                if (!GangcheoriReturnRun && Time.realtimeSinceStartup - (toolRun || deepRun ? toolStageStarted : controlStarted) > 180f)
                { checks.Add("Horizontal return exceeded 180 seconds."); Next(420); break; }
                var returnStation = deepRun && !T3GatherRun ? placedFurnace.position : placedWorkbench.position;
                if (Vector2.Distance(player.transform.position, returnStation) < 1.5f)
                { checks.Add($"Station reached at segment {Time.realtimeSinceStartup - controlStarted:F3}s."); Next(SessionState.GetBool("Nyangbingo.QA.CoolerOre",false) ? 1232 : SessionState.GetBool("Nyangbingo.QA.Day23Route", false) ? 1151 : GangcheoriReturnRun ? 1122 : T3GatherRun ? 420 : DoorCraftRun ? 810 : coreCraftRun || anvilRun ? 600 : deepRun ? 640 : toolRun ? 600 : 410); break; }
                if (!(bool)PlayerField("grounded")) return;
                var bridgeCurrent = tiles.WorldToCell(player.transform.position);
                bridgeDirection = Mathf.Sign(returnStation.x - player.transform.position.x);
                var bridgeNext = bridgeCurrent + new Vector3Int((int)bridgeDirection, 0, 0);
                if (!tiles.GetTile(bridgeNext).IsAir)
                { mineCell = bridgeNext; Next(481); break; }
                if (!tiles.GetTile(bridgeNext + Vector3Int.up).IsAir)
                { mineCell = bridgeNext + Vector3Int.up; Next(481); break; }
                ascentCell = bridgeNext + Vector3Int.down;
                if (tiles.GetTile(ascentCell).IsAir) Next(482);
                else Next(486);
                break;
            case 481:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(mineCell)), true, false);
                if (tiles.GetTile(mineCell).IsAir) Next(480);
                else if (Time.realtimeSinceStartup - phaseStarted > 12f)
                { checks.Add("Horizontal obstruction did not mine."); Next(420); }
                break;
            case 482:
                ascentStoneBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(AscentMaterial);
                Require(ascentStoneBefore > (deepRun ? 8 : 22), "bridge preserves crafting reserve");
                if (T3GatherRun)
                {
                    var bridgeSlots = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots;
                    var bridgeSlotIndex = -1;
                    for (var i = 0; i < Math.Min(8, bridgeSlots.Count); i++)
                        if (bridgeSlots[i].itemId == AscentMaterial && bridgeSlots[i].amount > 0) { bridgeSlotIndex = i; break; }
                    Require(bridgeSlotIndex >= 0, "earned bridge material is available in an actual hotbar slot");
                    Sample(0f, (KeyCode)((int)KeyCode.Alpha1 + bridgeSlotIndex));
                }
                else Sample(0f, deepRun ? KeyCode.Alpha1 : KeyCode.Alpha2);
                Next(483);
                break;
            case 483:
                BeginPlacementAudit();
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(ascentCell)), false, true);
                Next(484);
                break;
            case 484:
                Sample();
                VerifyPlacementAudit("horizontal bridge uses one earned block");
                Next(485);
                break;
            case 485:
                Sample(0f, MiningSlotKey);
                Next(486);
                break;
            case 486:
                Sample(bridgeDirection);
                if (Time.realtimeSinceStartup - phaseStarted >= .12f) Next(480);
                break;
            case 460:
                Sample();
                if (GangcheoriReturnRun && player.IsDead) throw new Exception("Normal loot ascent ended in death.");
                if (GangcheoriReturnRun && Time.realtimeSinceStartup - controlStarted > 450f) { checks.Add("Return ascent450s limit; save partial progress."); Next(1122); break; }
                if (bossReapproach && (player.IsDead || !FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive))
                { Next(904); break; }
                if (!GangcheoriReturnRun && Time.realtimeSinceStartup - (combatRun ? combatAscentStarted : toolRun || deepRun ? toolStageStarted : controlStarted) > 240f)
                { checks.Add("Ascent exceeded 240 seconds."); Next(420); break; }
                if (!(bool)PlayerField("grounded")) return;
                if (player.transform.position.y >= (SessionState.GetBool("Nyangbingo.QA.CoolerOre",false) ? 120.3f : combatRun || doorGatherRun || BossApproachRun ? combatElevation : placedWorkbench.position.y - .3f))
                {
                    if (BossApproachRun)
                    {
                        if (bossReapproach)
                        {
                            bossReapproach = false;
                            checks.Add($"Normal block reapproach reached at combat {Time.realtimeSinceStartup - bossFightStarted:F3}s; cumulative blocks={ascentSteps}, player={player.transform.position}");
                            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-reapproach.png"));
                            Next(934); break;
                        }
                        checks.Add($"Boss approach surface reached {Time.realtimeSinceStartup - combatAscentStarted:F3}s after ascent start; beforeSummon={BossSurfaceRun}; normal blocks={ascentSteps}, player={player.transform.position}");
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-approach-surface.png"));
                        Next(BossSurfaceRun ? 890 : 903); break;
                    }
                    if (doorGatherRun)
                    {
                        checks.Add($"Door route reached surface at {Time.realtimeSinceStartup - controlStarted:F3}s, normal ascent blocks={ascentSteps}.");
                        Next(761); break;
                    }
                    if (combatRun)
                    {
                        checks.Add($"Combat elevation reached at {Time.realtimeSinceStartup - combatStarted:F3}s with {ascentSteps} audited normal placements; position={player.transform.position}");
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "combat-surface.png"));
                        Next(SessionState.GetBool("Nyangbingo.QA.RewardCombat", false) ? 1216 : 749);
                        break;
                    }
                    if (SessionState.GetBool("Nyangbingo.QA.CoolerOre",false))
                    {
                        checks.Add($"Cooler ore ascent reached workshop elevation after {Time.realtimeSinceStartup-controlStarted:F3}s; normal blocks={ascentSteps}; player={player.transform.position}.");
                        controlStarted = Time.realtimeSinceStartup;
                        Next(480); break;
                    }
                    checks.Add($"Reached workbench elevation after {Time.realtimeSinceStartup - controlStarted:F3}s, {ascentSteps} ordinary block placements. Horizontal return remains unverified.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "ascent-elevation.png"));
                    toolStageStarted = Time.realtimeSinceStartup;
                    Next(toolRun || deepRun || DoorCraftRun || GangcheoriReturnRun ? 480 : 420);
                    break;
                }
                ascentCell = tiles.WorldToCell(player.transform.position);
                var ceilingFound = false;
                for (var up = 1; up <= 3; up++)
                    if (!tiles.GetTile(ascentCell + new Vector3Int(0, up, 0)).IsAir)
                    { mineCell = ascentCell + new Vector3Int(0, up, 0); ceilingFound = true; break; }
                if (ceilingFound) Next(461);
                else Next(462);
                break;
            case 461:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(mineCell)), true, false);
                if (tiles.GetTile(mineCell).IsAir) Next(460);
                else if (Time.realtimeSinceStartup - phaseStarted > 12f)
                { checks.Add($"Ascent ceiling mining failed at {mineCell}"); Next(420); }
                break;
            case 462:
                ascentStoneBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(AscentMaterial);
                Require(ascentStoneBefore > (deepRun ? 8 : 22), "ascent preserves crafting reserve");
                if (T3GatherRun)
                {
                    var ascentSlots = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots;
                    var ascentSlotIndex = -1;
                    for (var i = 0; i < Math.Min(8, ascentSlots.Count); i++)
                        if (ascentSlots[i].itemId == AscentMaterial && ascentSlots[i].amount > 0) { ascentSlotIndex = i; break; }
                    Require(ascentSlotIndex >= 0, "earned ascent material is available in an actual hotbar slot");
                    Sample(0f, (KeyCode)((int)KeyCode.Alpha1 + ascentSlotIndex));
                }
                else Sample(0f, deepRun ? KeyCode.Alpha1 : KeyCode.Alpha2);
                Next(463);
                break;
            case 463:
                Sample(0f, KeyCode.Space);
                if (player.transform.position.y > ascentCell.y + 1.5f) Next(464);
                else if (Time.realtimeSinceStartup - phaseStarted > 2f)
                { checks.Add("Ascent jump did not clear placement cell."); Next(466); }
                break;
            case 464:
                BeginPlacementAudit();
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(ascentCell)), true, false);
                Next(465);
                break;
            case 465:
                Sample();
                if ((combatRun || BossApproachRun || T3GatherRun || GangcheoriReturnRun) && placementAuditEvents == 0 && placementAuditRemoved == 0 && tiles.GetTile(ascentCell).IsAir)
                {
                    EndPlacementAudit();
                    checks.Add($"Combat ascent placement rejected without consumption at {ascentCell}; retry {++combatPlacementRetries}/12.");
                    Require(combatPlacementRetries <= 12, "combat ascent retries stay bounded");
                    Next(466);
                    break;
                }
                VerifyPlacementAudit($"ascent block {++ascentSteps}: normal click consumes exactly one {AscentMaterial}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "ascent-latest.png"));
                Next(466);
                break;
            case 466:
                Sample(0f, MiningSlotKey);
                Next(467);
                break;
            case 467:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted > .3f && (bool)PlayerField("grounded")) Next(460);
                break;
            case 401:
                Sample();
                if (player.IsDead) throw new Exception("Normal furnace route ended in player death; inspect route and damage before attributing cause.");
                var furnaceInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                var counts = new[] { furnaceInventory.Count("dirt"), furnaceInventory.Count("stone"),
                    furnaceInventory.Count("iron_ore"), furnaceInventory.Count("copper_ore"),
                    furnaceInventory.Count("clay"), furnaceInventory.Count("icesteel_ore"), furnaceInventory.Count("ice_shard") };
                var signature = counts.Aggregate(17, (value, count) => unchecked(value * 31 + count));
                if (T3GatherRun) signature = unchecked(signature * 31 + furnaceInventory.Count("frost_essence"));
                if (signature != furnaceInventorySignature)
                {
                    furnaceInventorySignature = signature;
                    checks.Add($"material route {Time.realtimeSinceStartup - furnaceStarted:F3}s: dirt={counts[0]}, stone={counts[1]}, iron={counts[2]}, copper={counts[3]}, clay={counts[4]}, icesteel={counts[5]}, ice={counts[6]}, position={player.transform.position}");
                    if (T3GatherRun) checks.Add($"T3 frost={furnaceInventory.Count("frost_essence")}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                }
                if (Time.realtimeSinceStartup - furnaceLastProgress > 15f)
                {
                    furnaceLastProgress = Time.realtimeSinceStartup;
                    File.WriteAllText(Path.Combine(directory, "progress.json"), JsonUtility.ToJson(new Result
                    { status = "running", wallSeconds = Time.realtimeSinceStartup - started, checks = checks.ToArray(), findings = findings.ToArray() }, true));
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "furnace-route-latest.png"));
                }
                if (coreGatherRun && counts[6] >= IceGatherGoal)
                {
                    coreGatherPassed = true;
                    checks.Add($"Ice-core gathering finished at {Time.realtimeSinceStartup - controlStarted:F3}s: ice={counts[6]}.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "core-ice-gathered.png"));
                    Next(420); break;
                }
                if (Time.realtimeSinceStartup - furnaceStarted > (SatbaCraftRun ? 180f : coreGatherRun ? 300f : deepRun ? 600f : 480f))
                {
                    checks.Add("Bounded material route exhausted; saving earned progress through pause UI. This is not proof of a game softlock.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "furnace-route-end.png"));
                    Next(420);
                    break;
                }
                var materialsReady = T3GatherRun ? counts[5] >= 10 && furnaceInventory.Count("frost_essence") >= 2 : !coreGatherRun && (deepRun
                    ? counts[1] >= 20 && counts[2] >= 6 && counts[4] >= 12 && counts[5] >= DeepOreRequired
                    : counts[0] >= 8 && counts[1] >= 22 && counts[2] >= 6 && counts[3] >= 4);
                if (!furnaceReturning && materialsReady)
                {
                    furnaceReturning = true;
                    checks.Add($"all furnace materials gathered at {Time.realtimeSinceStartup - controlStarted:F3}s, returning to actual workbench");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "furnace-materials.png"));
                    if (toolRun || deepRun)
                    {
                        checks.Add(deepRun ? "Natural deep ore and foundry resources collected; returning with ordinary dirt scaffolding."
                            : "Missing iron for remaining three iron ingots obtained; returning without consuming the reserves for another furnace.");
                        toolStageStarted = Time.realtimeSinceStartup;
                        Next(anvilRun ? 720 : 460);
                        break;
                    }
                }
                if (furnaceReturning && Vector2.Distance(player.transform.position, placedWorkbench.position) < (SatbaCraftRun ? 1.5f : 2.5f))
                {
                    if (SatbaCraftRun) checks.Add($"Satba workbench reached at {Time.realtimeSinceStartup - controlStarted:F3}s.");
                    Next(SatbaCraftRun ? 810 : 410);
                    break;
                }
                if (SatbaCraftRun && player.transform.position.y > placedWorkbench.position.y + 2f &&
                    Mathf.Abs(player.transform.position.x - placedWorkbench.position.x) > 1f)
                { Next(881); break; }
                if (TryChooseFurnaceRouteTile(counts, out mineCell))
                {
                    if (deepRun && tiles.GetTile(mineCell).elementType == WorldTileTypes.IceSteelOre)
                        checks.Add($"T2 targets natural icesteel at {mineCell}; hardness={tiles.GetTile(mineCell).hardness}");
                    Next(402);
                }
                else Next(404);
                break;
            case 402:
                if (tiles.GetTile(mineCell).IsAir) { Sample(); Next(403); break; }
                var aimedPoint = tiles.GetCellCenterWorld(mineCell);
                if(SessionState.GetBool("Nyangbingo.QA.ReplacementIceResume",false)) aimedPoint+=Vector3.up*.4f;
                var actualReach = (float)PlayerField("miningReach");
                if (!MainGamePlayerController.TryPickMiningCell(tiles, player.transform.position,
                    aimedPoint, aimedPoint - player.transform.position, actualReach, out var currentPicked) ||
                    currentPicked != mineCell)
                {
                    Sample();
                    checks.Add($"replan moved mining target {mineCell} at player={player.transform.position}, reach={actualReach}");
                    Next(405);
                    break;
                }
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(aimedPoint), true, false);
                if (Time.realtimeSinceStartup - phaseStarted > 12f)
                {
                    checks.Add($"Furnace mining timeout {mineCell}, player={player.transform.position}, type={tiles.GetTile(mineCell).elementType}, active={PlayerField("miningActive")}, required={PlayerField("miningRequiredSeconds")}, elapsed={PlayerField("miningElapsedSeconds")}, overUI={GameplayInput.IsPointerOverUi()}");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "furnace-mining-timeout.png"));
                    Next(420);
                }
                break;
            case 403:
                var pickupPoint = tiles.GetCellCenterWorld(mineCell);
                var dropRuntime = FindAnyObjectByType<MainGameWorldDropRuntime>();
                if (dropRuntime != null && dropRuntime.TryFindNearestStack(pickupPoint, 2f, out var pickupDrop))
                    pickupPoint = pickupDrop.position;
                var furnacePickupDelta = pickupPoint.x - player.transform.position.x;
                Sample(Mathf.Abs(furnacePickupDelta) > .3f ? Mathf.Sign(furnacePickupDelta) : 0f,
                    furnaceReturning && !SatbaCraftRun || pickupPoint.y > player.transform.position.y + .7f ? KeyCode.Space : KeyCode.None);
                if (Time.realtimeSinceStartup - phaseStarted >= 1.25f) Next(405);
                break;
            case 404:
                Sample(Mathf.Sign(furnaceRouteAim.x - player.transform.position.x), KeyCode.Space);
                if (Time.realtimeSinceStartup - phaseStarted >= .5f) Next(405);
                break;
            case 405:
                Sample();
                // Let an ordinary jump/fall finish before selecting a static mining target.
                if (Time.realtimeSinceStartup - phaseStarted >= .2f &&
                    ((bool)PlayerField("grounded") || Time.realtimeSinceStartup - phaseStarted > 1.5f)) Next(401);
                break;
            case 410:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedWorkbench.position), false, false, KeyCode.E);
                Next(411);
                break;
            case 411:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput, "normal return to workbench reopens station");
                var returningUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var returningRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController)
                    .GetField("filteredRecipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(returningUi);
                furnaceTargetIndex = returningRecipes.FindIndex(r => r.Id == "furnace");
                Require(furnaceTargetIndex >= 0, "returning workbench contains furnace recipe");
                Next(306);
                break;
            case 412:
                Sample();
                if (FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("furnace") == 1)
                {
                    checks.Add($"first furnace normally crafted at {Time.realtimeSinceStartup - controlStarted:F3}s; furnace segment {Time.realtimeSinceStartup - furnaceStarted:F3}s");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "first-furnace-crafted.png"));
                    Next(413);
                }
                else if (Time.realtimeSinceStartup - phaseStarted > 45f)
                    throw new Exception("Furnace did not finish within normal recipe duration plus tolerance.");
                break;
            case 413:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                furnaceCrafted = true;
                Next(414);
                break;
            case 414:
                Sample(0f, KeyCode.Escape);
                Next(415);
                break;
            case 415:
                Sample();
                Next(420);
                break;
            case 420:
                Sample(0f, KeyCode.Escape);
                Next(421);
                break;
            case 421:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Pause,
                    "bounded route opens pause for earned-progress save");
                ((Button)typeof(MainGameShellUiController).GetField("pauseSaveButton",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameShellUiController>())).onClick.Invoke();
                Next(422);
                break;
            case 422:
                if (T3CraftRun)
                {
                    Sample();
                    Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var t3CraftDisk) && t3CraftDisk.inventory.Where(s => s.itemId == "icesteel_claw").Sum(s => s.amount) == 1, "ordinary save contains crafted T3 claw");
                    var t3Shell = FindAnyObjectByType<GameShellController>();
                    Require(t3Shell.RequestReturnToTitle() && t3Shell.Confirm(), "normal Title return after T3 save");
                    Next(1055); break;
                }
                if (InvasionRepairRun)
                {
                    Sample();
                    Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var repairedDisk), "normal pause save writes repaired world");
                    foreach (var x in new[] { 297, 298 })
                        Require(repairedDisk.tileChanges.Last(r => r.x == x && r.y == 119).placed, "saved repaired floor " + x);
                    var repairedShell = FindAnyObjectByType<GameShellController>();
                    Require(repairedShell.RequestReturnToTitle() && repairedShell.Confirm(), "normal return after repair save");
                    Next(1035); break;
                }
                Sample();
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var routeSave) &&
                    routeSave.placedObjectRecords.Any(r => r.objectId == placedWorkbench.objectId),
                    "bounded route preserves earned progress in isolated save");
                if (BaekjungReachRun)
                {
                    Require(routeSave.day == 15 && routeSave.baekjungProgress != null && routeSave.baekjungProgress.nextWaveIndex >= 1 && !routeSave.baekjungProgress.hasEnded, "normal save preserves active day15 Baekjung first dispatched wave");
                    Finish("passed", "Earned day7 actual invasion dawn save advanced by ordinary bed confirmations to day15 night. Active first Baekjung wave and normal save inspected. Not all waves, battle victory, rewards, Continue or continuous day30 proof.");
                    break;
                }
                if (T3GatherRun)
                {
                    var t3Ore = routeSave.inventory.Where(s => s.itemId == "icesteel_ore").Sum(s => s.amount);
                    var t3Frost = routeSave.inventory.Where(s => s.itemId == "frost_essence").Sum(s => s.amount);
                    var t3Returned = Vector2.Distance(player.transform.position, placedWorkbench.position) < 2.5f;
                    checks.Add($"T3 saved ore={t3Ore}, frost={t3Frost}, returned={t3Returned}, position={player.transform.position}; segment={Time.realtimeSinceStartup - controlStarted:F3}s.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "t3-gather-saved.png"));
                    Finish(t3Ore >= 10 && t3Frost >= 2 && t3Returned ? "passed" : "failed", "Actual post-king T3 resource route saved. Only logged resources/return are verified; no T3 smelting/crafting, novice timing or full30-day completion.");
                    break;
                }
                if (StorageRestoreRun)
                {
                    Require(routeSave.jangdokStorages.Count==1 && routeSave.jangdokStorages[0].slots.Where(x=>x.itemId=="ice_shard").Sum(x=>x.amount)==4,"normal save retains actual four stored ice");
                    var storageShell=FindAnyObjectByType<GameShellController>();
                    Require(storageShell.RequestReturnToTitle() && storageShell.Confirm(),"normal storage return to title");Next(1290);break;
                }
                if (StorageUseRun)
                {
                    Require(routeSave.jangdokStorages.Count==1,"normal save contains actual storage contents");
                    Finish("passed","Normal earned storage placement, actual UI ice transfer, natural next dawn and ordinary save. Logged melt is open-core storage only; closed-room comparison and Continue remain separate.");break;
                }
                if (StorageCraftRun)
                {
                    Require(wallCraftPassed && routeSave.inventory.Where(x=>x.itemId=="jangdok").Sum(x=>x.amount)==1,"ordinary save contains normally crafted storage");
                    checks.Add($"Storage prepared: day={routeSave.day}; ice={routeSave.inventory.Where(x=>x.itemId=="ice_shard").Sum(x=>x.amount)}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}.");
                    Finish("passed","Normal return, jangdok crafting and ordinary save from earned shelter branch. Placement, actual storage transfer, daily melt and sealed-room benefit remain unverified.");break;
                }
                if (wallCraftRun)
                {
                    if (BedUseRun)
                    {
                        Require(routeSave.placedObjectRecords.Count(r => r.definitionId == "nest_bed") == 1 && !routeSave.inventory.Any(r => r.itemId == "nest_bed" && r.amount > 0), "save retains one installed bed without inventory duplication");
                        if (BedWarmRun)
                        {
                            Require(bedRestCount == 2 && routeSave.day == FindAnyObjectByType<MainGameBootstrap>().TimeService.Day, "save persists both ordinary rest transitions");
                            bedSavedDay = routeSave.day;
                            var warmReturn = FindAnyObjectByType<GameShellController>();
                            Require(warmReturn.RequestReturnToTitle() && warmReturn.Confirm(), "normal title return after bed save");
                            Next(983); break;
                        }
                        Finish("passed", bedSleepAllowed ? "Normal bed placement, rest confirmation cancellation and save passed. Actual sleep/time consumers/Continue unverified." : "Normal bed placement, temperature restriction and save passed. Warm-room sleep/time consumers/Continue unverified.");
                        break;
                    }
                    if (BedCraftRun)
                    {
                        Require(routeSave.inventory.Where(r => r.itemId == "nest_bed").Sum(r => r.amount) == (wallCraftPassed ? wallBefore + 1 : 0), "saved bed count matches completed craft");
                        Finish(wallCraftPassed ? "passed" : "failed", "Normal earned-material bed return/craft/save segment. Installation, sleep, time consumers and Continue remain unverified. Legacy seal-wall screenshots refer to nest_bed.");
                        break;
                    }
                    if (SatbaCraftRun)
                    {
                        Require(routeSave.inventory.Where(r => r.itemId == "ssireum_satba").Sum(r => r.amount) == (wallCraftPassed ? wallBefore + 1 : 0), "saved satba count matches completed craft");
                        Finish(wallCraftPassed ? "passed" : "failed", "Normal earned-material satba return/craft/save segment; summon and boss victory are separate unverified steps.");
                        break;
                    }
                    if (RestoreShelterRun)
                    {
                        var shelterShell = FindAnyObjectByType<GameShellController>();
                        Require(shelterShell.RequestReturnToTitle() && shelterShell.Confirm(), "return to title after shelter save");
                        Next(874); break;
                    }
                    if (DoorInstallRun)
                    {
                        Require(routeSave.inventory.Where(r => r.itemId == "door").Sum(r => r.amount) == 0, "saved inventory contains no duplicate consumed door");
                        Require(routeSave.tileChanges.Any(r => r.tileId == "door" && r.placed), "normal save retains installed door footprint");
                        Finish("passed", "Earned door normal installation, three open-close cycles, closed blocking and actual outward/return crossing saved. Full enclosure cooling remains unverified.");
                        break;
                    }
                    if (DoorCraftRun)
                    {
                        if(!wallCraftPassed) { Finish("failed","Normal bounded return saved partial progress before door craft. No crafted-door claim; resume this exact checkpoint.");break; }
                        Require(routeSave.inventory.Where(r => r.itemId == "door").Sum(r => r.amount) == wallBefore + 1, "normal save retains crafted door");
                        Require(routeSave.placedObjectRecords.Any(r => r.definitionId == "ice_core"), "return and craft preserve existing core");
                        Finish(wallCraftPassed ? "passed" : "failed", "Normal return and door craft saved. Installation and enclosure remain unverified.");
                        break;
                    }
                    Require(routeSave.inventory.Where(s => s.itemId == "insul_wall").Sum(s => s.amount) == wallBefore, "normal save retains wall inventory after placement");
                    var savedWallCell = tiles.WorldToCell(placementPoint);
                    Require(routeSave.tileChanges.Any(r => r.tileId == "insul_wall" && r.x == savedWallCell.x && r.y == savedWallCell.y && r.placed), "normal save retains installed seal wall tile at its actual position");
                    Finish(wallCraftPassed ? "passed" : "failed", EnclosureRun ? "Normal earned room boundary construction and saved seal result. Door-state reload regression is separate." : "Normal seal-wall crafting, placement and save; complete sealed enclosure not yet verified.");
                    break;
                }
                if (installCoreRun)
                {
                    Require(routeSave.placedObjectRecords.Any(r => r.objectId == placedFurnace.objectId && r.definitionId == "ice_core"), "normal save retains installed core");
                    var coreShell = FindAnyObjectByType<GameShellController>();
                    Require(coreShell.RequestReturnToTitle() && coreShell.Confirm(), "normal return to title after core save");
                    Next(801); break;
                }
                if (coreCraftRun)
                {
                    if (coreCraftPassed) Require(routeSave.inventory.Where(s => s.itemId == "ice_core").Sum(s => s.amount) == 1,
                        "normal save retains crafted ice core");
                    Finish(coreCraftPassed ? "passed" : "failed", "Earned ice-core return/craft segment saved. Installation and enclosure not tested in this segment.");
                    break;
                }
                if (coreGatherRun)
                {
                    Require(routeSave.inventory.Where(s => s.itemId == "ice_shard").Sum(s => s.amount) ==
                        FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ice_shard"), "normal save preserves ice shards");
                    Finish(coreGatherPassed ? "passed" : "failed", "Bounded ice-shard gathering saved. Core crafting, placement and shelter completion remain unverified.");
                    break;
                }
                if (harvestRun)
                {
                    var harvestInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                    foreach (var id in new[] { "wood", "hemp_stalk", "club_shard" })
                        Require(routeSave.inventory.Where(s => s.itemId == id).Sum(s => s.amount) == harvestInventory.Count(id),
                            "normal save preserves gathered " + id);
                    Finish(harvestPassed ? "passed" : "failed", "Bounded natural surface collection saved. Only recorded material gains are verified; satba crafting and boss fight remain unverified.");
                    break;
                }
                if (combatRun)
                {
                    var savedTears = routeSave.inventory.Where(s => s.itemId == "yokai_tear").Sum(s => s.amount);
                    Require(savedTears == FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("yokai_tear"), "normal save preserves current combat tear count");
                    if (T3CombatRun) Require(t3DamageObserved && t3SlowObserved, "actual T3 enemy damage and frost slow were observed before normal loot save");
                    Finish(combatPassed && savedTears > combatTearsBefore ? "passed" : "failed", "Natural combat observation and normal save complete. Only logged damage, loot and inventory checks prove combat progress; not a full night or 30-day completion.");
                    break;
                }
                if (foundrySmeltRun && smeltPassed)
                    Require(routeSave.placedObjectRecords.Any(r => r.objectId == placedFurnace.objectId && r.definitionId == "blast_furnace") &&
                        routeSave.inventory.Any(s => s.itemId == "icesteel_ingot" && s.amount == 1), "save retains installed foundry and collected ice-steel ingot");
                if (toolRun || deepRun)
                {
                    Require(routeSave.placedObjectRecords.Any(r => r.objectId == placedFurnace.objectId),
                        "earned furnace identity survives tool-route save");
                    if (toolPassed) Require(routeSave.inventory.Any(s => s.itemId == "iron_claw" && s.amount == 1),
                        "save retains crafted iron claw in inventory");
                    if (deepPassed) Require(routeSave.inventory.Any(s => s.itemId == "blast_furnace" && s.amount == 1) &&
                        routeSave.inventory.Any(s => s.itemId == "icesteel_ore" && s.amount >= deepOreBefore + 2),
                        "save retains normal foundry and deep ore");
                }
                if (anvilPassed) Require(routeSave.inventory.Any(s => s.itemId == "ice_anvil" && s.amount == 1), "save retains normally crafted ice anvil");
                Finish(anvilPassed || deepPassed || toolPassed || smeltPassed || furnaceCrafted ? "passed" : "failed", anvilPassed
                    ? "Normal mining, return, iron/copper/ice-steel smelting and ice-anvil crafting/save passed. Installation, combat and later progression unverified."
                    : deepPassed
                    ? "Normal T2 deep mining, collection, return and foundry crafting passed. Foundry installation, ice-steel smelting and later progression remain unverified."
                    : toolPassed
                    ? "Normal iron-claw material gathering, smelting, crafting, inventory-based tier upgrade and save passed. Deep mining and combat remain unverified."
                    : smeltPassed
                    ? $"Earned {SmeltBuilding} installed; normal {SmeltIngot} smelting, collection and save passed. Further equipment/progression unverified."
                    : furnaceCrafted
                    ? "Normal workbench return, furnace crafting and isolated save passed. Installation, smelting and full progression remain unverified."
                    : "Partial route checkpoint saved; full return and furnace completion not established by this segment.");
                break;
            case 600:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedFurnace.position), false, false, KeyCode.E);
                Next(601);
                break;
            case 601:
                if (Time.realtimeSinceStartup - phaseStarted < .3f) { Sample(); return; }
                Require(MainGameCraftingUiController.BlocksGameplayInput, "returned player opens earned furnace");
                Sample(0f, KeyCode.Q);
                Next(602);
                break;
            case 602:
                Sample();
                var toolServices = FindAnyObjectByType<MainGameRuntimeServices>();
                var toolUi = FindAnyObjectByType<MainGameCraftingUiController>();
                Require(toolUi.FurnaceSmeltingViewActive, "tool preparation stays in smelting view");
                if (T3CraftRun && toolServices.PlayerInventory.Count("icesteel_ingot") >= 5)
                {
                    checks.Add($"T3 five ingots collected at {Time.realtimeSinceStartup - controlStarted:F3}s.");
                    Next(1050); break;
                }
                if (CoolerCraftRun && toolServices.PlayerInventory.Count(anvilFoundryStage ? "icesteel_ingot" : "copper_ingot") >= (anvilFoundryStage ? 5 : 3))
                {
                    checks.Add($"Cooler ingot stage complete: foundry={anvilFoundryStage}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}.");
                    Next(anvilFoundryStage ? 1262 : 1263); break;
                }
                if (!CoolerCraftRun && !BedAutoOnlyRun && toolServices.PlayerInventory.Count("iron_ingot") >= (coreCraftRun ? 2 : anvilRun ? 5 : 4) && (coreCraftRun || toolServices.PlayerInventory.Count("copper_ingot") >= 2))
                {
                    if (anvilRun && !anvilFoundryStage) { Next(710); break; }
                    if (!anvilRun || toolServices.PlayerInventory.Count("icesteel_ingot") >= 4)
                    { checks.Add($"All {ProgressionRecipe} ingots collected at {Time.realtimeSinceStartup - controlStarted:F3}s."); Next(610); break; }
                }
                var needsIron = !CoolerCraftRun && (BedAutoOnlyRun || toolServices.PlayerInventory.Count("iron_ingot") < (coreCraftRun ? 2 : anvilRun ? 5 : 4));
                toolSmeltId = anvilFoundryStage ? "smelt_icesteel" : needsIron ? "smelt_iron" : "smelt_copper";
                toolOreId = anvilFoundryStage ? "icesteel_ore" : needsIron ? "iron_ore" : "copper_ore";
                toolIngotId = anvilFoundryStage ? "icesteel_ingot" : needsIron ? "iron_ingot" : "copper_ingot";
                var toolSelectedSmelt = (Nyangbingo.Data.SmeltingDefinition)typeof(MainGameCraftingUiController)
                    .GetMethod("CurrentSmelting", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(toolUi, null);
                if (toolSelectedSmelt?.Id != toolSmeltId)
                { Require(++toolNavigationAttempts < 8, "bounded tool smelting selection"); Next(608); break; }
                toolNavigationAttempts = 0;
                smeltOreBefore = toolServices.PlayerInventory.Count(toolOreId);
                smeltCoalBefore = toolServices.PlayerInventory.Count("coal");
                toolIngotBefore = toolServices.PlayerInventory.Count(toolIngotId);
                Require(smeltOreBefore >= 2 && smeltCoalBefore >= (anvilFoundryStage ? 2 : 1), "earned resources cover selected ingot");
                Next(603);
                break;
            case 608:
                Sample(0f, KeyCode.RightArrow);
                Next(602);
                break;
            case 603:
                Sample(0f, KeyCode.E);
                smeltStarted = Time.realtimeSinceStartup;
                Next(604);
                break;
            case 604:
                Sample();
                var toolInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                Require(toolInventory.Count(toolOreId) == smeltOreBefore - 2 && toolInventory.Count("coal") == smeltCoalBefore - (anvilFoundryStage ? 2 : 1),
                    "tool smelting consumes ordinary ore and coal");
                if (BedAutoOnlyRun) { Next(995); break; }
                Next(605);
                break;
            case 605:
                Sample();
                var preparingServices = FindAnyObjectByType<MainGameRuntimeServices>();
                if ((anvilFoundryStage ? preparingServices.Foundry : preparingServices.Furnace).Completed.Count == 0)
                { if (Time.realtimeSinceStartup - phaseStarted > (anvilFoundryStage ? 58f : 45f)) throw new Exception("Tool ingot smelting timeout."); return; }
                checks.Add($"{toolIngotId} smelt finished after {Time.realtimeSinceStartup - smeltStarted:F3}s.");
                Next(606);
                break;
            case 606:
                Sample();
                ((Button)typeof(MainGameCraftingUiController).GetField("collectButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameCraftingUiController>())).onClick.Invoke();
                Next(607);
                break;
            case 607:
                Sample();
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(toolIngotId) == toolIngotBefore + 1,
                    "tool ingot collected through normal button callback");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"tool-{toolIngotId}-{toolIngotBefore + 1}.png"));
                Next(602);
                break;
            case 610:
                Sample(0f, KeyCode.Q);
                toolNavigationAttempts = 0;
                Next(611);
                break;
            case 611:
                Sample();
                var clawUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var clawRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController)
                    .GetField("filteredRecipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(clawUi);
                var clawIndex = clawRecipes.FindIndex(r => r.Id == ProgressionRecipe);
                Require(clawIndex >= 0, "earned furnace offers selected progression recipe");
                var clawSelected = (int)typeof(MainGameCraftingUiController)
                    .GetField("selectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(clawUi);
                if (clawIndex != clawSelected)
                {
                    Require(++toolNavigationAttempts <= 128, "bounded claw recipe selection");
                    bridgeDirection = clawIndex > clawSelected ? 1f : -1f;
                    Next(612); break;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, deepRun ? "foundry-ready.png" : "iron-claw-ready.png"));
                savedStone = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("stone");
                if (coreCraftRun) coreIceBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ice_shard");
                savedDirt = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("clay");
                smeltOreBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("iron_ore");
                Next(613);
                break;
            case 612:
                Sample(0f, bridgeDirection > 0f ? KeyCode.DownArrow : KeyCode.UpArrow);
                Next(611);
                break;
            case 613:
                Sample(0f, KeyCode.E);
                toolStageStarted = Time.realtimeSinceStartup;
                Next(614);
                break;
            case 614:
                Sample();
                var clawServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(clawServices.StationProduction.Export().Any(s => s.jobs.Count > 0 && s.jobs[0].recipeId == ProgressionRecipe), "normal E starts selected progression crafting");
                if (CoolerCraftRun) Require(clawServices.PlayerInventory.Count("icesteel_ingot")==0 && clawServices.PlayerInventory.Count("copper_ingot")==0 && clawServices.PlayerInventory.Count("yeouiju")==0,"cooler craft consumes five ice ingots, three copper ingots and earned Yeouiju");
                else if (T3CraftRun) Require(clawServices.PlayerInventory.Count("icesteel_ingot") == 0 && clawServices.PlayerInventory.Count("frost_essence") == coreIceBefore - 2, "T3 craft consumes five ingots and two earned frost essence");
                else if (coreCraftRun) Require(clawServices.PlayerInventory.Count("iron_ingot") == 0 &&
                    clawServices.PlayerInventory.Count("ice_shard") == coreIceBefore - 20 &&
                    clawServices.PlayerInventory.Count("stone") == savedStone - 10,
                    "ice core consumes two iron ingots, twenty ice shards and ten stone");
                else if (anvilRun) Require(clawServices.PlayerInventory.Count("iron_ingot") == 0 &&
                    clawServices.PlayerInventory.Count("copper_ingot") == 0 && clawServices.PlayerInventory.Count("icesteel_ingot") == 0,
                    "ice anvil consumes exactly five iron, two copper and four ice-steel ingots");
                else if (deepRun) Require(clawServices.PlayerInventory.Count("stone") == savedStone - 20 &&
                    clawServices.PlayerInventory.Count("clay") == savedDirt - 12 && clawServices.PlayerInventory.Count("iron_ore") == smeltOreBefore - 6,
                    "foundry consumes twenty stone, twelve clay and six iron ore");
                else Require(clawServices.PlayerInventory.Count("iron_ingot") == 0 && clawServices.PlayerInventory.Count("copper_ingot") == 0 &&
                    clawServices.PlayerInventory.Count("stone") == savedStone - 5, "iron claw consumes four iron, two copper and five stone");
                Next(615);
                break;
            case 615:
                Sample();
                if (FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(ProgressionRecipe) == 0) return;
                checks.Add($"{ProgressionRecipe} crafted after {Time.realtimeSinceStartup - toolStageStarted:F3}s; continuation elapsed {Time.realtimeSinceStartup - controlStarted:F3}s.");
                if (CoolerCraftRun)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory,"cooler-crafted-ui.png"));
                    Next(1255); break;
                }
                if (T3CraftRun)
                {
                    Require((int)typeof(MainGamePlayerController).GetMethod("ResolveMiningClawTier", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, null) == 3, "crafted T3 changes actual mining tier to three");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "t3-claw-crafted.png"));
                    Next(414); break;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, deepRun ? "foundry-crafted.png" : "iron-claw-crafted.png"));
                if (coreCraftRun) coreCraftPassed = true;
                else if (anvilRun) anvilPassed = true;
                else if (deepRun) deepPassed = true;
                Next(deepRun ? 414 : 616);
                break;
            case 718:
                healingHealthBefore = ((Nyangbingo.Combat.Health)PlayerField("health")).Current;
                healingCountBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(healingItemId);
                SampleUse();
                Next(719);
                break;
            case 719:
                Sample();
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(healingItemId) == healingCountBefore - 1 &&
                    (healingReturnPhase == 1160 || ((Nyangbingo.Combat.Health)PlayerField("health")).Current > healingHealthBefore),
                    "normal right-click consumes one earned mushroom and restores HP");
                checks.Add($"Healing {healingItemId} at {Time.realtimeSinceStartup - controlStarted:F3}s: HP {healingHealthBefore} -> {((Nyangbingo.Combat.Health)PlayerField("health")).Current}; room={FindAnyObjectByType<MainGameRuntimeServices>().PlayerTemperature.CurrentRoomTemperature}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "normal-healing-latest.png"));
                Next(healingReturnPhase);
                break;
            case 712:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput, "inventory opens for earned healing item selection");
                ((Button[])typeof(MainGameCraftingUiController).GetField("inventoryGridButtons", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameCraftingUiController>()))[healingSourceSlot].onClick.Invoke();
                Next(713);
                break;
            case 713:
                Sample(0f, KeyCode.LeftShift);
                ((Button[])typeof(MainGameCraftingUiController).GetField("inventoryGridButtons", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameCraftingUiController>()))[6].onClick.Invoke();
                Next(714);
                break;
            case 714:
                Sample(0f, KeyCode.Escape);
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots[6].itemId == healingItemId,
                    "normal inventory Shift-click moves earned healing stack to hotbar 7");
                Next(715);
                break;
            case 715:
                Sample(0f, KeyCode.Alpha7);
                Next(718);
                break;
            case 720:
                Sample(0f, KeyCode.Escape);
                Next(721);
                break;
            case 721:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Pause, "gather checkpoint opens normal pause");
                ((Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameShellUiController>())).onClick.Invoke();
                Next(722);
                break;
            case 722:
                Sample(0f, KeyCode.Escape);
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var gatheredSave) &&
                    gatheredSave.inventory.Where(s => s.itemId == "icesteel_ore").Sum(s => s.amount) >= DeepOreRequired,
                    "normal save retains gathered ice-steel ore before return");
                File.Copy(Path.Combine(directory, "nyangbingo-save-0.json"), Path.Combine(directory, "gathered-checkpoint.json"), true);
                toolStageStarted = Time.realtimeSinceStartup;
                Next(460);
                break;
            case 710:
                Sample(0f, KeyCode.Escape);
                anvilFoundryStage = true;
                placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "blast_furnace");
                Next(711);
                break;
            case 711:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Next(600);
                break;
            case 700:
                if (Time.realtimeSinceStartup - phaseStarted < .3f) { Sample(); return; }
                Require(MainGameCraftingUiController.BlocksGameplayInput, "earned furnace opens for foundry install selection");
                var installUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var installRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController)
                    .GetField("filteredRecipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(installUi);
                var installIndex = installRecipes.FindIndex(r => r.Id == SmeltBuilding);
                var selectedInstall = (int)typeof(MainGameCraftingUiController).GetField("selectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(installUi);
                Require(installIndex >= 0 && ++toolNavigationAttempts < 128, "bounded foundry install recipe selection");
                if (selectedInstall != installIndex)
                { Sample(0f, selectedInstall < installIndex ? KeyCode.DownArrow : KeyCode.UpArrow); Next(701); break; }
                Sample();
                ((Button)typeof(MainGameCraftingUiController).GetField("collectButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(installUi)).onClick.Invoke();
                checks.Add("Installation button callback selects normally earned foundry; no item grant.");
                Next(501);
                break;
            case 701:
                Sample();
                Next(700);
                break;
            case 730:
                var anvilUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var anvilRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController)
                    .GetField("filteredRecipes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(anvilUi);
                var coldIndex = anvilRecipes.FindIndex(r => r.Id == "cold_device");
                var anvilIndex = (int)typeof(MainGameCraftingUiController).GetField("selectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(anvilUi);
                Require(coldIndex >= 0 && ++toolNavigationAttempts < 128, "installed ice anvil exposes cold-device recipe in bounded navigation");
                if (anvilIndex != coldIndex)
                { Sample(0f, anvilIndex < coldIndex ? KeyCode.DownArrow : KeyCode.UpArrow); Next(731); break; }
                Sample();
                var nearbyAnvil = (CraftingStation)typeof(MainGameCraftingUiController)
                    .GetMethod("NearbyStation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(anvilUi, null);
                Require(nearbyAnvil == CraftingStation.IceAnvil, "opened ice anvil is actually usable as a crafting station");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, anvilReloaded ? "anvil-restored-menu.png" : "anvil-cold-device-menu.png"));
                if (anvilReloaded)
                { Next(741); break; }
                Next(732);
                break;
            case 731: Sample(); Next(730); break;
            case 732: Sample(0f, KeyCode.E); Next(733); break;
            case 733:
                Sample();
                var coldUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var coldMessage = (string)typeof(MainGameCraftingUiController).GetField("message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(coldUi);
                Require(coldMessage.StartsWith("재료 부족:") && FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("cold_device") == 0,
                    "cold-device attempt explains missing materials without granting an item: " + coldMessage);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "anvil-missing-materials.png"));
                Next(734);
                break;
            case 734: Sample(0f, KeyCode.Escape); Next(735); break;
            case 735: Sample(); Next(736); break;
            case 736: Sample(0f, KeyCode.Escape); Next(737); break;
            case 737:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Pause, "anvil checkpoint opens pause");
                ((Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameShellUiController>())).onClick.Invoke();
                Next(738);
                break;
            case 738:
                Sample();
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var anvilDisk) &&
                    anvilDisk.placedObjectRecords.Count(r => r.definitionId == "ice_anvil" && r.objectId == placedFurnace.objectId) == 1 &&
                    !anvilDisk.inventory.Any(s => s.itemId == "ice_anvil" && s.amount > 0), "normal save retains one placed anvil and consumed inventory item");
                var anvilShell = FindAnyObjectByType<GameShellController>();
                Require(anvilShell.RequestReturnToTitle() && anvilShell.Confirm(), "anvil route returns to title through normal callbacks");
                Next(739);
                break;
            case 739:
                Sample();
                var anvilTitle = FindAnyObjectByType<TitleShellController>();
                if (anvilTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(anvilTitle.TryContinue(), "Continue loads saved installed anvil");
                Next(740);
                break;
            case 740:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var reloadedAnvils = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Where(r => r.definitionId == "ice_anvil").ToArray();
                Require(reloadedAnvils.Length == 1 && reloadedAnvils[0].objectId == placedFurnace.objectId &&
                    Vector2.Distance(reloadedAnvils[0].position, placedFurnace.position) < .01f &&
                    FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ice_anvil") == 0,
                    "scene reload restores same anvil identity and position without duplication");
                anvilReloaded = true;
                toolNavigationAttempts = 0;
                Next(506);
                break;
            case 760: Sample(0f, KeyCode.Alpha7); Next(doorGatherRun ? 460 : 761); break;
            case 761: TickNaturalHarvest(); break;
            case 747:
                Sample();
                if (player.IsDead) throw new Exception("Natural underground waiting ended in player death.");
                if (FindAnyObjectByType<MainGameBootstrap>().TimeService.IsNight)
                {
                    checks.Add($"Night ready after {Time.realtimeSinceStartup - combatStarted:F3}s in this segment (may already be night on load); HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                    combatAscentStarted = Time.realtimeSinceStartup;
                    Next(751);
                }
                break;
            case 751:
                Sample(0f, KeyCode.Escape);
                Next(752);
                break;
            case 752:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Pause,
                    "natural-night checkpoint opens ordinary pause");
                ((Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(FindAnyObjectByType<MainGameShellUiController>())).onClick.Invoke();
                Next(753);
                break;
            case 753:
                Sample(0f, KeyCode.Escape);
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var combatNightSave) &&
                    combatNightSave.placedObjectRecords.Any(r => r.objectId == placedWorkbench.objectId),
                    "natural-night checkpoint preserves earned workbench");
                File.Copy(Path.Combine(directory, "nyangbingo-save-0.json"), Path.Combine(directory, "natural-night-checkpoint.json"), true);
                combatAscentStarted = Time.realtimeSinceStartup;
                Next(748);
                break;
            case 748: Sample(0f, MiningSlotKey); Next(460); break;
            case 749: Sample(0f, KeyCode.Alpha7); Next(750); break;
            case 890:
                if (FreshNightRun && !FindAnyObjectByType<MainGameBootstrap>().TimeService.IsNight)
                {
                    Sample();
                    if (player.IsDead) throw new Exception("Player died while normally waiting for night on surface.");
                    return;
                }
                if (BossDeathRecoveryRun) checks.Add("Pre-summon inventory: " + string.Join(",", FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots.Where(s => s.amount > 0).Select(s => s.itemId + "=" + s.amount)));
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ssireum_satba") == 1,
                    "earned checkpoint has exactly one normally crafted satba");
                Require(!FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive, "no boss before inventory use");
                Sample(0f, KeyCode.Tab); Next(891); break;
            case 891:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .15f) return;
                var satbaUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var satbaInv = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                var satbaIndex = Enumerable.Range(0, satbaInv.Slots.Count).First(i => satbaInv.Slots[i].itemId == "ssireum_satba");
                var satbaSelected = (int)typeof(MainGameCraftingUiController).GetField("selectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(satbaUi);
                if (satbaSelected == satbaIndex) { Next(892); break; }
                Next(satbaSelected < satbaIndex ? 899 : 900); break;
            case 899: Sample(0f, KeyCode.RightArrow); Next(891); break;
            case 900: Sample(0f, KeyCode.LeftArrow); Next(891); break;
            case 892: SampleUse(); Next(893); break;
            case 893:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var confirmUi = FindAnyObjectByType<MainGameCraftingUiController>();
                Require(((GameObject)typeof(MainGameCraftingUiController).GetField("summonConfirmationRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(confirmUi)).activeSelf,
                    "normal inventory right-click opens summon confirmation");
                checks.Add("Confirmation text: " + ((Text)typeof(MainGameCraftingUiController).GetField("summonConfirmationText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(confirmUi)).text);
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ssireum_satba") == 1, "opening confirmation does not consume satba");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "satba-confirmation.png"));
                Next(894); break;
            case 894:
                if (Time.realtimeSinceStartup - phaseStarted < .4f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(895); break;
            case 895:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .4f) return;
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ssireum_satba") == 1 && !FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive,
                    "cancel preserves one satba and does not spawn a boss");
                Require(!((GameObject)typeof(MainGameCraftingUiController).GetField("summonConfirmationRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameCraftingUiController>())).activeSelf,
                    "cancel closes summon confirmation");
                Next(896); break;
            case 896: SampleUse(); Next(897); break;
            case 897:
                if (Time.realtimeSinceStartup - phaseStarted < .4f) { Sample(); return; }
                SampleUse(); Next(898); break;
            case 898:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var summonedManager = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                Require(summonedManager.IsBossActive && summonedManager.ActiveDefinition.Id == "king_dokkaebi", "ordinary confirmation summons king_dokkaebi");
                if (BossDawnRun || FreshNightRun) summonedManager.BossEnded += (definition, defeated) => { naturalBossEnded = true; naturalBossDefeated = defeated; };
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ssireum_satba") == 0, "successful summon consumes exactly one earned satba");
                checks.Add($"Normal summon after {Time.realtimeSinceStartup - controlStarted:F3}s; boss position={summonedManager.ActiveHealth.transform.position}; player={player.transform.position}; boss health={summonedManager.ActiveHealth.Current}");
                if (FreshNightRun) checks.Add($"Fresh-night summon day={FindAnyObjectByType<MainGameBootstrap>().TimeService.Day}, clock={FindAnyObjectByType<MainGameBootstrap>().TimeService.TimeOfDayGameSeconds:F3}; inventory=" + string.Join(",", FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots.Where(s => s.amount > 0).Select(s => s.itemId + "=" + s.amount)));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "satba-normal-summon.png"));
                Next(901); break;
            case 901:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                if (BossSaveHintRun) { Next(906); break; }
                if (BossOpeningHintRun) { Next(930); break; }
                if (BossApproachRun)
                {
                    if (BossSurfaceRun) { Next(903); break; }
                    combatAscentStarted = Time.realtimeSinceStartup;
                    combatElevation = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().ActiveHealth.transform.position.y - .3f;
                    Next(748); break;
                }
                Finish("passed", "Normal inventory confirmation, cancel preservation and king summon consumption checked. Combat victory, flight/retry and save recovery remain untested; original earned checkpoint preserved.");
                break;
            case 903:
                Sample(0f, KeyCode.Alpha7);
                bossFightStarted = Time.realtimeSinceStartup;
                Next(902); break;
            case 902:
                var observedBoss = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                var observedPlayerHp = ((Nyangbingo.Combat.Health)PlayerField("health")).Current;
                if (Time.realtimeSinceStartup >= bossLogAt)
                {
                    bossLogAt = Time.realtimeSinceStartup + 5f;
                    checks.Add($"Boss observation {Time.realtimeSinceStartup - bossFightStarted:F3}s: active={observedBoss.IsBossActive}, bossHP={observedBoss.ActiveHealth?.Current}, playerHP={observedPlayerHp}, player={player.transform.position}, boss={observedBoss.ActiveHealth?.transform.position}");
                }
                if (player.IsDead || !observedBoss.IsBossActive || Time.realtimeSinceStartup - bossFightStarted >= (FreshNightRun ? 720f : 120f))
                {
                    Sample();
                    checks.Add($"Boss observation ended: playerDead={player.IsDead}, bossActive={observedBoss.IsBossActive}, duration={Time.realtimeSinceStartup - bossFightStarted:F3}s. No victory claim from timeout or inactive boss alone.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-observation-end.png"));
                    Next(904); break;
                }
                var bossDelta = observedBoss.ActiveHealth.transform.position - player.transform.position;
                if (BossColliderAimRun)
                {
                    var bossColliders = observedBoss.ActiveHealth.GetComponentsInChildren<Collider2D>()
                        .Where(c => c.enabled && c.gameObject.activeInHierarchy && c.GetComponentInParent<Nyangbingo.Combat.Health>() == observedBoss.ActiveHealth).ToArray();
                    if (bossColliders.Length == 0) throw new Exception("Active boss has no target collider.");
                    var nearestCollider = bossColliders.OrderBy(c => ((Vector2)c.ClosestPoint(player.transform.position) - (Vector2)player.transform.position).sqrMagnitude).First();
                    var nearestPoint = nearestCollider.ClosestPoint(player.transform.position);
                    var nearestOffset = nearestPoint - (Vector2)player.transform.position;
                    var aimPoint = nearestPoint + ((Vector2)nearestCollider.bounds.center - nearestPoint).normalized * .1f;
                    var aimMove = nearestOffset.magnitude > 1.1f && Mathf.Abs(nearestOffset.x) > .15f ? Mathf.Sign(nearestOffset.x) : 0f;
                    var openingCombat = observedBoss.ActiveHealth.GetComponent<Nyangbingo.Bosses.BossCombatController>();
                    if (BossOpeningRetreatRun && !openingCombat.IsOpeningDodgeActive && nearestOffset.y > 1.6f && (bool)PlayerField("grounded"))
                    {
                        bossReapproach = true;
                        combatAscentStarted = Time.realtimeSinceStartup;
                        combatElevation = player.transform.position.y + Mathf.Min(4f, nearestOffset.y - .7f);
                        checks.Add($"Start normal block reapproach at combat {Time.realtimeSinceStartup - bossFightStarted:F3}s, targetY={combatElevation:F2}.");
                        Next(748); break;
                    }
                    if (BossOpeningRetreatRun)
                    {
                        if (openingCombat.IsOpeningDodgeActive)
                            aimMove = nearestOffset.magnitude < 9f ? -Mathf.Sign(bossDelta.x) : 0f;
                        else if (!bossOpeningEndedObserved)
                        {
                            bossOpeningEndedObserved = true;
                            checks.Add($"Opening immunity ended naturally at combat {Time.realtimeSinceStartup - bossFightStarted:F3}s, playerHP={observedPlayerHp}, multiplier={observedBoss.ActiveHealth.DamageTakenMultiplier}.");
                            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-opening-ended.png"));
                        }
                        if (!bossFirstDamageObserved && observedBoss.ActiveHealth.Current < observedBoss.ActiveHealth.MaxHealth)
                        {
                            bossFirstDamageObserved = true;
                            checks.Add($"First actual boss HP decrease at combat {Time.realtimeSinceStartup - bossFightStarted:F3}s, bossHP={observedBoss.ActiveHealth.Current}, playerHP={observedPlayerHp}.");
                            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-first-damage.png"));
                        }
                    }
                    var aimJump = (aimMove != 0f || nearestOffset.y > .7f) && Time.realtimeSinceStartup >= combatJumpAt;
                    if (aimJump) combatJumpAt = Time.realtimeSinceStartup + .9f;
                    if (Time.realtimeSinceStartup >= bossAimLogAt)
                    {
                        bossAimLogAt = Time.realtimeSinceStartup + 5f;
                        var aimCombat = observedBoss.ActiveHealth.GetComponent<Nyangbingo.Bosses.BossCombatController>();
                        checks.Add($"Collider aim rootDelta={bossDelta}, closestDelta={nearestOffset}, bounds={nearestCollider.bounds}, lastHits={player.GetComponent<Nyangbingo.Combat.MeleeArcAttack>().LastHitCount}, openingDodge={aimCombat.IsOpeningDodgeActive}, damageMultiplier={observedBoss.ActiveHealth.DamageTakenMultiplier}");
                    }
                    if (aimMove != 0f && (bool)PlayerField("grounded"))
                    {
                        var aimFront = tiles.WorldToCell(player.transform.position) + new Vector3Int((int)aimMove, 0, 0);
                        var aimObstacle = !tiles.GetTile(aimFront).IsAir ? aimFront : aimFront + Vector3Int.up;
                        if (!tiles.GetTile(aimObstacle).IsAir)
                        {
                            GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(aimObstacle)), true, false);
                            break;
                        }
                    }
                    GameplayInput.Sample(aimMove, Camera.main.WorldToScreenPoint(aimPoint), nearestOffset.magnitude <= 1.5f && (!BossOpeningRetreatRun || !openingCombat.IsOpeningDodgeActive), false,
                        aimJump ? new[] { KeyCode.Space } : Array.Empty<KeyCode>());
                    break;
                }
                var bossMove = Mathf.Abs(bossDelta.x) > (BossDeathRecoveryRun ? .3f : 1f) ? Mathf.Sign(bossDelta.x) : 0f;
                if (BossDeathRecoveryRun && bossMove != 0f && (bool)PlayerField("grounded"))
                {
                    var frontCell = tiles.WorldToCell(player.transform.position) + new Vector3Int((int)bossMove, 0, 0);
                    var obstacleCell = !tiles.GetTile(frontCell).IsAir ? frontCell : frontCell + Vector3Int.up;
                    if (!tiles.GetTile(obstacleCell).IsAir)
                    {
                        GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(obstacleCell)), true, false);
                        break;
                    }
                }
                var bossJump = (bossMove != 0f || BossDeathRecoveryRun && bossDelta.y > .7f) && Time.realtimeSinceStartup >= combatJumpAt;
                if (bossJump) combatJumpAt = Time.realtimeSinceStartup + .9f;
                GameplayInput.Sample(bossMove, Camera.main.WorldToScreenPoint(observedBoss.ActiveHealth.transform.position), bossDelta.sqrMagnitude <= 4f, false,
                    bossJump ? new[] { KeyCode.Space } : Array.Empty<KeyCode>());
                break;
            case 904:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                if (FreshNightRun)
                {
                    checks.Add($"Fresh-night outcome: endedEvent={naturalBossEnded}, defeated={naturalBossDefeated}, playerDead={player.IsDead}.");
                    if (!naturalBossEnded || !naturalBossDefeated || player.IsDead)
                    {
                        Finish("failed", "Fresh-night normal boss victory not achieved. Actual death, flight or observation timeout is recorded; not a human balance verdict.");
                        break;
                    }
                    checks.Add("Victory inventory before save: " + string.Join(",", FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots.Where(s => s.amount > 0).Select(s => s.itemId + "=" + s.amount)));
                    Next(940); break;
                }
                if (BossDeathRecoveryRun)
                {
                    Require(player.IsDead, "death recovery probe actually reached player death");
                    Next(905); break;
                }
                Finish("passed", "Bounded normal approach/combat observation completed; outcome is in checks, not an assertion of boss victory. No healing during combat in this initial policy; failure does not establish human difficulty.");
                break;
            case 905:
                Sample();
                if (player.IsDead) return;
                var recoveryServices = FindAnyObjectByType<MainGameRuntimeServices>();
                var recoveryBoss = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                Require(((Nyangbingo.Combat.Health)PlayerField("health")).Current > 0, "normal death sequence returns a living player");
                Require(recoveryServices.PlayerInventory.Count("ssireum_satba") == 0, "death does not refund consumed satba");
                checks.Add($"Recovery observed {Time.realtimeSinceStartup - phaseStarted + 1f:F3}s after death observation; HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, position={player.transform.position}, bossActive={recoveryBoss.IsBossActive}, bossHP={recoveryBoss.ActiveHealth?.Current}, pouches={recoveryServices.DeathTearPouches.Active.Count}");
                checks.Add("After recovery inventory: " + string.Join(",", recoveryServices.PlayerInventory.Slots.Where(s => s.amount > 0).Select(s => s.itemId + "=" + s.amount)));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-death-recovered.png"));
                Next(906); break;
            case 906:
                if (Time.realtimeSinceStartup - phaseStarted < .5f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(907); break;
            case 907:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var recoverySaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                checks.Add($"Post-death pause save button: visible={recoverySaveButton.gameObject.activeInHierarchy}, interactable={recoverySaveButton.interactable}; bossActive={FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive}");
                Require(recoverySaveButton.gameObject.activeInHierarchy, "post-death pause menu opens through Escape");
                if (BossSaveHintRun)
                {
                    var pauseHint = (Text)typeof(MainGameShellUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                    Require(!recoverySaveButton.interactable && pauseHint.gameObject.activeInHierarchy && pauseHint.text.Contains("저장 불가") && pauseHint.text.Contains("새벽"),
                        "boss pause displays restriction reason and next action without pressing disabled save");
                    checks.Add("Visible pause hint: " + pauseHint.text);
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-death-pause-save.png"));
                Next(908); break;
            case 908:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < (BossSaveHintRun ? 3f : .5f)) return;
                if (BossDawnRun) { Next(913); break; }
                Finish("passed", BossSaveHintRun ? "Normal enabled save and boss disabled save with visible reason checked. No save callback forced; dawn re-enable remains separate." : "Observed actual death, automatic living recovery, post-death inventory/boss state and pause-save availability. No victory, saved recovery, dawn flight or second attempt claim.");
                break;
            case 909: Sample(0f, KeyCode.Escape); Next(910); break;
            case 910:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var normalShellUi = FindAnyObjectByType<MainGameShellUiController>();
                var normalSaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(normalShellUi);
                var normalPauseHint = (Text)typeof(MainGameShellUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(normalShellUi);
                Require(normalSaveButton.gameObject.activeInHierarchy && normalSaveButton.interactable && !normalPauseHint.text.Contains("보스 전투"), "normal pause save remains enabled without boss restriction hint");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "normal-pause-save.png"));
                Next(911); break;
            case 911:
                if (Time.realtimeSinceStartup - phaseStarted < .5f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(912); break;
            case 912:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Next(890); break;
            case 913:
                Sample(0f, KeyCode.Escape);
                dawnWaitStarted = Time.realtimeSinceStartup;
                Next(914); break;
            case 914:
                Sample();
                var dawnClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                if (Time.realtimeSinceStartup >= dawnLogAt)
                {
                    dawnLogAt = Time.realtimeSinceStartup + 30f;
                    checks.Add($"Natural dawn wait {Time.realtimeSinceStartup - dawnWaitStarted:F3}s; day={dawnClock.Day}, clock={dawnClock.TimeOfDayGameSeconds:F3}, night={dawnClock.IsNight}, dead={player.IsDead}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                }
                if (!naturalBossEnded) return;
                Require(!naturalBossDefeated && !dawnClock.IsNight && dawnClock.Day == 4, "natural dawn ends boss as flight, not defeat");
                if (player.IsDead) return;
                Require(!FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive, "boss inactive after natural dawn");
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ssireum_satba") == 0, "dawn does not refund consumed summon item");
                checks.Add($"Dawn reached after {Time.realtimeSinceStartup - dawnWaitStarted:F3}s natural wait.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "natural-boss-dawn.png"));
                Next(915); break;
            case 915:
                if (Time.realtimeSinceStartup - phaseStarted < 1f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(916); break;
            case 916:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var dawnUi = FindAnyObjectByType<MainGameShellUiController>();
                var dawnButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dawnUi);
                var dawnHint = (Text)typeof(MainGameShellUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dawnUi);
                Require(dawnButton.gameObject.activeInHierarchy && dawnButton.interactable && !dawnHint.text.Contains("저장 불가"), "dawn re-enables save and removes boss restriction hint");
                dawnButton.onClick.Invoke();
                Next(917); break;
            case 917:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var dawnSave) && dawnSave.day == 4 && !dawnSave.inventory.Any(s => s.itemId == "ssireum_satba" && s.amount > 0), "normal post-dawn save persists day4 and consumed satba");
                var savedHint = (Text)typeof(MainGameShellUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                Require(savedHint.text == "저장 완료", "save success status replaces restriction hint");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "natural-dawn-saved.png"));
                Next(918); break;
            case 918:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Finish("passed", "Natural-clock boss flight at dawn, save re-enable, hint removal and ordinary save persisted day4/satba0. No boss victory, OS restart or full30-day claim.");
                break;
            case 934:
                Sample(0f, KeyCode.Alpha7);
                Next(902); break;
            case 935:
                Sample();
                var prepClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var prepHealth = ((Nyangbingo.Combat.Health)PlayerField("health")).Current;
                if (player.IsDead) throw new Exception("Natural next-night preparation ended in player death; no safe-wait claim.");
                if (FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive)
                    throw new Exception("Unexpected active boss during unsummoned preparation.");
                if (FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ssireum_satba") != 1)
                    throw new Exception("Earned summon item changed during natural preparation.");
                if (Time.realtimeSinceStartup >= dawnLogAt)
                {
                    dawnLogAt = Time.realtimeSinceStartup + 30f;
                    checks.Add($"Next-night preparation {Time.realtimeSinceStartup - dawnWaitStarted:F3}s; day={prepClock.Day}, clock={prepClock.TimeOfDayGameSeconds:F3}, night={prepClock.IsNight}, HP={prepHealth}, position={player.transform.position}");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "next-night-progress.png"));
                }
                // Save before dusk so the next ordinary ascent can finish near night start.
                if (prepClock.Day < 4 || prepClock.TimeOfDayGameSeconds < 860f) return;
                Require(prepClock.Day == 4 && !prepClock.IsNight, "naturally reached day4 before dusk with earned satba intact");
                checks.Add($"Natural preparation reached target in {Time.realtimeSinceStartup - dawnWaitStarted:F3}s, HP={prepHealth}.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "next-night-ready.png"));
                Next(936); break;
            case 936:
                if (Time.realtimeSinceStartup - phaseStarted < 1f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(937); break;
            case 937:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                var prepUi = FindAnyObjectByType<MainGameShellUiController>();
                var prepButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(prepUi);
                Require(prepButton.gameObject.activeInHierarchy && prepButton.interactable, "normal preparation save is enabled");
                prepButton.onClick.Invoke(); Next(938); break;
            case 938:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var prepSave) && prepSave.day == 4 && prepSave.inventory.Any(s => s.itemId == "ssireum_satba" && s.amount == 1), "normal save persists day4 and unconsumed earned satba");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "next-night-saved.png"));
                Next(939); break;
            case 939:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Finish("passed", "Naturally waited from earned late day3 satba checkpoint to day4 before dusk and normally saved satba1. No time changes, healing, summon or victory; next combat unverified.");
                break;
            case 970:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                bedSleepAllowed = FindAnyObjectByType<MainGameRuntimeServices>().Bed.CanSleep(placementPoint, out var bedRoom, out var bedReason);
                if (BedWarmRun) Require(bedSleepAllowed, "normal placement selected a genuinely usable warm bed position");
                checks.Add($"Bed interaction eligibility: allowed={bedSleepAllowed}, room={bedRoom}, reason={bedReason}, player={player.transform.position}.");
                Next(971); break;
            case 971:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(972); break;
            case 972:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .7f) return;
                var bedShell = FindAnyObjectByType<GameShellController>();
                Require(bedSleepAllowed ? bedShell.PendingConfirmation == GameShellConfirmation.Rest : bedShell.Screen == GameShellScreen.Gameplay, "normal bed interaction agrees with actual rest eligibility");
                if (!bedSleepAllowed)
                {
                    var bedStatus = (Text)typeof(MainGameBossSummonUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameBossSummonUiController>());
                    Require(bedStatus.isActiveAndEnabled && bedStatus.text.Contains("침실이 너무 춥습니다") && bedStatus.text.Contains("-4"), "visible bed restriction explains current temperature and minimum");
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "bed-interaction.png"));
                bedClockBefore = FindAnyObjectByType<MainGameBootstrap>().TimeService.GameSeconds;
                Next(973); break;
            case 973:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .7f) return;
                if (bedSleepAllowed)
                {
                    Require(FindAnyObjectByType<MainGameBootstrap>().TimeService.GameSeconds == bedClockBefore, "rest confirmation freezes game clock");
                    Require(FindAnyObjectByType<GameShellController>().CancelConfirmation(), "rest cancellation uses normal confirmation callback");
                    Require(FindAnyObjectByType<MainGameBootstrap>().TimeService.GameSeconds == bedClockBefore, "cancel does not skip game time");
                }
                Next(974); break;
            case 974:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Require(Time.timeScale > 0f && !player.IsDead, "bed interaction returns to living gameplay");
                Next(BedWarmRun ? 975 : 420); break;
            case 975:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(976); break;
            case 976:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var restClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var restShell = FindAnyObjectByType<GameShellController>();
                Require(restShell.PendingConfirmation == GameShellConfirmation.Rest, "ordinary right click opens rest confirmation again");
                bedClockBefore = restClock.GameSeconds;
                if (BedAutoOnlyRun) Require(FindAnyObjectByType<MainGameRuntimeServices>().Furnace.Active?.Id == "smelt_iron", "earned-material iron smelting is still active when normal rest is confirmed");
                bedDayBefore = restClock.Day;
                bedNightBefore = restClock.IsNight;
                checks.Add($"Before rest {bedRestCount + 1}: day={bedDayBefore}, night={bedNightBefore}, clock={restClock.TimeOfDayGameSeconds:F3}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                Require(restShell.Confirm(), "rest proceeds through ordinary confirmation callback");
                Next(977); break;
            case 977:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var restedClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(restedClock.IsNight != bedNightBefore && restedClock.Day == bedDayBefore + (bedNightBefore ? 1 : 0), "rest reaches the correct next day-night boundary");
                Require(!player.IsDead, "ordinary bed time transition preserves living gameplay");
                bedRestCount++;
                checks.Add($"After rest {bedRestCount}: day={restedClock.Day}, night={restedClock.IsNight}, clock={restedClock.TimeOfDayGameSeconds:F3}, advanced={restedClock.GameSeconds - bedClockBefore:F3}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, elapsed={Time.realtimeSinceStartup - controlStarted:F3}s");
                if (bedNightBefore)
                {
                    var bedSavePath = Path.Combine(directory, "nyangbingo-save-0.json");
                    Require(File.Exists(bedSavePath), "dawn save exists after normal night rest");
                    File.Copy(bedSavePath, Path.Combine(directory, "bed-dawn-autosave.json"), true);
                    Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var bedAutoSave) && bedAutoSave.day == restedClock.Day, "normal night rest triggers current-day dawn autosave");
                    if (BedAutoOnlyRun)
                    {
                        var liveRestHealth = ((Nyangbingo.Combat.Health)PlayerField("health")).Current;
                        var liveRestOutputs = FindAnyObjectByType<MainGameRuntimeServices>().StationProduction.PendingCount("iron_ingot") + FindAnyObjectByType<MainGameRuntimeServices>().Furnace.Completed.Where(r => r.item.Id == "iron_ingot").Sum(r => r.amount);
                        checks.Add($"Dawn snapshot comparison: savedHP={bedAutoSave.playerState.currentHealth}, liveHP={liveRestHealth}, savedIronOutputs={bedAutoSave.smeltingOutputs.Where(r => r.itemId == "iron_ingot").Sum(r => r.amount)}, liveIronOutputs={liveRestOutputs}");
                        Require(bedAutoSave.playerState.currentHealth == liveRestHealth, "dawn autosave captures post-rest health");
                        Require(liveRestOutputs == bedOutputBefore + 1 && bedAutoSave.smeltingOutputs.Where(r => r.itemId == "iron_ingot").Sum(r => r.amount) == liveRestOutputs, "dawn autosave captures completed earned-material iron smelting");
                        bedSavedDay = bedAutoSave.day;
                    }
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"bed-rest-{bedRestCount}.png"));
                Next(978); break;
            case 978:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                if (SessionState.GetBool("Nyangbingo.QA.Day23Route", false)) { Next(1151); break; }
                if (Day18ResidentsRun) { Next(1111); break; }
                if (Day16InvasionRun) { Next(1091); break; }
                if (SessionState.GetBool("Nyangbingo.QA.StorageInvasion",false)) { Require(bedRestCount==1,"day6 storage checkpoint reaches invasion with one ordinary rest"); Next(1010); break; }
                if (BaekjungReachRun)
                {
                    var eventClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                    Require(bedRestCount <= 17 && eventClock.Day <= 15, "bounded ordinary rest route to day15");
                    Next(eventClock.Day == 15 && eventClock.IsNight ? 1061 : 975);
                    break;
                }
                if (BedInvasionRun)
                {
                    if (bedRestCount == 3)
                    {
                        if (Time.realtimeSinceStartup - phaseStarted < 5f) return;
                        var announcementHud = FindAnyObjectByType<MainGameHudController>();
                        var announcementText = (Text)typeof(MainGameHudController).GetField("alertOverlayText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(announcementHud);
                        checks.Add($"Eve warning: active={announcementText.isActiveAndEnabled}, text={announcementText.text}");
                        var announcementRect = announcementText.rectTransform;
                        for (Transform diagnosticParent = announcementRect; diagnosticParent != null; diagnosticParent = diagnosticParent.parent)
                        {
                            if (diagnosticParent is RectTransform diagnosticRect)
                                checks.Add($"Alert layout {diagnosticParent.name}: rect={diagnosticRect.rect}, anchors={diagnosticRect.anchorMin}/{diagnosticRect.anchorMax}, sizeDelta={diagnosticRect.sizeDelta}, scale={diagnosticRect.localScale}");
                        }
                        checks.Add($"Alert font: {announcementText.font?.name}, size={announcementText.fontSize}, color={announcementText.color}, canvasAlpha={announcementText.canvasRenderer.GetAlpha()}, preferred={announcementText.preferredWidth}/{announcementText.preferredHeight}");
                        var announcementRoot = (RectTransform)announcementRect.parent;
                        var announcementCanvasRect = (RectTransform)announcementRoot.parent;
                        Require(announcementRoot.rect.width > 0f && announcementRoot.rect.height > 0f && (announcementRoot.rect.size - announcementCanvasRect.rect.size).sqrMagnitude < .01f, "invasion overlay fills positive HUD bounds");
                        var warningCorners = new Vector3[4];
                        announcementRect.GetWorldCorners(warningCorners);
                        Require(warningCorners.All(corner => announcementCanvasRect.rect.Contains(announcementCanvasRect.InverseTransformPoint(corner))), "invasion warning text lies inside actual HUD bounds");
                        Require(announcementText.isActiveAndEnabled && announcementText.text.Contains("내일 밤 침공"), "day5 daytime invasion announcement names tomorrow night");
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "first-invasion-eve-warning.png"));
                    }
                    Require(bedRestCount <= 6, "first invasion reached within six ordinary rest transitions");
                    Next(bedRestCount < 6 ? 975 : 1010); break;
                }
                Next(BedAutoOnlyRun ? 991 : bedRestCount < 2 ? 975 : 420); break;
            case 1010:
                var invasionTime = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var invasionServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(invasionTime.Day == 6 && invasionTime.IsNight && invasionServices.Invasion.IsCurrentInvasionNight, "ordinary rest transitions reach actual first invasion night");
                Require(!invasionServices.Bed.CanSleep(placementPoint, out var invasionRoom, out var invasionReason) && invasionRoom >= -4f && invasionReason.Contains("침공"), "warm bed is locked by invasion rather than temperature");
                checks.Add($"First invasion eligibility: room={invasionRoom}, reason={invasionReason}, elapsed={Time.realtimeSinceStartup - controlStarted:F3}s");
                bedClockBefore = invasionTime.GameSeconds;
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(1011); break;
            case 1011:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .7f) return;
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Gameplay, "invasion bed click does not open a rest confirmation");
                var invasionText = (Text)typeof(MainGameBossSummonUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameBossSummonUiController>());
                Require(invasionText.isActiveAndEnabled && invasionText.text.Contains("침공") && invasionText.text.Contains("잠들 수 없습니다"), "actual invasion bed denial is visible on screen");
                var blockedClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(blockedClock.Day == 6 && blockedClock.IsNight && blockedClock.GameSeconds - bedClockBefore < 5f, "denied rest does not skip the invasion night");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "first-invasion-bed-denied.png"));
                Next(1012); break;
            case 1012:
                Sample();
                if (InvasionOvernightRun)
                {
                    var observeClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                    var observeServices = FindAnyObjectByType<MainGameRuntimeServices>();
                    var observeEncounters = FindAnyObjectByType<MainGameEncounterCoordinator>();
                    var observeElapsed = Time.realtimeSinceStartup - phaseStarted;
                    var observeBucket = (int)(observeElapsed / 30f);
                    if (observeBucket != invasionObservationBucket)
                    {
                        invasionObservationBucket = observeBucket;
                        checks.Add($"Invasion observation: elapsed={observeElapsed:F3}s, day={observeClock.Day}, clock={observeClock.TimeOfDayGameSeconds:F3}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, heat={observeServices.Invasion.TemperatureRiseCelsius}, recoolDay={observeServices.Invasion.RecoolAvailableDay}, raid={observeEncounters.ActiveRaidCount}, regular={observeEncounters.ActiveRegularCount}, pending={observeEncounters.PendingRegularCount}");
                        if(SessionState.GetBool("Nyangbingo.QA.StorageInvasion",false))
                        {
                            var exists=observeServices.JangdokStorage.TryGet(storageTestId,out var invasionIce);
                            observeServices.StorageTemperature.TryGetStatus(storageTestId,out var invasionStorageTemp,out _);
                            checks.Add($"Storage invasion: elapsed={observeElapsed:F3}, exists={exists}, temperature={invasionStorageTemp}, ice={(exists?invasionIce.Count("ice_shard"):-1)}, remainder={(exists?invasionIce.Slots.Where(x=>x.itemId=="ice_shard").Sum(x=>x.storageMeltRemainder):-1)}");
                        }
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"invasion-observation-{observeBucket:D2}.png"));
                    }
                    if (player.IsDead) throw new Exception("Full invasion observation ended in player death; do not infer full-night survival.");
                    if (observeClock.Day == 7 && !observeClock.IsNight)
                    {
                        checks.Add($"Natural invasion dawn reached after {observeElapsed:F3}s; heat={observeServices.Invasion.TemperatureRiseCelsius}, canRecool={observeServices.Invasion.CanRecoolNow}");
                        Next(1014);
                    }
                    break;
                }
                if (Time.realtimeSinceStartup - phaseStarted < 10f) return;
                Require(!player.IsDead && FindAnyObjectByType<MainGameRuntimeServices>().Invasion.IsCurrentInvasionNight, "brief first invasion observation remains alive and active");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "first-invasion-observed.png"));
                Next(1013); break;
            case 1020:
            case 1050:
                if (phase == 1050) { Sample(0f, KeyCode.Escape); Next(1051); break; }
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var restoredInvasionClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var restoredInvasionServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(restoredInvasionClock.Day == 7 && !restoredInvasionClock.IsNight && !player.IsDead, "actual invasion dawn Continue restores living day7 daytime");
                Require(((Nyangbingo.Combat.Health)PlayerField("health")).Current == 100, "actual invasion dawn Continue restores HP100");
                Require(tiles.GetTile(new Vector3Int(297, 119, 0)).IsAir && tiles.GetTile(new Vector3Int(298, 119, 0)).IsAir, "both actually destroyed floor cells remain absent after Continue");
                Require(tiles.GetTile(new Vector3Int(299, 119, 0)).elementType == "insul_wall", "neighboring intact insulation floor remains present");
                var restoredInvasionObjects = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects();
                Require(restoredInvasionObjects.Count(r => r.definitionId == "ice_core") == 1 && restoredInvasionObjects.Count(r => r.definitionId == "nest_bed") == 1, "Continue preserves exactly one core and bed without duplication");
                Require(restoredInvasionServices.Invasion.TemperatureRiseCelsius == 0f && !restoredInvasionServices.Invasion.HasPendingRecool, "Continue does not invent core heat or recooling debt");
                var restoredInvasionGuide = (Text)typeof(MainGameHudController).GetField("shelterGuideText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameHudController>());
                checks.Add($"Restored shelter guide: active={restoredInvasionGuide.isActiveAndEnabled}, text={restoredInvasionGuide.text}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "first-invasion-damage-continued.png"));
                Next(1021); break;
            case 1021:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                if (InvasionRepairRun) { toolStageStarted = Time.realtimeSinceStartup; Next(1030); break; }
                Finish("passed", "Actual passive-invasion day7 autosave loaded through normal Title Continue. Both destroyed floor cells stay absent, neighboring wall/core/bed and HP100 preserved, no heat debt invented. No repair, recooling, new full-night trial or OS restart.");
                break;
            case 1030:
                if (player.transform.position.x > 299.65f)
                {
                    Sample(-1f, (Time.realtimeSinceStartup - phaseStarted) % 1.2f < .12f ? KeyCode.Space : KeyCode.None);
                    if (Time.realtimeSinceStartup - phaseStarted > 12f) throw new Exception("Normal repair approach blocked; inspect route.");
                    break;
                }
                Sample();
                checks.Add($"Repair approach player={player.transform.position}; elapsed={Time.realtimeSinceStartup - toolStageStarted:F3}s.");
                placedWorkbench = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().First(r => r.definitionId == "workbench");
                toolNavigationAttempts = 0;
                Next(1040); break;
            case 1040:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1.5f) return;
                Require(Vector2.Distance(player.transform.position, placedWorkbench.position) < 2f, "repair approach settles within workbench reach");
                Next(810); break;
            case 1031:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                ((Button)typeof(MainGameCraftingUiController).GetField("collectButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameCraftingUiController>())).onClick.Invoke();
                placementPoint = tiles.GetCellCenterWorld(new Vector3Int(wallsRepaired == 0 ? 298 : 297, 119, 0));
                Next(1032); break;
            case 1032:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false);
                if (Time.realtimeSinceStartup - phaseStarted < .25f) return;
                Require((bool)typeof(MainGameTilePaletteController).GetField("foregroundPlacementValid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameTilePaletteController>()), "actual destroyed floor accepts normal wall preview");
                Next(1033); break;
            case 1033:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, true);
                Next(1034); break;
            case 1034:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                Require(tiles.GetTile(tiles.WorldToCell(placementPoint)).elementType == "insul_wall", "normal click repairs actual invasion damage");
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("insul_wall") == wallBefore, "repair placement consumes one crafted wall");
                wallsRepaired++;
                checks.Add($"Actual damaged floor repaired count={wallsRepaired}; segment={Time.realtimeSinceStartup - controlStarted:F3}s.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"invasion-floor-repaired-{wallsRepaired}.png"));
                Next(1038); break;
            case 1038:
                Sample(0f, FindAnyObjectByType<MainGameTilePaletteController>().IsForegroundPlacementActive ? KeyCode.Escape : KeyCode.None);
                Next(1039); break;
            case 1039:
                Sample();
                toolNavigationAttempts = 0;
                Next(wallsRepaired < 2 ? 1030 : 420); break;
            case 1035:
                Sample();
                var repairTitle = FindAnyObjectByType<TitleShellController>();
                if (repairTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(repairTitle.TryContinue(), "Title Continue restores actual floor repair");
                Next(1036); break;
            case 1036:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                foreach (var x in new[] { 297, 298, 299 }) Require(tiles.GetTile(new Vector3Int(x, 119, 0)).elementType == "insul_wall", "Continue retains repaired and original floor " + x);
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("insul_wall") == 0, "Continue has no duplicate repair items");
                checks.Add($"Repair restore alive={!player.IsDead}; shelter={FindAnyObjectByType<MainGameRuntimeServices>().RoomTemperature.InspectShelter(player.transform.position).Sealed}; segment={Time.realtimeSinceStartup - controlStarted:F3}s.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "invasion-floor-repair-continued.png"));
                Next(1037); break;
            case 1037:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Finish("passed", "Two actually destroyed floor cells repaired through ordinary approach, workbench crafting and placement, saved and restored through Title Continue. No grants/time injection/forced damage. Not complete enclosure, recooling, novice proof or full-night repeat.");
                break;
            case 1060:
                Sample();
                placementPoint = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "nest_bed").position;
                Require(Vector2.Distance(player.transform.position, placementPoint) < 2.5f, "earned actual invasion dawn position is near existing bed");
                Require(FindAnyObjectByType<MainGameRuntimeServices>().Bed.CanSleep(placementPoint, out _, out _), "existing earned bed is usable at day7 dawn");
                checks.Add("Actual first-invasion-full-night-1 day7 dawn source; ordinary rest to day15 night, no direct clock or resource changes.");
                Next(975); break;
            case 1061:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var baekjungEncounters = FindAnyObjectByType<MainGameEncounterCoordinator>();
                var baekjung = baekjungEncounters.BaekjungScheduler;
                checks.Add($"Baekjung precondition diagnostic: active={baekjung.IsActive}, waves={baekjung.DispatchedWaveCount}, elapsedGame={baekjung.ElapsedSeconds}, ended={baekjung.HasEnded}, raid={baekjungEncounters.ActiveRaidCount}");
                Require(baekjung.IsActive && baekjung.DispatchedWaveCount == 1, "normal day15 night starts first Baekjung wave");
                var baekjungBedAllowed = FindAnyObjectByType<MainGameRuntimeServices>().Bed.CanSleep(placementPoint, out var eventRoom, out var eventBedReason);
                checks.Add($"Baekjung reached after {Time.realtimeSinceStartup - controlStarted:F3}s, rests={bedRestCount}; waves={baekjung.DispatchedWaveCount}; elapsedGame={baekjung.ElapsedSeconds}; raid={baekjungEncounters.ActiveRaidCount}; regular={baekjungEncounters.ActiveRegularCount}; bedAllowed={baekjungBedAllowed}; room={eventRoom}; reason={eventBedReason}");
                checks.Add("Actual enemy kinds: " + string.Join(",", FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null).Select(b => b.Definition.Id)));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day15-baekjung-first-wave.png"));
                Next(1062); break;
            case 1062:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Next(BaekjungOvernightRun ? 1070 : 420); break;
            case 1124:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                var nearClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(nearClock.Day == 18 && !nearClock.IsNight && !player.IsDead, "near-resident checkpoint Continue restores living day18 daytime");
                var nearGang = FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null && b.Definition.Kind == YokaiKind.Gangcheori).ToArray();
                Require(nearGang.Length == 1, "near-resident Continue creates exactly one Gangcheori");
                var previousGangPosition = new Vector2(142.5f,12.5f);
                var restoredGangPosition = (Vector2)nearGang[0].transform.position;
                checks.Add($"Near-save Continue observation: player={player.transform.position}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, room={FindAnyObjectByType<MainGameRuntimeServices>().PlayerTemperature.CurrentRoomTemperature}, oldTarget={previousGangPosition}, newTarget={restoredGangPosition}, relocation={Vector2.Distance(previousGangPosition,restoredGangPosition):F3}, playerDistance={Vector2.Distance(player.transform.position,restoredGangPosition):F3}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "near-resident-continued.png"));
                Next(1125); break;
            case 1130:
                if (Time.realtimeSinceStartup - phaseStarted < 2f) { Sample(); return; }
                var victoryClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(victoryClock.Day == 18 && !victoryClock.IsNight && !player.IsDead, "victory Continue restores living day18 daytime");
                var victoryInv = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                Require(victoryInv.Count("gangcheol_scale") == 1 && victoryInv.Count("yokai_tear") == 3, "victory Continue restores scale1 and collected tears3");
                Require(!FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Any(b => b.Definition != null && b.Definition.Kind == YokaiKind.Gangcheori), "killed Gangcheori does not respawn on same-day Continue");
                var residentSnapshot = new Nyangbingo.Save.SaveGame();
                Require(FindAnyObjectByType<MainGameEncounterCoordinator>().CaptureProgress(residentSnapshot) && residentSnapshot.regularEncounter.residentLastKilledDays.Any(r => r.yokaiId == "gangcheol" && r.lastKilledDay == 18), "runtime restored last-killed day18");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "gangcheori-victory-restored.png"));
                Sample(0f, KeyCode.J); Next(1131); break;
            case 1140:
                if (Time.realtimeSinceStartup - phaseStarted < 2f) { Sample(); return; }
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                placedWorkbench = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "workbench");
                var returnInv = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                Require(returnInv.Count("gangcheol_scale") == 1 && returnInv.Count("yokai_tear") == 4, "return starts with earned scale1 and tears4");
                if (SessionState.GetBool("Nyangbingo.QA.Day23Route", false))
                {
                    placementPoint = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "nest_bed").position;
                    placedWorkbench = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "nest_bed");
                    controlStarted = toolStageStarted = Time.realtimeSinceStartup;
                    if (GameObject.Find("ShelterGuide") != null) GameObject.Find("ShelterGuideToggle").GetComponent<Button>().onClick.Invoke();
                    Sample(0f, MiningSlotKey); Next(460); break;
                }
                if (SessionState.GetBool("Nyangbingo.QA.ReturnRecipeAudit", false))
                {
                    Require(Vector2.Distance(player.transform.position, placedWorkbench.position) < 1.5f, "Continue restores workbench arrival within1.5 tiles");
                    var auditRuntime = FindAnyObjectByType<MainGameRuntimeServices>();
                    foreach (var recipeId in new[] { "frostclaw_gauntlet", "ice_altar_offering" })
                    {
                        var auditRecipe = AssetDatabase.LoadAssetAtPath<Nyangbingo.Data.RecipeDefinition>("Assets/Data/SO/Recipes/" + recipeId + ".asset");
                        Require(Nyangbingo.Crafting.RecipeUnlockPolicy.IsUnlocked(auditRecipe, auditRuntime.RecipeBook), recipeId + " available under restored unlock policy");
                        checks.Add("Reward recipe " + recipeId + ", station=" + auditRecipe.Station + ", seconds=" + auditRecipe.DurationSeconds + ": " + string.Join(", ", auditRecipe.Ingredients.Select(i => i.item.Id + "=" + returnInv.Count(i.item.Id) + "/" + i.amount)));
                    }
                    GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedWorkbench.position), false, false, KeyCode.E);
                    Next(1141); break;
                }
                checks.Add($"Normal loot return start={player.transform.position}, target={placedWorkbench.position}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, stone={returnInv.Count("stone")}, shiitake={returnInv.Count("shiitake")}");
                controlStarted = toolStageStarted = Time.realtimeSinceStartup;
                if (GameObject.Find("ShelterGuide") != null) GameObject.Find("ShelterGuideToggle").GetComponent<Button>().onClick.Invoke();
                Sample(0f, MiningSlotKey); Next(460); break;
            case 1150:
                Require(!player.IsDead, "living player walks to earned bed");
                if (Vector2.Distance(player.transform.position, placementPoint) < 1.4f) { Sample(); Next(1151); break; }
                Require(Time.realtimeSinceStartup - phaseStarted < 20f, "bounded nearby bed approach");
                Sample(Mathf.Sign(placementPoint.x - player.transform.position.x)); break;
            case 1151:
                Sample();
                var routeClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var routeRuntime = FindAnyObjectByType<MainGameRuntimeServices>();
                var lateRouteTarget = SessionState.GetInt("Nyangbingo.QA.LateRouteTarget", 23);
                Require(!player.IsDead && routeClock.Day <= lateRouteTarget && bedRestCount <= 10, "bounded living rest route through target day");
                var routeCanRest = routeRuntime.Bed.CanSleep(placementPoint, out var routeRoom, out var routeReason);
                checks.Add($"Late route boundary day={routeClock.Day}, night={routeClock.IsNight}, canRest={routeCanRest}, room={routeRoom}, reason={routeReason}, elapsed={Time.realtimeSinceStartup-controlStarted:F3}s");
                if (routeClock.Day == lateRouteTarget && routeClock.IsNight == (lateRouteTarget != 30 || SessionState.GetBool("Nyangbingo.QA.ImugiNight", false)))
                {
                    if (SessionState.GetBool("Nyangbingo.QA.ImugiNight", false))
                    {
                        var entryBoss = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                        Require(entryBoss.IsBossActive && entryBoss.ActiveDefinition.Id == "imugi_boss", "normal rest boundary has live Imugi");
                        if (SessionState.GetBool("Nyangbingo.QA.ImugiEarnedT3", false))
                            Require(routeRuntime.PlayerInventory.Count("icesteel_claw") == 1, "normal day30 crafted T3 preserved into Imugi encounter");
                        checks.Add($"Imugi entry bossHP={entryBoss.ActiveHealth.Current}, boss={entryBoss.ActiveHealth.transform.position}, player={player.transform.position}, playerHP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, elapsed={Time.realtimeSinceStartup-controlStarted:F3}");
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-entry-immediate.png"));
                    }
                    if (SessionState.GetBool("Nyangbingo.QA.ImugiFight", false))
                    {
                        gangFightStarted = Time.realtimeSinceStartup;
                        gangLastHp = -1;
                        Next(1160); break;
                    }
                    Next(1152); break;
                }
                Require(routeCanRest, "normal earned bed permits next boundary");
                Next(975); break;
            case 1160:
                var fightingImugi = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                var fightingHp = ((Nyangbingo.Combat.Health)PlayerField("health")).Current;
                if (player.IsDead) throw new Exception($"Normal Imugi attack/healing route died after {Time.realtimeSinceStartup-gangFightStarted:F3}s; last observed bossHP={gangLastHp}.");
                if (!fightingImugi.IsBossActive)
                {
                    Sample();
                    checks.Add($"Imugi became inactive at combat {Time.realtimeSinceStartup-gangFightStarted:F3}s; auditing actual result and kill snapshot.");
                    Next(1161); break;
                }
                var imugiHpNow = fightingImugi.ActiveHealth.Current;
                if (imugiHpNow != gangLastHp)
                {
                    checks.Add($"Imugi combat HP={imugiHpNow}, previous={gangLastHp}, playerHP={fightingHp}, elapsed={Time.realtimeSinceStartup-gangFightStarted:F3}, player={player.transform.position}");
                    gangLastHp = imugiHpNow;
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-combat-latest.png"));
                }
                var imugiFightLimit = SessionState.GetBool("Nyangbingo.QA.ImugiFullFight", false) ? 600f : 120f;
                if (Time.realtimeSinceStartup-gangFightStarted > imugiFightLimit) { Sample(); Finish("passed", $"Bounded{imugiFightLimit}s ordinary Imugi combat observation only; no victory or save claim."); break; }
                var imugiDelta = fightingImugi.ActiveHealth.transform.position.x-player.transform.position.x;
                var retreatAxis = Mathf.Abs(imugiDelta) < 2f ? -Mathf.Sign(imugiDelta) : Mathf.Abs(imugiDelta) > 3f ? Mathf.Sign(imugiDelta) : 0f;
                GameplayInput.Sample(retreatAxis, Camera.main.WorldToScreenPoint(fightingImugi.ActiveHealth.transform.position), true, false,
                    (Time.realtimeSinceStartup-gangFightStarted) % 1.4f < .12f ? new[] { KeyCode.Space } : Array.Empty<KeyCode>());
                break;
            case 1161:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var imugiSnapshot = FindAnyObjectByType<Nyangbingo.Save.MainGameSaveCoordinator>().CaptureSnapshot();
                File.WriteAllText(Path.Combine(directory, "imugi-terminal-snapshot.json"), JsonUtility.ToJson(imugiSnapshot, true));
                Require(imugiSnapshot.bossRecords.Any(r => r.bossId == "imugi_boss" && r.count == 1), "actual normal combat records exactly one Imugi kill, not dawn flight");
                checks.Add($"Imugi terminal shell={FindAnyObjectByType<GameShellController>().Screen}, resultKill={FindAnyObjectByType<GameShellController>().Result?.ImugiDefeated}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-terminal-before-result-check.png"));
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Result && FindAnyObjectByType<GameShellController>().Result.ImugiDefeated, "normal Imugi kill opens matching result screen");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-normal-result.png"));
                Next(1162); break;
            case 1162:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                ((Button)typeof(MainGameShellUiController).GetField("resultTitleButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>())).onClick.Invoke();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Gameplay, "ordinary result Continue button returns to gameplay");
                Next(1163); break;
            case 1163:
                if (player.IsDead) throw new Exception("Player died after normal result continuation.");
                if (Time.realtimeSinceStartup - phaseStarted < 3f) { Sample(); return; }
                var imugiPost = FindAnyObjectByType<Nyangbingo.Save.MainGameSaveCoordinator>().CaptureSnapshot();
                checks.Add("Imugi post-result inventory: " + string.Join(",", imugiPost.inventory.Where(s => s.amount > 0).Select(s => s.itemId + "=" + s.amount)));
                checks.Add("Imugi post-result ground: " + string.Join(",", imugiPost.worldDrops.Select(s => s.itemId + "=" + s.amount + "@" + s.position)));
                Require(imugiPost.inventory.Where(s => s.itemId == "yeouiju").Sum(s => s.amount) + imugiPost.worldDrops.Where(s => s.itemId == "yeouiju").Sum(s => s.amount) == 1, "normal kill creates exactly one Yeouiju in inventory plus world, collection assessed separately");
                Sample(0f, KeyCode.Escape); Next(1164); break;
            case 1180:
                Require(!player.IsDead && !FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive, "normal Imugi reward recovery begins alive without active boss");
                Require(Time.realtimeSinceStartup - phaseStarted < 30f, "normal Imugi reward collection stays within30s");
                var imugiLootInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                var imugiLootDrops = FindAnyObjectByType<MainGameWorldDropRuntime>().Export();
                var imugiLootExpected = new[] { ("yeouiju", 1), ("yeouiju_shard", 1), ("yeouiju_claw", 1), ("yokai_tear", 12) };
                foreach (var pair in imugiLootExpected)
                    if (imugiLootInventory.Count(pair.Item1) + imugiLootDrops.Where(d => d.itemId == pair.Item1).Sum(d => d.amount) != pair.Item2)
                        throw new Exception("Unexpected reward total: " + pair.Item1);
                if (imugiLootExpected.All(pair => imugiLootInventory.Count(pair.Item1) == pair.Item2))
                {
                    checks.Add($"All earned Imugi rewards collected after {Time.realtimeSinceStartup-controlStarted:F3}s of control; HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-rewards-collected.png"));
                    Sample(0f, KeyCode.Escape); Next(1164); break;
                }
                var imugiLootTarget = imugiLootDrops.Where(d => imugiLootExpected.Any(pair => pair.Item1 == d.itemId)).OrderBy(d => Vector2.Distance(d.position, player.transform.position)).First();
                Sample(Mathf.Abs(imugiLootTarget.position.x-player.transform.position.x) > .1f ? Mathf.Sign(imugiLootTarget.position.x-player.transform.position.x) : 0f);
                break;
            case 1164:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Pause, "post-Imugi ordinary pause opens");
                var imugiSaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                Require(imugiSaveButton.interactable, "post-Imugi normal save enabled");
                imugiSaveButton.onClick.Invoke(); Next(1165); break;
            case 1165:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var imugiDisk) && imugiDisk.bossRecords.Any(r => r.bossId == "imugi_boss" && r.count == 1), "ordinary disk save retains actual Imugi kill");
                var imugiShell = FindAnyObjectByType<GameShellController>();
                Require(imugiShell.RequestReturnToTitle() && imugiShell.Confirm(), "normal post-Imugi title return");
                Next(1166); break;
            case 1166:
                Sample();
                var imugiTitle = FindAnyObjectByType<TitleShellController>();
                if (imugiTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(imugiTitle.TryContinue(), "normal title Continue loads Imugi victory save");
                Next(1167); break;
            case 1167:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup-phaseStarted < 3f) return;
                var imugiRestored = FindAnyObjectByType<Nyangbingo.Save.MainGameSaveCoordinator>().CaptureSnapshot();
                Require(!player.IsDead && !FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive && imugiRestored.bossRecords.Any(r => r.bossId == "imugi_boss" && r.count == 1), "Continue restores living player and one kill without active Imugi");
                Require(imugiRestored.inventory.Where(s => s.itemId == "yeouiju").Sum(s => s.amount) + imugiRestored.worldDrops.Where(s => s.itemId == "yeouiju").Sum(s => s.amount) == 1, "Continue retains exactly one earned Yeouiju including uncollected ground loot");
                if (SessionState.GetBool("Nyangbingo.QA.ImugiLoot", false))
                    foreach (var pair in new[] { ("yeouiju", 1), ("yeouiju_shard", 1), ("yeouiju_claw", 1), ("yokai_tear", 12) })
                        Require(imugiRestored.inventory.Where(s => s.itemId == pair.Item1).Sum(s => s.amount) == pair.Item2 && !imugiRestored.worldDrops.Any(d => d.itemId == pair.Item1), "Continue retains collected reward without ground duplicate: " + pair.Item1);
                File.WriteAllText(Path.Combine(directory, "imugi-restored-snapshot.json"), JsonUtility.ToJson(imugiRestored, true));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-victory-continued.png"));
                Next(1168); break;
            case 1168:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < 1f) return;
                if (SessionState.GetBool("Nyangbingo.QA.ImugiRewardEquipment", false)) { Next(1190); break; }
                Finish("passed", "Normal Imugi kill record, result screen, result Continue, ordinary save/Title Continue and one Yeouiju across inventory plus ground verified. Loot collection, complete continuous new-game30-day route, novice play and OS restart remain separate.");
                break;
            case 1190:
                Sample(0f, KeyCode.G); Next(1191); break;
            case 1191:
            case 1194:
                if (Time.realtimeSinceStartup-phaseStarted < .3f) { Sample(); return; }
                Require(Time.realtimeSinceStartup-phaseStarted < 15f, "reward equipment selection remains bounded");
                var rewardEquipUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var rewardEquipFlags = BindingFlags.NonPublic | BindingFlags.Instance;
                var rewardActiveItems = (List<Nyangbingo.Data.ItemDefinition>)typeof(MainGameCraftingUiController).GetField("activeSlotItems", rewardEquipFlags).GetValue(rewardEquipUi);
                var rewardEquipmentItems = (List<Nyangbingo.Data.EquipmentDefinition>)typeof(MainGameCraftingUiController).GetField("ownedEquipment", rewardEquipFlags).GetValue(rewardEquipUi);
                var rewardEquipIndex = phase == 1191 ? rewardActiveItems.Count + rewardEquipmentItems.FindIndex(e => e.Id == "yeouiju_shard") : rewardActiveItems.FindIndex(e => e.Id == "yeouiju_claw");
                Require(rewardEquipIndex >= 0 && (phase != 1191 || rewardEquipmentItems.Any(e => e.Id == "yeouiju_shard")), "earned reward available in normal equipment list");
                var rewardSelected = (int)typeof(MainGameCraftingUiController).GetField("selectedIndex", rewardEquipFlags).GetValue(rewardEquipUi);
                if (rewardSelected == rewardEquipIndex)
                {
                    var rewardDescription = ((Text)typeof(MainGameCraftingUiController).GetField("detailsText", rewardEquipFlags).GetValue(rewardEquipUi)).text;
                    Require(phase == 1191
                        ? rewardDescription.Contains("얼음 자연 용해 속도 50%") && rewardDescription.Contains("소지만으로는 적용되지") && !rewardDescription.Contains("방어력: 0")
                        : rewardDescription.Contains("공격 피해 배율: 현재 발톱의 110%") && rewardDescription.Contains("채굴 등급은 유지"), "reward description shows distinct effect and use condition before generic stats");
                    Sample(); ScreenCapture.CaptureScreenshot(Path.Combine(directory, phase == 1191 ? "imugi-shard-description.png" : "imugi-claw-description.png"));
                    Next(phase == 1191 ? 1192 : 1195); break;
                }
                Sample(0f, Time.frameCount % 2 == 0 ? KeyCode.RightArrow : KeyCode.None); break;
            case 1192:
                if (Time.realtimeSinceStartup-phaseStarted < .5f) { Sample(); return; }
                Sample(0f, KeyCode.E); Next(1193); break;
            case 1193:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                var rewardEquipServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(rewardEquipServices.EquipmentSystem.Get(EquipmentSlot.AccessoryOne)?.Id == "yeouiju_shard" || rewardEquipServices.EquipmentSystem.Get(EquipmentSlot.AccessoryTwo)?.Id == "yeouiju_shard", "normal equipment G/right/E equips earned Yeouiju shard");
                var rewardContext = new Nyangbingo.Inventory.ArtifactActivationContext(false, true, false);
                checks.Add($"Equipped shard runtime modifier: radius={rewardEquipServices.ArtifactVerbs.ResolveCoolerRadiusTiles(rewardEquipServices.EquipmentSystem,rewardContext)}, melt={rewardEquipServices.ArtifactVerbs.ResolveIceMeltMultiplier(rewardEquipServices.EquipmentSystem,rewardContext)}; no installed cooler or physical melt test.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-shard-equipped.png"));
                Next(1194); break;
            case 1195:
                if (Time.realtimeSinceStartup-phaseStarted < .5f) { Sample(); return; }
                Sample(0f, KeyCode.E); Next(1196); break;
            case 1196:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                Require(FindAnyObjectByType<MainGameRuntimeServices>().ActiveSlot.EquippedItemId == "yeouiju_claw", "normal equipment selection/E equips earned Yeouiju claw");
                checks.Add($"Equipped claw active combat profile={player.ActiveCombatProfileId}; actual combat damage and persistence not tested here.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-claw-equipped.png"));
                Next(1197); break;
            case 1197:
                if (Time.realtimeSinceStartup-phaseStarted < 1f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(1200); break;
            case 1200:
                if (Time.realtimeSinceStartup-phaseStarted < .5f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(1201); break;
            case 1201:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                var equippedSaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                Require(equippedSaveButton.gameObject.activeInHierarchy && equippedSaveButton.interactable, "equipped rewards allow ordinary pause save");
                equippedSaveButton.onClick.Invoke(); Next(1202); break;
            case 1202:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                var equippedShell = FindAnyObjectByType<GameShellController>();
                Require(equippedShell.RequestReturnToTitle() && equippedShell.Confirm(), "equipped rewards ordinary return to title");
                Next(1203); break;
            case 1203:
                Sample();
                var equippedTitle = FindAnyObjectByType<TitleShellController>();
                if (equippedTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(equippedTitle.TryContinue(), "ordinary Continue after equipped reward save");
                Next(1204); break;
            case 1204:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup-phaseStarted < 3f) return;
                var equippedRestored = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(!player.IsDead && !FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive, "equipped reward Continue alive without boss");
                Require(equippedRestored.EquipmentCollection.Export().Count(id => id == "yeouiju_shard") == 1 && equippedRestored.PlayerInventory.Count("yeouiju_shard") == 0, "promoted shard retained exactly once in equipment collection not inventory");
                Require(equippedRestored.EquipmentSystem.Get(EquipmentSlot.AccessoryOne)?.Id == "yeouiju_shard" || equippedRestored.EquipmentSystem.Get(EquipmentSlot.AccessoryTwo)?.Id == "yeouiju_shard", "Continue retains equipped shard slot");
                Require(equippedRestored.ActiveSlot.EquippedItemId == "yeouiju_claw" && equippedRestored.ActiveSlot.IsUsingEquippedItem && player.ActiveCombatProfileId == "yeouiju_claw", "Continue retains equipped and active claw profile");
                Require(equippedRestored.PlayerInventory.Count("yeouiju_claw") == 0 && equippedRestored.PlayerInventory.Count("yeouiju") == 1 && equippedRestored.PlayerInventory.Count("yokai_tear") == 12, "equipped claw not duplicated in bag; crafting reward and tears retained");
                Require(!FindAnyObjectByType<MainGameWorldDropRuntime>().Export().Any(d => d.itemId == "yeouiju_shard" || d.itemId == "yeouiju_claw" || d.itemId == "yeouiju" || d.itemId == "yokai_tear"), "equipped reward Continue has no duplicate ground rewards");
                File.WriteAllText(Path.Combine(directory, "imugi-equipped-restored-snapshot.json"), JsonUtility.ToJson(FindAnyObjectByType<Nyangbingo.Save.MainGameSaveCoordinator>().CaptureSnapshot(), true));
                checks.Add($"Equipped reward restoration after {Time.realtimeSinceStartup-controlStarted:F3}s of control; active={player.ActiveCombatProfileId}");
                Next(1205); break;
            case 1205:
                Sample(0f, KeyCode.G); Next(1206); break;
            case 1206:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "imugi-equipped-continued-ui.png"));
                Next(1207); break;
            case 1207:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                Finish("passed", "Normal reward pickup then G/right/E equips shard and claw; ordinary pause save/Title Continue retains promoted collection, accessory slot, active weapon and no inventory/ground duplication. No cooler construction, actual reward combat, OS restart or novice proof.");
                break;
            case 1240:
                Sample(0f,KeyCode.Tab); Next(1241); break;
            case 1241:
            {
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.4f)return;
                var placementInventory=FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots;
                var placementSlot=-1;
                for(var i=0;i<placementInventory.Count;i++) if(placementInventory[i].itemId==CoolerPlacementId && placementInventory[i].amount>0){placementSlot=i;break;}
                Require(placementSlot>=0,"earned station in inventory "+CoolerPlacementId);
                var placementUi=FindAnyObjectByType<MainGameCraftingUiController>();
                var placementButtons=(Button[])typeof(MainGameCraftingUiController).GetField("inventoryGridButtons",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(placementUi);
                Require(placementButtons[placementSlot].isActiveAndEnabled,"inventory station selection button visible");
                placementButtons[placementSlot].onClick.Invoke();
                Next(1242); break;
            }
            case 1242:
                if(Time.realtimeSinceStartup-phaseStarted<.3f){Sample();return;}
                SampleUse(); placementProbe=0; Next(1243); break;
            case 1243:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.3f)return;
                Require(FindAnyObjectByType<MainGameTurretRuntime>().IsPlacementPreviewActive,"normal inventory right-click starts station placement");
                Next(1244); break;
            case 1244:
                Sample();
                Require(placementProbe<81,"bounded reachable station placement search");
                placementPoint=tiles.GetCellCenterWorld(tiles.WorldToCell(player.transform.position)+new Vector3Int(placementProbe%9-4,placementProbe/9-4,0));
                placementProbe++;
                if(Vector2.Distance(player.transform.position,placementPoint)<1.45f)Next(1245);
                break;
            case 1245:
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(placementPoint),false,false);
                if(Time.realtimeSinceStartup-phaseStarted>=.1f)Next(FindAnyObjectByType<MainGameTurretRuntime>().IsPlacementPreviewValid?1246:1244);
                break;
            case 1246:
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(placementPoint), false, true);Next(1247);break;
            case 1247:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.3f)return;
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count(CoolerPlacementId)==0 &&
                    FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Count(x=>x.definitionId==CoolerPlacementId)==1,"normal placement consumes exactly one station "+CoolerPlacementId);
                checks.Add($"Cooler station {CoolerPlacementId} installed at {placementPoint}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"cooler-station-"+CoolerPlacementId+".png"));
                if(StorageUseRun){Next(1280);break;}
                if(CoolerInstallRun){Next(1264);break;}
                coolerPlacementIndex++;
                Next(coolerPlacementIndex<2?1240:1250);break;
            case 1280:
                if(Time.realtimeSinceStartup-phaseStarted<.5f){Sample();return;}
                storageTestId=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="jangdok").objectId;
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);Next(1281);break;
            case 1281:
            {
                Sample();if(Time.realtimeSinceStartup-phaseStarted<.5f)return;
                Require(MainGameCraftingUiController.BlocksGameplayInput,"normal right click opens actual storage UI");
                var storageUi=FindAnyObjectByType<MainGameCraftingUiController>();
                var storagePlayerSlots=FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots;
                var storageIceSlot=-1;
                for(var i=0;i<storagePlayerSlots.Count;i++)if(storagePlayerSlots[i].itemId=="ice_shard" && storagePlayerSlots[i].amount==5){storageIceSlot=i;break;}
                Require(storageIceSlot>=0,"five earned ice in player inventory");
                var storageTransferLabels=(Text[])typeof(MainGameCraftingUiController).GetField("storagePlayerLabels",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(storageUi);
                var storageTransferButton=storageTransferLabels[storageIceSlot].transform.parent.GetComponent<Button>();
                Require(storageTransferButton.isActiveAndEnabled,"actual storage transfer button visible");
                storageTransferButton.onClick.Invoke();Next(1282);break;
            }
            case 1282:
            {
                if(Time.realtimeSinceStartup-phaseStarted<.5f){Sample();return;}
                var storageServices=FindAnyObjectByType<MainGameRuntimeServices>();
                Require(storageServices.JangdokStorage.TryGet(storageTestId,out var actualStorage) && actualStorage.Count("ice_shard")==5 && storageServices.PlayerInventory.Count("ice_shard")==0,"UI transfers five ice without duplication");
                storageServices.StorageTemperature.TryGetStatus(storageTestId,out var storageTemp,out var storageBand);
                storageTestDay=FindAnyObjectByType<MainGameBootstrap>().TimeService.Day;
                checks.Add($"Before natural dawn: day={storageTestDay}; storageTemp={storageTemp}; band={storageBand}; ice=5; elapsed={Time.realtimeSinceStartup-controlStarted:F3}.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"storage-ice-before-dawn.png"));
                Sample(0f,KeyCode.Escape);Next(1283);break;
            }
            case 1283:
            {
                Sample();
                Require(!player.IsDead,"living during natural storage observation");
                Require(Time.realtimeSinceStartup-phaseStarted<400f,"bounded natural storage dawn wait");
                if(FindAnyObjectByType<MainGameBootstrap>().TimeService.Day<=storageTestDay)return;
                var dawnStorageServices=FindAnyObjectByType<MainGameRuntimeServices>();
                Require(dawnStorageServices.JangdokStorage.TryGet(storageTestId,out var dawnStorage),"actual storage remains at dawn");
                var dawnIce=dawnStorage.Count("ice_shard");
                checks.Add($"Natural storage dawn: day={FindAnyObjectByType<MainGameBootstrap>().TimeService.Day}; ice={dawnIce}; wait={Time.realtimeSinceStartup-phaseStarted:F3}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}; remainder={dawnStorage.Slots.Where(x=>x.itemId=="ice_shard").Sum(x=>x.storageMeltRemainder)}.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"storage-natural-dawn.png"));Next(420);break;
            }
            case 1284:
            {
                if(Time.realtimeSinceStartup-phaseStarted<3f){Sample();return;}
                var restoredStorageServices=FindAnyObjectByType<MainGameRuntimeServices>();
                var restoredJar=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="jangdok");
                storageTestId=restoredJar.objectId;
                Require(restoredStorageServices.JangdokStorage.TryGet(storageTestId,out var restoredStorage) && restoredStorage.Count("ice_shard")==4 && Mathf.Approximately(restoredStorage.Slots.Where(x=>x.itemId=="ice_shard").Sum(x=>x.storageMeltRemainder),.25f),"Continue restores actual ice4 and melt remainder0.25");
                Require(restoredStorageServices.PlayerInventory.Count("ice_shard")==0,"no player-bag duplication after restore");
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(restoredJar.position), false, false, KeyCode.E);Next(1285);break;
            }
            case 1285:
            {
                Sample();if(Time.realtimeSinceStartup-phaseStarted<.5f)return;
                var restoredJarUi=FindAnyObjectByType<MainGameCraftingUiController>();
                var restoredJarLabel=(Text)typeof(MainGameCraftingUiController).GetField("storageLabelText",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(restoredJarUi);
                var restoredJarHint=(Text)typeof(MainGameCraftingUiController).GetField("storageHintText",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(restoredJarUi);
                Require(restoredJarLabel.isActiveAndEnabled && restoredJarHint.isActiveAndEnabled,"actual restored storage labels visible");
                checks.Add("Restored storage UI: "+restoredJarLabel.text+" / "+restoredJarHint.text);
                Require(restoredJarHint.text.Contains("얼음") && restoredJarHint.text.Contains("-10℃") && !restoredJarHint.text.Contains("음식"),"actual ice-only storage hint names requirement without irrelevant food loss");
                Require(MainGameCraftingUiController.BuildStorageRiskHint(0,1,0,-5,-10).Contains("필요 -5℃"),"food-only hint uses chilled requirement");
                Require(MainGameCraftingUiController.BuildStorageRiskHint(0,1,1,-5,-10).Contains("음식·얼음 2슬롯"),"mixed-risk hint names both classes");
                Require(MainGameCraftingUiController.BuildStorageRiskHint(-10,0,0,-5,-10).StartsWith("보관 등급 충족"),"no-risk hint removes warning");
                var restoredSlotLabels=(Text[])typeof(MainGameCraftingUiController).GetField("storageLabels",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(restoredJarUi);
                Require(restoredSlotLabels[0].text.Contains("4") && restoredSlotLabels[0].isActiveAndEnabled && restoredSlotLabels[0].transform.GetSiblingIndex()==restoredSlotLabels[0].transform.parent.childCount-1,"stored ice count rendered above slot icon");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"storage-restored-ui.png"));Next(1286);break;
            }
            case 1286:
                if(Time.realtimeSinceStartup-phaseStarted<.7f){Sample();return;}
                Sample(0f,KeyCode.Escape);Next(1287);break;
            case 1287:
                Sample();if(Time.realtimeSinceStartup-phaseStarted<.3f)return;
                Next(420);break;
            case 1290:
                Sample();var restoredStorageTitle=FindAnyObjectByType<TitleShellController>();
                if(restoredStorageTitle==null || SceneTransitionRequest.IsLoadingSceneLoaded())return;
                Require(restoredStorageTitle.TryContinue(),"ordinary storage Continue after resave");Next(1291);break;
            case 1291:
            {
                Sample();player=FindAnyObjectByType<MainGamePlayerController>();
                if(player==null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup-phaseStarted<3f)return;
                var secondStorageServices=FindAnyObjectByType<MainGameRuntimeServices>();
                Require(secondStorageServices.JangdokStorage.TryGet(storageTestId,out var secondStorage) && secondStorage.Count("ice_shard")==4 && Mathf.Approximately(secondStorage.Slots.Where(x=>x.itemId=="ice_shard").Sum(x=>x.storageMeltRemainder),.25f),"resaved ice4 and remainder0.25 survive second Continue");
                Require(secondStorageServices.PlayerInventory.Count("ice_shard")==0 && !player.IsDead,"restored living player without ice duplication");
                Finish("passed","Actual ice4 and remainder0.25 restored, storage UI inspected, normal resave/Title Continue retained contents. No additional dawn, closed-room preservation or novice proof.");break;
            }
            case 1264:
            {
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<2f)return;
                var installedServices=FindAnyObjectByType<MainGameRuntimeServices>();
                var installedEnvironment=FindAnyObjectByType<MainGameEnvironmentState>();
                var installedInspection=installedServices.RoomTemperature.InspectShelter(player.transform.position);
                checks.Add($"Installed cooler observation: sealed={installedInspection.Sealed}; coreDelta={installedInspection.CoreDelta}; room={installedServices.RoomTemperature.Resolve(player.transform.position)}; recoveryMultiplier={installedEnvironment.ResolveTemperatureRecoveryMultiplier(player.transform.position,FindAnyObjectByType<MainGameBootstrap>().SealSystem)}; body={installedServices.PlayerTemperature.Current}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}. This open workshop is not a sealed-room benefit test.");
                Next(1256);break;
            }
            case 1262:
                Sample(0f,KeyCode.Escape);Next(1251);break;
            case 1263:
                Sample(0f,KeyCode.Escape);Next(1253);break;
            case 1250:
                Sample();
                anvilFoundryStage=true;
                placedFurnace=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="blast_furnace");
                toolNavigationAttempts=0;Next(600);break;
            case 1251:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.3f)return;
                anvilFoundryStage=false;
                placedFurnace=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="furnace");
                toolNavigationAttempts=0;Next(600);break;
            case 1253:
                if(Time.realtimeSinceStartup-phaseStarted<.3f){Sample();return;}
                placedFurnace=FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="ice_anvil");
                GameplayInput.Sample(0f,Camera.main.WorldToScreenPoint(placedFurnace.position), false, false, KeyCode.E);
                Next(1254);break;
            case 1254:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.3f)return;
                Require(MainGameCraftingUiController.BlocksGameplayInput,"normal right click opens earned installed ice anvil");
                toolNavigationAttempts=0;Next(611);break;
            case 1255:
                if(Time.realtimeSinceStartup-phaseStarted<.6f){Sample();return;}
                Sample(0f,KeyCode.Escape);Next(1256);break;
            case 1256:
                if(Time.realtimeSinceStartup-phaseStarted<.4f){Sample();return;}
                Sample(0f,KeyCode.Escape);Next(1257);break;
            case 1257:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.4f)return;
                var coolerSaveButton=(Button)typeof(MainGameShellUiController).GetField("pauseSaveButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                Require(coolerSaveButton.isActiveAndEnabled && coolerSaveButton.interactable,"normal cooler save button available");
                coolerSaveButton.onClick.Invoke();Next(1258);break;
            case 1258:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.4f)return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _,out var coolerDisk) && coolerDisk.inventory.Where(x=>x.itemId=="ice_crystal_cooler").Sum(x=>x.amount)==(CoolerInstallRun?0:1) &&
                    coolerDisk.inventory.Where(x=>x.itemId=="yeouiju").Sum(x=>x.amount)==0,"ordinary save retains one normally crafted cooler and spent reward");
                if(CoolerInstallRun)Require(coolerDisk.placedObjectRecords.Count(x=>x.definitionId=="ice_crystal_cooler")==1,"ordinary save persists installed cooler");
                checks.Add($"Cooler saved day={coolerDisk.day}; elapsed={Time.realtimeSinceStartup-controlStarted:F3}; ore/coal/copper must match ordinary recipe costs.");
                var coolerShell=FindAnyObjectByType<GameShellController>();
                Require(coolerShell.RequestReturnToTitle() && coolerShell.Confirm(),"normal crafted cooler title return");Next(1259);break;
            case 1259:
                Sample();
                var coolerTitle=FindAnyObjectByType<TitleShellController>();
                if(coolerTitle==null || SceneTransitionRequest.IsLoadingSceneLoaded())return;
                Require(coolerTitle.TryContinue(),"normal crafted cooler Continue");Next(1260);break;
            case 1260:
                Sample();
                player=FindAnyObjectByType<MainGamePlayerController>();
                if(player==null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup-phaseStarted<3f)return;
                Require(!player.IsDead && FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ice_crystal_cooler")== (CoolerInstallRun?0:1),"Continue restores living player and crafted cooler");
                if(CoolerInstallRun)Require(FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Count(x=>x.definitionId=="ice_crystal_cooler")==1,"Continue restores installed cooler without bag duplicate");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"cooler-crafted-continued.png"));Next(1261);break;
            case 1261:
                Sample();
                if(Time.realtimeSinceStartup-phaseStarted<.7f)return;
                Finish("passed",CoolerInstallRun ? "Normal installation of earned cooler in open workshop, read-only room observation and ordinary save/Title Continue. No sealed-room benefit, actual storage change or novice proof." : "Earned furnace/anvil normal installation, five ice and three copper smelts, normal cooler craft and ordinary save/Title Continue. Cooler placement/effect, novice discovery and continuous new-game30-day completion remain unverified.");break;
            case 1230:
                Sample(0f, KeyCode.Alpha7); Next(1231); break;
            case 1235:
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("icesteel_ore")==9,"cooler ore source contains nine earned ore");
                placedWorkbench = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(x=>x.definitionId=="blast_furnace");
                checks.Add("Normal approach/mining toward persisted ore at304.775,39.227. Newly mined ore may satisfy requirement first; no forced pickup.");
                Sample(0f,KeyCode.Alpha7); Next(1236); break;
            case 1236:
            {
                var oreServices=FindAnyObjectByType<MainGameRuntimeServices>();
                var oreHp=((Nyangbingo.Combat.Health)PlayerField("health")).Current;
                if (player.IsDead || oreHp<=15) throw new Exception("Cooler ore route safety stop; no completion claim.");
                if (oreServices.PlayerInventory.Count("icesteel_ore")>=10)
                {
                    checks.Add($"Cooler ore requirement reached at {Time.realtimeSinceStartup-controlStarted:F3}s; ore={oreServices.PlayerInventory.Count("icesteel_ore")}; player={player.transform.position}; HP={oreHp}.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory,"cooler-ore-collected.png"));
                    Sample(); Next(460); break;
                }
                if (Time.realtimeSinceStartup-controlStarted>180f) throw new Exception("Cooler ore descent exceeded180seconds.");
                if (oreHp<=60 && oreServices.PlayerInventory.Count("oyster_mushroom")>0)
                {
                    checks.Add($"Cooler ore normal oyster consumption requested HP={oreHp}.");
                    SampleUse(); Next(1238); break;
                }
                var oreOffset=304.5f-player.transform.position.x;
                var oreMove=Mathf.Abs(oreOffset)>.15f ? Mathf.Sign(oreOffset) : 0f;
                var oreCell=tiles.WorldToCell(player.transform.position+(oreMove!=0f ? new Vector3(oreMove*.8f,.5f) : new Vector3(0f,-.6f)));
                if (!tiles.GetTile(oreCell).IsAir)
                    GameplayInput.Sample(oreMove,Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(oreCell)),true,false);
                else Sample(oreMove);
                break;
            }
            case 1238:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .4f) return;
                checks.Add($"Cooler ore after consumption HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}; oyster={FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("oyster_mushroom")}.");
                Next(1236); break;
            case 1231:
            {
                if (player.IsDead) throw new Exception("Reward workshop return ended in death.");
                var workshopServices = FindAnyObjectByType<MainGameRuntimeServices>();
                var workshopTarget = new Vector3(299.5f,120.23f,0f);
                if (workshopServices.PlayerInventory.Count("workbench") == 1 && Vector2.Distance(player.transform.position,workshopTarget) < 2f)
                {
                    checks.Add($"Workshop return reached after {Time.realtimeSinceStartup-controlStarted:F3}s; player={player.transform.position}; HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}; workbench={workshopServices.PlayerInventory.Count("workbench")}; ore={workshopServices.PlayerInventory.Count("icesteel_ore")}; yeouiju={workshopServices.PlayerInventory.Count("yeouiju")}.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory,"imugi-workshop-return.png"));
                    Sample(); Next(1232); break;
                }
                if (Time.realtimeSinceStartup-controlStarted > 55f) throw new Exception("Workshop return exceeded55seconds; inspect route.");
                var workshopMove = Mathf.Abs(workshopTarget.x-player.transform.position.x) > .4f ? Mathf.Sign(workshopTarget.x-player.transform.position.x) : 0f;
                var workshopCell = tiles.WorldToCell(player.transform.position + new Vector3(workshopMove*.9f,.5f));
                if (workshopMove != 0f && !tiles.GetTile(workshopCell).IsAir)
                    GameplayInput.Sample(workshopMove,Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(workshopCell)),true,false);
                else Sample(workshopMove);
                break;
            }
            case 1232:
                if (Time.realtimeSinceStartup-phaseStarted < .5f) { Sample(); return; }
                Sample(0f,KeyCode.Escape); Next(1233); break;
            case 1233:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                var workshopSaveUi = FindAnyObjectByType<MainGameShellUiController>();
                var workshopSaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(workshopSaveUi);
                Require(workshopSaveButton.gameObject.activeInHierarchy && workshopSaveButton.interactable,"ordinary workshop checkpoint save available");
                workshopSaveButton.onClick.Invoke(); Next(1234); break;
            case 1234:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .5f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _,out var workshopDisk),"workshop checkpoint written to ordinary isolated save");
                Require(workshopDisk.inventory.Where(x=>x.itemId=="workbench").Sum(x=>x.amount)==1 && workshopDisk.inventory.Where(x=>x.itemId=="yeouiju").Sum(x=>x.amount)==1,"workbench pickup and earned Yeouiju persisted");
                if(SessionState.GetBool("Nyangbingo.QA.CoolerOre",false))
                    Require(workshopDisk.inventory.Where(x=>x.itemId=="icesteel_ore").Sum(x=>x.amount)>=10,"ordinary workshop save has enough cooler ore");
                File.WriteAllText(Path.Combine(directory,"workshop-return-snapshot.json"),JsonUtility.ToJson(workshopDisk,true));
                Finish("passed",SessionState.GetBool("Nyangbingo.QA.CoolerOre",false)
                    ? "Normal additional cooler ore acquisition, block ascent/bridge return and ordinary workshop save with ore>=10. No station installation, cooler craft/effect, Title Continue or full30-day completion."
                    : "Normal post-Imugi return, original workbench recovery and ordinary save. Not extra ore retrieval, station installation, cooler crafting/effect, Title Continue or full30-day completion.");
                break;
            case 1210:
                Require(player.ActiveCombatProfileId == "yeouiju_claw", "earned equipped Yeouiju profile restored before comparison");
                combatRun = true;
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                combatElevation = tiles.FindSurfaceNaturalY(tiles.WorldToCell(player.transform.position).x) + 1.38f;
                combatAscentStarted = Time.realtimeSinceStartup;
                checks.Add($"Reward approach requires normal surface ascent to {combatElevation:F2}; starts {player.transform.position}.");
                Sample(0f, KeyCode.Alpha7); Next(460); break;
            case 1216:
                Sample(0f, KeyCode.Q); Next(1217); break;
            case 1217:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .3f) return;
                Require(player.ActiveCombatProfileId == "yeouiju_claw", "normal Q restores reward after ascent bare-hand selection");
                combatStarted = Time.realtimeSinceStartup;
                Next(1211); break;
            case 1211:
            case 1213:
            {
                if (player.IsDead || ((Nyangbingo.Combat.Health)PlayerField("health")).Current <= 15) throw new Exception("Reward combat safety stop HP<=15.");
                if (Time.realtimeSinceStartup - combatStarted > 150f) throw new Exception("Reward combat surface observation exceeded150seconds.");
                var rewardProfile = ((Nyangbingo.Combat.MeleeArcAttack)PlayerField("attack")).CombatProfile;
                var expectedRewardProfile = phase == 1211 ? "yeouiju_claw" : "icesteel_claw";
                if (rewardProfile.Id != expectedRewardProfile) throw new Exception("Comparison active profile expected=" + expectedRewardProfile + "; actual=" + rewardProfile.Id);
                if (combatTarget != null)
                {
                    var observedHealth = combatTarget.GetComponent<Nyangbingo.Combat.Health>().Current;
                    if (observedHealth < combatTargetHealth)
                    {
                        checks.Add($"Reward combat hit: profile={rewardProfile.Id}; baseDamage={rewardProfile.AttackDamage}; foe={combatTarget.Definition.Id}; hpBefore={combatTargetHealth}; hpAfter={observedHealth}; delta={combatTargetHealth-observedHealth}; slowRemaining={combatTarget.FrostSlowRemaining:F3}; speedMultiplier={combatTarget.FrostSpeedMultiplier:F3}; elapsed={Time.realtimeSinceStartup-combatStarted:F3}; playerHP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, phase == 1211 ? "reward-claw-actual-hit.png" : "base-t3-actual-hit.png"));
                        Sample(); combatTarget = null;
                        Next(phase == 1211 ? 1212 : 1214); break;
                    }
                }
                var rewardFoe = FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>()
                    .Where(b => b.Definition != null && b.GetComponent<Nyangbingo.Combat.Health>() != null && !b.GetComponent<Nyangbingo.Combat.Health>().IsDead)
                    .OrderBy(b => Vector2.Distance(b.transform.position, player.transform.position)).FirstOrDefault();
                if (rewardFoe == null) { Sample(); break; }
                if (rewardFoe != combatTarget)
                    checks.Add($"Reward target: {rewardFoe.Definition.Id} at {rewardFoe.transform.position}; player={player.transform.position}; elapsed={Time.realtimeSinceStartup-combatStarted:F3}");
                combatTarget = rewardFoe;
                combatTargetHealth = rewardFoe.GetComponent<Nyangbingo.Combat.Health>().Current;
                var probeDelta = rewardFoe.transform.position - player.transform.position;
                var probeMove = Mathf.Abs(probeDelta.x) > 1f ? Mathf.Sign(probeDelta.x) : 0f;
                if (probeMove != 0f && probeDelta.sqrMagnitude > 4f)
                {
                    var obstacleCell = tiles.WorldToCell(player.transform.position + new Vector3(probeMove * .9f, .5f));
                    if (!tiles.GetTile(obstacleCell).IsAir)
                    {
                        GameplayInput.Sample(probeMove, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(obstacleCell)), true, false);
                        break;
                    }
                }
                var rewardJump = Time.realtimeSinceStartup >= combatJumpAt && (probeMove != 0f || probeDelta.y > 1f);
                if (rewardJump) combatJumpAt = Time.realtimeSinceStartup + .9f;
                GameplayInput.Sample(probeMove, Camera.main.WorldToScreenPoint(rewardFoe.transform.position), probeDelta.sqrMagnitude <= 4f, false,
                    rewardJump ? new[] { KeyCode.Space } : Array.Empty<KeyCode>());
                break;
            }
            case 1212:
                if (Time.realtimeSinceStartup-phaseStarted < .7f) { Sample(); return; }
                Sample(0f, KeyCode.Q); Next(1215); break;
            case 1215:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .3f) return;
                Require(player.ActiveCombatProfileId == "icesteel_claw", "ordinary Q switches reward to earned base T3");
                Next(1213); break;
            case 1214:
                Sample();
                if (Time.realtimeSinceStartup-phaseStarted < .7f) return;
                Finish("passed", "Observed actual normal attacks with earned Yeouiju then normal Q base T3. Read individual HP/slow measurements; not all enemies, reward loot save, novice timing or full30-day completion.");
                break;
            case 1152:
                Sample();
                if (player.IsDead)
                {
                    checks.Add($"Late night death after observation={Time.realtimeSinceStartup-phaseStarted:F3}s; controlElapsed={Time.realtimeSinceStartup-controlStarted:F3}s; player={player.transform.position}");
                    throw new Exception("Late night observation ended in death.");
                }
                if (Time.realtimeSinceStartup - phaseStarted < 12f) return;
                var lateBrains = FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null).ToArray();
                checks.Add("Late night actual runtime enemies: " + string.Join(";", lateBrains.Select(b => b.Definition.Id + "@" + b.transform.position)));
                var lateHud = FindAnyObjectByType<MainGameHudController>();
                var lateAlert = (Text)typeof(MainGameHudController).GetField("alertOverlayText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lateHud);
                checks.Add($"Late night alert active={lateAlert.isActiveAndEnabled}, text={lateAlert.text}");
                if (SessionState.GetBool("Nyangbingo.QA.ImugiNight", false))
                {
                    var imugiManager = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                    Require(imugiManager.IsBossActive && imugiManager.ActiveDefinition.Id == "imugi_boss", "ordinary day30 night starts Imugi without offering or forced spawn");
                    checks.Add($"Imugi actual state: HP={imugiManager.ActiveHealth.Current}, boss={imugiManager.ActiveHealth.transform.position}, player={player.transform.position}, distance={Vector2.Distance(imugiManager.ActiveHealth.transform.position,player.transform.position):F3}");
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day" + SessionState.GetInt("Nyangbingo.QA.LateRouteTarget", 23) + "-boundary-observed.png"));
                Next(1153); break;
            case 1153:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                if (SessionState.GetBool("Nyangbingo.QA.ImugiNight", false))
                {
                    Finish("passed", "Normal day30 bed transition and actual living Imugi spawn observed. No fight, victory, reward or night checkpoint saved; input dawn save preserved. Not novice discovery or full30-day completion.");
                    break;
                }
                Next(1122); break;
            case 1141:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput, "normal right-click opens workbench after earned return Continue");
                var rewardUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var rewardFlags = BindingFlags.NonPublic | BindingFlags.Instance;
                var rewardRecipes = (List<Nyangbingo.Data.RecipeDefinition>)typeof(MainGameCraftingUiController).GetField("filteredRecipes", rewardFlags).GetValue(rewardUi);
                checks.Add("Returned workbench visible recipes: " + string.Join(",", rewardRecipes.Select(r => r.Id)));
                checks.Add("Returned workbench details: " + ((Text)typeof(MainGameCraftingUiController).GetField("detailsText", rewardFlags).GetValue(rewardUi)).text);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "returned-workbench-recipe-view.png"));
                Next(1142); break;
            case 1142:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Finish("passed", "Earned return save Continue and ordinary workbench right-click; read-only reward recipe requirements and restored unlock policy. No crafting or material grants; not proof of novice discovery, altar use or day30 completion.");
                break;
            case 1131:
                if (Time.realtimeSinceStartup - phaseStarted < 1f) { Sample(); return; }
                var codexUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var codexFlags = BindingFlags.NonPublic | BindingFlags.Instance;
                var qaCodex = (Nyangbingo.Save.YokaiCodexPresentationModel)typeof(MainGameCraftingUiController).GetField("codexModel", codexFlags).GetValue(codexUi);
                var qaCodexButtons = (Button[])typeof(MainGameCraftingUiController).GetField("codexCardButtons", codexFlags).GetValue(codexUi);
                var qaGrid = (GameObject)typeof(MainGameCraftingUiController).GetField("codexGridRoot", codexFlags).GetValue(codexUi);
                checks.Add($"Codex open: cards={qaCodex.Cards.Count}, selected={qaCodex.SelectedCard?.EntryId ?? "none"}, grid={qaGrid.activeInHierarchy}, layout={qaGrid.GetComponent<GridLayoutGroup>() != null}, rect={qaGrid.GetComponent<RectTransform>().rect}");
                for (var ci = 0; ci < qaCodexButtons.Length; ci++)
                {
                    var cb = qaCodexButtons[ci];
                    checks.Add($"Codex card{ci}: id={qaCodex.Cards[ci].EntryId}, unlocked={qaCodex.Cards[ci].IsUnlocked}, kills={qaCodex.Cards[ci].KillCount}, active={cb.gameObject.activeInHierarchy}, rect={cb.GetComponent<RectTransform>().rect}, pos={cb.GetComponent<RectTransform>().anchoredPosition}");
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "gangcheori-victory-codex.png"));
                Sample(); Next(1133); break;
            case 1133:
                if (Time.realtimeSinceStartup - phaseStarted < .5f) { Sample(); return; }
                var pickCodexUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var pickFlags = BindingFlags.NonPublic | BindingFlags.Instance;
                var pickModel = (Nyangbingo.Save.YokaiCodexPresentationModel)typeof(MainGameCraftingUiController).GetField("codexModel", pickFlags).GetValue(pickCodexUi);
                var pickButtons = (Button[])typeof(MainGameCraftingUiController).GetField("codexCardButtons", pickFlags).GetValue(pickCodexUi);
                var gangCardIndex = Enumerable.Range(0, pickModel.Cards.Count).Single(i => pickModel.Cards[i].EntryId == "gangcheol");
                Require(pickModel.Cards[gangCardIndex].IsUnlocked && pickModel.Cards[gangCardIndex].KillCount == 1, "live codex model unlocks Gangcheori kill1");
                pickButtons[gangCardIndex].onClick.Invoke();
                Sample(); Next(1134); break;
            case 1134:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "gangcheori-codex-selected.png"));
                Next(1135); break;
            case 1135:
                if (Time.realtimeSinceStartup - phaseStarted < .5f) { Sample(); return; }
                if (SessionState.GetBool("Nyangbingo.QA.CodexLayout", false))
                {
                    var layoutUi = FindAnyObjectByType<MainGameCraftingUiController>();
                    var lf = BindingFlags.NonPublic | BindingFlags.Instance;
                    var backdrop = (GameObject)typeof(MainGameCraftingUiController).GetField("codexExpandedBackdrop", lf).GetValue(layoutUi);
                    backdrop.GetComponent<Button>().onClick.Invoke();
                    var root = (GameObject)typeof(MainGameCraftingUiController).GetField("codexGridRoot", lf).GetValue(layoutUi);
                    var buttons = (Button[])typeof(MainGameCraftingUiController).GetField("codexCardButtons", lf).GetValue(layoutUi);
                    Require(buttons.Select(b => b.GetComponent<RectTransform>().anchoredPosition).Distinct().Count() == 17, "all17 codex cards occupy distinct layout positions");
                    var scroller = root.GetComponent<ScrollRect>();
                    Require(scroller != null && scroller.content.rect.height > scroller.viewport.rect.height, "codex has overflow content and scroll viewport");
                    scroller.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { scrollDelta = new Vector2(0f, -30f) });
                    Sample(); Next(1136); break;
                }
                Sample(0f, KeyCode.Escape); Next(1132); break;
            case 1136:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var bottomUi = FindAnyObjectByType<MainGameCraftingUiController>();
                var bottomRoot = (GameObject)typeof(MainGameCraftingUiController).GetField("codexGridRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bottomUi);
                Require(bottomRoot.GetComponent<ScrollRect>().verticalNormalizedPosition < .01f, "UI scroll event reaches last row");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "codex-bottom-row.png"));
                Next(1137); break;
            case 1137:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Finish("passed", "Codex distinct17 layout, unlocked Gangcheori detail callback and UI scroll event to last row verified. Not physical pointer hit test, small resolution or return-route proof.");
                break;
            case 1132:
                if (player.IsDead) throw new Exception("Victory loot pickup ended in death.");
                if (FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("yokai_tear") == 4)
                {
                    Sample(); checks.Add("Normal movement collected remaining persisted tear:3->4");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "gangcheori-last-tear-collected.png"));
                    Next(1122); break;
                }
                Require(Time.realtimeSinceStartup - phaseStarted < 12f, "remaining tear pickup stays bounded");
                Sample(player.transform.position.x > 140.3f ? -1f : 0f); break;
            case 1125:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Finish("passed", "Near-resident Continue observation completed. See old/new target coordinates for relocation; not exact actor-state restoration, combat or return-route approval.");
                break;
            case 1120:
                Sample(0f, KeyCode.Alpha7);
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Require(FindAnyObjectByType<MainGameBootstrap>().TimeService.Day == 18 && !player.IsDead, "earned day18 dawn starts living approach");
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                if (GameObject.Find("ShelterGuide") != null) GameObject.Find("ShelterGuideToggle").GetComponent<Button>().onClick.Invoke();
                gangApproachLogAt = 0f;
                gangApproachStarted = Time.realtimeSinceStartup;
                gangApproachDescending = false;
                gangApproachLastPosition = player.transform.position;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "gangcheori-approach-start.png"));
                Next(1121); break;
            case 1121:
                if (player.IsDead) throw new Exception("Normal Gangcheori approach ended in death.");
                var gangTarget = FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().SingleOrDefault(b => b.Definition != null && b.Definition.Kind == YokaiKind.Gangcheori && !b.GetComponent<Nyangbingo.Combat.Health>().IsDead);
                if (gangTarget == null)
                {
                    Require(GangcheoriFightRun && gangLastHp > 0, "previously observed living Gangcheori no longer alive; verify kill progress separately");
                    checks.Add($"Gangcheori absent after close-range segment={Time.realtimeSinceStartup-gangFightStarted:F3}s, approach+combat={Time.realtimeSinceStartup-gangApproachStarted:F3}s");
                    Sample(); ScreenCapture.CaptureScreenshot(Path.Combine(directory, "gangcheori-no-longer-alive.png"));
                    Next(1126); break;
                }
                var gangPosition = (Vector2)player.transform.position;
                var gangDelta = (Vector2)gangTarget.transform.position - gangPosition;
                var gangElapsed = Time.realtimeSinceStartup - gangApproachStarted;
                if (Time.realtimeSinceStartup >= gangApproachLogAt)
                {
                    checks.Add($"Gangcheori approach real={gangElapsed:F3}, player={gangPosition}, target={gangTarget.transform.position}, distance={gangDelta.magnitude:F2}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, moved={(gangPosition-gangApproachLastPosition).magnitude:F2}");
                    gangApproachLastPosition = gangPosition;
                    gangApproachLogAt = Time.realtimeSinceStartup + 30f;
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"gangcheori-route-{(int)gangElapsed:D3}.png"));
                }
                if ((!GangcheoriFightRun && gangDelta.magnitude < 12f) || gangElapsed > 600f)
                {
                    Sample();
                    checks.Add(gangDelta.magnitude < 12f ? "Normal movement reached within12 tiles; combat not started by policy." : "Approach segment reached600s; target not yet reached, preserve real progress.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "gangcheori-approach-stop.png"));
                    Next(1122); break;
                }
                if (GangcheoriFightRun && gangDelta.magnitude < 12f)
                {
                    if (gangFightStarted < 0f)
                    {
                        gangFightStarted = Time.realtimeSinceStartup;
                        checks.Add($"Continuous approach reached12 tiles at {gangElapsed:F3}s; continuing without save/reload.");
                    }
                    var gangHp = gangTarget.GetComponent<Nyangbingo.Combat.Health>().Current;
                    if (gangHp != gangLastHp)
                    {
                        checks.Add($"Gangcheori live HP={gangHp}, previous={gangLastHp}, closeSegment={Time.realtimeSinceStartup-gangFightStarted:F3}, distance={gangDelta.magnitude:F2}, playerHP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                        gangLastHp = gangHp;
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"gangcheori-hp-{gangHp}.png"));
                    }
                    if (gangDelta.magnitude <= 2.5f)
                    {
                        GameplayInput.Sample(Mathf.Abs(gangDelta.x) > 1f ? Mathf.Sign(gangDelta.x) : 0f, Camera.main.WorldToScreenPoint(gangTarget.transform.position), true, false);
                        break;
                    }
                }
                var gangCell = tiles.WorldToCell(player.transform.position);
                if (GangcheoriPreparedRun && Mathf.Abs(gangDelta.x) < 4f) gangApproachDescending = true;
                var gangNavigationDelta = GangcheoriPreparedRun && !gangApproachDescending
                    ? new Vector2(gangTarget.transform.position.x, 62f) - gangPosition : gangDelta;
                var gangRoutePoint = gangPosition + gangNavigationDelta.normalized * 3f;
                var gangAim = gangRoutePoint;
                var gangBestScore = float.PositiveInfinity;
                for (var gx = -3; gx <= 3; gx++)
                for (var gy = -3; gy <= 1; gy++)
                {
                    var candidate = gangCell + new Vector3Int(gx,gy,0);
                    if (GangcheoriPreparedRun && !gangApproachDescending && candidate.y < 60) continue;
                    if (!tiles.InBounds(candidate) || tiles.GetTile(candidate).IsAir) continue;
                    var point = (Vector2)tiles.GetCellCenterWorld(candidate);
                    if (!MainGamePlayerController.TryPickMiningCell(tiles, gangPosition, point, point-gangPosition, 4f, out var picked) || picked != candidate) continue;
                    var score = (point-gangRoutePoint).sqrMagnitude + .3f*(point-gangPosition).sqrMagnitude;
                    if (score >= gangBestScore) continue;
                    gangBestScore = score; gangAim = point;
                }
                GameplayInput.Sample(Mathf.Abs(gangNavigationDelta.x) > 1f ? Mathf.Sign(gangNavigationDelta.x) : 0f, Camera.main.WorldToScreenPoint(gangAim), true, false);
                break;
            case 1126:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                checks.Add("Post-combat inventory: " + string.Join(";", FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Slots.Where(s => s.amount > 0).Select(s => s.itemId + "=" + s.amount)));
                Next(1122); break;
            case 1122:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().OpenPause(), "normal approach pause opens");
                var gangSaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                Require(gangSaveButton.isActiveAndEnabled && gangSaveButton.interactable, "normal approach save available");
                gangSaveButton.onClick.Invoke(); Next(1123); break;
            case 1123:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var gangDisk) && Vector2.Distance(gangDisk.playerState.position, player.transform.position) < .2f, "approach checkpoint preserves actual position");
                if (GangcheoriVictoryRestoreRun)
                {
                    Require(gangDisk.inventory.Where(r => r.itemId == "yokai_tear").Sum(r => r.amount) == 4 && gangDisk.inventory.Where(r => r.itemId == "gangcheol_scale").Sum(r => r.amount) == 1, "normal save retains tears4 and scale1");
                    Require(gangDisk.dogam.Any(r => r.yokaiId == "gangcheol" && r.kills == 1), "loaded codex kill1 survives normal resave");
                    Require(gangDisk.regularEncounter.residentLastKilledDays.Any(r => r.yokaiId == "gangcheol" && r.lastKilledDay == 18), "normal resave retains resident last-killed day18");
                }
                Finish("passed", "Bounded normal movement/mining approach segment saved. Actual distance is in checks; passed verifies checkpoint, not arrival or victory. No teleport, item grants, clock or HP injection.");
                break;
            case 1110:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var day18Start = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(day18Start.Day == 17 && !day18Start.IsNight && !player.IsDead, "earned day17 dawn restored alive");
                placementPoint = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "nest_bed").position;
                Require(Vector2.Distance(player.transform.position, placementPoint) < 2f, "earned bed reachable without teleport");
                checks.Add("Day17 actual enemies: " + string.Join(";", FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null).Select(b => b.Definition.Id + "@" + b.transform.position)));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day17-before-rest.png"));
                Next(975); break;
            case 1111:
                Sample();
                var day18Clock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(bedRestCount <= 3 && day18Clock.Day <= 18, "bounded normal three-rest route to day18 night");
                checks.Add($"Resident checkpoint day={day18Clock.Day} night={day18Clock.IsNight}: " + string.Join(";", FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null).Select(b => b.Definition.Id + "@" + b.transform.position)));
                if (day18Clock.Day != 18) { Next(975); break; }
                Require(FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Count(b => b.Definition != null && b.Definition.Kind == Nyangbingo.Core.YokaiKind.Gangcheori) == 1, "day18 naturally creates exactly one Gangcheori resident");
                if (!day18Clock.IsNight) { Next(975); break; }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day18-night.png"));
                Next(1112); break;
            case 1112:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 5f) return;
                checks.Add("Day18 night runtime enemies: " + string.Join(";", FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null).Select(b => b.Definition.Id + "@" + b.transform.position)));
                var day18SaveShell = FindAnyObjectByType<GameShellController>();
                Require(day18SaveShell.OpenPause(), "normal day18 pause opens");
                var day18SaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                Require(day18SaveButton.isActiveAndEnabled && day18SaveButton.interactable, "normal day18 pause save available");
                day18SaveButton.onClick.Invoke();
                Next(1116); break;
            case 1116:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var day18Disk) && day18Disk.day == 18 && day18Disk.timeState.isNight, "ordinary pause save contains day18 night");
                File.Copy(Path.Combine(directory, "nyangbingo-save-0.json"), Path.Combine(directory, "day18-night-save.json"), true);
                var day18ReturnShell = FindAnyObjectByType<GameShellController>();
                Require(day18ReturnShell.RequestReturnToTitle() && day18ReturnShell.Confirm(), "normal day18 return to Title");
                Next(1113); break;
            case 1113:
                Sample();
                var day18Title = FindAnyObjectByType<TitleShellController>();
                if (day18Title == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(day18Title.TryContinue(), "normal day18 Title Continue");
                Next(1114); break;
            case 1114:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var day18Restored = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(day18Restored.Day == 18 && day18Restored.IsNight && !player.IsDead, "day18 night Continue restores living player");
                Require(FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Count(b => b.Definition != null && b.Definition.Kind == Nyangbingo.Core.YokaiKind.Gangcheori) == 1, "day18 Continue reconciles one Gangcheori without duplication");
                checks.Add("Restored day18 runtime enemies: " + string.Join(";", FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null).Select(b => b.Definition.Id + "@" + b.transform.position)));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day18-night-continued.png"));
                Next(1115); break;
            case 1115:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                Finish("passed", "Day18 normal rest progression, runtime resident presence and night save Continue verified. No resident approach, combat, loot or novice discovery proof.");
                break;
            case 1100:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var restoredSecondClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var restoredSecondServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(restoredSecondClock.Day == ObservedInvasionDay && restoredSecondClock.IsNight && restoredSecondServices.Invasion.IsCurrentInvasionNight && !player.IsDead, "normal Title Continue restores living target invasion night");
                placementPoint = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "nest_bed").position;
                Require(!restoredSecondServices.Bed.CanSleep(placementPoint, out _, out var restoredSecondReason) && restoredSecondReason.Contains("침공"), "Continue preserves invasion bed restriction");
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                checks.Add($"Second invasion initial state: clock={restoredSecondClock.TimeOfDayGameSeconds:F3}, heat={restoredSecondServices.Invasion.TemperatureRiseCelsius}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"day{ObservedInvasionDay}-night-continued.png"));
                invasionObservationBucket = -1;
                Next(1101); break;
            case 1101:
                Sample();
                var secondObserveClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var secondObserveServices = FindAnyObjectByType<MainGameRuntimeServices>();
                var secondObserveEncounters = FindAnyObjectByType<MainGameEncounterCoordinator>();
                var secondObserveReal = Time.realtimeSinceStartup - phaseStarted;
                var secondObserveBucket = (int)(secondObserveReal / 30f);
                if (secondObserveBucket != invasionObservationBucket)
                {
                    invasionObservationBucket = secondObserveBucket;
                    checks.Add($"Second invasion observation: real={secondObserveReal:F3}, day={secondObserveClock.Day}, clock={secondObserveClock.TimeOfDayGameSeconds:F3}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, heat={secondObserveServices.Invasion.TemperatureRiseCelsius}, recoolDay={secondObserveServices.Invasion.RecoolAvailableDay}, raid={secondObserveEncounters.ActiveRaidCount}, regular={secondObserveEncounters.ActiveRegularCount}, pending={secondObserveEncounters.PendingRegularCount}; floor=" + string.Join(",", Enumerable.Range(297, 6).Select(x => x + ":" + tiles.GetTile(new Vector3Int(x,119,0)).elementType)));
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"day{ObservedInvasionDay}-night-observation-{secondObserveBucket:D2}.png"));
                }
                if (player.IsDead) throw new Exception("Second invasion observation ended in player death; no full-night survival claim.");
                if (secondObserveClock.Day != ObservedInvasionDay + 1 || secondObserveClock.IsNight) return;
                Require(!secondObserveServices.Invasion.IsCurrentInvasionNight, "natural target dawn ends invasion state");
                checks.Add($"Second invasion natural dawn reached after {secondObserveReal:F3}s of passive observation.");
                Next(1102); break;
            case 1102:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                var secondDawnServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var secondDawnDisk) && secondDawnDisk.day == ObservedInvasionDay + 1 && !secondDawnDisk.timeState.isNight, "natural invasion dawn autosaves target daytime");
                secondInvasionSavedHeat = secondDawnServices.Invasion.TemperatureRiseCelsius;
                bedOutputBefore = ((Nyangbingo.Combat.Health)PlayerField("health")).Current;
                Require(secondDawnDisk.invasionTemperatureRise == secondInvasionSavedHeat && secondDawnDisk.playerState.currentHealth == bedOutputBefore, "target dawn autosave matches live heat and health");
                File.Copy(Path.Combine(directory, "nyangbingo-save-0.json"), Path.Combine(directory, $"day{ObservedInvasionDay + 1}-natural-dawn-autosave.json"), true);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"day{ObservedInvasionDay + 1}-natural-dawn.png"));
                Next(1103); break;
            case 1103:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                var secondDawnShell = FindAnyObjectByType<GameShellController>();
                Require(secondDawnShell.OpenPause() && secondDawnShell.RequestReturnToTitle() && secondDawnShell.Confirm(), "ordinary pause returns to Title without manual dawn save");
                Next(1104); break;
            case 1104:
                Sample();
                var secondDawnTitle = FindAnyObjectByType<TitleShellController>();
                if (secondDawnTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(secondDawnTitle.TryContinue(), "Title Continue loads natural dawn autosave");
                Next(1105); break;
            case 1105:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var secondRestoredTime = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var secondRestoredService = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(secondRestoredTime.Day == ObservedInvasionDay + 1 && !secondRestoredTime.IsNight && !secondRestoredService.Invasion.IsCurrentInvasionNight && !player.IsDead, "Continue restores living next day without active invasion");
                Require(secondRestoredService.Invasion.TemperatureRiseCelsius == secondInvasionSavedHeat && ((Nyangbingo.Combat.Health)PlayerField("health")).Current == bedOutputBefore, "Continue preserves observed heat and health");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"day{ObservedInvasionDay + 1}-dawn-continued.png"));
                Next(1106); break;
            case 1106:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                Finish("passed", $"Saved day{ObservedInvasionDay} invasion restored, remaining night observed without time acceleration to natural day{ObservedInvasionDay + 1} dawn, autosave and Title Continue. Passive route, not active-defense victory; heat and damage require recorded evidence. No recooling or full30-day proof.");
                break;
            case 1080:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var initialEventClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var initialEventSchedule = FindAnyObjectByType<MainGameEncounterCoordinator>().BaekjungScheduler;
                Require(initialEventClock.Day == 16 && !initialEventClock.IsNight && !player.IsDead, "earned natural event dawn Continue restores living day16");
                Require(initialEventSchedule.HasEnded && !initialEventSchedule.IsActive && initialEventSchedule.DispatchedWaveCount == 3, "earned natural event dawn preserves ended schedule without new wave");
                Require(FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e => e.isActiveAndEnabled) == 1, "first restored gameplay has exactly one active EventSystem");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "baekjung-first-continue.png"));
                Next(Day16InvasionRun ? 1081 : 1072); break;
            case 1081:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                placementPoint = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "nest_bed").position;
                Require(Vector2.Distance(player.transform.position, placementPoint) < 2f, "earned day16 bed remains within normal interaction reach");
                Require(FindAnyObjectByType<MainGameRuntimeServices>().Bed.CanSleep(placementPoint, out var day16Room, out var day16Reason), "day16 daytime bed permits normal rest");
                var day16Alert = (Text)typeof(MainGameHudController).GetField("alertOverlayText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameHudController>());
                checks.Add($"Day16 daytime presentation: room={day16Room}, alertActive={day16Alert.isActiveAndEnabled}, alert={day16Alert.text}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day16-before-rest.png"));
                Next(975); break;
            case 1091:
                var secondInvasionClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var secondInvasionServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(secondInvasionClock.Day == 16 && secondInvasionClock.IsNight && secondInvasionServices.Invasion.IsCurrentInvasionNight, "one ordinary day16 rest reaches actual second invasion night");
                Require(!secondInvasionServices.Bed.CanSleep(placementPoint, out var secondRoom, out var secondReason) && secondRoom >= -4f && secondReason.Contains("침공"), "day16 warm bed is restricted by invasion rather than cold");
                checks.Add($"Day16 invasion eligibility: room={secondRoom}, reason={secondReason}, elapsed={Time.realtimeSinceStartup - controlStarted:F3}s");
                bedClockBefore = secondInvasionClock.GameSeconds;
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placementPoint), false, false, KeyCode.E);
                Next(1092); break;
            case 1092:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .7f) return;
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Gameplay, "day16 denied bed does not open rest confirmation");
                var secondDenial = (Text)typeof(MainGameBossSummonUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameBossSummonUiController>());
                Require(secondDenial.isActiveAndEnabled && secondDenial.text.Contains("침공") && secondDenial.text.Contains("잠들 수 없습니다"), "day16 actual bed click displays invasion denial");
                Require(FindAnyObjectByType<MainGameBootstrap>().TimeService.GameSeconds - bedClockBefore < 5f, "denied day16 bed does not skip time");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day16-invasion-bed-denied.png"));
                Next(1093); break;
            case 1093:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 10f) return;
                var secondEncounters = FindAnyObjectByType<MainGameEncounterCoordinator>();
                var secondLiveServices = FindAnyObjectByType<MainGameRuntimeServices>();
                Require(!player.IsDead && secondLiveServices.Invasion.IsCurrentInvasionNight, "brief day16 invasion observation remains alive and active");
                checks.Add($"Day16 night population: raid={secondEncounters.ActiveRaidCount}, regular={secondEncounters.ActiveRegularCount}, pending={secondEncounters.PendingRegularCount}, heat={secondLiveServices.Invasion.TemperatureRiseCelsius}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "day16-invasion-observed.png"));
                Next(1094); break;
            case 1094:
                Sample(0f, KeyCode.Escape); Next(1095); break;
            case 1095:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var secondSaveUi = FindAnyObjectByType<MainGameShellUiController>();
                var secondSaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(secondSaveUi);
                Require(secondSaveButton.isActiveAndEnabled && secondSaveButton.interactable, "ordinary day16 invasion pause save is available");
                secondSaveButton.onClick.Invoke();
                Next(1096); break;
            case 1096:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var secondNightDisk) && secondNightDisk.day == 16 && secondNightDisk.timeState.isNight, "ordinary pause save preserves actual day16 invasion night checkpoint");
                Finish("passed", "Natural day16 dawn checkpoint, one normal confirmed bed rest, active invasion and actual denied second bed click, brief observation and ordinary pause save. Not full-night defense, heat/recooling, enemy kills, saved night Continue or full30-day proof.");
                break;
            case 1070:
                Sample();
                var eventObserveClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var eventObserveEncounters = FindAnyObjectByType<MainGameEncounterCoordinator>();
                var eventObserveScheduler = eventObserveEncounters.BaekjungScheduler;
                var eventObserveReal = Time.realtimeSinceStartup - phaseStarted;
                var eventObserveBucket = (int)(eventObserveReal / 30f);
                if (eventObserveBucket != baekjungObservationBucket || eventObserveScheduler.DispatchedWaveCount != baekjungObservedWaves)
                {
                    baekjungObservationBucket = eventObserveBucket;
                    baekjungObservedWaves = eventObserveScheduler.DispatchedWaveCount;
                    checks.Add($"Baekjung natural observation: real={eventObserveReal:F3}, day={eventObserveClock.Day}, clock={eventObserveClock.TimeOfDayGameSeconds:F3}, elapsedGame={eventObserveScheduler.ElapsedSeconds:F3}, waves={baekjungObservedWaves}, ended={eventObserveScheduler.HasEnded}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}, raid={eventObserveEncounters.ActiveRaidCount}, regular={eventObserveEncounters.ActiveRegularCount}; kinds=" + string.Join(",", FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>().Where(b => b.Definition != null).Select(b => b.Definition.Id)));
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"baekjung-wave-{baekjungObservedWaves}-sample-{eventObserveBucket:D2}.png"));
                }
                if (player.IsDead) throw new Exception("Natural Baekjung observation ended in death; not a full-night survival result.");
                if (eventObserveClock.Day == 15)
                {
                    var expectedEventWaves = eventObserveClock.TimeOfDayGameSeconds >= 1200.2f ? 3 : eventObserveClock.TimeOfDayGameSeconds >= 1050.2f ? 2 : 1;
                    var eventBoundary = Math.Abs(eventObserveClock.TimeOfDayGameSeconds - 1050f) < .3f || Math.Abs(eventObserveClock.TimeOfDayGameSeconds - 1200f) < .3f;
                    if (!eventBoundary && eventObserveScheduler.DispatchedWaveCount != expectedEventWaves)
                        throw new Exception("Baekjung wave count does not match natural night elapsed time.");
                }
                if (eventObserveClock.Day == 16 && !eventObserveClock.IsNight)
                {
                    Require(eventObserveScheduler.HasEnded && !eventObserveScheduler.IsActive && eventObserveScheduler.DispatchedWaveCount == 3, "natural dawn ends all three scheduled waves");
                    checks.Add($"Natural Baekjung dawn after {eventObserveReal:F3} real seconds.");
                    Next(1071);
                }
                break;
            case 1071:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var eventDawnDisk) && eventDawnDisk.day == 16 && eventDawnDisk.baekjungProgress.hasEnded && eventDawnDisk.baekjungProgress.nextWaveIndex == 3, "natural dawn autosave preserves ended event and all dispatched waves");
                File.Copy(Path.Combine(directory, "nyangbingo-save-0.json"), Path.Combine(directory, "baekjung-natural-dawn-autosave.json"), true);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "baekjung-natural-dawn.png"));
                Next(1072); break;
            case 1072:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                var eventReturnShell = FindAnyObjectByType<GameShellController>();
                Require(eventReturnShell.OpenPause(), "open normal pause menu before event Title return");
                Require(eventReturnShell.RequestReturnToTitle() && eventReturnShell.Confirm(), "return to Title without manual save after event dawn");
                Next(1073); break;
            case 1073:
                Sample();
                var eventContinueTitle = FindAnyObjectByType<TitleShellController>();
                if (eventContinueTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(eventContinueTitle.TryContinue(), "ordinary Title Continue uses event dawn autosave");
                Next(1074); break;
            case 1074:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var eventRestoredClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                var eventRestoredSchedule = FindAnyObjectByType<MainGameEncounterCoordinator>().BaekjungScheduler;
                Require(eventRestoredClock.Day == 16 && !eventRestoredClock.IsNight && !player.IsDead, "event autosave Continue restores living day16");
                Require(eventRestoredSchedule.HasEnded && !eventRestoredSchedule.IsActive && eventRestoredSchedule.DispatchedWaveCount == 3, "Continue does not restart ended Baekjung waves");
                Require(FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e => e.isActiveAndEnabled) == 1, "second restored gameplay has exactly one active EventSystem");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "baekjung-dawn-continued.png"));
                Next(1075); break;
            case 1075:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                if (BaekjungRestoreRun) Require(transitionEventSystemPeak == 1, "every sampled transition frame has at most one active EventSystem");
                Finish("passed", BaekjungRestoreRun
                    ? "Earned natural day16 event autosave restored twice through Title Continue, with normal Pause and return confirmation between loads. No new full-night observation, combat rewards or full30-day approval."
                    : "Ordinary bed route to day15 then unaccelerated passive observation of three wave timings, natural day16 dawn autosave and Title Continue. No enemy kills, reward multiplier, active defense, novice or full30-day approval.");
                break;
            case 1051:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .4f) return;
                placedFurnace = FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Single(r => r.definitionId == "ice_anvil");
                coreIceBefore = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("frost_essence");
                toolNavigationAttempts = 0;
                Next(1052); break;
            case 1052:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedFurnace.position), false, false, KeyCode.E);
                Next(1053); break;
            case 1053:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .4f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput, "normal nearby anvil opens for earned T3 craft");
                Next(611); break;
            case 1055:
                Sample();
                var t3Title = FindAnyObjectByType<TitleShellController>();
                if (t3Title == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(t3Title.TryContinue(), "normal Title Continue loads crafted T3");
                Next(1056); break;
            case 1056:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var t3Inventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                var fromDay30 = SessionState.GetBool("Nyangbingo.QA.Day30T3", false);
                Require(t3Inventory.Count("icesteel_claw") == 1 && t3Inventory.Count("icesteel_ingot") == 0 && t3Inventory.Count("icesteel_ore") == (fromDay30 ? 9 : 0) && t3Inventory.Count("coal") == (fromDay30 ? 38 : 0) && t3Inventory.Count("frost_essence") == (fromDay30 ? 6 : 2), "Continue preserves exact T3 and recipe costs for selected earned source");
                Require((int)typeof(MainGamePlayerController).GetMethod("ResolveMiningClawTier", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, null) == 3, "Continue restores actual mining tier three");
                checks.Add($"T3 restored at segment {Time.realtimeSinceStartup - controlStarted:F3}s; HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "t3-claw-continued.png"));
                Next(1057); break;
            case 1057:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Finish("passed", "Five normally smelted ingots and earned frost crafted T3 claw; inventory-based mining tier3 and ordinary save/Title Continue verified. Actual combat effect, fresh novice run and full30-day completion remain unverified.");
                break;
            case 1014:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var invasionDawnSave) && invasionDawnSave.day == 7, "full invasion natural dawn writes day7 autosave");
                File.Copy(Path.Combine(directory, "nyangbingo-save-0.json"), Path.Combine(directory, "first-invasion-dawn-autosave.json"), true);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "first-invasion-natural-dawn.png"));
                Next(1015); break;
            case 1015:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Finish("passed", "Ordinary bed preparation followed by unaccelerated passive first-invasion observation until natural day7 dawn and autosave. No attacks, healing or forced enemies. Infiltration depends on recorded heat; not proof of successful active defense, actual recooling, Continue or full progression.");
                break;
            case 1013:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Finish("passed", "Six ordinary warm-bed transitions reach day6 invasion. Actual bed click is denied with visible reason and no night skip. Ten-second observation only; not enemy infiltration, recooling, full-night survival or full progression.");
                break;
            case 995:
                Sample(0f, KeyCode.Escape);
                Next(996); break;
            case 996:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .4f) return;
                Require(!MainGameCraftingUiController.BlocksGameplayInput, "smelting continues after normal recipe UI close");
                Next(970); break;
            case 991:
                Sample(0f, KeyCode.Escape);
                Next(992); break;
            case 992:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var autoOnlyShell = FindAnyObjectByType<GameShellController>();
                Require(autoOnlyShell.Screen == GameShellScreen.Pause && autoOnlyShell.RequestReturnToTitle() && autoOnlyShell.Confirm(), "return to title without overwriting dawn autosave with manual save");
                Next(983); break;
            case 986:
                Sample(0f, KeyCode.Alpha1);
                foreach (var bedObject in FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects()) checks.Add($"Warm-bed nearby object: {bedObject.definitionId} {bedObject.position}");
                for (var bedX = 301; bedX <= 302; bedX++)
                    for (var bedY = 119; bedY <= 121; bedY++)
                        checks.Add($"Warm-bed cell {bedX},{bedY}: {tiles.GetTile(new Vector3Int(bedX, bedY, 0)).elementType}");
                Next(987); break;
            case 987:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(tiles.GetCellCenterWorld(new Vector3Int(301,119,0))), false, tiles.GetTile(new Vector3Int(301,119,0)).IsAir);
                if (!tiles.GetTile(new Vector3Int(301,119,0)).IsAir) { Next(988); break; }
                if (Time.realtimeSinceStartup - phaseStarted > 3f) throw new Exception("Warm-bed normal floor placement could not fill support cell.");
                break;
            case 988:
                Sample(0f, KeyCode.Alpha7);
                checks.Add("Warm-bed support cell is now " + tiles.GetTile(new Vector3Int(301,119,0)).elementType);
                Next(989); break;
            case 989:
                Sample(player.transform.position.x < 301.25f ? 1f : 0f, KeyCode.Space);
                if (player.transform.position.x >= 301.25f) { Next(990); break; }
                if (Time.realtimeSinceStartup - phaseStarted > 5f) throw new Exception("Normal warm-bed approach could not cross nearby facilities.");
                break;
            case 990:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1.5f) return;
                checks.Add($"Normal warm-bed approach position {player.transform.position}; elapsed={Time.realtimeSinceStartup - controlStarted:F3}s");
                Next(810); break;
            case 983:
                Sample();
                var bedTitle = FindAnyObjectByType<TitleShellController>();
                if (bedTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(bedTitle.TryContinue(), "title Continue restores bed rest save");
                Next(984); break;
            case 984:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Require(FindAnyObjectByType<MainGameBootstrap>().TimeService.Day == bedSavedDay && !player.IsDead, "rest save Continue preserves day and living player");
                Require(FindAnyObjectByType<MainGameEnvironmentState>().ExportPlacedObjects().Count(r => r.definitionId == "nest_bed") == 1 && FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("nest_bed") == 0, "Continue preserves installed bed without duplication");
                if(SessionState.GetBool("Nyangbingo.QA.StoragePreserve",false))
                {
                    var preservedServices=FindAnyObjectByType<MainGameRuntimeServices>();
                    Require(preservedServices.JangdokStorage.TryGet(storageTestId,out var preservedIce) && preservedIce.Count("ice_shard")==PreservedIceCount && Mathf.Approximately(preservedIce.Slots.Where(x=>x.itemId=="ice_shard").Sum(x=>x.storageMeltRemainder),PreservedMeltRemainder),"normal dawn rest and Continue preserve expected ice and unchanged melt remainder");
                    Require(preservedServices.StorageTemperature.TryGetStatus(storageTestId,out var preservedTemp,out _) && preservedTemp<=-10,"restored storage remains frozen");
                    checks.Add($"Restored sealed storage: temperature={preservedTemp}; ice={PreservedIceCount}; remainder={PreservedMeltRemainder}; day={bedSavedDay}.");
                    var warmGuide=GameObject.Find("ShelterGuide")?.GetComponent<Text>();
                    Require(warmGuide!=null && warmGuide.isActiveAndEnabled && warmGuide.text.Contains("보관함은 저장고 주변") && warmGuide.text.Contains("휴식은 따뜻한 곳에서") && !warmGuide.text.Contains("칸 안으로 이동하세요"),"actual warm guide distinguishes storage placement from player movement");
                }
                if (BedAutoOnlyRun)
                {
                    Require(((Nyangbingo.Combat.Health)PlayerField("health")).Current == 100, "dawn autosave Continue restores recovered HP100");
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().StationProduction.PendingCount("iron_ingot") + FindAnyObjectByType<MainGameRuntimeServices>().Furnace.Completed.Where(r => r.item.Id == "iron_ingot").Sum(r => r.amount) == bedOutputBefore + 1, "dawn autosave Continue restores completed iron output");
                    Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("iron_ore") == smeltOreBefore - 2 && FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("coal") == smeltCoalBefore - 1, "autosave restoration does not refund consumed smelting materials");
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "bed-rest-continued.png"));
                Next(985); break;
            case 985:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                Finish("passed", BedAutoOnlyRun ? "Normal earned bed and active iron smelting, rest cancellation and night sleep. Post-rest HP and iron output match dawn autosave and actual Continue without manual save. Other consumers, natural dawn, OS restart and full progression remain unverified." : "Normal warm bed placement, cancel without time skip, both day-night transitions, dawn autosave and manual-save Continue passed. Active crafting/time-consumer consistency and OS restart remain unverified.");
                break;
            case 1000:
                Sample();
                var naturalStart = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                bedDayBefore = naturalStart.Day;
                Require(naturalStart.IsNight && !player.IsDead, "earned victory checkpoint resumes alive at night");
                checks.Add($"Natural dawn start: day={naturalStart.Day}, clock={naturalStart.TimeOfDayGameSeconds:F3}, HP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                Next(1001); break;
            case 1001:
                Sample();
                if (player.IsDead) throw new Exception("Natural dawn waiting ended in death.");
                if (FindAnyObjectByType<MainGameBootstrap>().TimeService.Day > bedDayBefore)
                {
                    checks.Add($"Natural dawn reached after {Time.realtimeSinceStartup - phaseStarted:F3}s.");
                    Next(1002); break;
                }
                if (Time.realtimeSinceStartup - phaseStarted > 180f) throw new Exception("Natural dawn observation exceeded bounded wait.");
                break;
            case 1002:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var naturalDisk), "natural dawn writes readable autosave without manual save");
                Require(naturalDisk.day == bedDayBefore + 1 && naturalDisk.timeOfDaySec < 2f, "natural dawn autosave records new-day boundary");
                bedSavedDay = naturalDisk.day;
                bedOutputBefore = naturalDisk.playerState.currentHealth;
                Require(bedOutputBefore == ((Nyangbingo.Combat.Health)PlayerField("health")).Current, "natural dawn snapshot health matches live state");
                Require(naturalDisk.bossRecords.Any(r => r.bossId == "king_dokkaebi" && r.count == 1 && r.firstDay == 4), "natural dawn preserves actual king victory record");
                File.Copy(Path.Combine(directory, "nyangbingo-save-0.json"), Path.Combine(directory, "natural-dawn-autosave.json"), true);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "natural-dawn-saved.png"));
                checks.Add($"Natural dawn autosave day={naturalDisk.day}, clock={naturalDisk.timeOfDaySec:F3}, HP={bedOutputBefore}");
                Next(1003); break;
            case 1003:
                Sample(0f, KeyCode.Escape);
                Next(1004); break;
            case 1004:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                var naturalShell = FindAnyObjectByType<GameShellController>();
                Require(naturalShell.Screen == GameShellScreen.Pause && naturalShell.RequestReturnToTitle() && naturalShell.Confirm(), "title return leaves natural dawn autosave untouched");
                Next(1005); break;
            case 1005:
                Sample();
                var naturalTitle = FindAnyObjectByType<TitleShellController>();
                if (naturalTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(naturalTitle.TryContinue(), "Continue uses natural dawn autosave");
                Next(1006); break;
            case 1006:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Require(!player.IsDead && FindAnyObjectByType<MainGameBootstrap>().TimeService.Day == bedSavedDay, "natural dawn Continue preserves day and survival");
                Require(((Nyangbingo.Combat.Health)PlayerField("health")).Current == bedOutputBefore, "natural dawn Continue preserves saved health");
                Require(!FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive, "natural dawn Continue does not resurrect defeated king");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "natural-dawn-continued.png"));
                Next(1007); break;
            case 1007:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                Finish("passed", "Natural dawn after actual victory: unaccelerated clock, autosave new day/health/victory record and Title Continue without manual save passed. No sleep or forced day. Other event boundaries, OS restart and full progression remain unverified.");
                break;
            case 950:
                Sample(0f, KeyCode.Alpha7);
                Require(!FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive && !player.IsDead, "victory Continue restores living player without resurrecting boss");
                var rewardStartInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                var rewardStartDrops = FindAnyObjectByType<MainGameWorldDropRuntime>().Export();
                foreach (var pair in new[] { ("dokkaebi_fire_essence", 1), ("club_shard", 2), ("ssireum_knot", 1), ("yokai_tear", 4) })
                    Require(rewardStartInventory.Count(pair.Item1) + rewardStartDrops.Where(d => d.itemId == pair.Item1).Sum(d => d.amount) == pair.Item2, "Continue preserves expected total " + pair.Item1 + "=" + pair.Item2);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "king-reward-restored-ground.png"));
                Next(951); break;
            case 951:
                if (player.IsDead) throw new Exception("Normal reward recovery ended in death.");
                var rewardInventory = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                if (rewardInventory.Count("dokkaebi_fire_essence") == 1 && rewardInventory.Count("club_shard") == 2 && rewardInventory.Count("ssireum_knot") == 1 && rewardInventory.Count("yokai_tear") == 4)
                {
                    Sample();
                    checks.Add($"All king rewards collected by normal movement/mining after {Time.realtimeSinceStartup - controlStarted:F3}s; position={player.transform.position}");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "king-rewards-collected.png"));
                    Next(952); break;
                }
                var rewardGround = FindAnyObjectByType<MainGameWorldDropRuntime>().Export()
                    .Where(d => d.itemId == "dokkaebi_fire_essence" || d.itemId == "club_shard" || d.itemId == "ssireum_knot" || d.itemId == "yokai_tear")
                    .OrderBy(d => Vector2.Distance(d.position, player.transform.position)).ToArray();
                if (rewardGround.Length == 0) throw new Exception("Missing expected rewards with no remaining world drops.");
                var rewardTarget = rewardGround[0].position;
                var rewardDelta = rewardTarget - (Vector2)player.transform.position;
                if (Time.realtimeSinceStartup >= dawnLogAt)
                {
                    dawnLogAt = Time.realtimeSinceStartup + 5f;
                    checks.Add($"Reward approach {Time.realtimeSinceStartup - controlStarted:F3}s player={player.transform.position} target={rewardTarget} remaining={rewardGround.Length}; essence={rewardInventory.Count("dokkaebi_fire_essence")}, club={rewardInventory.Count("club_shard")}, knot={rewardInventory.Count("ssireum_knot")}, tears={rewardInventory.Count("yokai_tear")}");
                }
                var rewardMove = Mathf.Abs(rewardDelta.x) > .25f ? Mathf.Sign(rewardDelta.x) : 0f;
                var rewardAim = rewardDelta.y < -1.3f && Mathf.Abs(rewardDelta.x) < .8f ? (Vector2)tiles.GetCellCenterWorld(tiles.WorldToCell(player.transform.position) + Vector3Int.down) : rewardTarget;
                if (rewardDelta.y < -1.3f && Mathf.Abs(rewardDelta.x) < .8f)
                {
                    var rewardFootCell = tiles.WorldToCell(player.transform.position) + Vector3Int.down;
                    var solidFootings = new[] { rewardFootCell, rewardFootCell + Vector3Int.left, rewardFootCell + Vector3Int.right }
                        .Where(c => !tiles.GetTile(c).IsAir)
                        .OrderBy(c => Vector2.Distance(tiles.GetCellCenterWorld(c), player.transform.position)).ToArray();
                    if (solidFootings.Length > 0) rewardAim = tiles.GetCellCenterWorld(solidFootings[0]);
                }
                GameplayInput.Sample(rewardMove, Camera.main.WorldToScreenPoint(rewardAim), true, false);
                break;
            case 952:
                if (Time.realtimeSinceStartup - phaseStarted < 1f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(953); break;
            case 953:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                var rewardSaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameShellUiController>());
                Require(rewardSaveButton.gameObject.activeInHierarchy && rewardSaveButton.interactable, "reward recovery enables ordinary save");
                rewardSaveButton.onClick.Invoke(); Next(954); break;
            case 954:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var rewardDisk), "ordinary reward save can be read");
                foreach (var pair in new[] { ("dokkaebi_fire_essence", 1), ("club_shard", 2), ("ssireum_knot", 1), ("yokai_tear", 4) })
                    Require(rewardDisk.inventory.Where(s => s.itemId == pair.Item1).Sum(s => s.amount) == pair.Item2 && !rewardDisk.worldDrops.Any(d => d.itemId == pair.Item1), "saved reward inventory exact and no duplicate ground stack: " + pair.Item1);
                Require(rewardDisk.bossRecords.Any(r => r.bossId == "king_dokkaebi" && r.count == 1 && r.firstDay == 4), "reward save retains first king defeat");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "king-rewards-saved.png"));
                Next(955); break;
            case 955:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                var rewardShell = FindAnyObjectByType<GameShellController>();
                Require(rewardShell.RequestReturnToTitle() && rewardShell.Confirm(), "reward route returns to title normally");
                Next(956); break;
            case 956:
                Sample();
                var rewardTitle = FindAnyObjectByType<TitleShellController>();
                if (rewardTitle == null || SceneTransitionRequest.IsLoadingSceneLoaded()) return;
                Require(rewardTitle.TryContinue(), "Continue loads normally collected king rewards");
                Next(957); break;
            case 957:
                Sample();
                player = FindAnyObjectByType<MainGamePlayerController>();
                if (player == null || !player.IsInitialized || SceneTransitionRequest.IsLoadingSceneLoaded() || Time.realtimeSinceStartup - phaseStarted < 2f) return;
                var restoredRewards = FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory;
                var restoredGround = FindAnyObjectByType<MainGameWorldDropRuntime>().Export();
                foreach (var pair in new[] { ("dokkaebi_fire_essence", 1), ("club_shard", 2), ("ssireum_knot", 1), ("yokai_tear", 4) })
                    Require(restoredRewards.Count(pair.Item1) == pair.Item2 && !restoredGround.Any(d => d.itemId == pair.Item1), "Continue restores exact acquired rewards without ground duplication: " + pair.Item1);
                Require(!player.IsDead && !FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive, "reward Continue leaves player alive and boss defeated");
                var earlyGuideHud = FindAnyObjectByType<MainGameHudController>();
                var earlyGuideText = (Text)typeof(MainGameHudController).GetField("shelterGuideText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(earlyGuideHud);
                Require(!earlyGuideText.gameObject.activeInHierarchy || earlyGuideText.text.Contains("얼음 저장고 만들기") && earlyGuideText.text.Contains($"돌 {restoredRewards.Count("stone")}/10"), "first Continue frame hides unready guide or shows restored goal and material count");
                checks.Add($"Early Continue guide at real={Time.realtimeSinceStartup:F3}, unscaled={Time.unscaledTime:F3}, refreshAt={typeof(MainGameHudController).GetField("shelterGuideRefreshAt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(earlyGuideHud)}, enabled={earlyGuideHud.enabled}, active={earlyGuideHud.gameObject.activeInHierarchy}, initialized={FindAnyObjectByType<MainGameRuntimeServices>().IsInitialized}; text={earlyGuideText?.text}");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "king-rewards-continued.png"));
                Next(958); break;
            case 958:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 6f) return;
                foreach (var probeHud in FindObjectsByType<MainGameHudController>(FindObjectsInactive.Include))
                {
                    object HudField(string name) => typeof(MainGameHudController).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probeHud);
                    var probeServices = (MainGameRuntimeServices)HudField("runtimeServices");
                    var probePlayer = (MainGamePlayerController)HudField("playerController");
                    var probeGuide = (Text)HudField("shelterGuideText");
                    Require(probeGuide != null && probeGuide.gameObject.activeInHierarchy && probeGuide.text.Contains("얼음 저장고 만들기") && probeGuide.text.Contains($"돌 {probeServices.PlayerInventory.Count("stone")}/10"), "settled Continue guide is visible and matches restored facilities and inventory");
                    checks.Add($"Guide diagnostic hud={probeHud.name} enabled={probeHud.enabled} active={probeHud.gameObject.activeInHierarchy} services={probeServices?.name} initialized={probeServices?.IsInitialized} dirt={probeServices?.PlayerInventory?.Count("dirt")} stone={probeServices?.PlayerInventory?.Count("stone")} playerValid={probePlayer != null} guideRefreshAt={HudField("shelterGuideRefreshAt")} unscaledNow={Time.unscaledTime}; guide={probeGuide?.text}");
                }
                checks.Add("Runtime service instances: " + string.Join(",", FindObjectsByType<MainGameRuntimeServices>(FindObjectsInactive.Include).Select(s => $"{s.name} active={s.gameObject.activeInHierarchy} initialized={s.IsInitialized} dirt={s.PlayerInventory?.Count("dirt")}")));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "king-guide-diagnostic.png"));
                Next(959); break;
            case 959:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                Finish("passed", "Restored actual king-victory ground rewards, normally collected, saved and Title Continued exact rewards without duplication. Same one-victory checkpoint; not another boss defeat, OS restart, next crafting or full30-day completion.");
                break;
            case 940:
                Sample(0f, KeyCode.Escape); Next(941); break;
            case 941:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                var victoryUi = FindAnyObjectByType<MainGameShellUiController>();
                var victorySaveButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(victoryUi);
                Require(victorySaveButton.gameObject.activeInHierarchy && victorySaveButton.interactable, "actual boss defeat enables ordinary save");
                victorySaveButton.onClick.Invoke(); Next(942); break;
            case 942:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 1f) return;
                Require(FindAnyObjectByType<Nyangbingo.Save.SaveManager>().TryLoadLatest(out _, out var victorySave) && victorySave.bossRecords.Any(r => r.bossId == "king_dokkaebi" && r.count == 1 && r.firstDay == 4), "normal save records first actual king defeat on day4");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "fresh-night-victory-saved.png"));
                Next(943); break;
            case 943:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Finish("passed", "Normal fresh-night king defeat event and ordinary save with first defeat record confirmed. Reward pickup, Continue, later bosses and full30-day remain separate checks.");
                break;
            case 930:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 2f) return;
                var openingHintBoss = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                var openingHint = (Text)typeof(MainGameHudController).GetField("bossStatusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameHudController>());
                Require(openingHintBoss.ActiveHealth.GetComponent<Nyangbingo.Bosses.BossCombatController>().IsOpeningDodgeActive && openingHintBoss.ActiveHealth.DamageTakenMultiplier == 0f, "initial hint matches actual boss opening immunity");
                Require(openingHint.gameObject.activeInHierarchy && openingHint.enabled && openingHint.text == "공격 불가 · 먼저 피하세요" && !openingHint.raycastTarget, "opening hint visible and does not intercept world clicks");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-hint-immune.png"));
                Next(931); break;
            case 931:
                Sample();
                var openEndedBoss = FindAnyObjectByType<Nyangbingo.Bosses.BossManager>();
                if (!openEndedBoss.IsBossActive) throw new Exception("Boss ended before opening hint transition.");
                if (openEndedBoss.ActiveHealth.GetComponent<Nyangbingo.Bosses.BossCombatController>().IsOpeningDodgeActive) return;
                var attackHint = (Text)typeof(MainGameHudController).GetField("bossStatusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(FindAnyObjectByType<MainGameHudController>());
                if (attackHint.text != "공격 가능 · 전조를 보며 싸우세요") return;
                Require(openEndedBoss.ActiveHealth.DamageTakenMultiplier == 1f && attackHint.gameObject.activeInHierarchy && attackHint.enabled, "attack possible hint matches natural opening end and multiplier1");
                checks.Add($"Natural opening hint transition at run {Time.realtimeSinceStartup - started:F3}s.");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "boss-hint-attackable.png"));
                Next(932); break;
            case 932:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Finish("passed", "Normal earned summon opening-immunity hint and natural attackable transition verified. No actual attack, victory, dawn removal or OS restart claim.");
                break;
            case 920:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                var restoredDawnClock = FindAnyObjectByType<MainGameBootstrap>().TimeService;
                Require(restoredDawnClock.Day == 4 && !restoredDawnClock.IsNight, "Continue restores earned day4 daytime");
                Require(!FindAnyObjectByType<Nyangbingo.Bosses.BossManager>().IsBossActive, "Continue does not resurrect dawn-fled boss");
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("ssireum_satba") == 0, "Continue does not refund consumed satba");
                Require(!player.IsDead, "Continue restores living player");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "dawn-continue-world.png"));
                Next(921); break;
            case 921:
                if (Time.realtimeSinceStartup - phaseStarted < 1f) { Sample(); return; }
                Sample(0f, KeyCode.Escape); Next(922); break;
            case 922:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                var restoredDawnUi = FindAnyObjectByType<MainGameShellUiController>();
                var restoredDawnButton = (Button)typeof(MainGameShellUiController).GetField("pauseSaveButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(restoredDawnUi);
                var restoredDawnHint = (Text)typeof(MainGameShellUiController).GetField("statusText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(restoredDawnUi);
                Require(restoredDawnButton.gameObject.activeInHierarchy && restoredDawnButton.interactable && !restoredDawnHint.text.Contains("저장 불가"), "Continue retains enabled save and no stale boss restriction");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "dawn-continue-pause.png"));
                Next(923); break;
            case 923:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < 3f) return;
                Finish("passed", "Title Continue restores naturally earned day4 save, living player, satba0, no active boss and enabled save. Not OS restart or boss victory.");
                break;
            case 750:
                if (player.IsDead) throw new Exception("Natural combat attempt ended in player death.");
                var fightServices = FindAnyObjectByType<MainGameRuntimeServices>();
                if (fightServices.PlayerInventory.Count("yokai_tear") > combatTearsBefore)
                {
                    combatPassed = true;
                    checks.Add($"Natural combat tears acquired after {Time.realtimeSinceStartup - combatStarted:F3}s including natural wait; tears={fightServices.PlayerInventory.Count("yokai_tear")}");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "combat-tears-collected.png"));
                    Sample(); Next(420); break;
                }
                if (Time.realtimeSinceStartup - combatStarted > 1100f)
                { checks.Add("Natural combat observation reached 1100-second limit; saving partial progress."); Sample(); Next(420); break; }
                var foe = FindObjectsByType<Nyangbingo.Yokai.YokaiBrain>()
                    .Where(b => b.Definition != null && b.GetComponent<Nyangbingo.Combat.Health>() != null && !b.GetComponent<Nyangbingo.Combat.Health>().IsDead)
                    .OrderBy(b => Vector2.Distance(b.transform.position, player.transform.position)).FirstOrDefault();
                if (foe == null) { Sample(); break; }
                var foeHealth = foe.GetComponent<Nyangbingo.Combat.Health>().Current;
                if (T3CombatRun && foe == combatTarget && foeHealth < combatTargetHealth)
                {
                    t3DamageObserved = true;
                    checks.Add($"T3 actual HP delta={combatTargetHealth - foeHealth}; foe={foe.Definition.Id}; slowRemaining={foe.FrostSlowRemaining:F3}; speedMultiplier={foe.FrostSpeedMultiplier:F3}");
                }
                if (T3CombatRun && !t3SlowObserved && foe.FrostSlowRemaining > 0f && foe.FrostSpeedMultiplier < 1f)
                {
                    t3SlowObserved = true;
                    checks.Add($"T3 actual slow observed at {Time.realtimeSinceStartup - combatStarted:F3}s: remaining={foe.FrostSlowRemaining:F3}; multiplier={foe.FrostSpeedMultiplier:F3}");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "t3-actual-frost-hit.png"));
                }
                if (foe != combatTarget || foeHealth != combatTargetHealth)
                {
                    checks.Add($"Natural foe at {Time.realtimeSinceStartup - combatStarted:F3}s: {foe.Definition.Id}, HP={foeHealth}, position={foe.transform.position}, playerHP={((Nyangbingo.Combat.Health)PlayerField("health")).Current}");
                    combatTarget = foe; combatTargetHealth = foeHealth;
                }
                var fightDelta = foe.transform.position - player.transform.position;
                if (fightDelta.y > 2.5f && (bool)PlayerField("grounded"))
                {
                    combatElevation = foe.transform.position.y - .3f;
                    combatAscentStarted = Time.realtimeSinceStartup;
                    Sample(); Next(748); break;
                }
                var fightMove = Mathf.Abs(fightDelta.x) > 1f ? Mathf.Sign(fightDelta.x) : 0f;
                var jumpFight = fightMove != 0f && Time.realtimeSinceStartup >= combatJumpAt;
                if (jumpFight) combatJumpAt = Time.realtimeSinceStartup + .9f;
                // Do not mine distant terrain merely because an enemy exists beyond attack reach.
                GameplayInput.Sample(fightMove, Camera.main.WorldToScreenPoint(foe.transform.position), fightDelta.sqrMagnitude <= 4f, false,
                    jumpFight ? new[] { KeyCode.Space } : Array.Empty<KeyCode>());
                break;
            case 741:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "anvil-restored-menu.png"));
                Next(742);
                break;
            case 742:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Finish("passed", "Normal ice-anvil installation, cold-device recipe and missing-material response, save, title Continue and restored station use passed. Cold-device crafting, OS restart, combat and full progression unverified.");
                break;
            case 640:
                GameplayInput.Sample(0f, Camera.main.WorldToScreenPoint(placedFurnace.position), false, false, KeyCode.E);
                Next(641);
                break;
            case 641:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                if (!MainGameCraftingUiController.BlocksGameplayInput)
                {
                    checks.Add($"Furnace failed to open at player={player.transform.position}, furnace={placedFurnace.position}; preserving partial route.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "foundry-station-open-failed.png"));
                    Next(420);
                    break;
                }
                Require(!FindAnyObjectByType<MainGameCraftingUiController>().FurnaceSmeltingViewActive,
                    "returning to furnace opens crafting list for foundry");
                toolNavigationAttempts = 0;
                Next(611);
                break;
            case 616:
                Sample(0f, KeyCode.G);
                Next(617);
                break;
            case 617:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .3f) return;
                var actualClawTier = (int)typeof(MainGamePlayerController)
                    .GetMethod("ResolveMiningClawTier", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, null);
                Require(actualClawTier == 2 && FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("iron_claw") == 1,
                    "normally crafted inventory claw raises actual mining tier to T2 without active-slot equip");
                toolPassed = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "iron-claw-equipment-view.png"));
                Next(414);
                break;
            case 3:
                Sample(1f);
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                Require(player.transform.position.x > origin.x + .05f, "held right input moves player through physics");
                origin = player.transform.position;
                Next(4);
                break;
            case 4:
                Sample(-1f);
                if (Time.realtimeSinceStartup - phaseStarted < .6f) return;
                Require(player.transform.position.x < origin.x - .05f, "held left input moves player through physics");
                Next(5);
                break;
            case 5: Sample(0f, KeyCode.C); Next(6); break;
            case 6:
                Sample();
                Require(MainGameCraftingUiController.BlocksGameplayInput, "C opens menu and blocks movement");
                Next(7);
                break;
            case 7: Sample(0f, KeyCode.E); Next(8); break;
            case 8:
                Sample();
                var ui = FindAnyObjectByType<MainGameCraftingUiController>();
                var message = (string)typeof(MainGameCraftingUiController).GetField("message",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);
                Require(message.StartsWith("재료 부족:"), "craft failure names missing materials: " + message);
                Require(FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.IsEmpty,
                    "failed crafting grants no items");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"crafting-{pass + 1}.png"));
                Next(15);
                break;
            case 15:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted >= .3f) Next(9);
                break;
            case 9: Sample(0f, KeyCode.Escape); Next(10); break;
            case 10:
                Sample();
                Require(!MainGameCraftingUiController.BlocksGameplayInput, "Escape closes menu");
                shortcutCheck = 0;
                Next(200);
                break;
            case 200:
                Sample(0f, (KeyCode)((int)KeyCode.Alpha1 + shortcutCheck));
                Next(201);
                break;
            case 201:
                Sample();
                Require(!MainGameCraftingUiController.BlocksGameplayInput &&
                    FindAnyObjectByType<MainGameTilePaletteController>().SelectedSlotIndex == shortcutCheck,
                    $"number {shortcutCheck + 1} selects hotbar without opening a menu");
                if (++shortcutCheck < 8) Next(200);
                else { shortcutCheck = 0; Next(202); }
                break;
            case 202:
                Sample(0f, PanelSmokeKeys[shortcutCheck]);
                Next(203);
                break;
            case 203:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .2f) return;
                Require(MainGameCraftingUiController.BlocksGameplayInput == (shortcutCheck % 2 == 0),
                    $"{PanelSmokeKeys[shortcutCheck]} toggles panel {(shortcutCheck % 2 == 0 ? "open" : "closed")}");
                if (pass == 0 && shortcutCheck % 2 == 0)
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"panel-{PanelSmokeKeys[shortcutCheck]}.png"));
                if (++shortcutCheck < PanelSmokeKeys.Length) Next(202);
                else if (++pass < 3) Next(2);
                else Next(20);
                break;
            case 20:
                Sample();
                if (!player.IsGrounded) return;
                origin = player.transform.position;
                Next(21);
                break;
            case 21:
                Sample(0f, KeyCode.Space);
                if (Time.realtimeSinceStartup - phaseStarted < .18f) return;
                Require(player.transform.position.y > origin.y + .05f, "Space causes a real physics jump");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "jump.png"));
                Next(22);
                break;
            case 22:
                Sample();
                if (!player.IsGrounded || Time.realtimeSinceStartup - phaseStarted < .3f) return;
                Require(Mathf.Abs(player.transform.position.y - origin.y) < .15f,
                    "released jump lands on original ground");
                Next(23);
                break;
            case 23: Sample(0f, KeyCode.Escape); Next(24); break;
            case 24:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Pause && Time.timeScale == 0f,
                    "Escape opens pause and freezes simulation");
                origin = player.transform.position;
                pausedGameTime = Time.time;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "pause.png"));
                Next(25);
                break;
            case 25:
                Sample(1f, KeyCode.Space);
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                Require(Vector3.Distance(origin, player.transform.position) < .001f && Time.time == pausedGameTime,
                    "movement/jump input cannot move player while paused");
                Next(26);
                break;
            case 26: Sample(0f, KeyCode.Escape); Next(27); break;
            case 27:
                Sample();
                Require(FindAnyObjectByType<GameShellController>().Screen == GameShellScreen.Gameplay && Time.timeScale > 0f,
                    "Escape resumes gameplay");
                Next(28);
                break;
            case 28:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .2f) return;
                if (player.transform.position.y > origin.y + .05f)
                {
                    findings.Add($"PAUSE_INPUT_LEAK: paused Space input causes a jump after resume without new jump input; rise={player.transform.position.y - origin.y:F3}");
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, "pause-input-leak.png"));
                }
                else checks.Add("post-loop: no buffered jump after resume");
                Next(29);
                break;
            case 29:
                Sample();
                if (!player.IsGrounded) return;
                Next(11);
                break;
            case 11:
                Sample();
                tiles = FindAnyObjectByType<MainGameBootstrap>().TileService;
                var center = tiles.WorldToCell(player.transform.position);
                var candidates = new List<Vector3Int>();
                for (var x = -2; x <= 2; x++)
                for (var y = -2; y <= 1; y++)
                {
                    var cell = center + new Vector3Int(x, y, 0);
                    if (!tiles.InBounds(cell)) continue;
                    var material = tiles.GetTile(cell).elementType;
                    if (material == WorldTileTypes.Dirt || material == WorldTileTypes.Stone)
                        candidates.Add(cell);
                }
                Require(candidates.Count > 0, "natural starter material exists in local mining range");
                mineCell = candidates.OrderBy(c =>
                    (tiles.GetCellCenterWorld(c) - player.transform.position).sqrMagnitude).First();
                miningStarted = Time.realtimeSinceStartup;
                Next(12);
                break;
            case 12:
                if (tiles.GetTile(mineCell).IsAir)
                {
                    Sample();
                    checks.Add($"mining {++mined}/3: held primary broke natural cell {mineCell}; " +
                        $"wall seconds={Time.realtimeSinceStartup - miningStarted:F3}");
                    Next(13);
                    break;
                }
                var camera = Camera.main;
                if (camera == null) throw new Exception("No gameplay camera for mining input.");
                GameplayInput.Sample(0f, camera.WorldToScreenPoint(tiles.GetCellCenterWorld(mineCell)), true, false);
                if (Time.realtimeSinceStartup - phaseStarted > 12f)
                    throw new Exception("Natural cell did not break using held input: " + mineCell);
                break;
            case 13:
                Sample();
                if (Time.realtimeSinceStartup - phaseStarted < .5f) return;
                if (mined < 3) Next(11);
                else Finish("passed", "Three UI/movement repetitions and three natural-cell mining checks. " +
                    "No item grant, teleport, or victory injection. Combat and full progression unverified.");
                break;
        }
    }
    private bool TryChooseFurnaceRouteTile(int[] counts, out Vector3Int chosen)
    {
        chosen = default;
        var current = tiles.WorldToCell(player.transform.position);
        bool Needed(string type) => T3GatherRun ? type == WorldTileTypes.IceSteelOre && counts[5] < 10 || type == WorldTileTypes.FrostEssence && FindAnyObjectByType<MainGameRuntimeServices>().PlayerInventory.Count("frost_essence") < 2 : coreGatherRun ? type == WorldTileTypes.IceShard && counts[6] < IceGatherGoal : deepRun
            ? (type == WorldTileTypes.Stone || type == WorldTileTypes.StoneMid || type == WorldTileTypes.StoneDeep) && counts[1] < 20 ||
                type == WorldTileTypes.IronOre && counts[2] < 6 || type == WorldTileTypes.Clay && counts[4] < 12 ||
                type == WorldTileTypes.IceSteelOre && counts[5] < DeepOreRequired
            : (type == WorldTileTypes.Dirt && counts[0] < 8) ||
            ((type == WorldTileTypes.Stone || type == WorldTileTypes.StoneMid) && counts[1] < 22) ||
            (type == WorldTileTypes.IronOre && counts[2] < 6) ||
            (type == WorldTileTypes.CopperOre && counts[3] < 4);
        bool Mineable(string type) => type == WorldTileTypes.Dirt || type == WorldTileTypes.Stone ||
            type == WorldTileTypes.StoneMid || type == WorldTileTypes.Clay || type == WorldTileTypes.Coal ||
            type == WorldTileTypes.IronOre || type == WorldTileTypes.CopperOre ||
            coreGatherRun && type == WorldTileTypes.IceShard ||
            deepRun && (type == WorldTileTypes.StoneDeep || type == WorldTileTypes.IceSteelOre || type == WorldTileTypes.FrostEssence);
        // Privileged read-only route knowledge; no world, inventory or position mutations.
        furnaceRouteAim = furnaceReturning ? placedWorkbench.position : (Vector2)tiles.GetCellCenterWorld(current + new Vector3Int(2, -3, 0));
        var nearestDistance = float.PositiveInfinity;
        if (!furnaceReturning)
            for (var dx = -35; dx <= 35; dx++)
            for (var dy = -40; dy <= (coreGatherRun ? 35 : 1); dy++)
            {
                var cell = current + new Vector3Int(dx, dy, 0);
                if (!tiles.InBounds(cell) || !Needed(tiles.GetTile(cell).elementType)) continue;
                var distance = dx * dx + dy * dy;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                furnaceRouteAim = tiles.GetCellCenterWorld(cell);
            }
        var routePoint = furnaceRouteAim;
        if (furnaceReturning && furnaceRouteAim.y > player.transform.position.y + 1f)
        {
            var direction = Mathf.Abs(furnaceRouteAim.x - player.transform.position.x) < 1f
                ? (current.y % 4 < 2 ? 1f : -1f) : Mathf.Sign(furnaceRouteAim.x - player.transform.position.x);
            routePoint = (Vector2)tiles.GetCellCenterWorld(current) + new Vector2(direction, 2f);
        }
        var bestScore = float.PositiveInfinity;
        var found = false;
        for (var dx = -3; dx <= 3; dx++)
        for (var dy = furnaceReturning && !SatbaCraftRun ? 0 : -3; dy <= (furnaceReturning ? 3 : 1); dy++)
        {
            var cell = current + new Vector3Int(dx, dy, 0);
            if (!tiles.InBounds(cell) || tiles.GetTile(cell).IsAir) continue;
            var type = tiles.GetTile(cell).elementType;
            if (!Mineable(type)) continue;
            var point = (Vector2)tiles.GetCellCenterWorld(cell);
            if ((SatbaCraftRun || SessionState.GetBool("Nyangbingo.QA.ReplacementIceResume",false)) && EventSystem.current != null)
            {
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                    { position = Camera.main.WorldToScreenPoint((Vector3)point + (SessionState.GetBool("Nyangbingo.QA.ReplacementIceResume",false)?Vector3.up*.4f:Vector3.zero)) }, hits);
                if (hits.Any(hit => hit.module is GraphicRaycaster)) continue;
            }
            if (!MainGamePlayerController.TryPickMiningCell(tiles, player.transform.position,
                point, point - (Vector2)player.transform.position, 4f, out var picked) || picked != cell) continue;
            // Prefer reachable nearby material to distant edges that leave drops on ledges.
            var score = (point - routePoint).sqrMagnitude + 4f * (point - (Vector2)player.transform.position).sqrMagnitude;
            if (SatbaCraftRun && routePoint.y < player.transform.position.y - 2f && dx == 0 && dy < 0) score -= 2000f;
            // Door route starts on the surface; avoid clearing whole horizontal layers
            // while the actual ice target (or initial exploration direction) is below.
            if (DoorIceRun && routePoint.y < player.transform.position.y - 2f &&
                nearestDistance > 16f && dx == 0 && dy < 0)
                score -= 2000f;
            // Once shallow supplies are ready, cut a narrow shaft instead of clearing
            // each horizontal layer. Still uses ordinary mining and gravity, not teleportation.
            if (deepRun && !coreGatherRun && !furnaceReturning && counts[1] >= 20 && counts[2] >= 6 && counts[4] >= 12 &&
                nearestDistance > 16f && dy < 0 && dx == 0)
                score -= 2000f;
            if (!furnaceReturning && Needed(type)) score -= 10000f;
            if (score >= bestScore) continue;
            bestScore = score;
            chosen = cell;
            found = true;
        }
        return found;
    }

    private void Finish(string status, string note)
    {
        EndPlacementAudit();
        finished = true;
        checks.Add(note);
        if (status == "passed" && findings.Count > 0) status = "completed_with_findings";
        var result = new Result { status = status, wallSeconds = Time.realtimeSinceStartup - started,
            checks = checks.ToArray(), findings = findings.ToArray() };
        if (workbenchRun) result.scope = "First-workbench bot route; local tile/recipe knowledge, normal input and pickup. Not a novice benchmark.";
        if (furnaceRun) result.scope = "First-furnace bot route; read-only tile knowledge up to 40 cells away, ordinary inputs and pickup. Not a novice benchmark; route failures do not establish game softlocks.";
        if (resumeEarnedSave) result.scope += " Resumed copy of normally earned furnace-access-2 save; times are continuation segment only.";
        if (ascentRun) result.scope = "Normal-input ascent from furnace-route-resume-3 earned save. Segment timing only; furnace completion and full return not implied.";
        if (bridgeRun) result.scope = "Normal horizontal return and furnace craft from furnace-ascent-1 earned checkpoint. Segment timing only; prior gathering and ascent measured separately.";
        if (smeltRun) result.scope = "Installation and first iron smelting from first-furnace-1 normal earned save. Ordinary gameplay inputs; collection/save UI callbacks; no completion injection.";
        if (foundrySmeltRun) result.scope = "Install foundry and smelt ice steel from first-foundry-1 earned save. Installation-selection, collection and save use UI button callbacks; placement and smelting use normal inputs. No item grant, teleport or completion injection. Legacy iron/furnace screenshot filenames refer to this foundry run.";
        if (toolRun) result.scope = "Iron-claw preparation from first-smelting-3 normal earned checkpoint. Read-only tile knowledge; normal gather, return, smelt and craft inputs; collect/save callbacks. Inventory-based T2 verification, not active-slot equip. No item grant, teleport or completion injection.";
        if (deepRun) result.scope = $"Deep mining and foundry from {deepSource}. Scenario targets T2 mining, pickups, scaffolding and crafting; only individual checks prove execution. Read-only local tile knowledge; save callback. No item grant, teleport or completion injection.";
        if (anvilRun) result.scope = "Ice-anvil route from first-icesteel-3 earned save: normal T2 ore gathering, return, iron/copper/ice-steel smelting, anvil craft and save. UI collect/save callbacks; read-only tile knowledge. No grants, teleport, or completion injection. Legacy foundry screenshot names refer to anvil in this mode.";
        if (installAnvilRun) result.scope = "Install first-ice-anvil-1 earned item using normal placement, open station and inspect cold-device recipe; save and title Continue via UI callbacks. No grants or forced completion. Legacy furnace image/log names refer to ice anvil.";
        if (combatRun) result.scope = $"Combat from {combatSource}. Scene enemy positions read for automatic aim/movement; ordinary attacks and pickup, normal UI save callback. Natural clock; existing saved enemies may restore. No item/damage/time injection. Not a novice benchmark or full progression proof.";
        if (harvestRun) result.scope = "Surface material gathering from natural-combat-night-repeat-3 earned save. Read-only tree/hemp registry for navigation; ordinary movement/jump/mining/pickup/healing, save UI callback. No grants, direct harvest calls, teleport, time injection or forced spawn. Not novice or full progression proof.";
        if (doorGatherRun) result.scope = "Door wood6/hemp4 gathering from leak-repair-chain-1 earned save. Normal ascent/mining/placement/harvest/pickup with read-only world navigation, UI save callback. No grants, teleport or time injection. Ice4, return, door crafting and full enclosure not verified.";
        if (coreGatherRun) result.scope = "Ice-shard gathering from ice-anvil-install-visual-4 normal checkpoint. Read-only nearby tile knowledge, normal mining/movement/pickup/healing and UI save. No grants, teleport, time changes or core completion injection. Not a novice benchmark or whole progression timing.";
        if (DoorIceRun) result.scope = "Gather door ice to4 from door-material-gather-1 earned checkpoint retaining wood6 hemp4. Normal mining/movement/pickup/healing, read-only nearby tile navigation and UI save. No grants, teleport, time injection. Door craft, return and enclosure remain unverified.";
        if (coreCraftRun) result.scope = "Ice-core craft from ice-core-gather-fixed-1 normal checkpoint. Normal scaffold return, mining, smelting and crafting; UI collect/save callbacks. No grants, teleport, time change or completion injection. Not a novice benchmark or full shelter completion. Legacy foundry screenshot names refer to ice core.";
        if (installCoreRun) result.scope = "Ice-core install from first-ice-core-craft-1 earned checkpoint. Normal placement; install-selection/save/title callbacks, real service inspection. No grants or completion injection. Not enclosure construction or novice proof. Legacy furnace image names refer to core.";
        if (wallCraftRun) result.scope = "Seal-wall crafting from ice-core-install-visual-1 earned checkpoint. Normal workbench interaction, recipe selection, material consumption, crafting wait; save UI callback. No grants, teleport or completion injection. Not a sealed shelter or novice test.";
        if (DoorCraftRun) result.scope = "Normal ascent/return and door crafting from door-ice-gather-1 earned checkpoint. Ordinary mining, scaffold, movement, real recipe consumption/wait and UI save callback. No grants, teleport or time injection. Door installation and enclosure not tested.";
        if (DoorInstallRun) result.scope = "Earned door install from first-door-craft-1 normal checkpoint; installation selection/save via UI callbacks, normal mouse placement, three right-click open-close cycles and physical crossing checks. Read-only seal registry inspection. No grants or state injection; full enclosed room not tested.";
        if (EnclosureRun) result.scope = "Build a usable 5x2 interior around core and facilities with existing normal door at x296, right boundary x302, floor119 and ceiling122. Starts door-crossing-1 earned save; preserve natural/valid boundary, normally craft/mine/place missing walls. Read-only tile planning, UI installation/save callbacks; no grants, teleport or injected seal state.";
        if (RestoreShelterRun) result.scope = "Restore complete normally built room from 6f4fbe153e1c43fb8b8078287b5d8df3. Normal movement/right-click door, actual seal/cooling inspection, UI save/title callbacks. Open and closed states each restored via Continue. No grants or injected cooling; not OS restart or newcomer test.";
        if (SatbaCraftRun) result.scope = "Return from satba-gather-fixed-3 earned checkpoint and craft ssireum_satba with normal movement/mining/pickup/recipe input and wait. Read-only route knowledge, UI save callback. No grants, teleport, time injection or forced summon. Not boss victory or novice proof; legacy seal-wall filenames refer to satba UI.";
        if (BedCraftRun) result.scope = "Return from door-material-gather-1 earned wood6 hemp4 checkpoint and craft nest_bed with normal movement/mining/pickup/healing and recipe input/wait. UI save callback and read-only route knowledge. No grants, teleport, time injection or instant crafting. No sleep or full progression claim; legacy seal-wall filenames refer to bed UI.";
        if (BedUseRun) result.scope = "Install first-bed-craft-1 normally earned bed using recipe navigation and ordinary placement. Inspect actual room eligibility, normal right-click interaction and cancel when available; save callback. No grants, teleport, direct sleep/time calls. Not actual sleep, Continue or full progression proof.";
        if (BedWarmRun) result.scope = "Normally earned bed, warm-position search using read-only room state and ordinary placement. Right-click rest, cancel and confirmation callbacks; real gameplay sleep advances time, never direct time injection. Dawn autosave snapshot and manual-save Continue. Active production consistency, novice behavior, OS restart and full progression not established.";
        if (BedAutoOnlyRun) result.scope = "Normally earned bed placement and active earned-material iron smelting followed by ordinary rest confirmation. Compare health and smelting output in dawn autosave, return to title without manual save and Continue. No grants, teleport, direct time manipulation or forced production. Other consumers, natural dawn and full progression not proved.";
        if (BedInvasionRun) result.scope = "Normally earned bed placement followed by six ordinary UI-confirmed rests from day3 night to day6 night. Check actual invasion state, warm-bed restriction, visible denial and no skipped invasion. No grants, direct time injection or teleport. Not full-night survival, actual enemy infiltration, recooling, boss continuity or novice proof.";
        if (InvasionOvernightRun) result.scope = "Normally earned bed checkpoint, ordinary rests to day6 night, then passive natural-clock observation to death or day7 dawn. Record live enemy counts, core heat, health and dawn autosave. No forced enemies, time injection, teleport, attacks or healing. Not novice timing, active-defense success, actual recooling, boss continuity or full progression.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageInvasion",false)) result.scope = "Actual shelter-safety-guide-fixed-3 day6 normally earned sealed storage save. Existing warm bed, ordinary cancellation and one rest to first invasion, passive unaccelerated night to death or day7 dawn. Record actual storage ice/remainder/temperature, heat and health. No grants, attacks, healing, teleport or clock injection. Not active defense, recooling, novice timing, Continue or full30-day completion.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageRecool",false)) result.scope = "Actual storage-first-invasion-natural-1 day7 damage autosave. Normal movement and core right click, exact earned ice cost and leaky storage temperature, ordinary manual save and Title Continue. No grants, heat injection, teleport, clock injection, wall repair or melted-item refund. Not full shelter recovery, repeated invasion, novice proof or full30-day completion.";
        if (InvasionRestoreRun) result.scope = "Normal Title Continue from isolated copy of first-invasion-full-night-1 actual day7 dawn autosave. Inspect actual absent floor297/298 and surviving floor299, core/bed counts, HP and invasion heat; screenshot restored guide. No save edits or forced damage. Not another full-night run, repair, recooling, novice proof or OS restart.";
        if (InvasionRepairRun) result.scope = "Actual invasion damage save: normal movement, two insul_wall crafts costing stone4 dirt2, placement on destroyed cells, pause save and Title Continue. No grants/time injection/forced damage. Not complete enclosure, actual recooling, novice test or repeated full night.";
        if (BaekjungReachRun) result.scope = "Actual first-invasion-full-night-1 earned day7 dawn save; ordinary existing bed right-click/confirmation to day15 night. Read-only event/enemy/bed inspection and normal save. No direct time changes/grants/forced spawn. Not all waves, rewards, novice proof or full30-day progression.";
        if (BaekjungOvernightRun) result.scope = "Earned day7 invasion dawn; ordinary bed route to day15, natural unaccelerated passive night observation, wave timing and day16 autosave/Title Continue. No grants, direct clock change, forced enemies or kills. Not combat reward, novice or continuous30-day proof.";
        if (T3GatherRun) result.scope = "Actual king-reward-recovery-1 checkpoint. T2 normal mining/movement/pickup/scaffolding and earned healing to gather ore10 frost2 and return. Read-only nearby tile knowledge, UI save callback. No grants/teleport/time injection. Not T3 crafting or novice timing.";
        if (T3CraftRun) result.scope = "Actual post-king-t3-gather-return-1 earned save. Normal foundry E smelt/collection, anvil E craft, mining-tier readback and UI save/Title Continue. No resources, time skip or completion injection. Not combat effect, repeated full route or novice proof.";
        if (T3CraftRun && SessionState.GetBool("Nyangbingo.QA.Day30T3", false)) result.scope = "Actual day30-dawn-route-1 earned save, initial ore19 coal48 frost8. Normal foundry E smelting and anvil E T3 crafting, ordinary save/Title Continue. No inventory merging, grants or time skips. Not Imugi victory or novice proof.";
        if (SatbaUseRun) result.scope = "Use first-satba-craft-1 earned satba through normal Tab/arrow/E/Escape inputs. Read-only inventory selection and boss inspection. No grants, direct summon calls, time changes or forced victory. Not combat completion or save persistence proof.";
        if (BossApproachRun) result.scope += " Then normal mined/scaffold ascent and bounded 120-second attack/movement observation. Position-aware automated policy, not a human benchmark. No healing policy in first combat probe.";
        if (BossSurfaceRun) result.scope += " Surface comparison variant: ascent occurs BEFORE normal inventory summon, not after it.";
        if (BossDeathRecoveryRun) result.scope += " Continue actual death sequence to living recovery and inspect pause menu with normal Escape. Do not invoke disabled save callbacks.";
        if (BossSaveHintRun) result.scope += " Normal pause then earned summon and boss pause: verify enabled/disabled save plus visible reason via ordinary Escape. No actual save or dawn transition in this test.";
        if (BossDawnRun) result.scope = "Normal earned satba inventory summon, pause restriction inspection, resume and wait for natural clock dawn. Read-only boss-ended observation; ordinary enabled save UI callback then disk save check. No time injection, forced flight or victory, item grants or teleport. Not OS restart or full progression.";
        if (DawnRestoreRun) result.scope = "Title Continue on isolated copy of boss-natural-dawn-save-1 earned save. Inspect day4 daytime, consumed satba0, no active boss, living player and ordinary Escape menu save enabled. No grants, time injection, forced flight or victory. Not OS process restart.";
        if (BossColliderAimRun) result.scope = "Earned satba save, normal surface ascent and inventory summon. Bounded 120s combat probe aims nearest actual boss collider with ordinary input and mines obstructing terrain. No healing/evasion, damage changes, grants or forced victory. Procedure completion is not boss victory or human difficulty measurement.";
        if (BossOpeningRetreatRun) result.scope = "Earned satba, normal surface ascent and inventory summon. During actual opening immunity, retreat with ordinary movement/jump/mining; then collider-aim normal attacks. Read-only state telemetry. No healing, combat stat changes, grants, injected time or forced victory. Bounded observation is not normal boss victory or human benchmark.";
        if (NextNightRun) result.scope = "Natural next-night preparation from first-satba-craft-1 earned checkpoint. Normal clock, untouched satba1, no healing or grants, ordinary pause-menu save before day4 dusk. Not boss victory, novice benchmark or full30-day completion.";
        if (FreshNightRun) result.scope = "Fresh-night normal boss attempt from next-night-preparation-1 earned save. Normal ascent, wait until night, inventory summon, opening retreat, collider-aim attacks and normal reapproach. No healing, grants, time changes, stat changes or forced victory. Defeat event and saved boss record are required for success; not full reward or30-day approval.";
        if (KingRewardsRun) result.scope = "Isolated copy of fresh-night-king-victory-1 actual victory save. Title Continue, read-only drop-position navigation, ordinary movement/mining/pickup, UI save and Title Continue. No item grants, direct pickup, teleport, time change or new forced victory. Not novice timing or OS restart.";
        if (NaturalAutosaveRun) result.scope = "Actual victory checkpoint, normal unaccelerated overnight wait, natural dawn autosave and Title Continue without manual save. Verify saved day, health and king victory record. No sleep, time injection, new forced victory, grants or teleport. Not full progression or OS restart.";
        if (BossOpeningHintRun) result.scope = "Normal earned satba inventory summon from underground checkpoint; natural game-clock opening immunity wait. Verify actual HUD label and health multiplier before and after transition. No injected time or health. Not combat victory, newcomer comprehension or boss-end removal test.";
        if (BaekjungRestoreRun) result.scope = "Isolated unchanged natural day16 Baekjung dawn autosave from day15-natural-waves-dawn-1. Title Continue, ordinary Pause/return confirmation, Title Continue again without manual save. Assert living day16, ended inactive event and three dispatched waves. Not a new full-night run, combat rewards, OS restart or full30-day proof.";
        if (Day16InvasionRun) result.scope = "Actual natural day16 dawn save, normal bed rest to second invasion, right-click rest denial, ten-second passive observation and ordinary pause save. No clock injection, grants, forced enemies or attacks. Not full-night survival, defense, recooling, night-checkpoint Continue or continuous30-day proof.";
        if (SecondInvasionNightRun) result.scope = "Normal Continue of day16-invasion-entry-1 actual night save, unaccelerated remaining-night passive observation, natural day17 dawn autosave and Title Continue. No clock injection, grants, forced enemies, attacks or healing. Not new full night from dusk, active defense, recooling, novice timing or full30-day proof.";
        if (SecondInvasionNightRun && ObservedInvasionDay == 26) result.scope = "Normal Continue of day26-rest-route-1 actual night save, unaccelerated remaining-night passive observation, natural day27 dawn autosave and Title Continue. No clock injection, grants, forced enemies, attacks or healing. Not new full night from dusk, active defense, recooling, novice timing or full30-day proof.";
        if (Day18ResidentsRun) result.scope = "Earned day17 natural dawn copy, three normal bed rests to day18 night, runtime resident observations and normal save/Title Continue. No injected dates, grants, forced enemies, teleport or attacks. Remote runtime presence is not player discovery, combat, loot, full day18 experience or full30-day proof.";
        if (GangcheoriApproachRun) result.scope = "Normal input movement/mining segment from earned day18 dawn. Privileged read-only target and tile coordinates, up to600seconds or within12tiles; save actual progress. No teleport, grants, clock or HP injection. Arrival determined by checks, not passed label; no victory, loot or novice navigation approval.";
        if (GangcheoriPreparedRun) result.scope += " Revised route approaches horizontally near y62 before descending; earned healing items consumed through normal inventory/selection/E controls at HP<=60. No artificial warming or protection. Partial checkpoint if supplies exhausted or time bound reached.";
        if (GangcheoriFightRun) result.scope += " Continue from approach into normal close-range attacks without intermediate reload. Runtime HP/absence observations require separate saved kill-progress and loot audit; checkpoint passed alone does not prove victory, return or30-day completion. Total route bounded600s.";
        if (GangcheoriNearRestoreRun) result.scope = "Unchanged earned approach checkpoint at player143.471,24.038 with former Gangcheori142.5,12.5. Normal Title Continue and read-only live observation of resident position and player health. No movement, mining, healing, grants, clock changes or resave. Passed means observation completed, not actor position preservation, combat or full30-day proof.";
        if (GangcheoriVictoryRestoreRun) result.scope = "Normally earned Gangcheori victory checkpoint. Title Continue checks live scale1/tears3 and no same-day resident respawn, opens J codex for visual audit, normal movement collects persisted tear then UI resaves tears4/scale1/codex kill1/lastKilled18. No grants/time/HP injection. Not return, next-day respawn, altar unlock UI or30-day proof.";
        if (SessionState.GetBool("Nyangbingo.QA.CodexLayout", false)) result.scope = "Earned victory Continue and J codex. Read actual card geometry/unlocks, existing button callbacks select Gangcheori and close expanded card, UI OnScroll event reaches bottom. No movement/resave/grants/time injection. Not physical pointer, small-screen, combat or full30-day proof.";
        if (GangcheoriReturnRun) result.scope = "Earned day18-victory-restore-1 loot checkpoint. Normal ceiling mining, jump/block placement, horizontal bridge and earned healing; read-only tile/target coordinates and UI save callback. Bounded450s ascent+300s horizontal. No grants/teleport/time injection. Passed checkpoint alone does not prove station reached; inspect position and checks. Not novice or full30-day proof.";
        if (SessionState.GetBool("Nyangbingo.QA.ReturnRecipeAudit", false)) result.scope = "Normal Continue of day18-loot-return-finish-1 and ordinary right-click on earned workbench. Read-only recipe assets, restored unlock policy and inventory counts. No grants, teleport, clock changes or crafting. Not novice discovery or full30-day proof.";
        if (SessionState.GetBool("Nyangbingo.QA.Day23Route", false)) result.scope = "Earned day18-loot-return-finish-1 normal Continue, walk to existing bed, ordinary right-click/confirmation rest through day23 night,12s passive observation and normal pause save. No direct clock change, grants, teleport or forced enemies. Rest-skipped days are not played-content or survival proof. Not full night, combat, rewards, novice timing or day30 completion.";
        if (SessionState.GetBool("Nyangbingo.QA.Day23Route", false) && SessionState.GetInt("Nyangbingo.QA.LateRouteTarget", 23) == 26) result.scope = "Earned day23-rest-route-1 normal Continue, existing bed ordinary right-click/confirmation rest through day26 invasion night,12s passive observation and normal pause save. No direct clock change, grants, teleport or forced enemies. Rest-skipped days are not played-content or survival proof. Not full invasion night, combat, rewards, novice timing or day30 completion.";
        if (SessionState.GetBool("Nyangbingo.QA.Day23Route", false) && SessionState.GetInt("Nyangbingo.QA.LateRouteTarget", 23) == 27) result.scope = "Earned day26-natural-night-dawn-1 day27 natural dawn autosave normal Continue, existing bed ordinary right-click/confirmation rest to day27 theme night,12s passive observation and normal pause save. No direct clock change, grants, teleport or forced enemies. Not full night, combat, rewards, novice timing or day30 completion.";
        if (SessionState.GetBool("Nyangbingo.QA.Day23Route", false) && SessionState.GetInt("Nyangbingo.QA.LateRouteTarget", 23) == 30) result.scope = "Earned day27-theme-entry-1 normal Continue, ordinary bed rest through day30 DAWN,12s observation and normal pause save. No direct clock change, grants, teleport or forced enemies. Rest-skipped days are not played content. No day30 night, Imugi spawn/fight/victory, novice timing or full30-day completion proof.";
        if (SessionState.GetBool("Nyangbingo.QA.ImugiNight", false)) result.scope = "Earned day30-dawn-route-1 normal Continue, ordinary bed rest to day30 night and12s observation of naturally triggered Imugi. Read-only boss state. No direct time change, forced boss, grants, attacks, victory or night save. Not full30-day completion.";
        if (SessionState.GetBool("Nyangbingo.QA.ImugiFight", false)) result.scope = "Earned day30-dawn-route-1 normal Continue/rest into natural Imugi, ordinary aimed attacks, spacing movement, periodic jumps and earned mushrooms through normal selection/E or inventory UI. Read-only boss aim/HP;120s bound. No grants, damage/time injection or teleport. No novice timing, victory or loot claim without separate evidence.";
        if (SessionState.GetBool("Nyangbingo.QA.ImugiFullFight", false)) result.scope = "Earned day30-dawn-route-1 normal Continue/rest into natural Imugi; ordinary attacks, spacing, periodic jumps and earned healing. Read-only boss aim/HP;600s bound, stop on death or exhausted healing. No grants, damage/time injection, teleport or intermediate reload. Boss absence alone is not victory: inspect kill/save evidence separately.";
        if (SessionState.GetBool("Nyangbingo.QA.ImugiEarnedT3", false)) result.scope = "Earned day30-t3-craft-1 normal Continue/rest into natural Imugi; T3 crafted on same actual day30 route. Ordinary attacks, spacing, jumps and earned healing;600s bound, death or exhausted healing stops. No grants, damage/time injection, teleport or intermediate reload. Boss absence alone is not victory: inspect kill/save evidence separately.";
        if (SessionState.GetBool("Nyangbingo.QA.ImugiLoot", false)) result.scope = "Actual day30-imugi-result-save-fixed-1 ordinary victory save copied into isolated QA slot. Normal movement collects persisted Yeouiju1/shard1/claw1/tears12; pause save and Title Continue check exact inventory/no duplicate ground loot. No grants/time/HP injection. This is reward recovery, not another fight, crafting, novice play or continuous30-day completion. Inspect terminal checks for actual outcome.";
        if (SessionState.GetBool("Nyangbingo.QA.ImugiRewardEquipment", false)) result.scope = "Normal reward recovery save/Continue then equipment G/right/E with actually earned Yeouiju shard and claw; ordinary pause save/Title Continue checks equipment collection, slot, active weapon and no duplicated bag/ground reward. Read-only modifier/profile observation, no cooler construction, actual combat, OS restart or novice proof.";
        if (SessionState.GetBool("Nyangbingo.QA.RewardCombat", false)) result.scope = "Unchanged normally earned imugi-equipment-hint-fixed-3 save copy. Normal movement/jump/aimed attacks with equipped Yeouiju and Q-switched base T3; read-only HP/profile/slow observations. No grants, forced enemies, injected HP/time, teleport or resave. Different targets may differ; inspect observations, not controlled balance or novice proof.";
        if (SessionState.GetBool("Nyangbingo.QA.CoolerReturn", false)) result.scope = "Normal imugi-equipment-hint-fixed-3 reward save return to original workshop at299.5,120.23, ordinary movement/mining/pickup and pause save. No grants, teleport or time/HP injection. Not ore retrieval, cooler craft/effect, novice timing or full30-day completion.";
        if (SessionState.GetBool("Nyangbingo.QA.CoolerOre", false)) result.scope = "Normal imugi-workshop-return-1 ore9 checkpoint, ordinary mining/movement toward persisted deep ore, earned oyster use if needed, normal block ascent and workshop save with ore>=10. No grants/teleport/time/HP injection. Only checks prove actual retrieval/return; not cooler crafting/effect or novice/full30-day proof.";
        if (CoolerCraftRun) result.scope="Actual cooler-ore-return-1 ordinary earned save. Inventory UI callback/E and normal placement for furnace/anvil, ordinary right-click/Q/E smelting with collect callbacks, cooler crafting and pause save/Title Continue. No grants, time/completion injection or merged inventories. Placement of finished cooler, real effect, novice and continuous30-day proof remain separate.";
        if (CoolerInstallRun) result.scope="Actual cooler-earned-craft-1 save: normal inventory selection/E/left-click placement, read-only room observation, normal save/Title Continue. No grants, time injection, forced seal or merged inventory. Open workshop only; sealed-room recovery and storage effects remain unverified.";
        if (StorageCraftRun) result.scope="Actual door-ice-gather-1 normal save: movement, mined route/scaffolds, workbench recipe input and ordinary save. No grants, time injection or merged inventory. Jangdok preparation only, not actual storage benefit or new-game30-day proof.";
        if (StorageUseRun) result.scope="Actual storage-earned-craft-1 save: normal placement, storage UI transfer, natural daily tick observation and ordinary save. No grants, clock injection or forced storage tick. Open-core branch only; sealed storage benefit and Continue remain unverified.";
        if (StorageRestoreRun) result.scope="Actual storage-open-natural-dawn-1 save: read-only initial restore assertions, normal right-click storage UI, pause resave/Title Continue. Ice4 and fractional melt0.25; no grants or clock injection. Not another daily melt test or sealed-room benefit.";
        if (StorageShelterGatherRun) result.scope="Actual storage-open-natural-dawn-1 save: ordinary ascent/mining/scaffolds, surface wood12/hemp8 harvest for door and bed, ordinary save. Preserve existing jar ice4. No grants, teleport, merged inventories or time injection. Door/bed craft, enclosed storage and extra ice remain separate.";
        if (SessionState.GetBool("Nyangbingo.QA.InvasionDoorGather",false)) result.scope="Actual storage-invasion-recool-3 day7 save after normal recooling. Ordinary movement/mining/scaffolds and surface harvest to wood6/hemp4 for replacement door, ordinary save. No grants, teleport, inventory merging or time injection. Returning, missing door ice, crafting, wall repair and recovered freezing are separate unverified stages.";
        if(SessionState.GetBool("Nyangbingo.QA.StorageFloorRepair",false)) result.scope="Actual storage-invasion-recool-3 day7 damage: ordinary workbench insul_wall recipe with stone2 dirt1, normal placement at destroyed floor297,119 and manual save. No grants, teleport, forced tile edit or clock injection. Door remains absent; not full sealing, future dawn preservation, Continue or novice proof.";
        if(SessionState.GetBool("Nyangbingo.QA.AfterStorageFloor",false)) result.scope="Actual storage-invasion-floor-repair-1 save: normal Continue verifies repaired floor297,119, then normal movement and surface harvest for replacement door wood6/hemp4, ordinary save. No grants, teleport or clock injection. Missing ice, return, door craft and sealing remain separate. Failed movement is not a game-softlock verdict.";
        if(SessionState.GetBool("Nyangbingo.QA.ReplacementTreeAscent",false)) result.scope="Continue actual storage-repair-gather-failed-2 partial route near x256; normal mining/scaffolds ascend to observed tree column244 surface then harvest wood6/hemp4 and save. No grants, teleport, clock injection or inventory merging. Prior route failures remain evidence; not novice discovery, return, door craft or full sealing.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageBedCraft",false)) result.scope="Actual storage-shelter-materials-1 save: ordinary return from x532, earned healing if needed, workbench nest_bed recipe and pause save. No grants, teleport, merged inventories or clock injection. Bed installation, door materials and sealed storage comparison remain separate.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageBedUse",false)) result.scope="Actual storage-shelter-bed-craft-1 save: normal earned bed placement at a warm position, real rest confirmation/cancellation and two ordinary sleep transitions, save and Title Continue. Read-only position eligibility knowledge; no grants, teleport, inventory merging or direct time injection. Not natural-day waiting, sealed storage comparison or novice discovery proof.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageDoorIce",false)) result.scope="Actual storage-shelter-bed-use-1 save: collect four additional ice shards with ordinary mining, movement, pickup and earned healing. Read-only tile navigation; no grants, teleport, storage withdrawal, inventory merging or clock injection. Ordinary pause save. Return, door crafting and sealed storage comparison remain separate.";
        if(SessionState.GetBool("Nyangbingo.QA.ReplacementDoorIce",false)) result.scope="Actual storage-repair-tree-ascent-1 day7 earned materials save. Normal movement/mining/ice pickup and earned healing, ordinary pause save for replacement door. No grants, teleport, jar withdrawal, inventory merging or clock injection. Return, door craft/install and restored sealing remain separate.";
        if(SessionState.GetBool("Nyangbingo.QA.ReplacementIceResume",false)) result.scope="Continue actual storage-repair-ice-failed-1 checkpoint; ordinary mining aims within upper part of target tile to avoid hotbar occlusion. Normal movement, earned healing, ice pickup and save; no UI bypass, grants, teleport, inventory merging, storage withdrawal or clock injection. Return, replacement door and sealing remain separate.";
        if(SessionState.GetBool("Nyangbingo.QA.ReplacementDoorCraft",false)) result.scope="Actual storage-repair-ice-gather-1 earned day7 materials: normal scaffold/mining return to existing workbench, wood6/hemp4/ice4 recipe and actual crafting wait, manual save. No grants, teleport, inventory merging, storage withdrawal or clock injection. Door installation and recovered sealed preservation remain separate.";
        if(SessionState.GetBool("Nyangbingo.QA.ReplacementDoorResume",false)) result.scope="Continue actual storage-repair-door-return-partial-1 x215 normal return save. Ordinary remaining horizontal mining/movement to workbench, wood6/hemp4/ice4 recipe, crafting wait and manual save. No grants, teleport, merged inventory, jar withdrawal or clock injection. Installation, sealing and future preservation remain unverified.";
        if(SessionState.GetBool("Nyangbingo.QA.ReplacementDoorInstall",false)) result.scope="Actual storage-repair-door-craft-1: normal replacement door placement at destroyed296,120, three ordinary open-close cycles, blocked/allowed physical crossing, inspect actual jar seal/temperature and preserve ice2, ordinary save. No grants, forced door/seal state or clock injection. Future dawn preservation and Continue remain separate.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageDoorCraft",false)) result.scope="Actual storage-shelter-door-ice-1 earned save: normal scaffold ascent and return, workbench door craft and ordinary pause save. No grants, teleport, merged inventory or clock injection. Door placement, enclosure and sealed storage benefit remain separate.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageDoorInstall",false)) result.scope="Actual storage-shelter-door-craft-1 save: normal approach and earned door placement at planned cell296,120, three normal open-close cycles and physical crossings, ordinary save. No grants, teleport, merged inventories or clock injection. Enclosure and storage benefit remain separate.";
        if (SessionState.GetBool("Nyangbingo.QA.StorageEnclose",false)) result.scope="Actual storage-shelter-door-install-1 save: normally craft and place missing boundaries for a planned six-by-two room (right wall303 avoids bed302), inspect real seal/cooling and ordinary save. No grants, teleport, merged inventories or clock injection. Daily preservation benefit and novice understanding remain separate.";
        if (SessionState.GetBool("Nyangbingo.QA.StoragePreserve",false)) result.scope="Actual storage-shelter-enclosed-1 save: normal movement to existing warm bed, rest cancellation and two ordinary rest confirmations through next dawn, manual save and Title Continue. Inspect actual frozen storage ice3/remainder0.25 before and after. No grants, teleport, storage transfer or direct time injection. This is sleep-driven daily preservation, not natural waiting, novice understanding or full30-day completion.";
        if(SessionState.GetBool("Nyangbingo.QA.StorageRecovered",false)) result.scope="Actual storage-repair-door-install-1 day7 save after real invasion, normal recooling, floor repair, material gathering and replacement door. Existing warm bed cancel/two normal rests through day8 dawn, manual save and Title Continue; frozen jar ice2/remainder0 before and after. No grants, teleport, jar transfer or time injection. Not natural waiting, repeated invasion, novice understanding or full30-day completion.";
        if(SessionState.GetBool("Nyangbingo.QA.ShelterGateGather",false)) result.scope="Actual storage-repaired-rest-3 day8 save. Ordinary mining/scaffolds to observed tree elevation, then wood5/hemp10 harvest and manual save for gate preparation. Club shard is absent and remains a separate natural combat requirement. No grants, teleport, merged inventories or clock injection. Not satba crafting, boss access, novice discovery or full30-day completion.";
        if(SessionState.GetBool("Nyangbingo.QA.ShelterGateResume",false)) result.scope="Continue actual shelter-gate-gather-partial-1 day8 manual save at x227 after normal ascent and outbound walking. Ordinary wood5/hemp10 harvest and save, per-target observation90s and total240s. Club shard remains separate. No grants, teleport, inventory merging or clock injection. Not satba craft, return, boss access or novice/full30-day proof.";
        if(SessionState.GetBool("Nyangbingo.QA.GateStorageInspect",false)) result.scope="Normal Continue of actual shelter-gate-gather-1 save; read-only actual jar temperature, ice count and guide outside core range. No repair, movement route, time skip, grants, transfer or resave. Observation only, not storage-preservation pass or full30-day completion.";
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonUtility.ToJson(result, true));
        Debug.Log("[InputQA] " + status + ": " + Path.Combine(directory, "result.json"));
        GameplayInput.EndReplay();
        Application.runInBackground = previousBackground;
        EditorApplication.isPlaying = false;
    }
    private void OnDestroy()
    {
        EndPlacementAudit();
        GameplayInput.EndReplay();
        Application.runInBackground = previousBackground;
        if (!finished && !string.IsNullOrEmpty(directory))
            File.WriteAllText(Path.Combine(directory, "result.json"), "{\"status\":\"interrupted\"}");
    }
}
