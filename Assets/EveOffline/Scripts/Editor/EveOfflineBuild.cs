#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOffline;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EveOfflineBuild
{
    const string ScenePath = "Assets/EveOffline/Scenes/Main.unity";

    [MenuItem("EVE Offline/Create Main Scene")]
    public static void CreateMainScene()
    {
        Directory.CreateDirectory("Assets/EveOffline/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var marker = new GameObject("EVE Offline - runtime bootstraps automatically");
        marker.transform.position = Vector3.zero;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("EVE_OFFLINE_SCENE_OK " + ScenePath);
    }

    public static void BuildWindows()
    {
        CreateMainScene();
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/EVE Offline.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Build failed: " + report.summary.result);
        Debug.Log($"EVE_OFFLINE_BUILD_OK {report.summary.totalSize} bytes");
    }

    public static void RunSmokeTests()
    {
        CreateMainScene();

        SmokeStartingRosterAndDeploymentSelection();
        SmokeMiningCyclesAndPersistentOperation();
        SmokeUnloadReturnAndMixedOreTransfer();
        SmokeReturnWarpLifecycle();
        SmokeAutomaticMiningFlow();
        SmokeAutoRetargetIdleInvariant();
        SmokeSkillsAndInjectors();
        SmokeShieldSkillPrerequisites();
        SmokeTrainingQueueReordering();
        SmokeSkillPlanCopying();
        SmokeJitaFallbackPrices();
        SmokeWarehouseOreValuation();
        SmokeIndustrialCores();
        SmokeFleetCompression();
        SmokeNpcDurabilityAndDestruction();
        SmokeRealLocations();
        SmokeOreGrades();
        SmokePersistentMiningSites();
        SmokeMiningSiteLifecycle();
        SmokeV10MiningSiteMigration();
        SmokeV12IceMigration();
        SmokePreparedPackagesAndMigration();
        SmokePreparedPackageCombatEstimates();
        SmokeBurstLoadoutAndTransfers();
        SmokePaidRepair();
        SmokePerseveranceIceMining();
        SmokeGasMining();
        SmokeDeepCoreMercoxit();
        SmokeSameSystemBeltWarp();
        SmokeManualSecurityFloor();
        SmokeGlobalAutomaticBeltRoute();
        SmokeAutomaticRouteDowntimeRestart();
        SmokeAutomaticRouteCoreAndBurstRestart();
        SmokeOfflineMiningUnloadRoute();
        SmokeOfflineTrainingAndTruncation();
        SmokeOfflineNpcRisk();
        SmokeOfflineDynamicAnomalyChronology();
        SmokeMiningSiteDeadlineCache();
        SmokeTravelLifecycle();

        Debug.Log(
            "EVE_OFFLINE_SMOKE_OK roster/deployment; persistent mining; 5+10+5 return warp; " +
            "automatic unload/retarget by m3; Large/Small injectors; live ESI shield prerequisites; validated queue reordering; copied worker skill plans; Jita fallbacks and warehouse ore valuation; queued industrial cores; core-gated lossless fleet compression; NPC ETA/destruction; official ore grades; 73-system/677-belt highsec catalog with lazy static state; real persistent belts/anomalies with downtime and offline catch-up; locked prepared packages + guarded legacy/v13 migration; package EHP and shared combat-drone DPS estimates; ordered three-effect global bursts; exact ice compatibility and six-hour Clear Icicle lifecycle; gas extractors, skills, barge/exhumer cycles, bursts, unload and persistence; deep-core Mercoxit; atomic local-warp rejection; persisted manual/local travel; security-floor global auto-route with lossless cross-system travel, downtime restart and booster restart; offline mine/unload/route across 11:00, no autosell/double award, full queue time beyond the 30-day fleet cap, NPC-free versus hostile risk, simulated-time anomaly respawn, and stable cached world deadlines");
    }

    static void SmokeStartingRosterAndDeploymentSelection()
    {
        var fresh = SaveService.NewGame();
        Require(fresh.Characters.Count == 10, "new game must have exactly ten pilots");
        Require(fresh.Ships.Count == 9, "new game must have exactly nine starter ships");
        Require(fresh.Ships.TrueForAll(ship => ship.HullId == "venture"), "all starter ships must be Venture");
        Require(fresh.Ships.TrueForAll(ship => ship.Location == ShipLocation.Station), "all starter Ventures must begin at the station");
        Require(Catalog.GetShip("venture").FallbackBuyPrice > 0, "paid Venture packages must include a nonzero hull price; only the explicit T0 package is free");
        Require(fresh.Characters.FindAll(pilot => !string.IsNullOrWhiteSpace(pilot.AssignedShipUid)).Count == 9, "exactly nine pilots must have ships");
        Require(string.IsNullOrWhiteSpace(fresh.Characters[9].AssignedShipUid), "the tenth pilot must start without a ship");
        Require(fresh.Characters.GetRange(0, 9).TrueForAll(pilot => fresh.Ships.Exists(ship => ship.Uid == pilot.AssignedShipUid)), "starter pilot-to-ship assignments must use persistent UIDs");
        Require(fresh.Characters.Select(pilot => pilot.Id).Distinct(StringComparer.Ordinal).Count() == 10, "pilot IDs must be unique");
        Require(fresh.Ships.Select(ship => ship.Uid).Distinct(StringComparer.Ordinal).Count() == 9, "ship UIDs must be unique");
        Require(fresh.Ships.All(ship => ship.PackageId == "venture-ore-t0" && string.IsNullOrWhiteSpace(ship.TankPresetId)), "each free Venture must use the canonical bare ore T0 package without a tank preset");
        Require(fresh.Ships.All(ship => ship.Modules.Count(module => module.ModuleId == "miner-i") == 2), "each free Venture must have its two starter Miner I modules");
        Require(fresh.Ships.All(ship => ship.Modules.Count == 2), "free Venture T0 must contain no automatic low-slot or command equipment");
        Require(fresh.Ships.All(ship => string.IsNullOrWhiteSpace(ship.CombatDroneId) && ship.CombatDroneCount == 0 && string.IsNullOrWhiteSpace(ship.MiningDroneId) && ship.MiningDroneCount == 0), "free Venture T0 must contain no drones");
        Require(fresh.Characters.All(pilot => PreparedPackageService.CanUsePackage(pilot, Catalog.GetPackage("venture-ore-t0"))), "every starting pilot must already have the complete Venture ore T0 skill package");
        Require(Math.Abs(fresh.Isk - 1_000_000d) < 1d, "shared starting wallet must contain exactly 1,000,000 ISK");
        Require(typeof(CharacterSave).GetField("Isk") == null, "pilots must not have individual wallets");

        foreach (var pilot in fresh.Characters) pilot.DeployOnLaunch = false;
        fresh.Characters[0].DeployOnLaunch = true;
        fresh.Characters[4].DeployOnLaunch = true;
        fresh.Characters[9].DeployOnLaunch = true; // selected, but intentionally has no ship
        OperationService.Start(fresh, "uitra-belt-1");
        Require(fresh.Operation.Fleet.Count == 2, "only selected pilots with assigned ships may deploy");
        Require(fresh.Operation.Fleet.Any(member => member.PilotId == fresh.Characters[0].Id), "first selected pilot did not deploy");
        Require(fresh.Operation.Fleet.Any(member => member.PilotId == fresh.Characters[4].Id), "second selected pilot did not deploy");
        Require(fresh.Operation.Fleet.All(member => member.PilotId != fresh.Characters[9].Id), "a selected pilot without a ship must not deploy");
        RequireNearly(fresh.Isk, SaveService.StartingIsk, .001d, "deployment must not alter the shared wallet");
        Require(OperationService.TryStop(fresh, out _), "an unlocked selected fleet must be able to return to station");
        Require(fresh.Ships.All(ship => ship.Location == ShipLocation.Station), "stopping an operation must dock selected ships");
    }

    static void SmokeMiningCyclesAndPersistentOperation()
    {
        var save = NewSinglePilotOperation("uitra-belt-1");
        var member = save.Operation.Fleet[0];
        var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
        var pilot = save.Characters.Find(candidate => candidate.Id == member.PilotId);
        var hull = Catalog.GetShip(ship.HullId);
        var miner = Catalog.GetModule("miner-i");
        var asteroid = save.Operation.Asteroids[0];
        asteroid.OreId = "veldspar";
        asteroid.RemainingUnits = 1_000_000d;
        member.X = 0;
        member.Y = 0;
        member.Z = 0;
        asteroid.X = 25; // 50 km at the model's public km/world-unit scale
        asteroid.Y = 0;
        asteroid.Z = 0;

        OperationService.AssignTarget(save, member.ShipUid, asteroid.Id);
        Require(member.Order == FleetOrder.Approaching, "assigning an asteroid must create an approaching order");
        var xBeforeApproach = member.X;
        OperationService.Tick(save, 1f);
        Require(member.Order == FleetOrder.Approaching && member.X > xBeforeApproach, "ship must approach a target outside mining range");
        Require(OperationService.HoldVolume(ship.MiningHold) == 0, "ship must not mine while outside range");

        var rangeKm = OperationService.MiningRangeKm(pilot, hull, miner);
        var preferredRangeKm = rangeKm * .9f;
        member.X = asteroid.X - (preferredRangeKm + .0005f) / OperationService.KmPerWorldUnit;
        OperationService.Tick(save, .01f);
        Require(member.Order == FleetOrder.Mining && member.MiningCycles.Count == 2,
            "float rounding at the requested stop distance must not trap a ship in Approaching");
        Require(OperationService.AssignTarget(save, member.ShipUid, asteroid.Id), "range-tolerance smoke must be able to reset the target");
        member.X = asteroid.X - rangeKm * .4f; // distance = 80% range after KmPerWorldUnit
        var cycle = OperationService.MiningCycleSeconds(pilot, hull, miner, save, member);
        var slot0Cycle = OperationService.MiningCycleSecondsForSlot(save, member, 0);
        var slot1Cycle = OperationService.MiningCycleSecondsForSlot(save, member, 1);
        var slot0Yield = OperationService.MiningYieldM3ForSlot(save, member, 0);
        var slot1Yield = OperationService.MiningYieldM3ForSlot(save, member, 1);
        Require(slot0Cycle > 0 && slot1Cycle > 0 && slot0Yield > 0 && slot1Yield > 0, "aggregate HUD inputs must expose every compatible fitted mining slot");
        var aggregateRate = slot0Yield / slot0Cycle + slot1Yield / slot1Cycle;
        var aggregateCycle = (slot0Yield + slot1Yield) / aggregateRate;
        RequireNearly(aggregateCycle, cycle, .001d, "identical miners must collapse to their shared cycle while summing yield");
        RequireNearly(OperationService.AsteroidVolumeM3(asteroid), asteroid.RemainingUnits * Catalog.GetOre(asteroid.OreId).UnitVolumeM3, .001d, "asteroid units-to-cubic-meters conversion mismatch");
        OperationService.Tick(save, cycle - .1f);
        Require(member.Order == FleetOrder.Mining, "in-range ship must enter the mining state");
        Require(OperationService.HoldVolume(ship.MiningHold) == 0, "mining must deliver ore only when the laser cycle completes");
        OperationService.Tick(save, .2f);
        var firstCycleVolume = OperationService.HoldVolume(ship.MiningHold);
        Require(firstCycleVolume > 0, "a completed laser cycle must add ore to the mining hold");
        Require(firstCycleVolume <= OperationService.MiningHoldCapacity(pilot, hull) + .001f, "a mining cycle must respect mining-hold capacity");

        // This round trip represents opening station UI while the belt remains model-owned.
        // There is no scene/page state in OperationSave, so the restored operation keeps ticking.
        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        var restoredMember = restored.Operation.Fleet.Find(candidate => candidate.ShipUid == member.ShipUid);
        var restoredShip = restored.Ships.Find(candidate => candidate.Uid == member.ShipUid);
        Require(restored.Operation.Active && restoredMember?.TargetAsteroidId == asteroid.Id, "active operation and mining target must survive serialization");
        var elapsedBefore = restored.Operation.ElapsedSeconds;
        var volumeBefore = OperationService.HoldVolume(restoredShip.MiningHold);
        OperationService.Tick(restored, cycle + .01f);
        Require(restored.Operation.ElapsedSeconds > elapsedBefore, "belt simulation must tick independently of the visible station/belt page");
        Require(OperationService.HoldVolume(restoredShip.MiningHold) > volumeBefore, "persistent belt simulation must complete another mining cycle");
    }

    static void SmokeUnloadReturnAndMixedOreTransfer()
    {
        var save = NewSinglePilotOperation("uitra-belt-1");
        var member = save.Operation.Fleet[0];
        var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
        var asteroid = save.Operation.Asteroids[0];
        OperationService.AssignTarget(save, ship.Uid, asteroid.Id);

        OperationService.AddItem(save.StationInventory, "veldspar", 10);
        OperationService.AddItem(ship.MiningHold, "veldspar", 100);
        OperationService.AddItem(ship.MiningHold, "scordite", 25);
        OperationService.AddItem(ship.CargoHold, "pyroxeres", 7);
        ship.ShieldHp = 1;
        OperationService.ReturnShip(save, ship.Uid, true);

        Require(ship.Location == ShipLocation.Transit && member.Order == FleetOrder.UnloadAndReturn, "unload-and-return must put the ship in transit");
        Require(member.WarpPhase == FleetWarpPhase.AligningOut, "unload-and-return must begin with the outbound alignment phase");
        RequireNearly(member.TransitSecondsLeft, OperationService.WarpAlignSeconds + OperationService.WarpTransitSeconds + OperationService.WarpInSeconds, .001d, "unload-and-return total warp countdown mismatch");
        Require(member.TargetAsteroidId == asteroid.Id, "unload-and-return must preserve the assigned asteroid target");
        OperationService.Tick(save, OperationService.WarpAlignSeconds + OperationService.WarpTransitSeconds - .01f);
        Require(ship.Location == ShipLocation.Transit && member.WarpPhase == FleetWarpPhase.InTransit, "ship must remain away until the ten-second transit phase completes");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, "veldspar"), 10d, .001d, "ore must remain aboard during transit");

        OperationService.Tick(save, .02f);
        Require(ship.Location == ShipLocation.Transit && member.WarpPhase == FleetWarpPhase.WarpingIn, "unloaded ship must spend five seconds warping back into the belt");
        Require(ship.MiningHold.Count == 0, "mining hold must be emptied at the station");
        RequireNearly(OperationService.ItemQuantity(ship.CargoHold, "pyroxeres"), 7d, .001d, "ordinary cargo must remain aboard during automatic ore unload");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, "veldspar"), 110d, .001d, "existing station ore and unloaded ore must merge");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, "scordite"), 25d, .001d, "second mining-hold ore must transfer independently");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, "pyroxeres"), 0d, .001d, "automatic ore unload must not move ordinary cargo");
        OperationService.Tick(save, OperationService.WarpInSeconds + .01f);
        Require(ship.Location == ShipLocation.Belt, "unload-and-return must rejoin the active belt after warp-in");
        Require(member.Order == FleetOrder.Approaching && member.TargetAsteroidId == asteroid.Id, "rejoined ship must resume its preserved target");
        RequireNearly(ship.ShieldHp, 1d, .001d, "automatic docking must preserve damage until paid repair");
    }

    static void SmokeReturnWarpLifecycle()
    {
        var save = SaveService.NewGame();
        foreach (var pilot in save.Characters) pilot.DeployOnLaunch = false;
        save.Characters[0].DeployOnLaunch = true;
        save.Characters[1].DeployOnLaunch = true;
        OperationService.Start(save, "uitra-belt-1");

        var operation = save.Operation;
        operation.RaidTimerSeconds = 99_999f;
        Require(operation.Fleet.Count == 2, "return-warp smoke setup must deploy exactly two ships");
        Require(operation.WarpPointInitialized &&
                !float.IsNaN(operation.WarpPointX) && !float.IsInfinity(operation.WarpPointX) &&
                !float.IsNaN(operation.WarpPointY) && !float.IsInfinity(operation.WarpPointY) &&
                !float.IsNaN(operation.WarpPointZ) && !float.IsInfinity(operation.WarpPointZ),
            "an operation must expose one finite shared warp point");

        var returning = operation.Fleet.Single(member => member.PilotId == save.Characters[0].Id);
        var docking = operation.Fleet.Single(member => member.PilotId == save.Characters[1].Id);
        var returningShip = save.Ships.Find(ship => ship.Uid == returning.ShipUid);
        var dockingShip = save.Ships.Find(ship => ship.Uid == docking.ShipUid);
        var asteroid = operation.Asteroids.First(candidate => candidate.RemainingUnits > 0 && OperationService.CanMineResource(returningShip, candidate.OreId));
        var dockingOreId = asteroid.OreId == "scordite" ? "veldspar" : "scordite";
        Require(OperationService.AssignTarget(save, returningShip.Uid, asteroid.Id), "returning ship must accept a resumable target");
        Require(OperationService.AssignTarget(save, dockingShip.Uid, asteroid.Id), "dock-and-stay ship must accept a target before departure");

        returning.Order = FleetOrder.Mining;
        returning.X = 12.375f; returning.Y = -3.25f; returning.Z = 41.625f;
        docking.X = -17.5f; docking.Y = 2.75f; docking.Z = 8.125f;
        var originX = returning.X; var originY = returning.Y; var originZ = returning.Z;
        var sharedWarpX = operation.WarpPointX; var sharedWarpY = operation.WarpPointY; var sharedWarpZ = operation.WarpPointZ;
        OperationService.AddItem(returningShip.MiningHold, asteroid.OreId, 123);
        OperationService.AddItem(dockingShip.MiningHold, dockingOreId, 37);

        var returnResult = OperationService.ReturnShipWithResult(save, returningShip.Uid, true);
        var dockResult = OperationService.ReturnShipWithResult(save, dockingShip.Uid, false);
        Require(returnResult.Success && returnResult.ImmediateCount == 1 && dockResult.Success && dockResult.ImmediateCount == 1,
            "both return commands must begin immediately for unlocked ships");
        Require(operation.WarpPointX == sharedWarpX && operation.WarpPointY == sharedWarpY && operation.WarpPointZ == sharedWarpZ,
            "every returning fleet member must reuse the operation's shared warp point");
        Require(returning.WarpPhase == FleetWarpPhase.AligningOut && docking.WarpPhase == FleetWarpPhase.AligningOut,
            "both return modes must begin in the common outbound phase");
        RequireNearly(returning.WarpPhaseSecondsLeft, OperationService.WarpAlignSeconds, .001d, "outbound alignment must begin at five seconds");
        RequireNearly(returning.TransitSecondsLeft, OperationService.WarpAlignSeconds + OperationService.WarpTransitSeconds + OperationService.WarpInSeconds, .001d, "round-trip total countdown mismatch");
        RequireNearly(docking.TransitSecondsLeft, OperationService.WarpAlignSeconds, .001d, "dock-and-stay must require only the outbound phase");
        Require(returning.ResumeOrder == FleetOrder.Mining && returning.ResumeTargetAsteroidId == asteroid.Id,
            "round-trip return must snapshot the exact order and target");
        Require(returning.WarpOriginX == originX && returning.WarpOriginY == originY && returning.WarpOriginZ == originZ,
            "round-trip return must snapshot the exact belt origin");

        OperationService.Tick(save, OperationService.WarpAlignSeconds - .01f);
        Require(returning.WarpPhase == FleetWarpPhase.AligningOut && docking.WarpPhase == FleetWarpPhase.AligningOut,
            "neither ship may leave the five-second alignment phase early");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, asteroid.OreId), 0d, .001d, "round-trip ore must remain aboard during alignment");
        OperationService.Tick(save, .02f);
        Require(returning.WarpPhase == FleetWarpPhase.InTransit && returningShip.Location == ShipLocation.Transit,
            "round-trip ship must enter the ten-second away phase after alignment");
        Require(dockingShip.Location == ShipLocation.Station && operation.Fleet.All(member => member.ShipUid != dockingShip.Uid),
            "dock-and-stay must reach the station and leave the fleet after the outbound phase");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, dockingOreId), 37d, .001d, "dock-and-stay must unload at the end of its outbound phase");

        OperationService.Tick(save, 3.25f);
        var phaseBeforeJson = returning.WarpPhaseSecondsLeft;
        var totalBeforeJson = returning.TransitSecondsLeft;
        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        var restoredOperation = restored.Operation;
        var restoredMember = restoredOperation.Fleet.Single(member => member.ShipUid == returningShip.Uid);
        var restoredShip = restored.Ships.Find(ship => ship.Uid == returningShip.Uid);
        Require(restoredMember.WarpPhase == FleetWarpPhase.InTransit && restoredShip.Location == ShipLocation.Transit,
            "JSON round-trip must preserve a mid-warp phase and ship location");
        RequireNearly(restoredMember.WarpPhaseSecondsLeft, phaseBeforeJson, .0001d, "JSON round-trip must preserve the active phase timer");
        RequireNearly(restoredMember.TransitSecondsLeft, totalBeforeJson, .0001d, "JSON round-trip must preserve the total return timer");
        Require(restoredMember.ResumeOrder == FleetOrder.Mining && restoredMember.ResumeTargetAsteroidId == asteroid.Id,
            "JSON round-trip must preserve the resumable order and target");
        Require(restoredMember.WarpOriginX == originX && restoredMember.WarpOriginY == originY && restoredMember.WarpOriginZ == originZ,
            "JSON round-trip must preserve the exact belt origin");
        RequireNearly(restoredOperation.WarpPointX, sharedWarpX, .0001d, "JSON round-trip must preserve shared warp point X");
        RequireNearly(restoredOperation.WarpPointY, sharedWarpY, .0001d, "JSON round-trip must preserve shared warp point Y");
        RequireNearly(restoredOperation.WarpPointZ, sharedWarpZ, .0001d, "JSON round-trip must preserve shared warp point Z");

        OperationService.Tick(restored, restoredMember.WarpPhaseSecondsLeft - .01f);
        Require(restoredMember.WarpPhase == FleetWarpPhase.InTransit, "round-trip ship must remain away for the full ten-second transit phase");
        RequireNearly(OperationService.ItemQuantity(restored.StationInventory, asteroid.OreId), 0d, .001d, "ore must not unload before the away phase ends");
        OperationService.Tick(restored, .02f);
        Require(restoredMember.WarpPhase == FleetWarpPhase.WarpingIn && restoredShip.Location == ShipLocation.Transit,
            "round-trip ship must begin its five-second warp-in after unloading");
        RequireNearly(restoredMember.WarpPhaseSecondsLeft, OperationService.WarpInSeconds - .01f, .01d, "warp-in phase duration mismatch");
        RequireNearly(OperationService.ItemQuantity(restored.StationInventory, asteroid.OreId), 123d, .001d, "away-phase completion must unload the mining hold");
        Require(restoredShip.MiningHold.Count == 0, "away-phase completion must empty the mining hold before warp-in");

        OperationService.Tick(restored, restoredMember.WarpPhaseSecondsLeft - .01f);
        Require(restoredMember.WarpPhase == FleetWarpPhase.WarpingIn, "ship must remain in warp-in until all five seconds elapse");
        OperationService.Tick(restored, .02f);
        Require(restoredShip.Location == ShipLocation.Belt && restoredOperation.Fleet.Contains(restoredMember),
            "round-trip ship must rejoin the same operation after warp-in");
        Require(restoredMember.X == originX && restoredMember.Y == originY && restoredMember.Z == originZ,
            "round-trip ship must return to its exact serialized belt origin");
        Require(restoredMember.TargetAsteroidId == asteroid.Id && restoredMember.Order == FleetOrder.Mining,
            "round-trip ship must restore its exact live target and order");
        Require(restoredMember.WarpPhase == FleetWarpPhase.None && restoredMember.WarpPhaseSecondsLeft == 0 && restoredMember.TransitSecondsLeft == 0,
            "completed return must clear every warp timer and phase");

        Require(OperationService.RequestStopWithWarp(restored, out _), "operation finish must request a visible fleet outbound warp");
        Require(restoredOperation.Active && restoredOperation.StopAfterFleetWarp && restoredOperation.Fleet.Count == 1,
            "operation must stay active while its last ship is aligning out");
        Require(restoredMember.Order == FleetOrder.DockAndStay && restoredMember.WarpPhase == FleetWarpPhase.AligningOut,
            "operation finish must put the remaining ship into dock-and-stay outbound alignment");
        OperationService.Tick(restored, OperationService.WarpAlignSeconds - .01f);
        Require(restoredOperation.Active && restoredOperation.StopAfterFleetWarp && restoredOperation.Fleet.Count == 1 && restoredShip.Location == ShipLocation.Transit,
            "operation must not finish before the last ship completes all five outbound seconds");
        OperationService.Tick(restored, .02f);
        Require(!restoredOperation.Active && !restoredOperation.StopAfterFleetWarp && restoredOperation.Fleet.Count == 0,
            "operation must finish and clear its fleet immediately after the last outbound warp completes");
        Require(restoredShip.Location == ShipLocation.Station, "operation finish must leave its last ship docked at Jita 4-4");
    }

    static void SmokeAutomaticMiningFlow()
    {
        var save = SaveService.NewGame();
        foreach (var pilot in save.Characters) pilot.DeployOnLaunch = false;
        for (var index = 0; index < 3; index++) save.Characters[index].DeployOnLaunch = true;
        OperationService.Start(save, "uitra-belt-1");

        var op = save.Operation;
        Require(!op.AutoUnload && !op.AutoRetarget, "automatic mining options must be opt-in for a fresh save");
        op.AutoUnload = true;
        op.AutoRetarget = true;
        op.RaidTimerSeconds = 99_999f;
        Require(op.Fleet.Count == 3, "automatic-mining smoke setup must deploy exactly three ships");

        var exhausted = new AsteroidSave { Id = "auto-exhausted", OreId = "veldspar", RemainingUnits = 0, X = 0, Y = 0, Z = 10, Scale = 2 };
        var freeSmall = new AsteroidSave { Id = "auto-free-veldspar", OreId = "veldspar", RemainingUnits = 1000, X = 5, Y = 0, Z = 10, Scale = 2 };
        var freeLarge = new AsteroidSave { Id = "auto-free-bistot", OreId = "bistot", RemainingUnits = 10, X = 10, Y = 0, Z = 10, Scale = 2 };
        var occupiedLargest = new AsteroidSave { Id = "auto-occupied-crokite", OreId = "crokite", RemainingUnits = 30, X = 15, Y = 0, Z = 10, Scale = 2 };
        var incompatible = new AsteroidSave { Id = "auto-incompatible", OreId = "clear-icicle", RemainingUnits = 10_000, X = 20, Y = 0, Z = 10, Scale = 2 };
        op.Asteroids = new List<AsteroidSave> { exhausted, freeSmall, freeLarge, occupiedLargest, incompatible };
        MiningSiteService.SnapshotActiveOperation(save);

        var miner = op.Fleet[0];
        var firstOccupier = op.Fleet[1];
        var secondOccupier = op.Fleet[2];
        var minerShip = save.Ships.Find(candidate => candidate.Uid == miner.ShipUid);
        var minerPilot = save.Characters.Find(candidate => candidate.Id == miner.PilotId);
        var minerHull = Catalog.GetShip(minerShip.HullId);
        Require(!OperationService.CanMineResource(minerShip, incompatible.OreId), "automatic target selection setup requires an incompatible ice asteroid");

        firstOccupier.TargetAsteroidId = occupiedLargest.Id;
        firstOccupier.Order = FleetOrder.Mining;
        miner.TargetAsteroidId = exhausted.Id;
        miner.Order = FleetOrder.Approaching;
        OperationService.Tick(save, .01f);
        Require(miner.TargetAsteroidId == freeLarge.Id, "automatic retarget must compare mixed ordinary ore by remaining m3, not misleading unit count");

        freeSmall.RemainingUnits = 1600;
        miner.TargetAsteroidId = exhausted.Id;
        miner.Order = FleetOrder.Approaching;
        Require(OperationService.TryAssignAutomaticTarget(save, miner), "automatic retarget must resolve equal-volume free candidates");
        Require(miner.TargetAsteroidId == freeLarge.Id, "equal-volume automatic retarget must use stable ordinal asteroid ID as its tiebreak");

        freeSmall.RemainingUnits = 0;
        firstOccupier.TargetAsteroidId = freeLarge.Id;
        firstOccupier.Order = FleetOrder.Mining;
        secondOccupier.TargetAsteroidId = occupiedLargest.Id;
        secondOccupier.Order = FleetOrder.Mining;
        miner.TargetAsteroidId = exhausted.Id;
        miner.Order = FleetOrder.Approaching;
        Require(OperationService.TryAssignAutomaticTarget(save, miner), "automatic retarget must fall back to an occupied compatible asteroid");
        Require(miner.TargetAsteroidId == occupiedLargest.Id, "automatic retarget fallback must join the occupied compatible asteroid with the most remaining m3 and exclude incompatible resources");

        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        Require(restored.Operation.AutoUnload && restored.Operation.AutoRetarget, "automatic-mining operation flags must survive serialization");

        var ore = Catalog.GetOre(occupiedLargest.OreId);
        var capacity = OperationService.MiningHoldCapacity(minerPilot, minerHull);
        OperationService.AddItem(minerShip.MiningHold, ore.Id, Math.Ceiling(capacity / ore.UnitVolumeM3));
        var stationUnitsBefore = OperationService.ItemQuantity(save.StationInventory, ore.Id);
        var shipUnitsBefore = OperationService.ItemQuantity(minerShip.MiningHold, ore.Id);
        miner.X = occupiedLargest.X;
        miner.Y = occupiedLargest.Y;
        miner.Z = occupiedLargest.Z;
        var fullHoldDetectionCycle = OperationService.MiningCycleSecondsForSlot(save, miner, 0);
        Require(fullHoldDetectionCycle > 0, "automatic unload smoke setup requires an active mining laser cycle");
        OperationService.Tick(save, fullHoldDetectionCycle + .01f);
        Require(minerShip.Location == ShipLocation.Transit && miner.Order == FleetOrder.UnloadAndReturn, "full mining hold with auto-unload enabled must begin unload-and-return");
        Require(miner.WarpPhase == FleetWarpPhase.AligningOut, "automatic unload must begin with outbound alignment");
        RequireNearly(miner.TransitSecondsLeft, OperationService.WarpAlignSeconds + OperationService.WarpTransitSeconds + OperationService.WarpInSeconds, .001d, "automatic unload total return countdown mismatch");
        Require(miner.TargetAsteroidId == occupiedLargest.Id, "automatic unload must preserve the selected asteroid target");

        OperationService.Tick(save, OperationService.WarpAlignSeconds + OperationService.WarpTransitSeconds - .01f);
        Require(minerShip.Location == ShipLocation.Transit && miner.WarpPhase == FleetWarpPhase.InTransit, "automatic unload must remain away until its ten-second transit completes");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, ore.Id), stationUnitsBefore, .001d, "automatic unload must not transfer ore while the ship is in transit");
        OperationService.Tick(save, .02f);
        Require(minerShip.Location == ShipLocation.Transit && miner.WarpPhase == FleetWarpPhase.WarpingIn, "automatic unload must enter the five-second warp-in phase after transfer");
        RequireNearly(OperationService.ItemQuantity(minerShip.MiningHold, ore.Id), 0d, .001d, "automatic unload must empty the mining hold");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, ore.Id), stationUnitsBefore + shipUnitsBefore, .001d, "automatic unload must transfer the mined resource to the station inventory");
        OperationService.Tick(save, OperationService.WarpInSeconds + .01f);
        Require(minerShip.Location == ShipLocation.Belt && miner.Order == FleetOrder.Approaching, "automatic unload must return the ship to its preserved target after warp-in");
    }

    static void SmokeAutoRetargetIdleInvariant()
    {
        var arrival = SaveService.NewGame();
        foreach (var pilot in arrival.Characters) pilot.DeployOnLaunch = false;
        for (var index = 0; index < 3; index++) arrival.Characters[index].DeployOnLaunch = true;
        arrival.Operation.AutoRetarget = true;
        OperationService.Start(arrival, "uitra-belt-1");
        arrival.Operation.RaidTimerSeconds = 99_999f;
        RequireAutomaticRetargetDistribution(arrival, 3, "arrival with auto-retarget enabled");

        var toggled = SaveService.NewGame();
        foreach (var pilot in toggled.Characters) pilot.DeployOnLaunch = false;
        for (var index = 0; index < 3; index++) toggled.Characters[index].DeployOnLaunch = true;
        OperationService.Start(toggled, "uitra-belt-1");
        toggled.Operation.RaidTimerSeconds = 99_999f;
        Require(toggled.Operation.Fleet.Count == 3 && toggled.Operation.Fleet.All(member => member.Order == FleetOrder.Idle && string.IsNullOrEmpty(member.TargetAsteroidId)),
            "auto-retarget toggle smoke setup requires an already-arrived idle fleet");
        toggled.Operation.AutoRetarget = true;
        OperationService.Tick(toggled, .01f);
        RequireAutomaticRetargetDistribution(toggled, 3, "first tick after enabling auto-retarget");

        var fullHold = NewSinglePilotOperation("uitra-belt-1");
        var fullOperation = fullHold.Operation;
        fullOperation.RaidTimerSeconds = 99_999f;
        var fullMember = fullOperation.Fleet.Single();
        var fullShip = fullHold.Ships.Find(ship => ship.Uid == fullMember.ShipUid);
        var fullPilot = fullHold.Characters.Find(pilot => pilot.Id == fullMember.PilotId);
        var fullHull = Catalog.GetShip(fullShip.HullId);
        var compatibleAsteroid = fullOperation.Asteroids.First(asteroid => asteroid.RemainingUnits > 0 && OperationService.CanMineResource(fullShip, asteroid.OreId));
        var compatibleResource = Catalog.GetOre(compatibleAsteroid.OreId);
        var capacity = OperationService.MiningHoldCapacity(fullPilot, fullHull);
        OperationService.AddItem(fullShip.MiningHold, compatibleResource.Id, Math.Ceiling(capacity / compatibleResource.UnitVolumeM3));
        Require(capacity - OperationService.HoldVolume(fullShip.MiningHold) < compatibleResource.UnitVolumeM3,
            "full-hold auto-retarget smoke setup must leave no room for one resource unit");

        fullOperation.AutoUnload = false;
        fullOperation.AutoRetarget = true;
        Require(!OperationService.TryAssignAutomaticTarget(fullHold, fullMember),
            "a full ship without auto-unload must reject automatic target assignment");
        for (var tick = 0; tick < 3; tick++) OperationService.Tick(fullHold, .25f);
        Require(fullShip.Location == ShipLocation.Belt && fullMember.Order == FleetOrder.Idle && string.IsNullOrEmpty(fullMember.TargetAsteroidId),
            "a full ship without auto-unload must remain idle and targetless across repeated ticks");
        Require(fullMember.WarpPhase == FleetWarpPhase.None && fullMember.TransitSecondsLeft == 0,
            "a full ship without auto-unload must not enter an automatic return loop");
    }

    static void RequireAutomaticRetargetDistribution(GameSave save, int expectedFleetCount, string context)
    {
        var operation = save.Operation;
        Require(operation.Active && operation.Fleet.Count == expectedFleetCount, $"{context}: unexpected fleet size");
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in operation.Fleet)
        {
            var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
            var pilot = save.Characters.Find(candidate => candidate.Id == member.PilotId);
            var hull = Catalog.GetShip(ship?.HullId);
            var freeHoldM3 = OperationService.MiningHoldCapacity(pilot, hull) - OperationService.HoldVolume(ship?.MiningHold);
            var compatible = operation.Asteroids
                .Where(asteroid => asteroid.RemainingUnits > 0 && OperationService.CanMineResource(ship, asteroid.OreId))
                .Where(asteroid =>
                {
                    var resource = Catalog.GetOre(asteroid.OreId);
                    return resource != null && resource.UnitVolumeM3 <= freeHoldM3 + 1e-6;
                })
                .ToArray();
            var expectedTarget = compatible
                .Where(asteroid => !occupied.Contains(asteroid.Id))
                .OrderByDescending(OperationService.AsteroidVolumeM3)
                .ThenBy(asteroid => asteroid.Id, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? compatible
                    .OrderByDescending(OperationService.AsteroidVolumeM3)
                    .ThenBy(asteroid => asteroid.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
            Require(expectedTarget != null, $"{context}: every mining ship needs a compatible asteroid");
            Require(member.TargetAsteroidId == expectedTarget.Id && member.Order is FleetOrder.Approaching or FleetOrder.Mining,
                $"{context}: ships must select the largest free compatible asteroid by remaining m3");
            occupied.Add(member.TargetAsteroidId);
        }
        Require(occupied.Count == expectedFleetCount, $"{context}: ships must spread across distinct free asteroids before sharing one");
    }

    static void SmokeSkillsAndInjectors()
    {
        RequireNearly(Catalog.PerfectOreLaserYieldImplantMultiplier, 1.1025d, .000001d, "Michi plus Highwall perfect-clone multiplier mismatch");
        RequireNearly(Catalog.PerfectIceHarvesterCycleImplantMultiplier, .95d, .000001d, "Yeti BX-2 perfect-clone cycle multiplier mismatch");
        RequireNearly(Catalog.PerfectMiningForemanMindlinkMultiplier, 1.25d, .000001d, "Mining Foreman Mindlink assumption mismatch");
        RequireNearly(SkillService.RequiredSp("mining", 1), 250d, .001d, "rank-1 skill level I SP formula mismatch");
        RequireNearly(SkillService.RequiredSp("mining", 2), 1415d, .001d, "rank-1 skill level II SP formula mismatch");
        RequireNearly(SkillService.RequiredSp("mining", 5), 256000d, .001d, "rank-1 skill level V SP formula mismatch");
        Require(Catalog.Skills.All(skill => skill.Rank > 0 && skill.TypeId > 0), "mining skill catalog must use real books with ranks and type IDs");

        var save = SaveService.NewGame();
        var trainee = save.Characters[9];
        var mining = SkillService.GetState(trainee, "mining");
        Require(mining != null && mining.BookOwned && SkillService.GetLevel(trainee, "mining") == 1, "starter mining skill state is invalid");
        Require(SkillService.TryTrainNextLevel(trainee, "mining", out _), "pilot must be able to queue Mining II");
        Require(SkillService.TryEnqueueToTarget(trainee, "mining", 3, out _), "pilot must be able to append Mining III without replacing Mining II");
        Require(SkillService.MaxTrainingQueueEntries == 150 && SkillService.GetTrainingQueue(trainee).Count == 2, "training queue must preserve both levels within the current EVE 150-entry limit");
        var spBefore = mining.SkillPoints;
        SkillService.TickAll(save, 60d);
        RequireNearly(mining.SkillPoints - spBefore, Catalog.PerfectTrainingSpPerMinute, .001d, "skill points must progress with real time");
        Require(trainee.TrainingSkillId == "mining" && trainee.TrainingTargetLevel == 2, "skill queue target must persist while training");

        var price = MarketService.InjectorPrice(save);
        Require(price > 0, "Large Skill Injector fallback price must be nonzero");
        save.Isk = price + 1_000_000d;
        var allocatedBefore = SkillService.AllocatedSp(trainee);
        Require(SkillService.TryBuyLargeSkillInjector(save, trainee, out _), "a funded pilot must be able to buy a Large Skill Injector");
        RequireNearly(trainee.UnallocatedSkillPoints, Catalog.LargeSkillInjectorSkillPoints, .001d, "every injector must grant the fixed 400,000 unallocated SP");
        RequireNearly(SkillService.AllocatedSp(trainee), allocatedBefore, .001d, "injector SP must remain unallocated until explicitly applied");
        RequireNearly(save.Isk, 1_000_000d, .001d, "injector must charge the one shared wallet exactly once");
        var freeBeforeApply = trainee.UnallocatedSkillPoints;
        Require(SkillService.TryApplyUnallocatedToLevel(trainee,"mining",2,out var explicitlyAppliedSp,out _), "unallocated SP must be applicable to an explicitly selected next skill level");
        Require(explicitlyAppliedSp>0&&explicitlyAppliedSp<freeBeforeApply,"only the selected level's missing SP must be allocated");
        Require(SkillService.GetLevel(trainee, "mining") == 2, "applying unallocated SP must complete Mining II");
        Require(trainee.UnallocatedSkillPoints < freeBeforeApply && trainee.UnallocatedSkillPoints > 0, "only SP needed for the next level must be allocated");
        Require(SkillService.GetTrainingQueue(trainee).Count == 1 && trainee.TrainingTargetLevel == 3, "finishing Mining II with free SP must advance the serialized queue to Mining III");
        Require(SkillService.TryRemoveTrainingQueueEntry(trainee, 0, out _) && SkillService.GetTrainingQueue(trainee).Count == 0, "a queued level must be removable without resetting the pilot");

        var smallPrice = MarketService.SmallInjectorPrice(save);
        Require(smallPrice > 0, "Small Skill Injector fallback price must be nonzero");
        save.Isk = smallPrice + 12_345d;
        var freeBeforeSmall = trainee.UnallocatedSkillPoints;
        var allocatedBeforeSmall = SkillService.AllocatedSp(trainee);
        Require(SkillService.TryBuySmallSkillInjector(save, trainee, out _), "a funded pilot must be able to buy a Small Skill Injector");
        RequireNearly(trainee.UnallocatedSkillPoints - freeBeforeSmall, Catalog.SmallSkillInjectorSkillPoints, .001d, "every Small Skill Injector must grant the fixed 80,000 unallocated SP");
        RequireNearly(SkillService.AllocatedSp(trainee), allocatedBeforeSmall, .001d, "Small Skill Injector SP must remain unallocated until explicitly applied");
        RequireNearly(save.Isk, 12_345d, .001d, "Small Skill Injector must charge the one shared wallet exactly once");

        var carrySave = SaveService.NewGame();
        var carryPilot = carrySave.Characters[9];
        Require(SkillService.TryEnqueueToTarget(carryPilot, "mining", 3, out _), "carry-over smoke must queue Mining II and III");
        var fullQueueSeconds = SkillService.TotalTrainingSecondsLeft(carryPilot);
        SkillService.TickAll(carrySave, fullQueueSeconds + .01d);
        Require(SkillService.GetLevel(carryPilot, "mining") == 3 && SkillService.GetTrainingQueue(carryPilot).Count == 0, "one real-time tick must carry across and complete multiple queued levels");

        var tierPilot = new CharacterSave { Skills = new List<CharacterSkillSave>() };
        foreach(var totalSp in new[]{0d,4_999_999d,5_000_000d,50_000_000d,80_000_000d,250_000_000d})
        {
            tierPilot.UnallocatedSkillPoints=totalSp;
            RequireNearly(MarketService.InjectorSp(save,tierPilot),400_000d,.001d,"fixed injector yield must not depend on pilot total SP");
            RequireNearly(MarketService.SmallInjectorSp(save,tierPilot),80_000d,.001d,"fixed Small Skill Injector yield must not depend on pilot total SP");
        }

        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        var restoredTrainee = restored.Characters.Find(pilot => pilot.Id == trainee.Id);
        Require(restoredTrainee != null && restoredTrainee.UnallocatedSkillPoints > 0, "unallocated SP must survive save serialization");
    }

    static void SmokeShieldSkillPrerequisites()
    {
        void RequireExactSkill(string skillId, int typeId, int rank, params string[] expectedPrerequisites)
        {
            var skill = Catalog.GetSkill(skillId);
            Require(skill != null && skill.TypeId == typeId && skill.Rank == rank,
                $"{skillId} must retain its live ESI type ID {typeId} and rank {rank}");
            var actual = (skill.Prerequisites ?? Array.Empty<SkillRequirement>())
                .Select(requirement => $"{requirement.SkillId}:{requirement.Level}")
                .OrderBy(signature => signature, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var expected = (expectedPrerequisites ?? Array.Empty<string>())
                .OrderBy(signature => signature, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Require(actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase),
                $"{skillId} live ESI prerequisite mismatch: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
        }

        RequireExactSkill("power-grid-management", 3413, 1);
        RequireExactSkill("shield-operation", 3416, 1, "power-grid-management:1");
        RequireExactSkill("shield-management", 3419, 3, "power-grid-management:3");
        RequireExactSkill("shield-upgrades", 3425, 2, "power-grid-management:2", "science:1");
        RequireExactSkill("tactical-shield-manipulation", 3420, 4, "power-grid-management:3");

        var ventureT1Skills = PreparedPackageService.RequiredSkills(Catalog.GetPackage("venture-ore-t1"));
        Require(ventureT1Skills.Any(requirement => requirement.SkillId == "power-grid-management" && requirement.Level == 3),
            "Venture T1 package must inherit Power Grid Management III from its fitted shield tank");
        Require(!ventureT1Skills.Any(requirement => requirement.SkillId == "shield-operation" && requirement.Level >= 4),
            "Venture T1 package must not require the obsolete Shield Operation IV prerequisite");
    }

    static void SmokeTrainingQueueReordering()
    {
        var independentSave = SaveService.NewGame();
        var independentPilot = independentSave.Characters[1];
        Require(SkillService.TryEnqueueToTarget(independentPilot, "mining", 2, out _), "queue-reorder smoke must enqueue Mining II");
        Require(SkillService.TryEnqueueToTarget(independentPilot, "mining-frigate", 2, out _), "queue-reorder smoke must enqueue Mining Frigate II");
        Require(TrainingQueueSignature(independentPilot) == "mining:2|mining-frigate:2", "queue-reorder smoke setup order mismatch");

        Require(SkillService.TryMoveTrainingQueueEntryUp(independentPilot, 1, out _), "independent queued skills must move upward");
        Require(TrainingQueueSignature(independentPilot) == "mining-frigate:2|mining:2", "moving an independent skill upward must swap exactly one queue position");
        Require(independentPilot.TrainingSkillId == "mining-frigate" && independentPilot.TrainingTargetLevel == 2,
            "moving a new entry to the queue head must update the serialized legacy active-entry mirror");

        Require(SkillService.TryMoveTrainingQueueEntryDown(independentPilot, 0, out _), "independent queued skills must move downward");
        Require(TrainingQueueSignature(independentPilot) == "mining:2|mining-frigate:2", "moving an independent skill downward must restore its previous order");
        Require(independentPilot.TrainingSkillId == "mining" && independentPilot.TrainingTargetLevel == 2,
            "moving the head downward must update the serialized legacy active-entry mirror");

        var topBoundaryBefore = JsonUtility.ToJson(independentPilot);
        Require(!SkillService.TryMoveTrainingQueueEntryUp(independentPilot, 0, out _), "the first queue entry must reject an upward move");
        Require(JsonUtility.ToJson(independentPilot) == topBoundaryBefore, "an upward boundary rejection must leave queue and legacy head unchanged");
        var bottomBoundaryBefore = JsonUtility.ToJson(independentPilot);
        Require(!SkillService.TryMoveTrainingQueueEntryDown(independentPilot, 1, out _), "the last queue entry must reject a downward move");
        Require(JsonUtility.ToJson(independentPilot) == bottomBoundaryBefore, "a downward boundary rejection must leave queue and legacy head unchanged");

        var prerequisiteSave = SaveService.NewGame();
        var prerequisitePilot = prerequisiteSave.Characters[1];
        SkillService.GetState(prerequisitePilot, "mining-upgrades", true).BookOwned = true;
        Require(SkillService.TryEnqueueToTarget(prerequisitePilot, "mining", 3, out _), "prerequisite-order smoke must enqueue Mining II-III");
        Require(SkillService.TryEnqueueToTarget(prerequisitePilot, "mining-upgrades", 1, out _), "prerequisite-order smoke must enqueue Mining Upgrades I after Mining III");
        Require(TrainingQueueSignature(prerequisitePilot) == "mining:2|mining:3|mining-upgrades:1", "prerequisite-order smoke setup mismatch");
        var prerequisiteBefore = JsonUtility.ToJson(prerequisitePilot);
        Require(!SkillService.TryMoveTrainingQueueEntryUp(prerequisitePilot, 2, out _), "a dependent skill must not move before its queued prerequisite");
        Require(JsonUtility.ToJson(prerequisitePilot) == prerequisiteBefore, "a rejected prerequisite-breaking move must leave the entire pilot state unchanged");

        var levelSave = SaveService.NewGame();
        var levelPilot = levelSave.Characters[1];
        Require(SkillService.TryEnqueueToTarget(levelPilot, "mining", 3, out _), "level-order smoke must enqueue Mining II-III");
        Require(TrainingQueueSignature(levelPilot) == "mining:2|mining:3", "level-order smoke setup mismatch");
        var levelBefore = JsonUtility.ToJson(levelPilot);
        Require(!SkillService.TryMoveTrainingQueueEntryUp(levelPilot, 1, out _), "Mining III must not move before Mining II");
        Require(JsonUtility.ToJson(levelPilot) == levelBefore, "a rejected level-order move must leave queue and legacy head unchanged");
    }

    static void SmokeSkillPlanCopying()
    {
        var commanderSave = SaveService.NewGame();
        var commander = commanderSave.Characters[0];
        var commanderWorker = commanderSave.Characters[1];
        Require(SkillService.TryEnqueueToTarget(commander, "mining", 2, out _), "commander-copy smoke must create an individual commander queue");
        Require(SkillService.TryEnqueueToTarget(commanderWorker, "mining-frigate", 2, out _), "commander-copy smoke must create a worker source queue");
        var commanderSaveBefore = JsonUtility.ToJson(commanderSave);
        Require(!SkillPlanService.TryCopyQueueToOtherWorkers(commanderSave, commander, false, out _), "pilot zero's individual commander plan must never be copied to workers");
        Require(!SkillPlanService.TryCopyQueue(commanderSave, commanderWorker, commander, false, out _), "pilot zero must never receive a copied worker plan");
        Require(JsonUtility.ToJson(commanderSave) == commanderSaveBefore, "rejected commander plan copies must leave all queues and the wallet unchanged");

        var paidSave = SaveService.NewGame();
        var paidCommander = paidSave.Characters[0];
        var paidSource = paidSave.Characters[1];
        SkillService.GetState(paidSource, "science", true).BookOwned = true;
        Require(SkillService.TryEnqueueToTarget(paidSource, "science", 2, out _), "paid plan source must queue Science I-II");
        Require(SkillService.TryEnqueueToTarget(paidCommander, "mining", 2, out _), "paid plan smoke must preserve the commander's individual queue");
        foreach (var target in paidSave.Characters.Skip(2))
            Require(SkillService.TryEnqueueToTarget(target, "mining-frigate", 2, out _), "paid plan smoke must seed every worker with a queue that will be replaced");

        var sourceQueueSignature = TrainingQueueSignature(paidSource);
        var paidSourceBefore = JsonUtility.ToJson(paidSource);
        var paidCommanderBefore = JsonUtility.ToJson(paidCommander);
        var paidPreview = SkillPlanService.PreviewCopyQueueToOtherWorkers(paidSave, paidSource, true);
        Require(paidPreview.Success && paidPreview.TargetCount == 8, "one worker source must preview exactly the other eight workers");
        Require(paidPreview.BooksPurchased == 8 && paidPreview.BookCostIsk > 0, "paid mass-copy preview must price one missing Science book for each target worker");
        paidSave.Isk = paidPreview.BookCostIsk + 12_345d;
        Require(SkillPlanService.TryCopyQueueToOtherWorkers(paidSave, paidSource, true, out var paidResult), "funded worker plan with automatic book purchases must commit");
        Require(paidResult.TargetCount == 8 && paidResult.TargetsChanged == 8 && paidResult.Targets.Count == 8, "paid worker plan must replace exactly eight target queues");
        Require(paidResult.BooksPurchased == 8 && paidResult.EntriesCopied == 16, "paid worker plan must buy each missing book once and copy both queued levels to every target");
        RequireNearly(paidResult.BookCostIsk, paidPreview.BookCostIsk, .001d, "paid plan must charge the fully preflighted book total");
        RequireNearly(paidSave.Isk, 12_345d, .001d, "paid plan must debit the one shared wallet exactly once");
        Require(JsonUtility.ToJson(paidSource) == paidSourceBefore && paidResult.Targets.All(target => target.PilotId != paidSource.Id), "source worker must be excluded and its queue must remain untouched");
        Require(JsonUtility.ToJson(paidCommander) == paidCommanderBefore, "mass worker-plan copy must not alter the commander's individual queue or books");
        foreach (var target in paidSave.Characters.Skip(2))
        {
            Require(TrainingQueueSignature(target) == sourceQueueSignature, "copied plan must fully replace a target worker's previous queue in source order");
            Require(SkillService.GetState(target, "science")?.BookOwned == true, "paid plan copy must inject the purchased book into each target worker");
        }

        var poorSave = SaveService.NewGame();
        var poorSource = poorSave.Characters[1];
        SkillService.GetState(poorSource, "science", true).BookOwned = true;
        Require(SkillService.TryEnqueueToTarget(poorSource, "science", 1, out _), "insufficient-ISK smoke must create a book-requiring source plan");
        foreach (var target in poorSave.Characters.Skip(2))
            Require(SkillService.TryEnqueueToTarget(target, "mining-frigate", 2, out _), "insufficient-ISK smoke must seed target queues");
        var poorPreview = SkillPlanService.PreviewCopyQueueToOtherWorkers(poorSave, poorSource, true);
        Require(poorPreview.Success && poorPreview.BookCostIsk > 0, "insufficient-ISK smoke must preflight a nonzero book total");
        poorSave.Isk = poorPreview.BookCostIsk - 1d;
        var poorSaveBefore = JsonUtility.ToJson(poorSave);
        Require(!SkillPlanService.TryCopyQueueToOtherWorkers(poorSave, poorSource, true, out var poorResult) && !poorResult.Success && poorResult.TargetsChanged == 0,
            "insufficient shared-wallet ISK must reject the entire multi-pilot plan");
        Require(JsonUtility.ToJson(poorSave) == poorSaveBefore, "insufficient-ISK skill-plan copy must leave the whole logical save byte-for-byte unchanged");

        var subsetSave = SaveService.NewGame();
        var subsetSource = subsetSave.Characters[1];
        var subsetTarget = subsetSave.Characters[2];
        SkillService.GetState(subsetSource, "science", true).BookOwned = true;
        SkillService.GetState(subsetSource, "astrogeology", true).BookOwned = true;
        Require(SkillService.TryEnqueueToTarget(subsetSource, "mining", 4, out _), "subset source must queue Mining II-IV");
        Require(SkillService.TryEnqueueToTarget(subsetSource, "science", 4, out _), "subset source must queue Science I-IV");
        Require(SkillService.TryEnqueueToTarget(subsetSource, "astrogeology", 1, out _), "subset source must queue Astrogeology after its projected prerequisites");
        SkillService.GetState(subsetTarget, "astrogeology", true).BookOwned = true;
        Require(SkillService.TryEnqueueToTarget(subsetTarget, "mining-frigate", 2, out _), "subset smoke must seed a target queue that will be replaced");
        var subsetWalletBefore = subsetSave.Isk;
        Require(SkillPlanService.TryCopyQueue(subsetSave, subsetSource, subsetTarget, false, out var subsetResult), "copy without books must still commit its valid subset");
        Require(subsetResult.TargetsChanged == 1 && subsetResult.EntriesCopied == 3 && subsetResult.EntriesSkipped == 5,
            "copy without books must report three valid Mining levels plus four missing-book and one prerequisite skip");
        Require(subsetResult.BooksPurchased == 0 && subsetResult.BookCostIsk == 0 && subsetResult.Message.Contains("пропущено"), "copy without books must report skips without charging or buying anything");
        RequireNearly(subsetSave.Isk, subsetWalletBefore, .001d, "copy without books must not touch the shared wallet");
        Require(TrainingQueueSignature(subsetTarget) == "mining:2|mining:3|mining:4", "copy without books must replace the old queue with only its prerequisite-valid, owned-book subset");
        Require(SkillService.GetState(subsetTarget, "science") == null, "copy without books must not silently purchase or create a missing Science book");
    }

    static string TrainingQueueSignature(CharacterSave pilot) => string.Join("|",
        SkillService.GetTrainingQueue(pilot).Select(entry => $"{entry.SkillId}:{entry.TargetLevel}"));

    static void SmokeJitaFallbackPrices()
    {
        var save = SaveService.NewGame();
        Require(MarketService.JitaStationId == 60003760 && MarketService.TheForgeRegionId == 10000002, "market must be scoped to Jita 4-4 in The Forge");
        Require(Catalog.Ores.All(ore => MarketService.OreBuyPerUnit(save, ore) > 0), "every ore must retain a nonzero Jita buy fallback");
        Require(Catalog.Ships.Where(hull => hull.FallbackBuyPrice > 0).All(hull => MarketService.HullSellPrice(save, hull) > 0), "every paid hull must retain a nonzero Jita sell fallback");
        Require(Catalog.Modules.All(module => MarketService.ModuleSellPrice(save, module) > 0), "every module must retain a nonzero Jita sell fallback");
        Require(Catalog.Drones.All(drone => MarketService.DroneSellPrice(save, drone) > 0), "every drone must retain a nonzero Jita sell fallback");
        Require(MarketService.GetSellPrice(save, Catalog.HeavyWaterTypeId, Catalog.HeavyWaterFallbackPrice) > 0, "Heavy Water fallback must be nonzero");
        Require(MarketService.InjectorPrice(save) > 0, "Large Skill Injector fallback must be nonzero");
        Require(MarketService.SmallInjectorPrice(save) > 0, "Small Skill Injector fallback must be nonzero");

        var veldspar = Catalog.GetOre("veldspar");
        save.PriceCache.Entries.Add(new MarketPriceEntry { TypeId = veldspar.TypeId, BuyPrice = 123.45d, SellPrice = 0 });
        RequireNearly(MarketService.OreBuyPerUnit(save, veldspar), 123.45d, .0001d, "valid cached Jita quote must override the fallback");
        RequireNearly(MarketService.GetSellPrice(save, veldspar.TypeId, veldspar.FallbackSellPrice), veldspar.FallbackSellPrice, .0001d, "zero quote must never turn an item free");
    }

    static void SmokeWarehouseOreValuation()
    {
        var save = SaveService.NewGame();
        var veldspar = Catalog.GetOre("veldspar");
        var pyroxeres = Catalog.GetOre("pyroxeres");
        const double veldsparUnits = 16_667d;
        const double pyroxeresUnits = 4_321d;
        OperationService.AddItem(save.StationInventory, veldspar.Id, veldsparUnits);
        OperationService.AddItem(save.StationInventory, pyroxeres.Id, pyroxeresUnits);

        var veldsparUnitPrice = MarketService.OreBuyPerUnit(save, veldspar);
        var pyroxeresUnitPrice = MarketService.OreBuyPerUnit(save, pyroxeres);
        Require(veldsparUnitPrice > 0 && pyroxeresUnitPrice > 0, "warehouse ore valuation requires positive Jita unit prices");
        var expectedOreValue = veldsparUnits * veldsparUnitPrice + pyroxeresUnits * pyroxeresUnitPrice;
        RequireNearly(WarehouseOreBuyValue(save), expectedOreValue, .001d,
            "warehouse total must aggregate each ore stack as quantity multiplied by its Jita buy price per unit");

        var oreValueBeforeNonOre = WarehouseOreBuyValue(save);
        OperationService.AddItem(save.StationInventory, "heavy-water", 1_000_000d);
        RequireNearly(WarehouseOreBuyValue(save), oreValueBeforeNonOre, .001d,
            "warehouse ore valuation must exclude fuel and every other non-ore stack");
    }

    static double WarehouseOreBuyValue(GameSave save)
    {
        if (save?.StationInventory == null) return 0d;
        var total = 0d;
        foreach (var stack in save.StationInventory)
        {
            var ore = Catalog.GetOre(stack?.ItemId);
            if (ore == null) Catalog.TryGetCompressedSource(stack?.ItemId, out ore);
            if (ore == null) continue;
            total += stack.Quantity * MarketService.OreBuyPerUnit(save, ore);
        }
        return total;
    }

    static void SmokeIndustrialCores()
    {
        SmokeIndustrialCore("porpoise", "medium-industrial-core-ii", 75f);
        SmokeIndustrialCore("orca", "industrial-core-ii", 150f);
        SmokeIndustrialCore("rorqual", "capital-industrial-core-ii", 300f);
    }

    static void SmokeIndustrialCore(string hullId, string coreId, float expectedCycle)
    {
        var save = SaveService.NewGame();
        foreach (var candidate in save.Characters) candidate.DeployOnLaunch = false;
        var pilot = save.Characters[9];
        GrantAllSkills(pilot);
        var ship = SaveService.CreateShip($"smoke-{hullId}", hullId);
        var package = Catalog.Packages.Single(candidate => candidate.HullId == hullId && candidate.Role == PreparedPackageRole.Booster && candidate.Grade == PreparedPackageGrade.T2);
        PreparedPackageService.ApplyLockedFit(ship, package);
        Require(ship.Modules.Any(module => module.ModuleId == coreId), $"{package.Id} must materialize its locked {coreId}");
        save.Ships.Add(ship);
        pilot.AssignedShipUid = ship.Uid;
        pilot.DeployOnLaunch = true;
        OperationService.Start(save, "uitra-belt-1");
        Require(save.Operation.Fleet.Count == 1 && ship.Location == ShipLocation.Belt, $"{hullId} must deploy for its core test");
        save.Operation.RaidTimerSeconds = 99_999f;

        var hull = Catalog.GetShip(hullId);
        var core = Catalog.GetModule(coreId);
        RequireNearly(OperationService.CoreFuelPerCycle(pilot, hull, core), 0, .001d, $"{coreId} must require no Heavy Water");
        RequireNearly(OperationService.CoreCycleSeconds(hull, core), expectedCycle, .001d, $"{coreId} cycle duration mismatch");
        var fuelBefore = OperationService.ItemQuantity(ship.FuelHold, "heavy-water");
        Require(!ship.Modules.Single(module => module.ModuleId == coreId).Active, $"newly materialized {coreId} must start inactive");
        Require(OperationService.ToggleCore(save, out _), $"{coreId} must activate with an empty fuel hold");
        Require(save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreShipUid == ship.Uid, $"{coreId} active state must identify its ship");
        RequireNearly(OperationService.ItemQuantity(ship.FuelHold, "heavy-water"), fuelBefore, .001d, $"{coreId} must not consume Heavy Water at cycle start");

        OperationService.Tick(save, expectedCycle + .01f);
        Require(save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreSecondsLeft > 0 && save.Operation.IndustrialCoreSecondsLeft <= expectedCycle, $"{coreId} must automatically begin another free cycle");
        RequireNearly(OperationService.ItemQuantity(ship.FuelHold, "heavy-water"), fuelBefore, .001d, $"repeating {coreId} cycles must remain fuel-free");

        var member = save.Operation.Fleet[0];
        var originalLocation = save.Operation.LocationId;
        OperationService.Start(save, "sobaseki-belt-1");
        Require(save.Operation.LocationId == originalLocation && save.Operation.IndustrialCoreActive, $"{coreId} must block an operation/location change");
        var returnResult = OperationService.ReturnFleetWithResult(save, false);
        Require(returnResult.Success && returnResult.RequestedCount == 1 && returnResult.QueuedCount == 1 && returnResult.Messages.Count == 1, $"{hullId} fleet return must expose its queued per-ship result/message");
        Require(ship.Location == ShipLocation.Belt && member.Order == FleetOrder.Idle && member.CoreReturnQueued && save.Operation.IndustrialCoreStopRequested, $"{hullId} must queue dock until its core cycle boundary");
        Require(!OperationService.TryStop(save, out _), $"fleet must not dock while {coreId} is active");

        var remainingCycle = save.Operation.IndustrialCoreSecondsLeft;
        OperationService.Tick(save, remainingCycle - .01f);
        Require(save.Operation.IndustrialCoreActive && ship.Location == ShipLocation.Belt, $"{coreId} must remain locked before the cycle boundary");
        OperationService.Tick(save, .02f);
        Require(!save.Operation.IndustrialCoreActive && string.IsNullOrWhiteSpace(save.Operation.IndustrialCoreShipUid), $"{coreId} must unlock at the queued-return cycle boundary");
        Require(ship.Location == ShipLocation.Transit && member.Order == FleetOrder.DockAndStay && !member.CoreReturnQueued, $"{hullId} must begin its queued dock at the boundary");
        RequireNearly(OperationService.ItemQuantity(ship.FuelHold, "heavy-water"), fuelBefore, .001d, $"queued stop must not consume Heavy Water");
        Require(member.WarpPhase == FleetWarpPhase.AligningOut, $"{hullId} queued dock must begin its outbound alignment");
        OperationService.Tick(save, member.WarpPhaseSecondsLeft + .01f);
        Require(ship.Location == ShipLocation.Station, $"{hullId} queued dock must complete after its five-second outbound phase");
    }

    static void SmokeFleetCompression()
    {
        foreach (var package in Catalog.Packages)
            Require((package.CompressorModuleIds ?? Array.Empty<string>()).All(id => Catalog.GetModule(id)?.Kind == ModuleKind.Compressor),
                $"package {package.Id} must reference only real compressor modules");

        RequireCompressionPackage(
            "porpoise", "medium-industrial-core-ii",
            new[] { "medium-asteroid-ore-compressor-i", "medium-gas-compressor-i" },
            new[] { 62622, 62624 });
        RequireCompressionPackage(
            "orca", "industrial-core-ii",
            new[] { "large-asteroid-ore-compressor-i", "large-ice-compressor-i", "large-gas-compressor-i", "large-mercoxit-compressor-i" },
            new[] { 62625, 62628, 62626, 62630 });
        RequireCompressionPackage(
            "rorqual", "capital-industrial-core-ii",
            new[] { "capital-asteroid-ore-compressor-i", "capital-ice-compressor-i", "capital-gas-compressor-i", "capital-mercoxit-compressor-i" },
            new[] { 62632, 62633, 62634, 62635 });

        var outrider = Catalog.GetShip("outrider");
        Require(outrider != null && !outrider.SupportsIndustrialCore, "Outrider must not expose Industrial Core support");
        foreach (var grade in new[] { "t1", "t2" })
        {
            var outriderPackage = Catalog.GetPackage($"outrider-booster-{grade}");
            Require(outriderPackage != null && string.IsNullOrWhiteSpace(outriderPackage.IndustrialCoreId), $"Outrider Booster {grade} must not invent an Industrial Core");
            Require((outriderPackage.CompressorModuleIds ?? Array.Empty<string>()).Length == 0 && PreparedPackageService.CompressionSummary(outriderPackage) == "Сжатие: нет",
                $"Outrider Booster {grade} must explicitly expose no compression capability");
        }

        var save = NewCompressionOperation(out var rorqual, out var recipient);
        OperationService.AddItem(rorqual.MiningHold, "veldspar", 250);
        OperationService.AddItem(recipient.MiningHold, "veldspar", 1000);
        OperationService.AddItem(recipient.MiningHold, "mercoxit", 10);
        OperationService.AddItem(recipient.MiningHold, "clear-icicle", 3);
        OperationService.AddItem(recipient.MiningHold, "fullerite-c50", 100);

        var beforeCore = JsonUtility.ToJson(save);
        Require(!OperationService.TryCompressFleetMiningHolds(save, rorqual.Uid, out var inactiveResult, out _),
            "compression must reject a fitted but inactive Industrial Core");
        Require(inactiveResult.UnitsCompressed == 0 && JsonUtility.ToJson(save) == beforeCore,
            "inactive-core rejection must leave every mining hold unchanged");

        Require(OperationService.ToggleCore(save, rorqual.Uid, out _), "Rorqual compression fixture must activate its own Industrial Core");
        var activeCoreShipUid = save.Operation.IndustrialCoreShipUid;
        save.Operation.IndustrialCoreShipUid = recipient.Uid;
        var wrongCoreSource = JsonUtility.ToJson(save);
        Require(!OperationService.TryCompressFleetMiningHolds(save, rorqual.Uid, out var wrongSourceResult, out _),
            "compression must reject an active Industrial Core attributed to another source ship");
        Require(wrongSourceResult.UnitsCompressed == 0 && JsonUtility.ToJson(save) == wrongCoreSource,
            "wrong-source core rejection must be atomic");
        save.Operation.IndustrialCoreShipUid = activeCoreShipUid;

        RequireCompressionVolume("veldspar", 100d);
        RequireCompressionVolume("mercoxit", 100d);
        RequireCompressionVolume("clear-icicle", 10d);
        RequireCompressionVolume("fullerite-c50", 10d);

        Require(OperationService.TryCompressFleetMiningHolds(save, rorqual.Uid, out var compressed, out _),
            "active same-source Rorqual core must compress all supported fleet mining holds");
        Require(compressed.ShipsAffected == 2 && compressed.StacksCompressed == 5 && compressed.UnitsCompressed == 1363d,
            "fleet compression result must count two ships, five raw stacks and every raw unit exactly once");
        RequireNearly(compressed.RawVolumeM3, 3625d, .0001d, "fleet compression raw-volume accounting mismatch");
        RequireNearly(compressed.CompressedVolumeM3, 315.25d, .0001d, "fleet compression compressed-volume accounting mismatch");
        RequireNearly(compressed.SavedVolumeM3, 3309.75d, .0001d, "fleet compression saved-volume accounting mismatch");
        RequireCompressedQuantity(rorqual, "veldspar", 250d);
        RequireCompressedQuantity(recipient, "veldspar", 1000d);
        RequireCompressedQuantity(recipient, "mercoxit", 10d);
        RequireCompressedQuantity(recipient, "clear-icicle", 3d);
        RequireCompressedQuantity(recipient, "fullerite-c50", 100d);

        var afterCompression = JsonUtility.ToJson(save);
        Require(!OperationService.TryCompressFleetMiningHolds(save, rorqual.Uid, out var repeated, out _),
            "a second compression pass with no raw supported resources must report no work");
        Require(repeated.UnitsCompressed == 0 && JsonUtility.ToJson(save) == afterCompression,
            "fleet compression must be idempotent and byte-for-byte stable after the first pass");

        var rawWarehouse = SaveService.NewGame();
        var compressedWarehouse = SaveService.NewGame();
        foreach (var resource in new[] { "veldspar", "mercoxit", "clear-icicle", "fullerite-c50" })
        {
            OperationService.AddItem(rawWarehouse.StationInventory, resource, 123d);
            OperationService.AddItem(compressedWarehouse.StationInventory, Catalog.CompressedItemId(resource), 123d);
        }
        RequireNearly(WarehouseOreBuyValue(compressedWarehouse), WarehouseOreBuyValue(rawWarehouse), .001d,
            "1:1 compression must preserve warehouse Jita value across raw and compressed identities");

        var restored = JsonUtility.FromJson<GameSave>(afterCompression);
        var restoredRorqual = restored.Ships.Find(ship => ship.Uid == rorqual.Uid);
        var restoredRecipient = restored.Ships.Find(ship => ship.Uid == recipient.Uid);
        Require(restored.Operation.IndustrialCoreActive && restored.Operation.IndustrialCoreShipUid == rorqual.Uid,
            "JSON round-trip must preserve the active same-source compression core");
        RequireCompressedQuantity(restoredRorqual, "veldspar", 250d);
        RequireCompressedQuantity(restoredRecipient, "veldspar", 1000d);
        RequireCompressedQuantity(restoredRecipient, "mercoxit", 10d);
        RequireCompressedQuantity(restoredRecipient, "clear-icicle", 3d);
        RequireCompressedQuantity(restoredRecipient, "fullerite-c50", 100d);
    }

    static void RequireCompressionPackage(string hullId, string coreId, string[] compressorIds, int[] typeIds)
    {
        var package = Catalog.GetPackage($"{hullId}-booster-t2");
        var t1Package = Catalog.GetPackage($"{hullId}-booster-t1");
        var t1CoreId = coreId.EndsWith("-ii", StringComparison.Ordinal) ? coreId.Substring(0, coreId.Length - 3) + "-i" : string.Empty;
        Require(package != null && package.Role == PreparedPackageRole.Booster && package.IndustrialCoreId == coreId,
            $"{hullId} T2 Booster must use its canonical Industrial Core");
        Require(t1Package != null && t1Package.Role == PreparedPackageRole.Booster && t1Package.IndustrialCoreId == t1CoreId,
            $"{hullId} T1 Booster must use its canonical Industrial Core");
        Require((package.CompressorModuleIds ?? Array.Empty<string>()).SequenceEqual(compressorIds),
            $"{hullId} T2 Booster compression capability matrix mismatch");
        Require((t1Package.CompressorModuleIds ?? Array.Empty<string>()).SequenceEqual(compressorIds),
            $"{hullId} T1 Booster compression capability matrix mismatch");
        Require(compressorIds.Length == typeIds.Length, $"{hullId} compressor smoke identity fixture mismatch");
        for (var index = 0; index < compressorIds.Length; index++)
        {
            var module = Catalog.GetModule(compressorIds[index]);
            Require(module?.Kind == ModuleKind.Compressor && module.TypeId == typeIds[index],
                $"{hullId} compressor {compressorIds[index]} identity/typeID mismatch");
        }
    }

    static GameSave NewCompressionOperation(out ShipSave compressor, out ShipSave recipient)
    {
        var save = SaveService.NewGame();
        foreach (var pilot in save.Characters) pilot.DeployOnLaunch = false;
        var compressorPilot = save.Characters[9];
        GrantAllSkills(compressorPilot);
        compressor = SaveService.CreateShip("smoke-compression-rorqual", "rorqual");
        PreparedPackageService.ApplyLockedFit(compressor, Catalog.GetPackage("rorqual-booster-t2"));
        save.Ships.Add(compressor);
        compressorPilot.AssignedShipUid = compressor.Uid;
        compressorPilot.DeployOnLaunch = true;
        var recipientPilot = save.Characters[0];
        recipientPilot.DeployOnLaunch = true;
        recipient = save.Ships.Find(ship => ship.Uid == recipientPilot.AssignedShipUid);
        OperationService.Start(save, "uitra-belt-1");
        save.Operation.RaidTimerSeconds = 99_999f;
        Require(save.Operation.Fleet.Count == 2 && compressor.Location == ShipLocation.Belt && recipient?.Location == ShipLocation.Belt,
            "fleet compression fixture must deploy one Rorqual source and one recipient to the same belt");
        return save;
    }

    static void RequireCompressionVolume(string rawId, double divisor)
    {
        var raw = Catalog.GetOre(rawId);
        var compressedId = Catalog.CompressedItemId(rawId);
        Require(raw != null && !string.IsNullOrWhiteSpace(compressedId) && Catalog.TryGetCompressedSource(compressedId, out var source) && source.Id == raw.Id,
            $"{rawId} must have a reversible compressed identity");
        Require(Catalog.TryGetItemVolumeM3(compressedId, out var compressedUnitVolume), $"{compressedId} must expose a unit volume");
        RequireNearly(raw.UnitVolumeM3 / compressedUnitVolume, divisor, .0001d, $"{rawId} compression volume divisor mismatch");
    }

    static void RequireCompressedQuantity(ShipSave ship, string rawId, double expectedQuantity)
    {
        Require(ship != null, $"compressed {rawId} carrier must survive the operation/JSON round-trip");
        RequireNearly(OperationService.ItemQuantity(ship.MiningHold, rawId), 0d, .0001d, $"raw {rawId} must be fully replaced");
        RequireNearly(OperationService.ItemQuantity(ship.MiningHold, Catalog.CompressedItemId(rawId)), expectedQuantity, .0001d,
            $"compressed {rawId} must retain raw-unit quantity 1:1");
    }

    static void SmokeNpcDurabilityAndDestruction()
    {
        var save = NewSinglePilotOperation("y-zxio-belt-1");
        var location = Catalog.GetLocation("y-zxio-belt-1");
        save.Operation.RaidTimerSeconds = 42.5f;
        Require(OperationService.TryGetNextNpcRaidEta(save, out var initialEta), "hostile belts must expose their serialized next-NPC countdown");
        RequireNearly(initialEta, 42.5d, .001d, "NPC countdown must read the exact saved raid timer");
        OperationService.Tick(save, 1f);
        Require(OperationService.TryGetNextNpcRaidEta(save, out var tickedEta), "NPC countdown must remain available before the raid arrives");
        RequireNearly(tickedEta, 41.5d, .001d, "NPC countdown must advance in simulation seconds");

        save.Operation.RaidTimerSeconds = 0;
        OperationService.Tick(save, .01f);
        Require(save.Operation.Enemies.Count == location.MaxNpcCount, "maximum-threat nullsec belt must spawn its configured NPC wave");
        Require(!OperationService.TryGetNextNpcRaidEta(save, out _), "NPC countdown must be hidden while the belt is already at its NPC cap");

        var safeSave = NewSinglePilotOperation("uitra-belt-1");
        Require(!OperationService.TryGetNextNpcRaidEta(safeSave, out _), "threat-zero belts must report that no NPC raid is expected");

        save.Operation.Enemies.Clear();
        save.Operation.RaidTimerSeconds = 99999;
        var member = save.Operation.Fleet[0];
        var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
        var pilot = save.Characters.Find(candidate => candidate.Id == member.PilotId);
        GrantAllSkills(pilot);
        ship.CombatDroneId = "hobgoblin-i";
        ship.CombatDroneCount = 5;
        var bandwidthTarget = new EnemySave { Id = "bandwidth-smoke", Name = "Bandwidth target", X = member.X, Y = member.Y, Z = member.Z, ShieldHp = 1_000_000, ArmorHp = 1_000_000, StructureHp = 1_000_000, Dps = 0, TargetShipUid = ship.Uid };
        save.Operation.Enemies.Add(bandwidthTarget);
        OperationService.Tick(save, 1f);
        RequireNearly(bandwidthTarget.ShieldHp, 1_000_000d - 24d, .001d, "combat drones must be clamped by Venture bandwidth as well as Drones V");
        save.Operation.Enemies.Clear();
        ship.CombatDroneCount = 0;
        var shieldBefore = ship.ShieldHp;
        var armorBefore = ship.ArmorHp;
        var structureBefore = ship.StructureHp;
        var enemy = new EnemySave
        {
            Id = "smoke-npc",
            Name = "Smoke NPC",
            X = member.X,
            Y = member.Y,
            Z = member.Z,
            ShieldHp = 1_000_000,
            ArmorHp = 1_000_000,
            StructureHp = 1_000_000,
            Dps = 10,
            SpeedKmPerSecond = 1,
            TargetShipUid = ship.Uid
        };
        save.Operation.Enemies.Add(enemy);
        OperationService.Tick(save, 1f);
        var resistedDamage = 10f * PreparedPackageService.IncomingShieldDamageMultiplier(ship, pilot);
        RequireNearly(ship.ShieldHp, shieldBefore - resistedDamage, .001d, "NPC shield damage must use the locked defensive preset resistance");
        RequireNearly(ship.ArmorHp, armorBefore, .001d, "armor must remain intact while shields absorb NPC damage");
        RequireNearly(ship.StructureHp, structureBefore, .001d, "structure must remain intact while shields absorb NPC damage");

        enemy.Dps = 1_000_000;
        OperationService.Tick(save, 1f);
        Require(!save.Ships.Exists(candidate => candidate.Uid == ship.Uid), "lethal NPC damage must remove the destroyed ship");
        Require(string.IsNullOrWhiteSpace(pilot.AssignedShipUid), "destroyed ship's pilot clone must return without an assigned hull");
        Require(!save.Operation.Fleet.Exists(candidate => candidate.ShipUid == ship.Uid), "destroyed ship must leave the active fleet");
    }

    static void SmokeRealLocations()
    {
        Require(Catalog.Locations.Count > 10, "location progression must expose the expanded authored belt catalog and dynamic anomalies");
        RequireRealLocation("uitra-belt-1", "Uitra", 30030141, 40342603);
        RequireRealLocation("kakakela-belt-1", "Kakakela", 30001405, 40089401);
        Require(Catalog.Locations.Count(location => location.SystemId == 30001405 && location.SiteKind == MiningSiteKind.StaticBelt) == 10,
            "Kakakela must expose all ten official static asteroid belts");
        Require(Catalog.Locations.Where(location => location.SystemId == 30001405).All(location => location.Threat <= 0 && location.MaxNpcCount == 0),
            "Kakakela must remain the dedicated NPC-free high-security belt system");
        RequireRealLocation("sobaseki-belt-1", "Sobaseki", 30001363, 40086852);
        RequireRealLocation("saisio-belt-1", "Saisio", 30000146, 40009272);
        Require(Catalog.Locations.Count(location => location.SystemId == 30000146 && location.SiteKind == MiningSiteKind.StaticBelt) == 9,
            "Saisio must expose all nine official static asteroid belts");
        Require(Catalog.GetLocation("saisio-belt-1").Band == SecurityBand.HighSec,
            "Saisio 0.7 must remain a high-security system");
        RequireRealLocation("korsiki-belt-1", "Korsiki", 30000181, 40011383);
        RequireRealLocation("otomainen-belt-1", "Otomainen", 30000172, 40010885);
        RequireRealLocation("otsela-belt-1", "Otsela", 30000194, 40012332);
        RequireRealLocation("hakonen-belt-1", "Hakonen", 30001448, 40092207);
        RequireRealLocation("p3en-e-belt-1", "P3EN-E", 30000250, 40015864);
        RequireRealLocation("y-zxio-belt-1", "Y-ZXIO", 30000306, 40019239);
        RequireRealLocation("wspace-c1-barren-reservoir", "W-space C1", 0, 0);
        var ice = Catalog.GetLocation("manatirid-clear-icicle");
        Require(ice != null && ice.SystemName == "Manatirid" && ice.SystemId == 30005230 && ice.BeltId == 0 && ice.IsAnomaly,
            "Manatirid must be a dynamic anomaly without a fabricated celestial ID");
        Require(ice.ResourceLayoutApproximate && ice.OreIds.SequenceEqual(new[] { "clear-icicle" }), "ice anomaly must flag its generated chunks as approximate");
        RequireNearly(ice.Security, .5485263f, .000001d, "Manatirid raw security mismatch");
        Require(ice.Band == SecurityBand.HighSec && ice.MaxNpcCount == 3 && Math.Abs(ice.Threat - .48f) < .001f, "Manatirid NPC tier must match 0.5 highsec");

        Require(Catalog.Locations.Select(location => location.Id).Distinct(StringComparer.Ordinal).Count() == Catalog.Locations.Count, "location IDs must be unique across the complete dynamic catalog");
        Require(Catalog.Locations.Where(location => location.SiteKind == MiningSiteKind.StaticBelt).All(location => location.SystemId > 0 && location.BeltId > 0), "every static belt must retain a real system and celestial identity");
        Require(Catalog.Locations.Where(location => location.SiteKind == MiningSiteKind.DynamicAnomaly).All(location => location.IsAnomaly && location.BeltId == 0), "dynamic anomalies must remain distinct from fabricated static celestial identities");
        Require(Catalog.Locations.All(location => location.OreIds.Length > 0 && location.OreIds.All(oreId => Catalog.GetOre(oreId) != null)), "every real belt must reference known ore");
        Require(Catalog.Locations.Any(location => location.Band == SecurityBand.HighSec) &&
                Catalog.Locations.Any(location => location.Band == SecurityBand.LowSec) &&
                Catalog.Locations.Any(location => location.Band == SecurityBand.NullSec),
            "location progression must cover highsec, lowsec, and nullsec");
        Require(Catalog.Ships.Count >= 17 && Catalog.Ores.Count >= 16, "mining hull and ore progression catalogs are incomplete");
        RequireNearly(Catalog.GetShip("venture").MiningHoldM3, 5000f, .01d, "Venture mining hold mismatch");
        RequireNearly(Catalog.GetShip("hulk").MiningHoldM3, 11500f, .01d, "Hulk mining hold mismatch");
        RequireNearly(Catalog.GetShip("rorqual").MiningHoldM3, 300000f, .01d, "Rorqual mining hold mismatch");

        var managed = Catalog.GetLocation("uitra-managed-mining-site");
        Require(managed != null && managed.SiteKind == MiningSiteKind.DynamicAnomaly && managed.AllowedHullIds.SequenceEqual(new[] { "venture" }) &&
                managed.AuthoredAsteroidCountMin == 10 && managed.AuthoredAsteroidCountMax == 12,
            "the Uitra managed site must expose its authored Venture-only 10–12-rock contract");
        var managedSave = SaveService.NewGame();
        managedSave.MiningSites.Clear();
        MiningSiteService.Tick(managedSave, new DateTimeOffset(2099, 8, 13, 10, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        var managedState = managedSave.MiningSites.Single(site => site.LocationId == managed.Id);
        Require(managedState.Lifecycle == MiningSiteLifecycle.Available && managedState.Asteroids.Count is >= 10 and <= 12 &&
                managedState.Asteroids.All(asteroid => asteroid.OreId == "veldspar"),
            "the managed site generator must create only 10–12 Veldspar deposits");
        var venture = managedSave.Ships[0];
        Require(OperationService.LocationSupportsShip(managed, venture, Catalog.GetShip(venture.HullId)),
            "the canonical starter Venture must pass the managed-site hull gate");
        var retriever = SaveService.CreateShip("managed-gate-retriever", "retriever");
        PreparedPackageService.ApplyLockedFit(retriever, Catalog.GetPackage("retriever-ore-t1"));
        Require(OperationService.CanMineResource(retriever, "veldspar"),
            "managed-site barge rejection fixture must otherwise be able to mine Veldspar");
        var porpoise = SaveService.CreateShip("managed-gate-porpoise", "porpoise");
        Require(!OperationService.LocationSupportsShip(managed, retriever, Catalog.GetShip(retriever.HullId)) &&
                !OperationService.LocationSupportsShip(managed, porpoise, Catalog.GetShip(porpoise.HullId)),
            "barges and command ships must not bypass the Venture-only managed-site gate");

        var highSecBelts = Catalog.Locations
            .Where(location => location.SiteKind == MiningSiteKind.StaticBelt && location.Band == SecurityBand.HighSec)
            .ToArray();
        Require(highSecBelts.Length == 677,
            "the expanded high-security catalog must expose exactly 677 real static belts");
        Require(highSecBelts.Select(location => location.SystemId).Distinct().Count() == 73,
            "the expanded high-security catalog must expose exactly 73 mineable systems");
        Require(highSecBelts.Select(location => location.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == highSecBelts.Length &&
                highSecBelts.Select(location => location.BeltId).Distinct().Count() == highSecBelts.Length &&
                highSecBelts.GroupBy(location => location.SystemId).All(group =>
                    group.Select(location => location.SystemName).Distinct(StringComparer.Ordinal).Count() == 1),
            "high-security location IDs, celestial belt IDs and system identity mappings must be unique");

        const long lazyWorldNow = 4_089_765_600;
        var lazyWorld = SaveService.NewGame();
        MiningSiteService.Tick(lazyWorld, lazyWorldNow);
        var dynamicLocationCount = Catalog.Locations.Count(location => location.SiteKind == MiningSiteKind.DynamicAnomaly);
        Require(lazyWorld.MiningSites.Count == dynamicLocationCount && lazyWorld.MiningSites.All(state =>
                    Catalog.GetLocation(state.LocationId)?.SiteKind == MiningSiteKind.DynamicAnomaly),
            "a pristine v13 world must eagerly persist dynamic anomalies only, never all 677 static belts");
        Require(MiningSiteService.IsAvailable(lazyWorld, "uitra-belt-1", lazyWorldNow) &&
                lazyWorld.MiningSites.Count == dynamicLocationCount,
            "checking an untouched static belt must use its implicit pristine state without materializing a save entry");
        MiningSiteService.LoadIntoOperation(lazyWorld, Catalog.GetLocation("uitra-belt-1"), lazyWorldNow);
        Require(lazyWorld.MiningSites.Count == dynamicLocationCount + 1 &&
                lazyWorld.MiningSites.Count(state => state.LocationId == "uitra-belt-1") == 1,
            "opening one static belt must materialize exactly that belt and no catalog-wide world snapshot");

        var legacyEagerWorld = SaveService.NewGame();
        MiningSiteService.Tick(legacyEagerWorld, lazyWorldNow);
        MiningSiteService.LoadIntoOperation(legacyEagerWorld, Catalog.GetLocation("uitra-belt-2"), lazyWorldNow);
        MiningSiteService.LoadIntoOperation(legacyEagerWorld, Catalog.GetLocation("uitra-belt-3"), lazyWorldNow);
        var partialLegacyState = legacyEagerWorld.MiningSites.Single(state => state.LocationId == "uitra-belt-3");
        partialLegacyState.Asteroids[0].RemainingUnits -= 1;
        var partialLegacySignature = AsteroidStateSignature(partialLegacyState.Asteroids);
        legacyEagerWorld.Operation.LocationId = string.Empty;
        legacyEagerWorld.Version = 12;
        SaveService.MigrateToCurrentVersion(legacyEagerWorld);
        var retainedPartial = legacyEagerWorld.MiningSites.Single(state => state.LocationId == "uitra-belt-3");
        Require(legacyEagerWorld.Version == 13 &&
                legacyEagerWorld.MiningSites.All(state => state.LocationId != "uitra-belt-2") &&
                AsteroidStateSignature(retainedPartial.Asteroids) == partialLegacySignature,
            "v13 migration must compact a pristine legacy static entry while preserving exact partial depletion");
    }

    static void SmokeOreGrades()
    {
        var familyTypeIds = new Dictionary<string, int[]>
        {
            ["veldspar"] = new[] { 1230, 17470, 17471, 46689 },
            ["scordite"] = new[] { 1228, 17463, 17464, 46687 },
            ["pyroxeres"] = new[] { 1224, 17459, 17460, 46686 },
            ["plagioclase"] = new[] { 18, 17455, 17456, 46685 },
            ["omber"] = new[] { 1227, 17867, 17868, 46684 },
            ["kernite"] = new[] { 20, 17452, 17453, 46683 },
            ["jaspet"] = new[] { 1226, 17448, 17449, 46682 },
            ["hemorphite"] = new[] { 1231, 17444, 17445, 46681 },
            ["hedbergite"] = new[] { 21, 17440, 17441, 46680 },
            ["gneiss"] = new[] { 1229, 17865, 17866, 46679 },
            ["dark-ochre"] = new[] { 1232, 17436, 17437, 46675 },
            ["crokite"] = new[] { 1225, 17432, 17433, 46677 },
            ["bistot"] = new[] { 1223, 17428, 17429, 46676 },
            ["arkonor"] = new[] { 22, 17425, 17426, 46678 },
            ["spodumain"] = new[] { 19, 17466, 17467, 46688 }
        };
        var gradeMultipliers = new[] { 1d, 1.05d, 1.10d, 1.15d };
        foreach (var family in familyTypeIds)
        {
            var baseOre = Catalog.GetOreVariant(family.Key, 1);
            Require(baseOre != null && baseOre.Id == family.Key, $"{family.Key} Grade I must retain its canonical save identity");
            for (var grade = 1; grade <= 4; grade++)
            {
                var ore = Catalog.GetOreVariant(family.Key, grade);
                Require(ore != null && ore.TypeId == family.Value[grade - 1], $"{family.Key} Grade {grade} official TypeID mismatch");
                Require(ore.Grade == grade && ore.FamilyId == family.Key && ore.BaseOreId == family.Key,
                    $"{family.Key} Grade {grade} family metadata mismatch");
                RequireNearly(ore.ValueMultiplier, gradeMultipliers[grade - 1], .000001d, $"{family.Key} Grade {grade} value multiplier mismatch");
                RequireNearly(ore.UnitVolumeM3, baseOre.UnitVolumeM3, .000001d, $"{family.Key} grade variants must keep one unit volume");
                Require(Catalog.GetOreFamilyId(ore) == family.Key && Catalog.GetOreGrade(ore.Id) == grade && Catalog.SameOreFamily(baseOre.Id, ore.Id),
                    $"{family.Key} Grade {grade} family lookup mismatch");
            }
        }

        var mercoxitTypeIds = new[] { 11396, 17869, 17870 };
        var mercoxitBase = Catalog.GetOre("mercoxit");
        Require(mercoxitBase != null, "Mercoxit Grade I catalog entry is missing");
        for (var grade = 1; grade <= 3; grade++)
        {
            var ore = Catalog.GetOreVariant("mercoxit", grade);
            Require(ore != null && ore.TypeId == mercoxitTypeIds[grade - 1] && ore.Grade == grade && Catalog.IsMercoxitFamily(ore),
                $"Mercoxit Grade {grade} official identity/family mismatch");
            RequireNearly(ore.UnitVolumeM3, mercoxitBase.UnitVolumeM3, .000001d, "all Mercoxit grades must retain 40 m3 per unit");
            RequireNearly(ore.ValueMultiplier, gradeMultipliers[grade - 1], .000001d, $"Mercoxit Grade {grade} value multiplier mismatch");
        }
        Require(Catalog.GetOreVariant("mercoxit", 4) == null && Catalog.Ores.All(ore => !(Catalog.IsMercoxitFamily(ore) && ore.Grade > 3)),
            "Mercoxit must expose I/II/III but no IV grade");

        var rookieVariants = new Dictionary<string, int>
        {
            ["veldspar-0-grade"] = 92371,
            ["scordite-0-grade"] = 92373,
            ["pyroxeres-0-grade"] = 95395
        };
        foreach (var expected in rookieVariants)
        {
            var ore = Catalog.GetOre(expected.Key);
            var baseOre = Catalog.GetOre(ore?.FamilyId);
            Require(ore != null && ore.TypeId == expected.Value && ore.Grade == 0 && baseOre != null,
                $"reserved rookie ore {expected.Key} identity mismatch");
            RequireNearly(ore.ValueMultiplier, .5d, .000001d, $"reserved rookie ore {expected.Key} multiplier mismatch");
            RequireNearly(ore.UnitVolumeM3, baseOre.UnitVolumeM3, .000001d, $"reserved rookie ore {expected.Key} unit volume mismatch");
        }
        Require(Catalog.Ores.Select(ore => ore.TypeId).Distinct().Count() == Catalog.Ores.Count,
            "ore grades and reserved rookie variants must retain unique official TypeIDs");
        Require(Catalog.Locations.All(location => (location.OreIds ?? Array.Empty<string>()).All(id => Catalog.GetOreGrade(id) != 0)),
            "no authored current location may directly request a reserved Grade 0 ore");

        var ordinaryMiner = Catalog.GetModule("miner-i");
        var ordinaryCrystal = Catalog.GetCrystal("simple-a-i");
        Require(ordinaryMiner != null && ordinaryCrystal != null, "ordinary ore-grade compatibility fixture is incomplete");
        var crystalFitting = new FittedModuleSave { ModuleId = "modulated-strip-miner-ii", ChargeId = ordinaryCrystal.Id };
        foreach (var veldspar in Catalog.Ores.Where(ore => Catalog.SameOreFamily(ore.Id, "veldspar")))
        {
            Require(OperationService.CanMineResource(ordinaryMiner, veldspar), $"ordinary miners must mine {veldspar.Id}");
            Require(ordinaryCrystal.SupportsOre(veldspar.Id) && OperationService.CanMineResource(crystalFitting, veldspar),
                $"one Veldspar-family crystal must work on {veldspar.Id}");
        }

        var miningDroneOnly = new ShipSave { Uid = "grade-drone-only", HullId = "venture", MiningDroneId = "mining-drone-i", MiningDroneCount = 2 };
        var deepCore = Catalog.GetModule("modulated-deep-core-miner-ii");
        var deepCrystal = Catalog.GetCrystal("mercoxit-crystal-a-i");
        Require(deepCore != null && deepCrystal != null, "Mercoxit grade compatibility fixture is incomplete");
        var deepCoreFitting = new FittedModuleSave { ModuleId = deepCore.Id, ChargeId = deepCrystal.Id };
        foreach (var mercoxit in Catalog.Ores.Where(ore => Catalog.IsMercoxitFamily(ore)))
        {
            Require(!OperationService.CanMineResource(miningDroneOnly, mercoxit.Id), $"mining drones must reject {mercoxit.Id}");
            Require(OperationService.CanMineResource(deepCore, mercoxit) && deepCrystal.SupportsOre(mercoxit.Id) && OperationService.CanMineResource(deepCoreFitting, mercoxit),
                $"deep-core module/crystal must accept {mercoxit.Id}");
        }

        const long deterministicNow = 4_089_765_600;
        var generatedA = SaveService.NewGame();
        var generatedB = SaveService.NewGame();
        generatedA.MiningSites.Clear();
        generatedB.MiningSites.Clear();
        MiningSiteService.Tick(generatedA, deterministicNow);
        MiningSiteService.Tick(generatedB, deterministicNow);
        string WorldSignature(GameSave save) => string.Join("||", save.MiningSites.OrderBy(site => site.LocationId, StringComparer.Ordinal)
            .Select(site => $"{site.LocationId}:{site.Lifecycle}:{site.InstanceSerial}:{AsteroidStateSignature(site.Asteroids)}"));
        Require(WorldSignature(generatedA) == WorldSignature(generatedB),
            "the same world time/location/instance state must generate byte-stable ore grades and asteroid layouts");
        var generatedOre = generatedA.MiningSites.SelectMany(site => site.Asteroids ?? new List<AsteroidSave>())
            .Select(asteroid => Catalog.GetOre(asteroid.OreId)).Where(ore => ore?.Kind == ResourceKind.Ore).ToArray();
        Require(generatedOre.Length > 0 && generatedOre.All(ore => ore.Grade >= 1) && generatedOre.Any(ore => ore.Grade > 1),
            "current deterministic world generation must never emit Grade 0 and must include higher-grade ore");
        var managed = generatedA.MiningSites.Single(site => site.LocationId == "uitra-managed-mining-site");
        Require(managed.Asteroids.Count is >= 10 and <= 12 && managed.Asteroids.All(asteroid => asteroid.OreId == "veldspar"),
            "Uitra managed rookie site must stay base Veldspar only, never Grade 0 or higher grades");

        var persistence = SaveService.NewGame();
        persistence.Operation.Asteroids.Clear();
        persistence.Operation.Asteroids.Add(new AsteroidSave { Id = "persisted-grade", OreId = "crokite-iv-grade", RemainingUnits = 123.5d });
        OperationService.AddItem(persistence.StationInventory, "veldspar-iii-grade", 456d);
        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(persistence));
        Require(restored.Operation.Asteroids.Single().OreId == "crokite-iv-grade" &&
                OperationService.ItemQuantity(restored.StationInventory, "veldspar-iii-grade") == 456d,
            "JSON persistence must preserve exact asteroid and inventory grade identities");

        var fallbackMarket = SaveService.NewGame();
        fallbackMarket.PriceCache.Entries.Clear();
        foreach (var family in familyTypeIds.Keys)
        {
            var baseOre = Catalog.GetOreVariant(family, 1);
            var basePrice = MarketService.OreBuyPerUnit(fallbackMarket, baseOre);
            for (var grade = 2; grade <= 4; grade++)
                RequireNearly(MarketService.OreBuyPerUnit(fallbackMarket, Catalog.GetOreVariant(family, grade)), basePrice * gradeMultipliers[grade - 1], .0001d,
                    $"{family} Grade {grade} cache-free market fallback must scale from Grade I");
        }
        RequireNearly(MarketService.OreBuyPerUnit(fallbackMarket, Catalog.GetOre("veldspar-0-grade")),
            MarketService.OreBuyPerUnit(fallbackMarket, Catalog.GetOre("veldspar")) * .5d, .0001d,
            "reserved Veldspar Grade 0 cache-free market fallback must be half of Grade I");

        var compression = NewCompressionOperation(out var compressor, out var compressionRecipient);
        OperationService.AddItem(compressionRecipient.MiningHold, "veldspar-iv-grade", 123d);
        OperationService.AddItem(compressionRecipient.MiningHold, "mercoxit-iii-grade", 2d);
        Require(OperationService.ToggleCore(compression, compressor.Uid, out _), "ore-grade compression fixture must activate its Rorqual core");
        Require(OperationService.TryCompressFleetMiningHolds(compression, compressor.Uid, out var gradeCompression, out _) &&
                gradeCompression.StacksCompressed == 2 && gradeCompression.UnitsCompressed == 125d,
            "fleet compression must process higher-grade ordinary ore and Mercoxit without merging identities");
        RequireCompressedQuantity(compressionRecipient, "veldspar-iv-grade", 123d);
        RequireCompressedQuantity(compressionRecipient, "mercoxit-iii-grade", 2d);
        RequireCompressionVolume("veldspar-iv-grade", 100d);
        RequireCompressionVolume("mercoxit-iii-grade", 100d);

        var targeting = NewSinglePilotOperation("uitra-belt-1");
        var targetMember = targeting.Operation.Fleet.Single();
        targeting.Operation.Asteroids = new List<AsteroidSave>
        {
            new() { Id = "more-units-less-volume", OreId = "veldspar-iv-grade", RemainingUnits = 1000d },
            new() { Id = "fewer-units-more-volume", OreId = "scordite-ii-grade", RemainingUnits = 800d }
        };
        targetMember.Order = FleetOrder.Idle;
        targetMember.TargetAsteroidId = string.Empty;
        Require(OperationService.TryAssignAutomaticTarget(targeting, targetMember) && targetMember.TargetAsteroidId == "fewer-units-more-volume",
            "automatic target selection must compare remaining m3 across ore grades, not raw unit count");
    }

    static void SmokePersistentMiningSites()
    {
        var beforeDowntime = new DateTimeOffset(2099, 8, 13, 10, 59, 50, TimeSpan.Zero).ToUnixTimeSeconds();
        var save = NewSinglePilotOperation("uitra-belt-1", beforeDowntime);
        var asteroid = save.Operation.Asteroids[0];
        var persistedUnits = asteroid.RemainingUnits * .37d;
        asteroid.RemainingUnits = persistedUnits;
        var serial = save.MiningSites.Single(site => site.LocationId == "uitra-belt-1").InstanceSerial;

        MiningSiteService.SnapshotActiveOperation(save);
        var sourceState = save.MiningSites.Single(site => site.LocationId == "uitra-belt-1");
        RequireNearly(sourceState.Asteroids.Single(candidate => candidate.Id == asteroid.Id).RemainingUnits, persistedUnits, .000001d,
            "partial belt depletion must be copied into the persistent world state");
        Require(OperationService.TryStop(save, out _), "persistent-site revisit setup must return its fleet to station");

        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        OperationService.Start(restored, "uitra-belt-2");
        Require(restored.Operation.Active && restored.Operation.LocationId == "uitra-belt-2", "a second static belt in the same system must remain independently visitable");
        Require(OperationService.TryStop(restored, out _), "persistent-site revisit setup must leave the second belt");
        OperationService.Start(restored, "uitra-belt-1");

        var revisited = restored.Operation.Asteroids.Single(candidate => candidate.Id == asteroid.Id);
        Require(restored.MiningSites.Single(site => site.LocationId == "uitra-belt-1").InstanceSerial == serial,
            "leaving and revisiting a belt before downtime must not create a new site instance");
        RequireNearly(revisited.RemainingUnits, persistedUnits, .000001d,
            "station visits, another belt and a JSON round trip must preserve exact partial depletion");
    }

    static void SmokeMiningSiteLifecycle()
    {
        var beforeDowntime = new DateTimeOffset(2099, 8, 13, 10, 59, 50, TimeSpan.Zero).ToUnixTimeSeconds();
        var afterDowntime = new DateTimeOffset(2099, 8, 13, 11, 0, 10, TimeSpan.Zero).ToUnixTimeSeconds();
        Require(MiningSiteService.NextStaticRefreshUnix(beforeDowntime) == beforeDowntime + 10,
            "the next static-belt boundary must be daily downtime at exactly 11:00 UTC");

        var active = NewSinglePilotOperation("uitra-belt-1", beforeDowntime);
        var activeState = active.MiningSites.Single(site => site.LocationId == "uitra-belt-1");
        var member = active.Operation.Fleet.Single();
        var oldSerial = activeState.InstanceSerial;
        foreach (var asteroid in activeState.Asteroids) asteroid.RemainingUnits = 0;
        foreach (var asteroid in active.Operation.Asteroids) asteroid.RemainingUnits = 0;
        activeState.Lifecycle = MiningSiteLifecycle.Cooldown;
        member.TargetAsteroidId = active.Operation.Asteroids[0].Id;
        member.Order = FleetOrder.Mining;
        member.CycleProgressSeconds = 4f;
        active.Operation.Enemies.Add(new EnemySave { Id = "downtime-cleanup" });

        MiningSiteService.Tick(active, beforeDowntime);
        Require(activeState.InstanceSerial == oldSerial && activeState.Lifecycle == MiningSiteLifecycle.Cooldown,
            "an exhausted static belt must not refresh before the 11:00 UTC boundary");
        MiningSiteService.Tick(active, afterDowntime);
        Require(activeState.InstanceSerial == oldSerial + 1 && activeState.Lifecycle == MiningSiteLifecycle.Available && MiningSiteService.RemainingM3(activeState.Asteroids) > 0,
            "the first tick after downtime must replenish an exhausted static belt once");
        Require(active.Operation.Asteroids.Count == activeState.Asteroids.Count && active.Operation.Asteroids[0].Id == activeState.Asteroids[0].Id,
            "an active operation must adopt the replenished persistent site instance");
        Require(string.IsNullOrWhiteSpace(member.TargetAsteroidId) && member.Order == FleetOrder.Idle && member.CycleProgressSeconds == 0 && active.Operation.Enemies.Count == 0,
            "downtime replacement of an active belt must clear stale targets, cycles and NPCs");
        MiningSiteService.Tick(active, afterDowntime + 60);
        Require(activeState.InstanceSerial == oldSerial + 1,
            "repeated ticks after the same downtime must not replenish the belt twice");

        var overdue = NewSinglePilotOperation("uitra-belt-1", beforeDowntime);
        var overdueState = overdue.MiningSites.Single(site => site.LocationId == "uitra-belt-1");
        var overdueBoundary = beforeDowntime + 10;
        var overdueSerial = overdueState.InstanceSerial;
        overdueState.NextRefreshUnix = overdueBoundary;
        foreach (var asteroid in overdue.Operation.Asteroids) asteroid.RemainingUnits = 0;
        MiningSiteService.SnapshotActiveOperation(overdue);
        Require(overdueState.Lifecycle == MiningSiteLifecycle.Cooldown && overdueState.NextRefreshUnix == overdueBoundary,
            "snapshotting an exhausted belt after its stored downtime boundary must not postpone the overdue refresh to tomorrow");
        MiningSiteService.Tick(overdue, afterDowntime);
        Require(overdueState.InstanceSerial == overdueSerial + 1 && overdueState.Lifecycle == MiningSiteLifecycle.Available &&
                MiningSiteService.RemainingM3(overdueState.Asteroids) > 0,
            "the tick following an overdue snapshot must still observe that boundary and replenish exactly once");

        var offline = SaveService.NewGame();
        offline.MiningSites.Clear();
        MiningSiteService.Tick(offline, beforeDowntime);
        MiningSiteService.LoadIntoOperation(offline, Catalog.GetLocation("uitra-belt-1"), beforeDowntime);
        var offlineState = offline.MiningSites.Single(site => site.LocationId == "uitra-belt-1");
        foreach (var asteroid in offlineState.Asteroids) asteroid.RemainingUnits = 0;
        offlineState.Lifecycle = MiningSiteLifecycle.Cooldown;
        offline.LastSaveUnix = beforeDowntime;
        var offlineSerial = offlineState.InstanceSerial;
        var restoredOffline = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(offline));
        var severalDaysLater = afterDowntime + 3 * 24 * 60 * 60;
        MiningSiteService.Tick(restoredOffline, severalDaysLater);
        var caughtUp = restoredOffline.MiningSites.Single(site => site.LocationId == "uitra-belt-1");
        Require(caughtUp.InstanceSerial == offlineSerial + 1 && caughtUp.Lifecycle == MiningSiteLifecycle.Available,
            "offline catch-up across several downtimes must restore the latest belt baseline without replaying one refresh per missed day");
        Require(caughtUp.NextRefreshUnix == MiningSiteService.NextStaticRefreshUnix(severalDaysLater) && caughtUp.LastRefreshUnix == caughtUp.NextRefreshUnix - 24 * 60 * 60,
            "offline catch-up must land on the latest completed and next daily downtime boundaries");

        const string anomalyId = "p3en-e-prospecting-l1";
        var anomalyLocation = Catalog.GetLocation(anomalyId);
        var anomaly = SaveService.NewGame();
        anomaly.MiningSites.Clear();
        MiningSiteService.Tick(anomaly, beforeDowntime);
        var anomalyState = anomaly.MiningSites.Single(site => site.LocationId == anomalyId);
        Require(anomalyState.Lifecycle == MiningSiteLifecycle.Available && MiningSiteService.RemainingM3(anomalyState.Asteroids) > 0,
            "the deterministic anomaly lifecycle fixture must begin with a live signature");
        anomaly.Operation.Active = true;
        anomaly.Operation.LocationId = anomalyId;
        MiningSiteService.LoadIntoOperation(anomaly, anomalyLocation);
        foreach (var asteroid in anomaly.Operation.Asteroids) asteroid.RemainingUnits = 0;
        var depletionStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MiningSiteService.SnapshotActiveOperation(anomaly);
        var depletionFinished = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var anomalySerial = anomalyState.InstanceSerial;
        Require(anomalyState.Lifecycle == MiningSiteLifecycle.Cooldown && anomalyState.Asteroids.Count == 0,
            "depleting a dynamic anomaly must remove its old signature and start its own cooldown");
        Require(anomalyState.RespawnUnix >= depletionStarted + (long)anomalyLocation.AnomalyRespawnMinSeconds - 1 &&
                anomalyState.RespawnUnix <= depletionFinished + (long)Math.Ceiling(anomalyLocation.AnomalyRespawnMaxSeconds) + 1,
            "dynamic anomaly cooldown must remain inside its authored data-driven range");

        anomalyState.RespawnUnix = afterDowntime + 3600;
        MiningSiteService.Tick(anomaly, afterDowntime);
        Require(anomalyState.Lifecycle == MiningSiteLifecycle.Cooldown && anomalyState.InstanceSerial == anomalySerial &&
                MiningSiteService.SecondsUntilAvailable(anomaly, anomalyId, afterDowntime) == 3600,
            "daily downtime must not reset an independently cooling anomaly");
        MiningSiteService.Tick(anomaly, anomalyState.RespawnUnix);
        Require(anomalyState.Lifecycle == MiningSiteLifecycle.Available && anomalyState.InstanceSerial == anomalySerial + 1 && MiningSiteService.RemainingM3(anomalyState.Asteroids) > 0,
            "an anomaly must publish a fresh instance only when its own respawn timer expires");
        Require(anomaly.Operation.Asteroids.Count == anomalyState.Asteroids.Count && anomaly.Operation.Asteroids[0].Id == anomalyState.Asteroids[0].Id,
            "an active anomaly operation must adopt the newly spawned signature without a station restart");
    }

    static void SmokeV10MiningSiteMigration()
    {
        foreach (var nullWorldState in new[] { true, false })
        {
            var legacy = NewSinglePilotOperation("uitra-belt-1");
            var operation = legacy.Operation;
            var member = operation.Fleet.Single();
            var ship = legacy.Ships.Single(candidate => candidate.Uid == member.ShipUid);
            var target = operation.Asteroids[0];
            target.RemainingUnits *= .421d;
            operation.Asteroids[1].RemainingUnits = 0;
            member.TargetAsteroidId = target.Id;
            member.Order = FleetOrder.Mining;
            member.CycleProgressSeconds = 7.25f;
            member.DroneCycleProgressSeconds = 3.5f;
            operation.ElapsedSeconds = 321.5f;
            operation.RaidTimerSeconds = 87.25f;
            operation.AutoUnload = true;
            operation.AutoRetarget = true;
            operation.AutoNextBelt = true;
            operation.Enemies.Add(new EnemySave
            {
                Id = "v10-live-npc",
                Name = "Persisted NPC",
                X = 11,
                Y = 2,
                Z = 33,
                ShieldHp = 41,
                ArmorHp = 42,
                StructureHp = 43,
                Dps = 4,
                SpeedKmPerSecond = .5f,
                TargetShipUid = ship.Uid
            });
            OperationService.AddItem(ship.MiningHold, "veldspar", 123);
            OperationService.AddItem(ship.CargoHold, "pyroxeres", 7);
            ship.ShieldHp = Math.Max(1f, ship.ShieldHp - 19f);
            ship.ArmorHp = Math.Max(1f, ship.ArmorHp - 11f);

            legacy.Version = 10;
            legacy.LastSaveUnix = new DateTimeOffset(2099, 8, 13, 10, 59, 50, TimeSpan.Zero).ToUnixTimeSeconds();
            legacy.MiningSites = nullWorldState ? null : new List<MiningSiteStateSave>();
            legacy.Operation.SiteInstanceSerial = 0;
            var deserialized = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(legacy));
            Require(deserialized.Version == 10 && deserialized.Operation.SiteInstanceSerial == 0 &&
                    (deserialized.MiningSites == null || deserialized.MiningSites.Count == 0),
                "v10 migration fixture must model a real save with no persistent mining-site world or instance serial");

            var asteroidsBefore = AsteroidStateSignature(deserialized.Operation.Asteroids);
            var fleetBefore = FleetStateSignature(deserialized.Operation.Fleet);
            var enemiesBefore = EnemyStateSignature(deserialized.Operation.Enemies);
            var shipBefore = StarterRuntimeSignature(deserialized.Ships.Single(candidate => candidate.Uid == ship.Uid));
            var elapsedBefore = deserialized.Operation.ElapsedSeconds;
            var raidBefore = deserialized.Operation.RaidTimerSeconds;
            var visitBefore = deserialized.Operation.VisitNumber;

            SaveService.MigrateToCurrentVersion(deserialized);

            var migratedOperation = deserialized.Operation;
            var migratedSite = deserialized.MiningSites.Single(site => site.LocationId == "uitra-belt-1");
            Require(deserialized.Version == SaveService.CurrentVersion && migratedOperation.SiteInstanceSerial > 0 &&
                    migratedSite.InstanceSerial == migratedOperation.SiteInstanceSerial,
                "v10 migration must seed one persistent instance and bind the active operation to it");
            Require(AsteroidStateSignature(migratedOperation.Asteroids) == asteroidsBefore &&
                    AsteroidStateSignature(migratedSite.Asteroids) == asteroidsBefore,
                "v10 migration must preserve every partial/depleted asteroid exactly and seed the site from that snapshot");
            Require(FleetStateSignature(migratedOperation.Fleet) == fleetBefore && EnemyStateSignature(migratedOperation.Enemies) == enemiesBefore,
                "v10 migration must preserve the exact active fleet, orders, mining cycles and NPC state");
            Require(StarterRuntimeSignature(deserialized.Ships.Single(candidate => candidate.Uid == ship.Uid)) == shipBefore,
                "v10 migration must preserve ship location, holds and HP exactly");
            RequireNearly(migratedOperation.ElapsedSeconds, elapsedBefore, .000001d, "v10 migration changed elapsed operation time");
            RequireNearly(migratedOperation.RaidTimerSeconds, raidBefore, .000001d, "v10 migration changed the NPC timer");
            Require(migratedOperation.VisitNumber == visitBefore && migratedOperation.AutoUnload && migratedOperation.AutoRetarget && migratedOperation.AutoNextBelt,
                "v10 migration must preserve visit and automation state");
        }
    }

    static void SmokeV12IceMigration()
    {
        int LegacyStableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value ?? string.Empty) hash = hash * 31 + character;
                return hash;
            }
        }

        int LegacyClearCooldownSeconds(string locationId, int serial)
        {
            const int minimum = 6 * 60 * 60;
            const int maximum = 8 * 60 * 60;
            var span = maximum - minimum + 1;
            return minimum + (int)((uint)(LegacyStableHash(locationId) ^ serial * 48611) % span);
        }

        string ModuleSignature(ShipSave ship) => string.Join("|", (ship?.Modules ?? new List<FittedModuleSave>()).Select(module => JsonUtility.ToJson(module)));

        var legacy = SaveService.NewGame();
        var obsoletePackages = new[]
        {
            (HullId: "venture", PackageId: "venture-ice-t1", MinerId: "ice-mining-laser-i", Location: ShipLocation.Belt),
            (HullId: "venture-consortium", PackageId: "venture-consortium-ice-t2", MinerId: "ice-mining-laser-ii", Location: ShipLocation.Transit),
            (HullId: "pioneer", PackageId: "pioneer-ice-ore", MinerId: "ore-ice-mining-laser", Location: ShipLocation.Station),
            (HullId: "pioneer-consortium", PackageId: "pioneer-consortium-ice-t1", MinerId: "ice-mining-laser-i", Location: ShipLocation.Belt)
        };
        var obsoleteShips = new List<ShipSave>();
        for (var index = 0; index < obsoletePackages.Length; index++)
        {
            var specification = obsoletePackages[index];
            Require(Catalog.GetPackage(specification.PackageId) == null,
                $"v12 migration fixture requires retired package identity {specification.PackageId}");
            var hull = Catalog.GetShip(specification.HullId);
            var ship = SaveService.CreateShip($"smoke-v11-obsolete-ice-{index}", specification.HullId);
            ship.PackageId = specification.PackageId;
            ship.TankPresetId = $"{specification.HullId}-shield-i";
            ship.Location = specification.Location;
            ship.Modules.Clear();
            for (var slot = 0; slot < hull.MiningHighSlots; slot++)
                ship.Modules.Add(new FittedModuleSave { Slot = slot, ModuleId = specification.MinerId, Active = true, BurstCycleSecondsLeft = index + slot + .25f });
            for (var slot = 0; slot < hull.LowSlots; slot++)
                ship.Modules.Add(new FittedModuleSave { Slot = 40 + slot, ModuleId = "ice-harvester-upgrade-i", Active = true });
            OperationService.AddItem(ship.MiningHold, "clear-icicle", index + 1);
            OperationService.AddItem(ship.CargoHold, "pyroxeres", index + 2);
            OperationService.AddItem(ship.FuelHold, "heavy-water", index + 3);
            ship.ShieldHp = 101f + index;
            ship.ArmorHp = 81f + index;
            ship.StructureHp = 61f + index;
            legacy.Ships.Add(ship);
            obsoleteShips.Add(ship);
        }

        const string clearLocationId = "manatirid-clear-icicle";
        var depletedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MiningSiteService.Tick(legacy, depletedUnix);
        var clearState = legacy.MiningSites.Single(site => site.LocationId == clearLocationId);
        clearState.Lifecycle = MiningSiteLifecycle.Cooldown;
        clearState.InstanceSerial = 1;
        clearState.Asteroids.Clear();
        clearState.NextRefreshUnix = 0;
        var legacyCooldownSeconds = LegacyClearCooldownSeconds(clearLocationId, clearState.InstanceSerial);
        Require(legacyCooldownSeconds > 6 * 60 * 60,
            "v12 Clear Icicle migration fixture must begin with a legacy cooldown longer than six hours");
        clearState.RespawnUnix = depletedUnix + legacyCooldownSeconds;
        var oldRespawnUnix = clearState.RespawnUnix;
        var oldSerial = clearState.InstanceSerial;
        legacy.Version = 11;
        legacy.LastSaveUnix = depletedUnix;

        var preservedShips = obsoleteShips.Select(ship => new
        {
            ship.Uid,
            ship.HullId,
            ship.PackageId,
            Runtime = StarterRuntimeSignature(ship),
            Modules = ModuleSignature(ship)
        }).ToArray();
        var deserialized = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(legacy));
        var deserializedClearState = deserialized.MiningSites.Single(site => site.LocationId == clearLocationId);
        Require(deserialized.Version == 11 && deserializedClearState.RespawnUnix == oldRespawnUnix &&
                preservedShips.All(expected => deserialized.Ships.Single(ship => ship.Uid == expected.Uid).PackageId == expected.PackageId),
            "v12 migration fixture must deserialize the exact v11 cooldown and retired identities");

        SaveService.MigrateToCurrentVersion(deserialized);

        Require(deserialized.Version == SaveService.CurrentVersion && SaveService.CurrentVersion == 13,
            "legacy ice migration must preserve its v12 repair and advance the save to schema v13");
        foreach (var expected in preservedShips)
        {
            var migratedShip = deserialized.Ships.Single(ship => ship.Uid == expected.Uid);
            Require(string.IsNullOrWhiteSpace(migratedShip.PackageId),
                $"retired ice identity on {expected.HullId} must become deployable LEGACY CUSTOM");
            Require(migratedShip.HullId == expected.HullId && ModuleSignature(migratedShip) == expected.Modules,
                $"v12 migration must not refit or replace the retired {expected.HullId} ice ship");
            Require(StarterRuntimeSignature(migratedShip) == expected.Runtime,
                $"v12 migration must preserve {expected.HullId} holds, HP and location exactly");
        }

        var exactRespawnUnix = depletedUnix + 6 * 60 * 60;
        Require(deserializedClearState.Lifecycle == MiningSiteLifecycle.Cooldown && deserializedClearState.InstanceSerial == oldSerial &&
                deserializedClearState.Asteroids.Count == 0 && deserializedClearState.RespawnUnix == exactRespawnUnix &&
                deserializedClearState.RespawnUnix < oldRespawnUnix,
            "v12 migration must reconstruct depletion time and replace the legacy 6-8 hour Clear Icicle cooldown with exactly six hours");
        var onceMigratedJson = JsonUtility.ToJson(deserialized);
        SaveService.MigrateToCurrentVersion(deserialized);
        Require(JsonUtility.ToJson(deserialized) == onceMigratedJson,
            "v12 ice migration must be idempotent after the schema version advances");
        MiningSiteService.Tick(deserialized, exactRespawnUnix - 1);
        Require(deserializedClearState.Lifecycle == MiningSiteLifecycle.Cooldown && deserializedClearState.InstanceSerial == oldSerial,
            "migrated Clear Icicle must remain absent one second before its new six-hour boundary");
        MiningSiteService.Tick(deserialized, exactRespawnUnix);
        Require(deserializedClearState.Lifecycle == MiningSiteLifecycle.Available && deserializedClearState.InstanceSerial == oldSerial + 1 &&
                deserializedClearState.RespawnUnix == 0 && MiningSiteService.RemainingM3(deserializedClearState.Asteroids) > 0 &&
                deserializedClearState.Asteroids.All(asteroid => asteroid.OreId == "clear-icicle"),
            "migrated Clear Icicle must publish one fresh ice instance exactly at the new six-hour boundary");
    }

    static void SmokePreparedPackagesAndMigration()
    {
        Require(Catalog.Packages.Count > Catalog.Ships.Count, "prepared package catalog must expose meaningful ore/ice/gas/Mercoxit/booster choices");
        Require(Catalog.Packages.Select(package => package.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Catalog.Packages.Count, "prepared package IDs must be unique");
        foreach (var package in Catalog.Packages)
        {
            var hull = Catalog.GetShip(package.HullId);
            Require(hull != null && Catalog.GetPackage(package.Id) == package, $"package {package.Id} must resolve to a known hull and stable catalog identity");
            Require(package.Id == "venture-ore-t0" ? string.IsNullOrWhiteSpace(package.TankPresetId) : Catalog.GetTankPreset(package.TankPresetId) != null, $"package {package.Id} defensive preset contract is invalid");
            Require(string.IsNullOrWhiteSpace(package.MinerModuleId) || Catalog.GetModule(package.MinerModuleId) != null, $"package {package.Id} references an unknown extractor");
            Require(string.IsNullOrWhiteSpace(package.LowUpgradeId) || Catalog.GetModule(package.LowUpgradeId)?.Kind is ModuleKind.MiningUpgrade or ModuleKind.IceHarvesterUpgrade, $"package {package.Id} references an invalid locked low-slot upgrade");
            Require(string.IsNullOrWhiteSpace(package.CombatDroneId) || Catalog.GetDrone(package.CombatDroneId)?.Mining == false, $"package {package.Id} must carry combat, not mining, drones");
            Require(PreparedPackageService.RequiredSkills(package).All(requirement => Catalog.GetSkill(requirement.SkillId) != null && requirement.Level is >= 1 and <= 5), $"package {package.Id} skill closure contains an invalid requirement");
        }

        var save = SaveService.NewGame();
        var starterPackage = Catalog.GetPackage("venture-ore-t0");
        Require(starterPackage != null && starterPackage.Grade == PreparedPackageGrade.T0 && PreparedPackageService.PackagePrice(save, starterPackage) == 0, "canonical Venture T0 package must be the only free starter grade");
        Require(string.IsNullOrWhiteSpace(starterPackage.LowUpgradeId) && string.IsNullOrWhiteSpace(starterPackage.CombatDroneId) && string.IsNullOrWhiteSpace(starterPackage.TankPresetId), "canonical Venture T0 package must not hide upgrades, drones or tank");
        var ventureT1Package = Catalog.GetPackage("venture-ore-t1");
        Require(ventureT1Package != null && MarketService.HullSellPrice(save, Catalog.GetShip("venture")) > 0 && PreparedPackageService.PackagePrice(save, ventureT1Package) > MarketService.HullSellPrice(save, Catalog.GetShip("venture")), "complete Venture T1 package must include a paid hull and locked equipment rather than inherit the free T0 exception");
        var hulkPackage = Catalog.GetPackage("hulk-ore-t2-a2");
        Require(hulkPackage != null && hulkPackage.ImplicitUniversalTypeA2, "Hulk ore T2 must carry the implicit universal Type A II profile");
        var hulkPackageSkills = PreparedPackageService.RequiredSkills(hulkPackage);
        Require(new[] { "simple-ore-processing", "coherent-ore-processing", "variegated-ore-processing", "complex-ore-processing" }.All(skillId => hulkPackageSkills.Any(requirement => requirement.SkillId == skillId && requirement.Level >= 4)), "universal Type A II package must require every ordinary ore-processing family at level IV");
        var hulk = SaveService.CreateShip("smoke-prepared-hulk", hulkPackage.HullId);
        PreparedPackageService.ApplyLockedFit(hulk, hulkPackage);
        var hulkHull = Catalog.GetShip(hulk.HullId);
        Require(hulk.PackageId == hulkPackage.Id && hulk.TankPresetId == hulkPackage.TankPresetId, "applying a package must persist both package identities");
        Require(hulk.Modules.Count(module => module.ModuleId == hulkPackage.MinerModuleId) == hulkHull.MiningHighSlots, "locked package must fill every mining high slot");
        Require(hulk.Modules.Count(module => module.ModuleId == hulkPackage.LowUpgradeId) == hulkHull.LowSlots, "locked package must fill every mining low slot");
        Require(hulk.Modules.Where(module => module.ModuleId == hulkPackage.MinerModuleId).All(module => string.IsNullOrWhiteSpace(module.ChargeId)), "universal Type A must be implicit and must not create fitted crystal instances or wear state");
        Require(hulk.CombatDroneCount > 0 && FittingService.DroneBayUsedM3(hulk) <= hulkHull.DroneBayM3 + .001f, "prepared combat drones must respect the hull drone bay");
        RequireNearly(hulk.ShieldHp, PreparedPackageService.MaxShieldHp(hulk), .001d, "fresh prepared ship must start at its tank-adjusted maximum shield HP");
        RequireNearly(hulk.ShieldHp + hulk.ArmorHp + hulk.StructureHp, PreparedPackageService.MaxTotalHp(hulk), .001d, "fresh prepared ship must start at its tank-adjusted maximum total HP");
        Require(PreparedPackageService.IncomingShieldDamageMultiplier(hulk) > 0 && PreparedPackageService.IncomingShieldDamageMultiplier(hulk) < 1, "locked tank must reduce incoming shield damage without making the ship invulnerable");

        var buyer = save.Characters[9];
        Require(!PreparedPackageService.CanUsePackage(buyer, hulkPackage), "an untrained pilot must not use an advanced prepared package");
        GrantAllSkills(buyer);
        Require(PreparedPackageService.CanUsePackage(buyer, hulkPackage), "a fully trained pilot must meet the complete hull/equipment/tank package requirements");
        var price = PreparedPackageService.PackagePrice(save, hulkPackage);
        Require(price > MarketService.HullSellPrice(save, hulkHull), "prepared package price must include its locked equipment rather than charge for a bare hull");
        save.Isk = price + 123d;
        Require(save.StationInventory.Count == 0 && save.StationItemInstances.Count == 0, "package purchase fixture must begin without station equipment");
        Require(PreparedPackageService.TryBuy(save, buyer.Id, hulkPackage.Id, out var boughtUid, out _), "funded and trained pilot must buy a complete prepared package atomically");
        RequireNearly(save.Isk, 123d, .001d, "prepared package purchase must debit the shared wallet exactly once");
        var bought = save.Ships.Find(ship => ship.Uid == boughtUid);
        Require(bought != null && bought.PackageId == hulkPackage.Id && bought.Modules.Count > 0 && buyer.AssignedShipUid == boughtUid, "purchase must create, materialize and assign one ready-to-fly ship");
        Require(save.StationInventory.Count == 0 && save.StationItemInstances.Count == 0, "package purchase must not consume or duplicate legacy station inventory");
        var lockedFitBefore = JsonUtility.ToJson(bought);
        Require(!FittingService.TrySwapMiner(save, buyer.Id, bought.Uid, 0, "miner-i", out _), "recognized prepared packages must reject manual refitting in the domain service, not only hide UI buttons");
        Require(JsonUtility.ToJson(bought) == lockedFitBefore, "rejected manual refit must leave the locked prepared package byte-for-byte unchanged");

        SmokeImplicitUniversalPackageYield(hulkPackage, "veldspar", expectBonus: true);
        var mercoxitPackage = Catalog.GetPackage("hulk-mercoxit-t2-a2");
        Require(mercoxitPackage != null && mercoxitPackage.ImplicitUniversalTypeA2, "Hulk Mercoxit T2 package must expose its implicit specialized Type A II profile");
        var mercoxitPackageSkills = PreparedPackageService.RequiredSkills(mercoxitPackage);
        Require(mercoxitPackageSkills.Any(requirement => requirement.SkillId == "mercoxit-ore-processing" && requirement.Level >= 4) && !mercoxitPackageSkills.Any(requirement => requirement.SkillId == "simple-ore-processing"), "Mercoxit Type A II package must require only its specialized processing family, not ordinary universal families");
        SmokeImplicitUniversalPackageYield(mercoxitPackage, "mercoxit", expectBonus: true);
        SmokeImplicitUniversalPackageYield(mercoxitPackage, "bistot", expectBonus: false);

        var ambiguousLegacyHulk = SaveService.CreateShip("legacy-hulk-without-proven-a-crystal", hulkPackage.HullId);
        PreparedPackageService.ApplyLockedFit(ambiguousLegacyHulk, hulkPackage);
        ambiguousLegacyHulk.PackageId = string.Empty;
        Require(PreparedPackageService.FindClosestPackage(ambiguousLegacyHulk) == null, "an old modulated fit without a consistently fitted Type A tier must remain legacy instead of receiving free implicit Type A II");

        // A v9 save may be mid-cycle and may contain obsolete market inventory.
        // Migration is identity-only except for the explicitly authorized T0
        // conversion of the nine original Ventures. Prove the conversion keeps
        // their damage, holds and location while every non-starter byte survives.
        var legacy = SaveService.NewGame();
        legacy.Isk = 987_654_321d;
        OperationService.AddItem(legacy.StationInventory, "simple-a-i", 7);
        legacy.StationItemInstances.Add(new ItemInstanceSave { Uid = "legacy-worn-crystal", ItemId = "simple-a-ii", Damage = .73f });
        var legacyCommandPilot = legacy.Characters[9]; GrantAllSkills(legacyCommandPilot); legacyCommandPilot.DeployOnLaunch = true;
        var legacyCommandShip = SaveService.CreateShip("legacy-active-rorqual", "rorqual");
        PreparedPackageService.ApplyLockedFit(legacyCommandShip, Catalog.GetPackage("rorqual-booster-t2"));
        OperationService.AddItem(legacyCommandShip.FuelHold, "heavy-water", 10_000);
        legacy.Ships.Add(legacyCommandShip); legacyCommandPilot.AssignedShipUid = legacyCommandShip.Uid;
        var bareLegacyShip = SaveService.CreateShip("legacy-bare-venture", "venture");
        legacy.Ships.Add(bareLegacyShip);
        var partialLegacyShip = SaveService.CreateShip("legacy-partial-venture", "venture");
        partialLegacyShip.Modules.Add(new FittedModuleSave { Slot = 0, ModuleId = "miner-i", Active = true });
        legacy.Ships.Add(partialLegacyShip);
        foreach (var legacyShip in legacy.Ships.Where(ship => ship != legacyCommandShip)) { legacyShip.PackageId = string.Empty; legacyShip.TankPresetId = string.Empty; }
        var convertedStarterUids = legacy.Ships.Where(ship => ship.Uid.StartsWith("venture-", StringComparison.OrdinalIgnoreCase)).Select(ship => ship.Uid).ToHashSet(StringComparer.Ordinal);
        legacy.Version = 9;
        OperationService.Start(legacy, "uitra-belt-1");
        Require(OperationService.ToggleBursts(legacy, legacyCommandShip.Uid, out _), "migration fixture must contain live burst magazines");
        Require(OperationService.ToggleCore(legacy, legacyCommandShip.Uid, out _), "migration fixture must contain an active Industrial Core cycle");
        legacyCommandShip.PackageId = string.Empty;
        legacyCommandShip.TankPresetId = string.Empty;
        var legacyMember = legacy.Operation.Fleet[0];
        var legacyShipInBelt = legacy.Ships.Find(ship => ship.Uid == legacyMember.ShipUid);
        var legacyRock = legacy.Operation.Asteroids[0];
        Require(OperationService.AssignTarget(legacy, legacyShipInBelt.Uid, legacyRock.Id), "migration fixture must enter a live mining order");
        legacyMember.X = legacyRock.X; legacyMember.Y = legacyRock.Y; legacyMember.Z = legacyRock.Z;
        OperationService.Tick(legacy, 3.25f);
        legacyShipInBelt.ShieldHp -= 17f;
        OperationService.AddItem(legacyShipInBelt.MiningHold, "veldspar", 11);
        var starterRuntimeBeforeMigration = legacy.Ships
            .Where(ship => convertedStarterUids.Contains(ship.Uid))
            .ToDictionary(ship => ship.Uid, StarterRuntimeSignature, StringComparer.Ordinal);
        var exactLegacyJson = JsonUtility.ToJson(legacy);
        var exactLegacyNonStarterJson = JsonUtility.ToJson(new GameSave
        {
            Version = legacy.Version,
            Isk = legacy.Isk,
            Characters = legacy.Characters,
            Ships = legacy.Ships.Where(ship => !convertedStarterUids.Contains(ship.Uid)).ToList(),
            StationInventory = legacy.StationInventory,
            StationItemInstances = legacy.StationItemInstances,
            PriceCache = legacy.PriceCache,
            Operation = legacy.Operation,
            LastSaveUnix = legacy.LastSaveUnix,
            NextShipSerial = legacy.NextShipSerial,
            NextItemSerial = legacy.NextItemSerial
        });

        SaveService.MigrateToCurrentVersion(legacy);
        Require(legacy.Version == SaveService.CurrentVersion, "v9 migration must advance the schema version");
        Require(legacy.Ships.Where(ship => convertedStarterUids.Contains(ship.Uid)).All(ship =>
            ship.PackageId == "venture-ore-t0" && string.IsNullOrWhiteSpace(ship.TankPresetId) &&
            ship.Modules.Count == 2 && ship.Modules.Any(module => module.Slot == 0 && module.ModuleId == "miner-i") &&
            ship.Modules.Any(module => module.Slot == 1 && module.ModuleId == "miner-i") &&
            string.IsNullOrWhiteSpace(ship.CombatDroneId) && ship.CombatDroneCount == 0 &&
            string.IsNullOrWhiteSpace(ship.MiningDroneId) && ship.MiningDroneCount == 0),
            "v9 migration must convert all nine original free Ventures to canonical bare T0");
        Require(legacy.Ships.Where(ship => convertedStarterUids.Contains(ship.Uid)).All(ship =>
            starterRuntimeBeforeMigration.TryGetValue(ship.Uid, out var before) && before == StarterRuntimeSignature(ship)),
            "starter T0 conversion must preserve current HP, holds and location exactly");
        Require(legacyCommandShip.PackageId == "rorqual-booster-t2" && !string.IsNullOrWhiteSpace(legacyCommandShip.TankPresetId), "v9 migration must recognize a complete command package without resetting its active core or bursts");
        Require(string.IsNullOrWhiteSpace(bareLegacyShip.PackageId) && string.IsNullOrWhiteSpace(bareLegacyShip.TankPresetId), "bare or custom v9 hull must remain an explicit legacy fit instead of receiving an invented package");
        Require(string.IsNullOrWhiteSpace(partialLegacyShip.PackageId) && string.IsNullOrWhiteSpace(partialLegacyShip.TankPresetId), "partial extractor fit must remain legacy instead of being mistaken for a complete package");
        var inferredPackages = legacy.Ships.Select(ship => ship.PackageId).ToArray();
        var inferredTanks = legacy.Ships.Select(ship => ship.TankPresetId).ToArray();
        legacy.Version = 9;
        for (var index = 0; index < legacy.Ships.Count; index++)
            if (!convertedStarterUids.Contains(legacy.Ships[index].Uid)) { legacy.Ships[index].PackageId = string.Empty; legacy.Ships[index].TankPresetId = string.Empty; }
        var migratedNonStarterJson = JsonUtility.ToJson(new GameSave
        {
            Version = legacy.Version,
            Isk = legacy.Isk,
            Characters = legacy.Characters,
            Ships = legacy.Ships.Where(ship => !convertedStarterUids.Contains(ship.Uid)).ToList(),
            StationInventory = legacy.StationInventory,
            StationItemInstances = legacy.StationItemInstances,
            PriceCache = legacy.PriceCache,
            Operation = legacy.Operation,
            LastSaveUnix = legacy.LastSaveUnix,
            NextShipSerial = legacy.NextShipSerial,
            NextItemSerial = legacy.NextItemSerial
        });
        Require(migratedNonStarterJson == exactLegacyNonStarterJson, "v9 migration must not alter non-starter modules, drones, HP, holds, ISK, inventory, locations, fleet orders, cycles, core or bursts");
        Require(exactLegacyJson != JsonUtility.ToJson(legacy), "v10 migration fixture must prove the explicitly authorized starter Venture T0 conversion occurred");
        legacy.Version = SaveService.CurrentVersion;
        for (var index = 0; index < legacy.Ships.Count; index++) { legacy.Ships[index].PackageId = inferredPackages[index]; legacy.Ships[index].TankPresetId = inferredTanks[index]; }
    }

    static void SmokePreparedPackageCombatEstimates()
    {
        var ventureT0 = Catalog.GetPackage("venture-ore-t0");
        var ventureT1 = Catalog.GetPackage("venture-ore-t1");
        var ventureT2 = Catalog.GetPackage("venture-ore-t2-a2");
        Require(ventureT0 != null && ventureT1 != null && ventureT2 != null, "Venture combat-estimate fixtures must resolve all three package grades");

        var t0 = AssertPackageCombat(ventureT0, 0, 0, 0);
        RequireNearly(t0.MinimumEffectiveHp, 600d, .01d, "bare Venture T0 minimum EHP mismatch");
        RequireNearly(t0.SkilledEffectiveHp, 600d, .01d, "bare Venture T0 skilled EHP mismatch");
        var t0Summary = MiningPackageInfoService.CombatSummary(ventureT0);
        Require(t0Summary.Contains("EHP") && t0Summary.Contains("боевых дронов нет"), "bare Venture T0 summary must expose EHP and explicitly report no combat drones");

        var t1 = AssertPackageCombat(ventureT1, 2, 16, 24);
        RequireNearly(t1.MinimumEffectiveHp, 3173.33d, 1d, "Venture T1 minimum EHP mismatch");
        RequireNearly(t1.SkilledEffectiveHp, 3416.67d, 1d, "Venture T1 skilled EHP mismatch");
        var t1Summary = MiningPackageInfoService.CombatSummary(ventureT1);
        Require(t1Summary.Contains("EHP") && t1Summary.Contains("DPS") && t1Summary.Contains("×2"), "Venture T1 summary must expose EHP, drone count and DPS");

        var t2 = AssertPackageCombat(ventureT2, 2, 24, 36);
        RequireNearly(t2.MinimumEffectiveHp, 4865.74d, 1d, "Venture T2 minimum EHP mismatch");
        RequireNearly(t2.SkilledEffectiveHp, 4865.74d, 1d, "Venture T2 skilled EHP mismatch");

        AssertPackageCombat(Catalog.GetPackage("porpoise-booster-t1"), 5, 80, 120);
        AssertPackageCombat(Catalog.GetPackage("porpoise-booster-t2"), 5, 120, 180);
        AssertPackageCombat(Catalog.GetPackage("orca-booster-t1"), 2, 56, 84);
        AssertPackageCombat(Catalog.GetPackage("orca-booster-t2"), 2, 84, 126);
        AssertPackageCombat(Catalog.GetPackage("rorqual-booster-t1"), 5, 140, 210);
        AssertPackageCombat(Catalog.GetPackage("rorqual-booster-t2"), 5, 210, 315);

        var fitted = SaveService.CreateShip("smoke-venture-t1-combat", ventureT1.HullId);
        PreparedPackageService.ApplyLockedFit(fitted, ventureT1);
        var minimumPilot = PilotWithPackageRequirements(ventureT1);
        var rawHp = PreparedPackageService.MaxTotalHp(fitted, minimumPilot);
        Require(PreparedPackageService.IncomingShieldDamageMultiplier(fitted, minimumPilot) < 1f && t1.MinimumEffectiveHp > rawHp,
            "Venture T1 EHP must include its hardener resistance and exceed the same fit's raw HP");
    }

    static MiningPackageCombatEstimate AssertPackageCombat(PreparedMiningPackage package, int expectedDroneCount, double expectedMinimumDps, double expectedSkilledDps)
    {
        Require(package != null, "combat-estimate package fixture is missing");
        var estimate = MiningPackageInfoService.EstimateCombat(package);
        Require(estimate.CombatDroneCount == expectedDroneCount, $"{package.Id} combat drone count mismatch");
        RequireNearly(estimate.MinimumDroneDps, expectedMinimumDps, .01d, $"{package.Id} minimum combat-drone DPS mismatch");
        RequireNearly(estimate.SkilledDroneDps, expectedSkilledDps, .01d, $"{package.Id} skilled combat-drone DPS mismatch");

        var ship = SaveService.CreateShip($"smoke-combat-{package.Id}", package.HullId);
        PreparedPackageService.ApplyLockedFit(ship, package);
        var minimumPilot = PilotWithPackageRequirements(package);
        RequireNearly(OperationService.CombatDroneDps(ship, minimumPilot), estimate.MinimumDroneDps, .01d,
            $"{package.Id} estimate must share the operation combat-drone DPS calculation at minimum skills");
        var skilledPilot = new CharacterSave();
        GrantAllSkills(skilledPilot);
        RequireNearly(OperationService.CombatDroneDps(ship, skilledPilot), estimate.SkilledDroneDps, .01d,
            $"{package.Id} estimate must share the operation combat-drone DPS calculation at skilled levels");
        return estimate;
    }

    static CharacterSave PilotWithPackageRequirements(PreparedMiningPackage package)
    {
        var pilot = new CharacterSave();
        foreach (var requirement in PreparedPackageService.RequiredSkills(package)) SetSkillLevel(pilot, requirement.SkillId, requirement.Level);
        return pilot;
    }

    static void SmokeImplicitUniversalPackageYield(PreparedMiningPackage package, string oreId, bool expectBonus)
    {
        var save = SaveService.NewGame();
        foreach (var candidate in save.Characters) candidate.DeployOnLaunch = false;
        var pilot = save.Characters[9]; GrantAllSkills(pilot);
        var ship = SaveService.CreateShip($"smoke-{package.Id}-{oreId}", package.HullId);
        PreparedPackageService.ApplyLockedFit(ship, package);
        save.Ships.Add(ship); pilot.AssignedShipUid = ship.Uid; ship.Location = ShipLocation.Belt;
        var member = new FleetMemberSave { PilotId = pilot.Id, ShipUid = ship.Uid, Order = FleetOrder.Mining, TargetAsteroidId = "package-yield-rock" };
        save.Operation = new OperationSave
        {
            Active = true,
            LocationId = "y-zxio-belt-1",
            Fleet = new List<FleetMemberSave> { member },
            Asteroids = new List<AsteroidSave> { new AsteroidSave { Id = member.TargetAsteroidId, OreId = oreId, RemainingUnits = 1_000_000 } }
        };
        var hull = Catalog.GetShip(ship.HullId);var miner = Catalog.GetModule(package.MinerModuleId);
        var withProfile = OperationService.MiningYieldM3(pilot, hull, miner, save, member);
        ship.PackageId = string.Empty;
        var withoutProfile = OperationService.MiningYieldM3(pilot, hull, miner, save, member);
        if (expectBonus) Require(withProfile > withoutProfile, $"{package.Id} implicit Type A profile must increase {oreId} yield");
        else RequireNearly(withProfile, withoutProfile, .001d, $"{package.Id} specialized Mercoxit profile must not bonus ordinary {oreId}");
    }

    static void SmokeBurstLoadoutAndTransfers()
    {
        var save = SaveService.NewGame();
        foreach (var candidate in save.Characters) candidate.DeployOnLaunch = false;
        var pilot = save.Characters[9];
        GrantAllSkills(pilot);
        var rorqual = SaveService.CreateShip("smoke-rorqual-fitting", "rorqual");
        var boosterPackage = Catalog.GetPackage("rorqual-booster-t2");
        Require(boosterPackage != null, "Rorqual must have one ready-made perfect booster package");
        PreparedPackageService.ApplyLockedFit(rorqual, boosterPackage);
        save.Ships.Add(rorqual);
        pilot.AssignedShipUid = rorqual.Uid;
        pilot.DeployOnLaunch = true;
        save.Characters[0].DeployOnLaunch = true;
        Require(Catalog.BurstCharges.Count == 4 && Catalog.BurstCharges.Select(charge => charge.TypeId).SequenceEqual(new[] { 42830, 42829, 90733, 42831 }), "burst catalog must keep the three prepared profiles in order and the preservation ID as legacy-only data");
        Require(Catalog.PreparedBurstProfileIds.SequenceEqual(new[] { "mining-laser-optimization-charge", "mining-laser-field-enhancement-charge", "mining-laser-efficiency-charge" }), "prepared burst order must be cycle, range, then critical plus residue");
        Require(!Catalog.PreparedBurstProfileIds.Contains("mining-equipment-preservation-charge"), "prepared packages must never install the crystal-preservation profile");
        foreach (var commandLoadout in new[] { (Hull: "outrider", Count: 1), (Hull: "porpoise", Count: 2), (Hull: "orca", Count: 3), (Hull: "rorqual", Count: 3) })
        {
            var package = Catalog.GetPackage($"{commandLoadout.Hull}-booster-t2");
            var fitted = SaveService.CreateShip($"smoke-{commandLoadout.Hull}-burst-order", commandLoadout.Hull);
            Require(package != null, $"{commandLoadout.Hull} booster package is missing");
            PreparedPackageService.ApplyLockedFit(fitted, package);
            var profiles = fitted.Modules.Where(module => Catalog.GetModule(module.ModuleId)?.Kind == ModuleKind.MiningBurst).OrderBy(module => module.Slot).Select(module => module.ChargeId).ToArray();
            Require(profiles.Length == commandLoadout.Count && profiles.SequenceEqual(Catalog.PreparedBurstProfileIds.Take(commandLoadout.Count)), $"{commandLoadout.Hull} must install the fixed burst prefix only");
        }
        Require(Catalog.GetShip("rorqual").CommandBurstSlots == 4, "Rorqual hull must retain four physical command burst slots");
        var fittedBursts = rorqual.Modules.Where(module => Catalog.GetModule(module.ModuleId)?.Kind == ModuleKind.MiningBurst).OrderBy(module => module.Slot).ToArray();
        Require(fittedBursts.Length == 3, "Rorqual booster package must leave its fourth physical burst slot empty");
        Require(fittedBursts.All(module => !module.Active && module.ChargeQuantity == 0 && module.ChargeQuantityInitialized && Catalog.PreparedBurstProfileIds.Contains(module.ChargeId)), "three prepared Rorqual bursts must start inactive with free built-in effects and no consumable charges");

        OperationService.Start(save, "uitra-belt-1");
        save.Operation.RaidTimerSeconds = 99_999f;
        var command = save.Operation.Fleet.Find(member => member.ShipUid == rorqual.Uid);
        var recipient = save.Operation.Fleet.Find(member => member.PilotId == save.Characters[0].Id);
        Require(command != null && recipient != null, "burst smoke requires command ship and fleet recipient");
        recipient.X = command.X + 10_000f;
        recipient.Y = command.Y + 10_000f;
        recipient.Z = command.Z + 10_000f;
        Require(OperationService.ToggleBursts(save, rorqual.Uid, out _), "three free Rorqual bursts must activate without consumable charges");
        Require(save.Operation.BurstsActive && fittedBursts.All(module => module.Active && module.ChargeQuantity == 0), "immediate pulse must leave all free burst quantities unchanged");
        var beltFleet = save.Operation.Fleet.Where(member => save.Ships.Find(ship => ship.Uid == member.ShipUid)?.Location == ShipLocation.Belt).ToArray();
        Require(beltFleet.All(member => save.Operation.BurstEffects.Count(effect => effect.ShipUid == member.ShipUid) == 3), "one pulse must apply all three prepared effects to every belt fleet ship regardless of range");
        Require(save.Operation.BurstEffects.Where(effect => effect.ShipUid == recipient.ShipUid).All(effect => Math.Abs(effect.SecondsLeft - 90f) < .001f), "Command Burst Specialist V must extend serialized effects to 90 seconds");
        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        Require(restored.Operation.BurstEffects.Count(effect => effect.ShipUid == recipient.ShipUid) == 3, "burst recipients, strengths and durations must survive serialization");
        OperationService.Tick(save, 61f);
        Require(save.Operation.BurstsActive && fittedBursts.All(module => module.Active && module.ChargeQuantity == 0), "each 60-second burst cycle must repeat without consuming charges");
        Require(save.Operation.BurstEffects.Count(effect => effect.ShipUid == recipient.ShipUid) == 3 && save.Operation.BurstEffects.Where(effect => effect.ShipUid == recipient.ShipUid).All(effect => effect.SecondsLeft > 0), "an out-of-range fleet ship must receive every repeated global pulse");
        OperationService.Tick(save, 120f);
        Require(save.Operation.BurstsActive && save.Operation.BurstEffects.Count(effect => effect.ShipUid == recipient.ShipUid) == 3, "free global bursts must keep cycling after the original effect duration");
        Require(OperationService.ToggleBursts(save, rorqual.Uid, out _), "active free Rorqual bursts must toggle off explicitly");
        Require(!save.Operation.BurstsActive && fittedBursts.All(module => !module.Active && module.ChargeQuantity == 0), "toggling bursts off must stop modules without changing charge quantities");

        Require(OperationService.TryStop(save, out _), "burst fitting operation must stop before station transfers");

        OperationService.AddItem(save.StationInventory, "heavy-water", 30_000);
        Require(FittingService.TryTransferFuel(save, pilot.Id, rorqual.Uid, 1000, true, out var moved, out _) && Math.Abs(moved - 1000) < .001, "fuel +1000 must load exactly");
        RequireNearly(FittingService.StackQuantity(rorqual.FuelHold, "heavy-water"), 1000, .001d, "fuel hold +1000 result mismatch");
        Require(FittingService.TryTransferFuel(save, pilot.Id, rorqual.Uid, 1000, false, out moved, out _) && Math.Abs(moved - 1000) < .001, "fuel -1000 must unload exactly");
        RequireNearly(FittingService.StackQuantity(rorqual.FuelHold, "heavy-water"), 0, .001d, "fuel hold -1000 result mismatch");
        Require(FittingService.TryTransferFuel(save, pilot.Id, rorqual.Uid, double.PositiveInfinity, true, out moved, out _), "fuel load-all must succeed");
        RequireNearly(FittingService.StackQuantity(rorqual.FuelHold, "heavy-water"), rorqual == null ? 0 : Catalog.GetShip("rorqual").FuelHoldM3 / .4d, .001d, "fuel load-all must stop exactly at hold capacity");
        Require(FittingService.TryTransferFuel(save, pilot.Id, rorqual.Uid, double.PositiveInfinity, false, out _, out _), "fuel unload-all must succeed");

        OperationService.AddItem(save.StationInventory, "veldspar", 1000);
        RequireNearly(OperationService.ItemVolumeM3("mining-burst"), Catalog.GetModule("mining-burst").VolumeM3, .001d, "operation inventory volume must route through the shared catalog");
        Require(FittingService.TryTransferCargo(save, pilot.Id, rorqual.Uid, "veldspar", 100, true, out moved, out _) && Math.Abs(moved - 100) < .001, "station-to-cargo selected stack transfer must load exact quantity");
        RequireNearly(FittingService.StackQuantity(rorqual.CargoHold, "veldspar"), 100, .001d, "cargo load mismatch");
        Require(FittingService.TryTransferCargo(save, pilot.Id, rorqual.Uid, "veldspar", double.PositiveInfinity, false, out moved, out _) && Math.Abs(moved - 100) < .001, "cargo-to-station unload-all must transfer exact available amount");
        var before = JsonUtility.ToJson(save);
        Require(!FittingService.TryTransferCargo(save, pilot.Id, rorqual.Uid, "unknown-item", 1, true, out _, out _), "unknown packaged volume must fail closed");
        Require(JsonUtility.ToJson(save) == before, "failed unknown-volume cargo transfer must be atomic");
    }

    static void SmokePaidRepair()
    {
        var save = SaveService.NewGame();
        var pilot = save.Characters[0];
        var ship = save.Ships.Find(candidate => candidate.Uid == pilot.AssignedShipUid);
        var hull = Catalog.GetShip(ship.HullId);
        ship.ShieldHp -= 10;
        ship.ArmorHp -= 20;
        ship.StructureHp -= 30;
        save.Isk = 5_999;
        var before = JsonUtility.ToJson(save);
        Require(!FittingService.TryRepairShip(save, pilot.Id, ship.Uid, out _, out _), "repair must reject insufficient shared-wallet ISK");
        Require(JsonUtility.ToJson(save) == before, "failed repair must not modify wallet or HP");
        save.Isk = 10_000;
        Require(FittingService.TryRepairShip(save, pilot.Id, ship.Uid, out var cost, out _), "funded station repair must succeed");
        RequireNearly(cost, 6_000, .001d, "repair price must be exactly 100 ISK per restored HP");
        RequireNearly(save.Isk, 4_000, .001d, "repair must debit the shared wallet once");
        RequireNearly(ship.ShieldHp, PreparedPackageService.MaxShieldHp(ship, pilot), .001d, "paid repair must restore tank-adjusted shield HP");
        RequireNearly(ship.ArmorHp, hull.ArmorHp, .001d, "paid repair must restore armor HP");
        RequireNearly(ship.StructureHp, hull.StructureHp, .001d, "paid repair must restore structure HP");
    }

    static void SmokePerseveranceIceMining()
    {
        var clearIce = Catalog.GetOre("clear-icicle");
        var laser = Catalog.GetModule("ice-mining-laser-i");
        var hull = Catalog.GetShip("perseverance");
        Require(clearIce?.TypeId == 16262 && clearIce.Kind == ResourceKind.Ice, "Clear Icicle type/kind mismatch");
        RequireNearly(clearIce.UnitVolumeM3, 1000, .001d, "Clear Icicle block volume mismatch");
        Require(laser?.TypeId == 37450 && Catalog.GetModule("ice-mining-laser-ii")?.TypeId == 37451 && Catalog.GetModule("ore-ice-mining-laser")?.TypeId == 37452, "ice laser IDs mismatch");
        RequireNearly(laser.BaseYieldM3, 1000, .001d, "Ice Mining Laser I yield mismatch");
        RequireNearly(laser.CycleSeconds, 360, .001d, "Ice Mining Laser I cycle mismatch");
        RequireNearly(laser.RangeKm, 7, .001d, "Ice Mining Laser I range mismatch");
        RequireNearly(Catalog.GetModule("ice-mining-laser-ii").ResidueChance, .34, .001d, "Ice Mining Laser II residue mismatch");
        var iceHarvesterI = Catalog.GetModule("ice-harvester-i");
        var iceHarvesterII = Catalog.GetModule("ice-harvester-ii");
        Require(iceHarvesterI?.TypeId == 16278 && iceHarvesterII?.TypeId == 22229, "Ice Harvester IDs mismatch");
        Require(iceHarvesterI.Kind == ModuleKind.IceHarvester && iceHarvesterII.Kind == ModuleKind.IceHarvester, "barge ice module kind mismatch");
        RequireNearly(iceHarvesterI.BaseYieldM3, 1000, .001d, "Ice Harvester I yield mismatch");
        RequireNearly(iceHarvesterI.CycleSeconds, 240, .001d, "Ice Harvester I cycle mismatch");
        RequireNearly(iceHarvesterII.CycleSeconds, 200, .001d, "Ice Harvester II cycle mismatch");
        RequireNearly(iceHarvesterII.ResidueChance, .34, .001d, "Ice Harvester II residue mismatch");
        Require(Catalog.GetSkill("ice-harvesting").BookFallbackPrice == 400_000, "Ice Harvesting book fallback mismatch");
        Require(hull.TypeId == 91174 && hull.MiningHighSlots == 3 && hull.SupportsIceMiningLasers, "Perseverance identity/fitting mismatch");
        RequireNearly(hull.MiningHoldM3, 21000, .001d, "Perseverance mining hold mismatch");
        RequireNearly(hull.CargoHoldM3, 250, .001d, "Perseverance cargo hold mismatch");
        RequireNearly(hull.RoleYieldMultiplier, 1, .001d, "Perseverance must not receive an ordinary yield bonus");

        var iceLaserHullIds = new HashSet<string>(new[] { "prospect", "endurance", "perseverance" }, StringComparer.OrdinalIgnoreCase);
        var iceHarvesterHullIds = new HashSet<string>(new[] { "retriever", "procurer", "covetor", "mackinaw", "skiff", "hulk" }, StringComparer.OrdinalIgnoreCase);
        var expectedIceHullIds = new HashSet<string>(iceLaserHullIds, StringComparer.OrdinalIgnoreCase);
        expectedIceHullIds.UnionWith(iceHarvesterHullIds);
        var icePackages = Catalog.Packages.Where(package => package.Role == PreparedPackageRole.Ice).ToArray();
        Require(expectedIceHullIds.SetEquals(icePackages.Select(package => package.HullId)),
            "prepared ice packages must exist only for Prospect, Endurance, Perseverance, barges and exhumers");
        foreach (var iceLaserHullId in iceLaserHullIds)
        {
            var grades = icePackages.Where(package => package.HullId == iceLaserHullId).Select(package => package.Grade).ToArray();
            Require(grades.Length == 3 && new HashSet<PreparedPackageGrade>(grades).SetEquals(new[] { PreparedPackageGrade.T1, PreparedPackageGrade.T2, PreparedPackageGrade.ORE }),
                $"{iceLaserHullId} must expose exactly T1, T2 and ORE Ice Mining Laser packages");
        }
        foreach (var iceHarvesterHullId in iceHarvesterHullIds)
        {
            var grades = icePackages.Where(package => package.HullId == iceHarvesterHullId).Select(package => package.Grade).ToArray();
            Require(grades.Length == 2 && new HashSet<PreparedPackageGrade>(grades).SetEquals(new[] { PreparedPackageGrade.T1, PreparedPackageGrade.T2 }),
                $"{iceHarvesterHullId} must expose exactly T1 and T2 Ice Harvester packages");
        }
        foreach (var icePackageDefinition in icePackages)
        {
            var module = Catalog.GetModule(icePackageDefinition.MinerModuleId);
            var expectedKind = iceLaserHullIds.Contains(icePackageDefinition.HullId) ? ModuleKind.IceMiningLaser : ModuleKind.IceHarvester;
            Require(module?.Kind == expectedKind,
                $"{icePackageDefinition.Id} must use the extractor family supported by its hull");
        }
        foreach (var candidateHull in Catalog.Ships)
        {
            Require(FittingService.CanFitMiningModule(candidateHull, laser) == iceLaserHullIds.Contains(candidateHull.Id),
                $"Ice Mining Laser compatibility matrix mismatch for {candidateHull.Id}");
            Require(FittingService.CanFitMiningModule(candidateHull, iceHarvesterII) == iceHarvesterHullIds.Contains(candidateHull.Id),
                $"Ice Harvester compatibility matrix mismatch for {candidateHull.Id}");
        }

        var clearLocation = Catalog.GetLocation("manatirid-clear-icicle");
        Require(clearLocation?.SiteKind == MiningSiteKind.DynamicAnomaly && clearLocation.AnomalyInitialSpawnChance == 1f,
            "Clear Icicle must be a guaranteed initial anomaly");
        RequireNearly(clearLocation.AnomalyRespawnMinSeconds, 6 * 60 * 60, .001d, "Clear Icicle minimum respawn mismatch");
        RequireNearly(clearLocation.AnomalyRespawnMaxSeconds, 6 * 60 * 60, .001d, "Clear Icicle maximum respawn mismatch");
        var lifecycle = SaveService.NewGame();
        lifecycle.MiningSites.Clear();
        var initialNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MiningSiteService.Tick(lifecycle, initialNow);
        var clearState = lifecycle.MiningSites.Single(site => site.LocationId == clearLocation.Id);
        Require(clearState.Lifecycle == MiningSiteLifecycle.Available && MiningSiteService.RemainingM3(clearState.Asteroids) > 0,
            "Clear Icicle must be present immediately in a newly initialized world");
        lifecycle.Operation.Active = true;
        lifecycle.Operation.LocationId = clearLocation.Id;
        MiningSiteService.LoadIntoOperation(lifecycle, clearLocation);
        foreach (var chunk in lifecycle.Operation.Asteroids) chunk.RemainingUnits = 0;
        var depletionStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MiningSiteService.SnapshotActiveOperation(lifecycle);
        var depletionFinished = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var depletedSerial = clearState.InstanceSerial;
        Require(clearState.Lifecycle == MiningSiteLifecycle.Cooldown && clearState.Asteroids.Count == 0,
            "depleted Clear Icicle must leave the available anomaly list");
        Require(clearState.RespawnUnix >= depletionStarted + 6 * 60 * 60 && clearState.RespawnUnix <= depletionFinished + 6 * 60 * 60,
            "Clear Icicle depletion must schedule its next instance exactly six hours later");
        var clearRespawnUnix = clearState.RespawnUnix;
        MiningSiteService.Tick(lifecycle, clearRespawnUnix - 1);
        Require(clearState.Lifecycle == MiningSiteLifecycle.Cooldown && clearState.InstanceSerial == depletedSerial,
            "Clear Icicle must remain absent one second before its six-hour boundary");
        MiningSiteService.Tick(lifecycle, clearRespawnUnix);
        Require(clearState.Lifecycle == MiningSiteLifecycle.Available && clearState.InstanceSerial == depletedSerial + 1 && MiningSiteService.RemainingM3(clearState.Asteroids) > 0,
            "Clear Icicle must publish exactly one fresh instance at its six-hour boundary");

        var hulk = Catalog.GetShip("hulk");
        var hulkPilot = new CharacterSave { Id = "ice-hulk-pilot", Name = "Ice Hulk pilot" };
        GrantAllSkills(hulkPilot);
        Require(FittingService.CanFitMiningModule(hulk, iceHarvesterII), "Hulk must fit Ice Harvester II");
        Require(!FittingService.CanFitMiningModule(hull, iceHarvesterII), "Perseverance must reject barge Ice Harvester");
        RequireNearly(OperationService.MiningRangeKm(hulkPilot, hulk, iceHarvesterII), 13d, .001d, "Hulk Ice Harvester range mismatch");
        RequireNearly(OperationService.MiningCycleSeconds(hulkPilot, hulk, iceHarvesterII, SaveService.NewGame(), null), 67.83d, .01d, "perfect Hulk Ice Harvester II cycle mismatch");

        var save = SaveService.NewGame();
        foreach (var candidate in save.Characters) candidate.DeployOnLaunch = false;
        var pilot = save.Characters[9]; GrantAllSkills(pilot);
        SetSkillLevel(pilot, "mining-destroyer", 2); SetSkillLevel(pilot, "ice-harvesting", 1);
        SetSkillLevel(pilot, "mining-precision", 0); SetSkillLevel(pilot, "mining-exploitation", 0);
        var ship = SaveService.CreateShip("smoke-perseverance", "perseverance");
        var icePackage = Catalog.GetPackage("perseverance-ice-t1");
        Require(icePackage != null, "Perseverance must expose a prepared ice T1 package");
        PreparedPackageService.ApplyLockedFit(ship, icePackage);
        save.Ships.Add(ship); pilot.AssignedShipUid = ship.Uid; pilot.DeployOnLaunch = true;
        Require(ship.Modules.Count(module => module.ModuleId == "ice-mining-laser-i") == 3, "Perseverance ice T1 package must materialize all three extractors");
        Require(!OperationService.CanMineResource(ship, "veldspar"), "ice laser must reject ore");
        Require(!OperationService.CanMineResource(save.Ships[0], "clear-icicle"), "ore miner must reject ice");

        OperationService.Start(save, "manatirid-clear-icicle");
        Require(save.Operation.Fleet.Count == 1 && ship.Location == ShipLocation.Belt, "Perseverance must launch into Manatirid");
        Require(save.Operation.Asteroids.Count == 24 && save.Operation.Asteroids.All(a => a.OreId == "clear-icicle" && a.RemainingUnits >= 80 && a.RemainingUnits <= 120), "approximate ice chunks mismatch");
        save.Operation.RaidTimerSeconds = 99999;
        var member = save.Operation.Fleet[0]; var asteroid = save.Operation.Asteroids[0];
        Require(OperationService.AssignTarget(save, ship.Uid, asteroid.Id), "Perseverance must target Clear Icicle");
        member.X = asteroid.X; member.Y = asteroid.Y; member.Z = asteroid.Z;
        RequireNearly(OperationService.MiningCriticalChance(pilot, hull, laser), .024d, .000001d, "Perseverance critical chance mismatch");
        RequireNearly(OperationService.MiningCriticalBonusYield(pilot, hull, laser), 2.2d, .000001d, "Perseverance critical bonus yield mismatch");
        RequireNearly(OperationService.MiningRangeKm(pilot, hull, laser), 9.8d, .0001d, "Perseverance range mismatch");
        var cycle = OperationService.MiningCycleSeconds(pilot, hull, laser, save, member);
        RequireNearly(cycle, 324.9d, .001d, "Ice Harvesting I plus perfect Yeti implant cycle mismatch");
        OperationService.Tick(save, cycle + .01f);
        Require(OperationService.ItemQuantity(ship.MiningHold, "clear-icicle") >= 3, "three ice lasers must deliver blocks");
        Require(OperationService.HoldVolume(ship.MiningHold) <= 21000.001, "ice must respect mining hold capacity");

        var commandPilot = save.Characters[8];
        GrantAllSkills(commandPilot);
        var commandShip = SaveService.CreateShip("smoke-atomic-warp-outrider", "outrider");
        PreparedPackageService.ApplyLockedFit(commandShip, Catalog.GetPackage("outrider-booster-t2"));
        save.Ships.Add(commandShip);
        commandPilot.AssignedShipUid = commandShip.Uid;
        Require(OperationService.Join(save, commandPilot.Id), "command ship must join the Clear Icicle operation through canonical location compatibility");
        var ordinaryDestination = Catalog.GetLocation("manatirid-belt-1");
        Require(OperationService.LocationSupportsShip(ordinaryDestination, commandShip, Catalog.GetShip(commandShip.HullId)) &&
                !OperationService.LocationSupportsShip(ordinaryDestination, ship, hull),
            "atomic local-warp fixture requires one compatible command ship and one incompatible ice-only ship");
        Require(MiningSiteService.IsAvailable(save, "manatirid-belt-1"), "atomic local-warp fixture requires the ordinary Manatirid belt");
        var operationBeforeRejectedWarp = JsonUtility.ToJson(save.Operation);
        var shipBeforeRejectedWarp = JsonUtility.ToJson(ship);
        var commandShipBeforeRejectedWarp = JsonUtility.ToJson(commandShip);
        var sourceSiteBeforeRejectedWarp = JsonUtility.ToJson(save.MiningSites.Single(site => site.LocationId == "manatirid-clear-icicle"));
        Require(!TravelService.TryStartSameSystemBeltWarp(save, "manatirid-belt-1", out var incompatibleWarpMessage) &&
                incompatibleWarpMessage.Contains("не подходит"),
            "an ice-only fleet must reject a same-system warp to an ordinary ore belt");
        Require(JsonUtility.ToJson(save.Operation) == operationBeforeRejectedWarp && JsonUtility.ToJson(ship) == shipBeforeRejectedWarp &&
                JsonUtility.ToJson(commandShip) == commandShipBeforeRejectedWarp &&
                JsonUtility.ToJson(save.MiningSites.Single(site => site.LocationId == "manatirid-clear-icicle")) == sourceSiteBeforeRejectedWarp,
            "incompatible local warp rejection must be atomic: no snapshot, transit, target or cycle mutation");

        OperationService.ReturnShip(save, ship.Uid, true);
        OperationService.Tick(save, OperationService.WarpAlignSeconds + OperationService.WarpTransitSeconds + OperationService.WarpInSeconds + .01f);
        Require(ship.Location == ShipLocation.Belt && ship.MiningHold.Count == 0 && OperationService.ItemQuantity(save.StationInventory, "clear-icicle") >= 3, "ice unload must reach common warehouse and finish its return warp");
        RequireNearly(MarketService.OreBuyPerUnit(save, clearIce), 186_000d, .001d, "Clear Icicle Jita fallback mismatch");
    }

    static void SmokeGasMining()
    {
        void RequireGasExtractor(string moduleId, int typeId, ModuleKind kind, float yieldM3, float cycleSeconds, float residueChance)
        {
            var module = Catalog.GetModule(moduleId);
            Require(module?.TypeId == typeId && module.Kind == kind, $"{moduleId} identity/kind mismatch");
            RequireNearly(module.BaseYieldM3, yieldM3, .0001d, $"{moduleId} yield mismatch");
            RequireNearly(module.CycleSeconds, cycleSeconds, .0001d, $"{moduleId} cycle mismatch");
            RequireNearly(module.ResidueChance, residueChance, .0001d, $"{moduleId} residue mismatch");
        }

        RequireGasExtractor("gas-cloud-scoop-i", 25266, ModuleKind.GasCloudScoop, 10, 30, 0);
        RequireGasExtractor("gas-cloud-scoop-ii", 25812, ModuleKind.GasCloudScoop, 20, 40, .34f);
        RequireGasExtractor("syndicate-gas-cloud-scoop", 28788, ModuleKind.GasCloudScoop, 20, 30, 0);
        RequireGasExtractor("gas-cloud-harvester-i", 60313, ModuleKind.GasCloudHarvester, 50, 100, 0);
        RequireGasExtractor("gas-cloud-harvester-ii", 60314, ModuleKind.GasCloudHarvester, 100, 80, .34f);
        RequireGasExtractor("ore-gas-cloud-harvester", 60315, ModuleKind.GasCloudHarvester, 100, 80, 0);

        foreach (var pioneerHullId in new[] { "pioneer", "pioneer-consortium" })
        {
            var t1Package = Catalog.GetPackage($"{pioneerHullId}-gas-t1");
            var t2Package = Catalog.GetPackage($"{pioneerHullId}-gas-t2");
            Require(t1Package != null && t2Package != null, $"{pioneerHullId} gas packages are missing");
            Require(PreparedPackageService.RequiredSkills(t1Package).Single(requirement => requirement.SkillId == "gas-cloud-harvesting").Level == 3,
                $"{pioneerHullId} with three Gas Cloud Scoops must require Gas Cloud Harvesting III");
            Require(PreparedPackageService.RequiredSkills(t2Package).Single(requirement => requirement.SkillId == "gas-cloud-harvesting").Level == 5,
                $"{pioneerHullId} T2 scoop requirement must remain Gas Cloud Harvesting V");
            var fittedPioneer = SaveService.CreateShip($"smoke-{pioneerHullId}-gas", pioneerHullId);
            PreparedPackageService.ApplyLockedFit(fittedPioneer, t1Package);
            Require(fittedPioneer.Modules.Count(module => module.ModuleId == "gas-cloud-scoop-i") == 3,
                $"{pioneerHullId} gas package must materialize all three scoops");
        }

        var skilledGasPilot = new CharacterSave { Id = "gas-cycle-pilot", Name = "Gas cycle pilot" };
        GrantAllSkills(skilledGasPilot);
        var t2Harvester = Catalog.GetModule("gas-cloud-harvester-ii");
        var gasCycleFixture = SaveService.NewGame();
        foreach (var expected in new[]
                 {
                     (HullId: "retriever", Seconds: 63d),
                     (HullId: "procurer", Seconds: 72d),
                     (HullId: "covetor", Seconds: 47.6d),
                     (HullId: "mackinaw", Seconds: 50.575d),
                     (HullId: "skiff", Seconds: 68d),
                     (HullId: "hulk", Seconds: 40.46d)
                 })
        {
            var gasHull = Catalog.GetShip(expected.HullId);
            Require(FittingService.CanFitMiningModule(gasHull, t2Harvester), $"{expected.HullId} must fit Gas Cloud Harvester II");
            RequireNearly(OperationService.MiningCycleSeconds(skilledGasPilot, gasHull, t2Harvester, gasCycleFixture, null), expected.Seconds, .001d,
                $"skilled {expected.HullId} Gas Cloud Harvester II cycle mismatch");
        }

        var save = SaveService.NewGame();
        foreach (var candidate in save.Characters) candidate.DeployOnLaunch = false;
        var pilot = save.Characters[9];
        GrantAllSkills(pilot);
        var hulk = SaveService.CreateShip("smoke-hulk-gas", "hulk");
        var hulkGasPackage = Catalog.GetPackage("hulk-gas-t2");
        Require(hulkGasPackage != null, "Hulk gas T2 package is missing");
        PreparedPackageService.ApplyLockedFit(hulk, hulkGasPackage);
        save.Ships.Add(hulk);
        pilot.AssignedShipUid = hulk.Uid;
        pilot.DeployOnLaunch = true;

        OperationService.Start(save, "wspace-c1-barren-reservoir");
        Require(save.Operation.Active && save.Operation.Fleet.Count == 1 && hulk.Location == ShipLocation.Belt,
            "gas-capable Hulk must enter the available Barren Perimeter Reservoir anomaly");
        Require(save.Operation.Asteroids.Count == 2 && save.Operation.Asteroids.All(cloud => Catalog.GetOre(cloud.OreId)?.Kind == ResourceKind.Gas),
            "Barren Perimeter Reservoir must materialize its two persistent gas clouds");
        save.Operation.RaidTimerSeconds = 99999;
        var member = save.Operation.Fleet.Single();
        var cloud = save.Operation.Asteroids.First(candidate => candidate.OreId == "fullerite-c50");
        Require(OperationService.AssignTarget(save, hulk.Uid, cloud.Id), "gas Hulk must accept a Fullerite-C50 cloud target");
        member.X = cloud.X;
        member.Y = cloud.Y;
        member.Z = cloud.Z;

        var hulkDefinition = Catalog.GetShip("hulk");
        var baseCycle = OperationService.MiningCycleSeconds(pilot, hulkDefinition, t2Harvester, save, member);
        var combinedYield = OperationService.MiningYieldM3(pilot, hulkDefinition, t2Harvester, save, member);
        RequireNearly(baseCycle, 80d * .70d * .85d * .85d, .001d,
            "skilled Hulk gas cycle must apply role, Mining Barge V and Exhumers V reductions");
        RequireNearly(combinedYield, 200d, .001d, "two Hulk T2 gas harvesters must yield 200 m3 per shared cycle");
        RequireNearly(combinedYield / baseCycle, 4.94315d, .0001d, "skilled Hulk T2 gas rate mismatch");
        RequireNearly(OperationService.MiningRangeKm(pilot, hulkDefinition, t2Harvester), 1.5d, .0001d,
            "gas extractor range must not inherit the Hulk ore-range hull bonus");

        save.Operation.BurstEffects.Add(new BurstRecipientEffectSave
        {
            ShipUid = hulk.Uid,
            SourceShipUid = "smoke-gas-booster",
            ChargeId = "mining-laser-optimization-charge",
            Strength = 1f,
            SecondsLeft = 60f
        });
        RequireNearly(OperationService.MiningCycleSeconds(pilot, hulkDefinition, t2Harvester, save, member), baseCycle * .85d, .001d,
            "Mining Laser Optimization must reduce a gas extractor cycle");
        save.Operation.BurstEffects.Clear();
        save.Operation.BurstEffects.Add(new BurstRecipientEffectSave
        {
            ShipUid = hulk.Uid,
            SourceShipUid = "smoke-gas-booster",
            ChargeId = "mining-laser-efficiency-charge",
            Strength = 1f,
            SecondsLeft = 60f
        });
        RequireNearly(OperationService.MiningCriticalChance(pilot, hulkDefinition, t2Harvester, save, member), 0d, .000001d,
            "gas extraction must never gain a critical chance from the efficiency burst");
        var baseResidueChance = OperationService.MiningResidueChance(t2Harvester, null, 0f);
        RequireNearly(baseResidueChance, .34d, .000001d, "Gas Cloud Harvester II base residue chance mismatch");
        RequireNearly(OperationService.MiningResidueChance(t2Harvester, null, 1f), baseResidueChance * .85d, .000001d,
            "efficiency burst must reduce gas residue by 15 percent relative per strength");
        RequireNearly(OperationService.MiningResidueChance(t2Harvester, null, 2f), baseResidueChance * .70d, .000001d,
            "stacked gas residue reduction must remain relative to the module base chance");
        save.Operation.BurstEffects.Clear();

        var cloudUnitsBefore = cloud.RemainingUnits;
        OperationService.Tick(save, baseCycle + .01f);
        RequireNearly(OperationService.ItemQuantity(hulk.MiningHold, cloud.OreId), 200d, .000001d,
            "one shared Hulk gas cycle must deliver both T2 harvesters into the mining hold");
        Require(cloud.RemainingUnits <= cloudUnitsBefore - 200d,
            "completed gas extractor cycles must deplete the targeted persistent cloud");
        Require(member.MiningCycles.Count == 2 && member.MiningCycles.All(cycle => cycle.ProgressSeconds > 0),
            "each fitted gas harvester must persist its own cycle progress under the shared target");

        var persistedHold = OperationService.ItemQuantity(hulk.MiningHold, cloud.OreId);
        var persistedCloudUnits = cloud.RemainingUnits;
        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        var restoredHulk = restored.Ships.Single(candidate => candidate.Uid == hulk.Uid);
        var restoredMember = restored.Operation.Fleet.Single(candidate => candidate.ShipUid == hulk.Uid);
        var restoredCloud = restored.Operation.Asteroids.Single(candidate => candidate.Id == cloud.Id);
        var restoredSiteCloud = restored.MiningSites.Single(site => site.LocationId == "wspace-c1-barren-reservoir").Asteroids.Single(candidate => candidate.Id == cloud.Id);
        Require(restored.Operation.Active && restoredMember.TargetAsteroidId == cloud.Id && restoredMember.MiningCycles.Count == 2,
            "gas anomaly, target and extractor cycles must survive JSON persistence");
        RequireNearly(OperationService.ItemQuantity(restoredHulk.MiningHold, cloud.OreId), persistedHold, .000001d,
            "mined gas hold quantity must survive JSON persistence");
        RequireNearly(restoredCloud.RemainingUnits, persistedCloudUnits, .000001d,
            "partially depleted gas cloud must survive JSON persistence");
        RequireNearly(restoredSiteCloud.RemainingUnits, persistedCloudUnits, .000001d,
            "partially depleted gas cloud must persist in the anomaly world state as well as the active operation");

        var restoredPilot = restored.Characters.Single(candidate => candidate.Id == pilot.Id);
        var restoredHull = Catalog.GetShip(restoredHulk.HullId);
        var freeHoldM3 = OperationService.MiningHoldCapacity(restoredPilot, restoredHull) - OperationService.HoldVolume(restoredHulk.MiningHold);
        OperationService.AddItem(restoredHulk.MiningHold, cloud.OreId, Math.Floor(freeHoldM3 / Catalog.GetOre(cloud.OreId).UnitVolumeM3));
        Require(OperationService.MiningHoldCapacity(restoredPilot, restoredHull) - OperationService.HoldVolume(restoredHulk.MiningHold) < Catalog.GetOre(cloud.OreId).UnitVolumeM3,
            "gas auto-unload fixture must fill the mining hold below one cloud unit of free space");
        var gasToUnload = OperationService.ItemQuantity(restoredHulk.MiningHold, cloud.OreId);
        var stationGasBefore = OperationService.ItemQuantity(restored.StationInventory, cloud.OreId);
        restored.Operation.AutoUnload = true;
        OperationService.Tick(restored, .01f);
        Require(restoredHulk.Location == ShipLocation.Transit && restoredMember.Order == FleetOrder.UnloadAndReturn && restoredMember.WarpPhase == FleetWarpPhase.AligningOut,
            "a full gas hold with auto-unload enabled must begin the persisted return-warp lifecycle");

        var inTransit = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(restored));
        var inTransitHulk = inTransit.Ships.Single(candidate => candidate.Uid == hulk.Uid);
        Require(inTransitHulk.Location == ShipLocation.Transit && OperationService.ItemQuantity(inTransitHulk.MiningHold, cloud.OreId) == gasToUnload,
            "gas and its active auto-unload warp must survive JSON persistence before docking");
        OperationService.Tick(inTransit, OperationService.WarpAlignSeconds + OperationService.WarpTransitSeconds + OperationService.WarpInSeconds + .01f);
        Require(inTransitHulk.Location == ShipLocation.Belt && inTransitHulk.MiningHold.Count == 0,
            "gas auto-unload must empty the mining hold and return the Hulk to its anomaly");
        RequireNearly(OperationService.ItemQuantity(inTransit.StationInventory, cloud.OreId), stationGasBefore + gasToUnload, .000001d,
            "gas auto-unload must transfer the exact mined quantity into the common warehouse");
        var durable = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(inTransit));
        RequireNearly(OperationService.ItemQuantity(durable.StationInventory, cloud.OreId), stationGasBefore + gasToUnload, .000001d,
            "warehouse gas delivered by auto-unload must survive a subsequent save/load");
    }

    static void SmokeSameSystemBeltWarp()
    {
        var save = NewSinglePilotOperation("uitra-belt-1");
        var member = save.Operation.Fleet.Single();
        var ship = save.Ships.Single(candidate => candidate.Uid == member.ShipUid);
        OperationService.AddItem(ship.MiningHold, "veldspar", 321);
        ship.ShieldHp = Math.Max(1f, ship.ShieldHp - 17f);
        var holdBefore = OperationService.ItemQuantity(ship.MiningHold, "veldspar");
        var stationBefore = OperationService.ItemQuantity(save.StationInventory, "veldspar");
        var shieldBefore = ship.ShieldHp;
        var rosterBefore = save.Operation.Fleet.Select(candidate => candidate.ShipUid).ToArray();

        Require(TravelService.TryStartSameSystemBeltWarp(save, "uitra-belt-2", out _),
            "an active fleet entirely in belt must be able to start a same-system belt warp");
        Require(save.Operation.Active && save.Operation.LocationId == "uitra-belt-1" && save.Operation.BeltWarpActive &&
                save.Operation.BeltWarpDestinationLocationId == "uitra-belt-2" && ship.Location == ShipLocation.Transit,
            "local warp must retain the source operation until arrival and expose a persisted countdown");
        RequireNearly(TravelService.BeltWarpProgress01(save.Operation), 0d, .000001d,
            "a newly started local warp must begin at zero progress");

        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        var restoredShip = restored.Ships.Single(candidate => candidate.Uid == ship.Uid);
        Require(restored.Operation.BeltWarpActive && restored.Operation.BeltWarpSecondsLeft > 0 &&
                restored.Operation.Fleet.Select(candidate => candidate.ShipUid).SequenceEqual(rosterBefore),
            "local destination, ETA and exact roster must survive a JSON round trip");
        var halfWarp = restored.Operation.BeltWarpTotalSeconds * .5f;
        Require(!TravelService.Tick(restored, halfWarp, out _), "local warp must not complete at half its countdown");
        RequireNearly(TravelService.BeltWarpProgress01(restored.Operation), .5d, .0001d,
            "local-warp progress must derive from its persisted total and remaining seconds");
        RequireNearly(OperationService.ItemQuantity(restoredShip.MiningHold, "veldspar"), holdBefore, .000001d,
            "same-system warp must not unload the mining hold in transit");
        RequireNearly(restoredShip.ShieldHp, shieldBefore, .000001d,
            "same-system warp must not repair or damage the ship in transit");
        Require(TravelService.Tick(restored, halfWarp + .01f, out _), "the remaining local-warp countdown must complete deterministically");
        Require(!restored.Operation.BeltWarpActive && restored.Operation.Active && restored.Operation.LocationId == "uitra-belt-2" && restoredShip.Location == ShipLocation.Belt,
            "local-warp completion must move the same active operation and fleet into the destination belt");
        Require(restored.Operation.Fleet.Select(candidate => candidate.ShipUid).SequenceEqual(rosterBefore),
            "same-system arrival must preserve the exact fleet roster and ordering");
        RequireNearly(OperationService.ItemQuantity(restoredShip.MiningHold, "veldspar"), holdBefore, .000001d,
            "same-system arrival must preserve the mining hold without a hidden station unload");
        RequireNearly(OperationService.ItemQuantity(restored.StationInventory, "veldspar"), stationBefore, .000001d,
            "same-system arrival must not transfer ore to the warehouse");
        RequireNearly(restoredShip.ShieldHp, shieldBefore, .000001d,
            "same-system arrival must preserve exact ship HP");

        var coreBlocked = NewSinglePilotOperation("uitra-belt-1");
        coreBlocked.Operation.IndustrialCoreActive = true;
        coreBlocked.Operation.IndustrialCoreSecondsLeft = 42f;
        Require(!TravelService.TryStartSameSystemBeltWarp(coreBlocked, "uitra-belt-2", out var coreMessage) &&
                coreMessage.Contains("Industrial Core") && !coreBlocked.Operation.BeltWarpActive,
            "active Industrial Core must delay a same-system belt change until its cycle boundary");

        var localRace = NewSinglePilotOperation("uitra-belt-1");
        var localRaceShip = localRace.Ships.Single(candidate => candidate.Uid == localRace.Operation.Fleet[0].ShipUid);
        OperationService.AddItem(localRaceShip.MiningHold, "veldspar", 77);
        Require(TravelService.TryStartSameSystemBeltWarp(localRace, "uitra-belt-2", out _),
            "local availability-race fixture must enter warp while the destination exists");
        var disappearingBelt = MiningSiteService.GetState(localRace, "uitra-belt-2");
        disappearingBelt.Lifecycle = MiningSiteLifecycle.Cooldown;
        disappearingBelt.NextRefreshUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
        foreach (var asteroid in disappearingBelt.Asteroids) asteroid.RemainingUnits = 0;
        Require(TravelService.Tick(localRace, 999f, out _),
            "local warp must resolve cleanly when availability changes during flight");
        Require(localRace.Operation.Active && localRace.Operation.LocationId == "uitra-belt-1" && !localRace.Operation.BeltWarpActive && localRaceShip.Location == ShipLocation.Belt,
            "a vanished local destination must return the fleet to its source operation");
        RequireNearly(OperationService.ItemQuantity(localRaceShip.MiningHold, "veldspar"), 77d, .000001d,
            "local availability-race recovery must not unload the fleet");
    }

    static void SmokeManualSecurityFloor()
    {
        const long nowUnix = 4_089_765_600;
        Require(MiningSiteService.SecurityTierTenths(.5485263f) == 5 &&
                MiningSiteService.SecurityTierTenths(.552350f) == 6 &&
                MiningSiteService.SecurityTierTenths(.55f) == 6,
            "security tiers must use displayed tenths with midpoint rounding away from zero");

        var manual = NewSinglePilotOperation("uitra-belt-1", nowUnix);
        manual.Operation.AutoSecurityFloorTenths = 6;
        manual.Operation.AutoSecurityFloorInitialized = true;
        var manualShip = manual.Ships.Single(candidate => candidate.Uid == manual.Operation.Fleet.Single().ShipUid);
        var operationBeforeRejectedAutomaticTravel = JsonUtility.ToJson(manual.Operation);
        Require(!TravelService.TryStartAutomaticBeltTravel(manual, "manatirid-belt-1", out var floorMessage, nowUnix) &&
                floorMessage.Contains("security") && JsonUtility.ToJson(manual.Operation) == operationBeforeRejectedAutomaticTravel &&
                manualShip.Location == ShipLocation.Belt,
            "the public automatic-travel API must atomically reject a destination below the committed security floor");

        Require(TravelService.TryStart(manual, "manatirid-belt-1", out _, nowUnix) &&
                manual.Operation.TravelActive && !manual.Operation.TravelIsAutomatic,
            "manual travel must remain free to enter a lower-security system");
        Require(manual.Operation.AutoSecurityFloorInitialized && manual.Operation.AutoSecurityFloorTenths == 6,
            "starting manual travel must not commit its destination security before arrival");
        var halfTravel = manual.Operation.TravelTotalSeconds * .5f;
        Require(!TravelService.Tick(manual, halfTravel, out _, nowUnix + 1) && manual.Operation.AutoSecurityFloorTenths == 6,
            "an in-flight manual route must retain its previously committed security floor");
        Require(TravelService.Tick(manual, halfTravel + .01f, out _, nowUnix + 2) &&
                manual.Operation.Active && manual.Operation.LocationId == "manatirid-belt-1" &&
                manual.Operation.AutoSecurityFloorInitialized && manual.Operation.AutoSecurityFloorTenths == 5,
            "a successful manual arrival must commit the destination displayed security as the new automation floor");

        var failedArrival = NewSinglePilotOperation("uitra-belt-1", nowUnix);
        failedArrival.Operation.AutoSecurityFloorTenths = 8;
        failedArrival.Operation.AutoSecurityFloorInitialized = true;
        Require(TravelService.TryStart(failedArrival, "manatirid-belt-1", out _, nowUnix),
            "failed-arrival fixture must begin an otherwise valid manual route");
        failedArrival.MiningSites.RemoveAll(state => string.Equals(state.LocationId, "manatirid-belt-1", StringComparison.OrdinalIgnoreCase));
        failedArrival.MiningSites.Add(new MiningSiteStateSave
        {
            LocationId = "manatirid-belt-1",
            Lifecycle = MiningSiteLifecycle.Cooldown,
            InstanceSerial = 1,
            LastRefreshUnix = nowUnix - 60,
            NextRefreshUnix = nowUnix + 3600,
            Asteroids = new List<AsteroidSave>()
        });
        Require(TravelService.Tick(failedArrival, 999f, out _, nowUnix + 1) &&
                !failedArrival.Operation.TravelActive && !failedArrival.Operation.Active &&
                failedArrival.Operation.AutoSecurityFloorInitialized && failedArrival.Operation.AutoSecurityFloorTenths == 8,
            "a destination that disappears before manual arrival must not commit a new security floor");
    }

    static void SmokeGlobalAutomaticBeltRoute()
    {
        const long nowUnix = 4_089_765_600;
        var save = NewSinglePilotOperation("manatirid-belt-1", nowUnix);
        save.Operation.AutoNextBelt = true;
        save.Operation.AutoSecurityFloorTenths = 5;
        save.Operation.AutoSecurityFloorInitialized = true;
        var expected = ExpectedAutomaticBeltByContract(save);
        Require(expected != null && MiningSiteService.SecurityTierTenths(expected) == 5 &&
                expected.SystemId != Catalog.GetLocation(save.Operation.LocationId).SystemId,
            "the global route fixture must choose the first compatible 0.5 belt in another system");
        Require(MiningSiteService.TryGetNextAutomaticBelt(save, out var selected, nowUnix) && selected.Id == expected.Id &&
                selected.SiteKind == MiningSiteKind.StaticBelt,
            "auto-next must exclude anomalies and follow tier, system name/id and belt name/id ordering globally");

        var rosterBefore = save.Operation.Fleet.Select(member => member.ShipUid).ToArray();
        var ship = save.Ships.Single(candidate => candidate.Uid == rosterBefore[0]);
        OperationService.AddItem(ship.MiningHold, "veldspar", 321d);
        OperationService.AddItem(ship.CargoHold, "pyroxeres", 17d);
        OperationService.AddItem(ship.FuelHold, "heavy-water", 9d);
        ship.ShieldHp = Math.Max(1f, ship.ShieldHp - 17f);
        ship.ArmorHp = Math.Max(1f, ship.ArmorHp - 11f);
        ship.StructureHp = Math.Max(1f, ship.StructureHp - 5f);
        var miningHoldBefore = OperationService.ItemQuantity(ship.MiningHold, "veldspar");
        var cargoHoldBefore = OperationService.ItemQuantity(ship.CargoHold, "pyroxeres");
        var fuelHoldBefore = OperationService.ItemQuantity(ship.FuelHold, "heavy-water");
        var stationBefore = OperationService.ItemQuantity(save.StationInventory, "veldspar");
        var shieldBefore = ship.ShieldHp;
        var armorBefore = ship.ArmorHp;
        var structureBefore = ship.StructureHp;
        foreach (var pilot in save.Characters) pilot.DeployOnLaunch = false;
        foreach (var asteroid in save.Operation.Asteroids) asteroid.RemainingUnits = 0;

        OperationService.Tick(save, .01f, null, nowUnix);
        Require(save.Operation.TravelActive && save.Operation.TravelIsAutomatic && !save.Operation.BeltWarpActive &&
                save.Operation.TravelSourceLocationId == "manatirid-belt-1" &&
                save.Operation.TravelDestinationLocationId == expected.Id && ship.Location == ShipLocation.Transit,
            "depletion must begin a persisted automatic cross-system route without consulting DeployOnLaunch");
        RequireNearly(save.Operation.TravelTotalSeconds,
            TravelService.EstimateSeconds(Catalog.GetLocation("manatirid-belt-1"), expected), .000001d,
            "automatic cross-system travel must use the canonical estimated duration");
        Require(save.Operation.Fleet.Select(member => member.ShipUid).SequenceEqual(rosterBefore),
            "automatic cross-system departure must preserve the exact active roster and ordering");
        RequireNearly(OperationService.ItemQuantity(ship.MiningHold, "veldspar"), miningHoldBefore, .000001d,
            "automatic cross-system departure must not unload the mining hold");
        RequireNearly(OperationService.ItemQuantity(ship.CargoHold, "pyroxeres"), cargoHoldBefore, .000001d,
            "automatic cross-system departure must preserve cargo");
        RequireNearly(OperationService.ItemQuantity(ship.FuelHold, "heavy-water"), fuelHoldBefore, .000001d,
            "automatic cross-system departure must preserve the fuel hold");

        var travelSeconds = save.Operation.TravelTotalSeconds;
        Require(TravelService.Tick(save, travelSeconds + .01f, out _, nowUnix + (long)Math.Ceiling(travelSeconds)) &&
                !save.Operation.TravelActive && save.Operation.Active && save.Operation.LocationId == expected.Id &&
                ship.Location == ShipLocation.Belt,
            "automatic cross-system arrival must move the same active operation directly into the selected belt");
        Require(save.Operation.Fleet.Select(member => member.ShipUid).SequenceEqual(rosterBefore) &&
                save.Operation.AutoSecurityFloorTenths == 5,
            "automatic arrival must preserve roster ordering and must never change the manually committed floor");
        RequireNearly(OperationService.ItemQuantity(ship.MiningHold, "veldspar"), miningHoldBefore, .000001d,
            "automatic arrival must preserve the mining hold");
        RequireNearly(OperationService.ItemQuantity(ship.CargoHold, "pyroxeres"), cargoHoldBefore, .000001d,
            "automatic arrival must preserve cargo");
        RequireNearly(OperationService.ItemQuantity(ship.FuelHold, "heavy-water"), fuelHoldBefore, .000001d,
            "automatic arrival must preserve the fuel hold");
        RequireNearly(OperationService.ItemQuantity(save.StationInventory, "veldspar"), stationBefore, .000001d,
            "automatic travel must not perform a hidden warehouse unload or sale");
        RequireNearly(ship.ShieldHp, shieldBefore, .000001d, "automatic travel must preserve shield HP");
        RequireNearly(ship.ArmorHp, armorBefore, .000001d, "automatic travel must preserve armor HP");
        RequireNearly(ship.StructureHp, structureBefore, .000001d, "automatic travel must preserve structure HP");

        var fleetAway = NewSinglePilotOperation("manatirid-belt-1", nowUnix);
        fleetAway.Operation.AutoNextBelt = true;
        fleetAway.Operation.AutoSecurityFloorTenths = 5;
        fleetAway.Operation.AutoSecurityFloorInitialized = true;
        var awayMember = fleetAway.Operation.Fleet.Single();
        var awayShip = fleetAway.Ships.Single(candidate => candidate.Uid == awayMember.ShipUid);
        OperationService.ReturnShip(fleetAway, awayShip.Uid, true);
        foreach (var asteroid in fleetAway.Operation.Asteroids) asteroid.RemainingUnits = 0;
        Require(awayMember.Order == FleetOrder.UnloadAndReturn && awayShip.Location == ShipLocation.Transit,
            "global auto-next waiting fixture must enter the canonical unload-return warp");
        OperationService.Tick(fleetAway, .01f, null, nowUnix);
        Require(!fleetAway.Operation.TravelActive && !fleetAway.Operation.BeltWarpActive &&
                fleetAway.Operation.LocationId == "manatirid-belt-1",
            "global auto-next must wait until every active fleet member is back in the belt");
    }

    static void SmokeAutomaticRouteDowntimeRestart()
    {
        var beforeDowntime = new DateTimeOffset(2099, 8, 13, 10, 59, 50, TimeSpan.Zero).ToUnixTimeSeconds();
        var afterDowntime = beforeDowntime + 20;
        var save = NewSinglePilotOperation("uitra-belt-1", beforeDowntime);
        save.Operation.AutoNextBelt = true;
        save.Operation.AutoSecurityFloorTenths = 5;
        save.Operation.AutoSecurityFloorInitialized = true;
        foreach (var asteroid in save.Operation.Asteroids) asteroid.RemainingUnits = 0;
        MiningSiteService.SnapshotActiveOperation(save, beforeDowntime);
        var sourceState = save.MiningSites.Single(state => state.LocationId == "uitra-belt-1");
        var sourceSerial = sourceState.InstanceSerial;
        var operationSerial = save.Operation.SiteInstanceSerial;

        MiningSiteService.Tick(save, afterDowntime);
        var expected = ExpectedAutomaticBeltByContract(save, true);
        Require(expected != null && MiningSiteService.SecurityTierTenths(expected) == 5 && expected.Id != "uitra-belt-1",
            "after downtime the first eligible route destination must restart at the minimum committed security tier");
        Require(sourceState.InstanceSerial == sourceSerial + 1 && MiningSiteService.RemainingM3(sourceState.Asteroids) > 0 &&
                save.Operation.SiteInstanceSerial == operationSerial && MiningSiteService.RemainingM3(save.Operation.Asteroids) <= 0,
            "downtime must refresh the exhausted 0.9 backing belt but defer adopting it when an earlier 0.5 route candidate exists");
        OperationService.Tick(save, .01f, null, afterDowntime);
        Require(save.Operation.TravelActive && save.Operation.TravelIsAutomatic &&
                save.Operation.TravelDestinationLocationId == expected.Id,
            "the depleted operation left intact at downtime must immediately route to the globally first minimum-tier belt");

        var returning = NewSinglePilotOperation("uitra-belt-1", beforeDowntime);
        var returningMember = returning.Operation.Fleet.Single();
        var returningShip = returning.Ships.Single(candidate => candidate.Uid == returningMember.ShipUid);
        returningMember.Order = FleetOrder.UnloadAndReturn;
        returningMember.TargetAsteroidId = returning.Operation.Asteroids[0].Id;
        returningMember.TransitSecondsLeft = 8.5f;
        returningMember.ReturnAfterUnload = true;
        returningMember.WarpPhase = FleetWarpPhase.AligningOut;
        returningMember.WarpPhaseSecondsLeft = 3.25f;
        returningMember.ResumeOrder = FleetOrder.Mining;
        returningMember.ResumeTargetAsteroidId = returning.Operation.Asteroids[0].Id;
        returningMember.WarpOriginX = 11f;
        returningMember.WarpOriginY = 12f;
        returningMember.WarpOriginZ = 13f;
        returningMember.MiningCycles.Add(new MiningCycleSave { Slot = 0, ProgressSeconds = 7f });
        returningShip.Location = ShipLocation.Transit;
        var returnStateBeforeRefresh = JsonUtility.ToJson(returningMember);
        MiningSiteService.Tick(returning, afterDowntime);
        Require(JsonUtility.ToJson(returningMember) == returnStateBeforeRefresh && returningShip.Location == ShipLocation.Transit,
            "downtime replacement must preserve an in-progress unload/dock warp instead of stranding a Transit ship as Idle");
    }

    static void SmokeAutomaticRouteCoreAndBurstRestart()
    {
        const long nowUnix = 4_089_765_600;
        var save = SaveService.NewGame();
        foreach (var pilot in save.Characters) pilot.DeployOnLaunch = false;
        var commandPilot = save.Characters[9];
        GrantAllSkills(commandPilot);
        var commandShip = SaveService.CreateShip("smoke-auto-route-porpoise", "porpoise");
        var package = Catalog.GetPackage("porpoise-booster-t2");
        Require(package != null, "automatic booster-route fixture requires the Porpoise T2 booster package");
        PreparedPackageService.ApplyLockedFit(commandShip, package);
        save.Ships.Add(commandShip);
        commandPilot.AssignedShipUid = commandShip.Uid;
        commandPilot.DeployOnLaunch = true;
        OperationService.Start(save, "manatirid-belt-1", nowUnix);
        Require(save.Operation.Fleet.Count == 1 && save.Operation.Fleet[0].ShipUid == commandShip.Uid,
            "automatic booster-route fixture must launch only the prepared Porpoise");
        save.Operation.AutoNextBelt = true;
        save.Operation.AutoSecurityFloorTenths = 5;
        save.Operation.AutoSecurityFloorInitialized = true;
        var expected = ExpectedAutomaticBeltByContract(save);
        Require(expected != null && expected.SystemId != Catalog.GetLocation(save.Operation.LocationId).SystemId,
            "automatic booster-route fixture requires a global cross-system destination");
        Require(OperationService.ToggleBursts(save, commandShip.Uid, out _) &&
                OperationService.ToggleCore(save, commandShip.Uid, out _),
            "prepared Porpoise must activate its free bursts and Industrial Core before depletion");
        var fittedBursts = commandShip.Modules
            .Where(module => Catalog.GetModule(module.ModuleId)?.Kind == ModuleKind.MiningBurst)
            .ToArray();
        Require(save.Operation.BurstsActive && fittedBursts.Length > 0 && fittedBursts.All(module => module.Active) &&
                save.Operation.IndustrialCoreActive,
            "booster-route fixture must begin with serialized active burst and core intent");

        foreach (var asteroid in save.Operation.Asteroids) asteroid.RemainingUnits = 0;
        OperationService.Tick(save, .01f, null, nowUnix);
        Require(save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreStopRequested &&
                save.Operation.AutoRestartIndustrialCore && save.Operation.AutoRestartIndustrialCoreShipUid == commandShip.Uid &&
                !save.Operation.TravelActive,
            "depletion must preserve core restart intent and wait for the active cycle boundary");
        var remainingCoreCycle = save.Operation.IndustrialCoreSecondsLeft;
        OperationService.Tick(save, remainingCoreCycle + .01f, null, nowUnix + 1);
        Require(!save.Operation.IndustrialCoreActive && save.Operation.AutoRestartIndustrialCore &&
                save.Operation.TravelActive && save.Operation.TravelIsAutomatic &&
                save.Operation.TravelDestinationLocationId == expected.Id,
            "the global route must start immediately after the queued Industrial Core boundary");
        Require(save.Operation.BurstsActive && save.Operation.BurstShipUid == commandShip.Uid &&
                fittedBursts.All(module => module.Active),
            "automatic travel must retain burst Active intent while the command ship is in transit");

        var travelSeconds = save.Operation.TravelTotalSeconds;
        Require(TravelService.Tick(save, travelSeconds + .01f, out _, nowUnix + 1 + (long)Math.Ceiling(travelSeconds)) &&
                save.Operation.Active && save.Operation.LocationId == expected.Id,
            "automatic booster route must complete at the selected global belt");
        Require(save.Operation.IndustrialCoreActive && save.Operation.IndustrialCoreShipUid == commandShip.Uid &&
                !save.Operation.AutoRestartIndustrialCore && string.IsNullOrWhiteSpace(save.Operation.AutoRestartIndustrialCoreShipUid),
            "arrival must restart the same Industrial Core and consume its one-shot restart intent");
        Require(save.Operation.BurstsActive && save.Operation.BurstShipUid == commandShip.Uid &&
                fittedBursts.All(module => module.Active) &&
                save.Operation.BurstEffects.Any(effect => effect.SourceShipUid == commandShip.Uid),
            "arrival must repulse the same active burst loadout onto the preserved fleet");
    }

    static void SmokeOfflineMiningUnloadRoute()
    {
        var offlineStart = new DateTimeOffset(2099, 8, 13, 10, 59, 30, TimeSpan.Zero).ToUnixTimeSeconds();
        var downtime = offlineStart + 30;
        var offlineEnd = offlineStart + 120;
        var save = NewSinglePilotOperation("manatirid-belt-1", offlineStart);
        save.Operation.AutoUnload = true;
        save.Operation.AutoRetarget = true;
        save.Operation.AutoNextBelt = true;
        save.Operation.AutoSecurityFloorTenths = 5;
        save.Operation.AutoSecurityFloorInitialized = true;
        save.Operation.RaidTimerSeconds = 99_999f;
        var expectedDestination = ExpectedAutomaticBeltByContract(save);
        Require(expectedDestination != null && expectedDestination.SystemId != Catalog.GetLocation(save.Operation.LocationId).SystemId,
            "offline mine/unload/route fixture requires a global destination in another system");

        var member = save.Operation.Fleet.Single();
        var pilot = save.Characters.Single(candidate => candidate.Id == member.PilotId);
        var ship = save.Ships.Single(candidate => candidate.Uid == member.ShipUid);
        var hull = Catalog.GetShip(ship.HullId);
        var target = save.Operation.Asteroids.First(asteroid =>
            asteroid.RemainingUnits > 0 && OperationService.CanMineResource(ship, asteroid.OreId));
        var resource = Catalog.GetOre(target.OreId);
        foreach (var asteroid in save.Operation.Asteroids) asteroid.RemainingUnits = 0;
        target.RemainingUnits = 1;
        Require(OperationService.AssignTarget(save, ship.Uid, target.Id),
            "offline fixture must assign its final source-belt ore unit");
        member.X = target.X;
        member.Y = target.Y;
        member.Z = target.Z;
        member.Order = FleetOrder.Mining;
        member.MiningCycles.Clear();
        foreach (var fitted in ship.Modules.Where(module => OperationService.CanMineResource(module, resource)))
        {
            var cycle = OperationService.MiningCycleSecondsForSlot(save, member, fitted.Slot);
            member.MiningCycles.Add(new MiningCycleSave
            {
                Slot = fitted.Slot,
                ProgressSeconds = Math.Max(0, cycle - .25f)
            });
        }
        Require(member.MiningCycles.Count > 0,
            "offline fixture must serialize at least one extractor just before its cycle boundary");

        var capacityM3 = OperationService.MiningHoldCapacity(pilot, hull);
        var initialHoldUnits = Math.Max(0, (capacityM3 - resource.UnitVolumeM3 * 1.01d) / resource.UnitVolumeM3);
        OperationService.AddItem(ship.MiningHold, target.OreId, initialHoldUnits);
        var stationUnitsBefore = OperationService.ItemQuantity(save.StationInventory, target.OreId);
        var walletBefore = 1_234_567_890d;
        save.Isk = walletBefore;
        save.LastSaveUnix = offlineStart;

        var report = OfflineSimulationService.CatchUp(save, offlineEnd);
        Require(!report.Failed && report.OreAddedM3 > 0,
            "offline simulation must complete the pending mining cycle and report newly mined volume");
        Require(OperationService.ItemQuantity(save.StationInventory, target.OreId) > stationUnitsBefore &&
                save.Operation.Active && save.Operation.LocationId == expectedDestination.Id,
            "offline catch-up must finish the full unload warp and then arrive at the globally selected belt");
        Require(report.RouteLegs >= 1 && report.SystemsVisited >= 2,
            "offline report must observe the completed cross-system automatic route");
        var refreshedSource = save.MiningSites.Single(state => state.LocationId == "manatirid-belt-1");
        Require(refreshedSource.InstanceSerial >= 2 && refreshedSource.LastRefreshUnix == downtime &&
                MiningSiteService.RemainingM3(refreshedSource.Asteroids) > 0,
            "offline chronology must mine and snapshot before 11:00, then refresh the source belt at the exact downtime boundary");
        RequireNearly(save.Isk, walletBefore, .000001d,
            "offline mining, unloading and routing must never autosell ore or change ISK");

        var afterFirstCatchUp = JsonUtility.ToJson(save);
        var secondReport = OfflineSimulationService.CatchUp(save, offlineEnd);
        Require(!secondReport.HasElapsedTime && secondReport.SimulatedSeconds == 0 && secondReport.OreAddedM3 == 0 &&
                JsonUtility.ToJson(save) == afterFirstCatchUp,
            "the persisted LastSaveUnix checkpoint must make a repeated catch-up at the same time a strict no-op");
    }

    static void SmokeOfflineTrainingAndTruncation()
    {
        var offlineStart = new DateTimeOffset(2099, 8, 13, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        const long fleetSimulationCap = 30L * 24L * 60L * 60L;
        const long requestedElapsed = 40L * 24L * 60L * 60L;
        var save = SaveService.NewGame();
        var pilot = save.Characters[9];
        GrantAllSkills(pilot);
        SkillService.ClearTrainingQueue(pilot);
        var longSkill = SkillService.GetState(pilot, "advanced-mass-production", true);
        longSkill.BookOwned = true;
        longSkill.SkillPoints = 0;
        Require(SkillService.TryEnqueueToTarget(pilot, "advanced-mass-production", 5, out _) &&
                SkillService.GetTrainingQueue(pilot).Count == 5,
            "offline truncation fixture must queue all five levels of a prerequisite-valid rank-8 skill");
        var fullTrainingSeconds = SkillService.TotalTrainingSecondsLeft(pilot);
        Require(fullTrainingSeconds > fleetSimulationCap && fullTrainingSeconds < requestedElapsed,
            "rank-8 level V must finish after the fleet cap but inside the requested forty-day elapsed interval");
        var walletBefore = save.Isk;
        save.LastSaveUnix = offlineStart;

        var report = OfflineSimulationService.CatchUp(save, offlineStart + requestedElapsed);
        Require(!report.Failed && report.Truncated &&
                Math.Abs(report.SimulatedSeconds - fleetSimulationCap) < .01d &&
                Math.Abs(report.IgnoredSeconds - (requestedElapsed - fleetSimulationCap)) < .01d,
            "fleet catch-up must cap at thirty days and report the exact ignored remainder");
        Require(SkillService.GetLevel(pilot, "advanced-mass-production") == 5 &&
                SkillService.GetTrainingQueue(pilot).Count == 0 && report.CompletedTrainingEntries == 5,
            "training must receive the full forty-day elapsed time even when fleet simulation is truncated at thirty days");
        Require(save.LastSaveUnix == offlineStart + requestedElapsed && report.Summary.Contains("не просчитано"),
            "truncation must checkpoint real now and remain visible in the player-facing report");
        RequireNearly(save.Isk, walletBefore, .000001d,
            "an offline training-only interval must not mutate the shared wallet");
    }

    static void SmokeOfflineNpcRisk()
    {
        var offlineStart = new DateTimeOffset(2099, 8, 13, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        const long elapsed = 15 * 60;

        var safe = NewSinglePilotOperation("kakakela-belt-1", offlineStart);
        var safeMember = safe.Operation.Fleet.Single();
        var safeShip = safe.Ships.Single(candidate => candidate.Uid == safeMember.ShipUid);
        safe.Operation.AutoRetarget = false;
        safe.Operation.AutoNextBelt = false;
        safeMember.Order = FleetOrder.Idle;
        safeMember.TargetAsteroidId = string.Empty;
        var safeShield = safeShip.ShieldHp;
        var safeArmor = safeShip.ArmorHp;
        var safeStructure = safeShip.StructureHp;
        safe.LastSaveUnix = offlineStart;
        var safeReport = OfflineSimulationService.CatchUp(safe, offlineStart + elapsed);
        Require(!safeReport.Failed && safeReport.ShipsLost == 0 && safe.Operation.Enemies.Count == 0,
            "NPC-free Kakakela must remain genuinely safe during offline idle time");
        RequireNearly(safeShip.ShieldHp, safeShield, .000001d, "NPC-free offline time must preserve shield HP");
        RequireNearly(safeShip.ArmorHp, safeArmor, .000001d, "NPC-free offline time must preserve armor HP");
        RequireNearly(safeShip.StructureHp, safeStructure, .000001d, "NPC-free offline time must preserve structure HP");

        var danger = NewSinglePilotOperation("manatirid-belt-1", offlineStart);
        var dangerMember = danger.Operation.Fleet.Single();
        var dangerShip = danger.Ships.Single(candidate => candidate.Uid == dangerMember.ShipUid);
        var dangerShipUid = dangerShip.Uid;
        danger.Operation.AutoRetarget = false;
        danger.Operation.AutoNextBelt = false;
        danger.Operation.RaidTimerSeconds = .01f;
        dangerMember.Order = FleetOrder.Idle;
        dangerMember.TargetAsteroidId = string.Empty;
        dangerShip.CombatDroneCount = 0;
        var dangerHpBefore = dangerShip.ShieldHp + dangerShip.ArmorHp + dangerShip.StructureHp;
        danger.LastSaveUnix = offlineStart;
        var dangerReport = OfflineSimulationService.CatchUp(danger, offlineStart + elapsed);
        var survivingDangerShip = danger.Ships.Find(candidate => candidate.Uid == dangerShipUid);
        var dangerHpAfter = survivingDangerShip == null
            ? 0
            : survivingDangerShip.ShieldHp + survivingDangerShip.ArmorHp + survivingDangerShip.StructureHp;
        Require(!dangerReport.Failed &&
                (dangerReport.ShipsLost > 0 || dangerHpAfter + .001f < dangerHpBefore),
            "an unarmed ship idling in NPC-enabled 0.5 space must take real offline damage or be destroyed");
    }

    static void SmokeOfflineDynamicAnomalyChronology()
    {
        const string anomalyId = "uitra-managed-mining-site";
        const long exactCooldownSeconds = 24L * 60L * 60L;
        var offlineStart = new DateTimeOffset(2099, 8, 13, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var location = Catalog.GetLocation(anomalyId);
        Require(location?.SiteKind == MiningSiteKind.DynamicAnomaly &&
                Math.Abs(location.AnomalyRespawnMinSeconds - exactCooldownSeconds) < .001f &&
                Math.Abs(location.AnomalyRespawnMaxSeconds - exactCooldownSeconds) < .001f,
            "dynamic-anomaly chronology fixture requires the guaranteed exact-24-hour managed-site cooldown");

        var save = NewSinglePilotOperation(anomalyId, offlineStart);
        save.Operation.AutoUnload = false;
        save.Operation.AutoRetarget = false;
        save.Operation.AutoNextBelt = true; // Dynamic anomalies must remain outside ordinary-belt roaming.
        save.Operation.RaidTimerSeconds = 99_999f;
        var member = save.Operation.Fleet.Single();
        var ship = save.Ships.Single(candidate => candidate.Uid == member.ShipUid);
        var target = save.Operation.Asteroids.First(asteroid =>
            asteroid.RemainingUnits > 0 && OperationService.CanMineResource(ship, asteroid.OreId));
        var resource = Catalog.GetOre(target.OreId);
        foreach (var asteroid in save.Operation.Asteroids) asteroid.RemainingUnits = 0;
        target.RemainingUnits = 1;
        Require(OperationService.AssignTarget(save, ship.Uid, target.Id),
            "dynamic-anomaly fixture must assign its final mineable unit");
        member.X = target.X;
        member.Y = target.Y;
        member.Z = target.Z;
        member.Order = FleetOrder.Mining;
        member.MiningCycles.Clear();
        foreach (var fitted in ship.Modules.Where(module => OperationService.CanMineResource(module, resource)))
        {
            var cycle = OperationService.MiningCycleSecondsForSlot(save, member, fitted.Slot);
            Require(cycle > 1f, "dynamic-anomaly fixture requires a mining cycle longer than one second");
            member.MiningCycles.Add(new MiningCycleSave { Slot = fitted.Slot, ProgressSeconds = cycle - 1f });
        }
        Require(member.MiningCycles.Count > 0,
            "dynamic-anomaly fixture must serialize at least one extractor one second before completion");

        var initialSerial = save.Operation.SiteInstanceSerial;
        var depletionUnix = offlineStart + 1;
        var firstCheckpointUnix = offlineStart + 5;
        save.LastSaveUnix = offlineStart;
        var depletionReport = OfflineSimulationService.CatchUp(save, firstCheckpointUnix);
        var cooldownState = save.MiningSites.Single(state => state.LocationId == anomalyId);
        Require(!depletionReport.Failed && cooldownState.Lifecycle == MiningSiteLifecycle.Cooldown &&
                cooldownState.InstanceSerial == initialSerial &&
                cooldownState.RespawnUnix == depletionUnix + exactCooldownSeconds &&
                MiningSiteService.RemainingM3(cooldownState.Asteroids) <= 0,
            "offline depletion must anchor the anomaly cooldown to its simulated mining-cycle boundary, not launch time");
        Require(save.Operation.Active && save.Operation.LocationId == anomalyId &&
                !save.Operation.TravelActive && !save.Operation.BeltWarpActive && depletionReport.RouteLegs == 0,
            "a depleted dynamic anomaly must not enter the ordinary automatic-belt route");

        var respawnUnix = cooldownState.RespawnUnix;
        var respawnReport = OfflineSimulationService.CatchUp(save, respawnUnix);
        Require(!respawnReport.Failed && cooldownState.Lifecycle == MiningSiteLifecycle.Available &&
                cooldownState.InstanceSerial == initialSerial + 1 && cooldownState.RespawnUnix == 0 &&
                cooldownState.LastRefreshUnix == respawnUnix && MiningSiteService.RemainingM3(cooldownState.Asteroids) > 0,
            "the long offline gap must reach the exact anomaly respawn event and generate its next instance");
        Require(save.Operation.Active && save.Operation.LocationId == anomalyId &&
                save.Operation.SiteInstanceSerial == cooldownState.InstanceSerial &&
                MiningSiteService.RemainingM3(save.Operation.Asteroids) > 0 &&
                !save.Operation.TravelActive && !save.Operation.BeltWarpActive && respawnReport.RouteLegs == 0,
            "anomaly respawn must reload the open operation in place without an anomaly auto-route");
    }

    static void SmokeMiningSiteDeadlineCache()
    {
        var beforeDowntime = new DateTimeOffset(2099, 8, 13, 10, 58, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var downtime = beforeDowntime + 2 * 60;
        var save = SaveService.NewGame();
        save.MiningSites.Clear();
        MiningSiteService.Tick(save, beforeDowntime);

        var materializedBelts = Catalog.Locations
            .Where(location => location.SiteKind == MiningSiteKind.StaticBelt)
            .Take(400)
            .ToArray();
        Require(materializedBelts.Length == 400,
            "deadline-cache fixture requires hundreds of persisted static belts");
        foreach (var belt in materializedBelts)
            MiningSiteService.LoadIntoOperation(save, belt, beforeDowntime);
        save.Operation.Active = false;
        save.Operation.LocationId = string.Empty;

        var persistedStaticCount = save.MiningSites.Count(state =>
            Catalog.GetLocation(state.LocationId)?.SiteKind == MiningSiteKind.StaticBelt);
        Require(persistedStaticCount == materializedBelts.Length,
            "deadline-cache fixture must materialize exactly the requested static-site states");
        var representative = save.MiningSites.Single(state => state.LocationId == materializedBelts[0].Id);
        var representativeSerial = representative.InstanceSerial;
        var persistedCount = save.MiningSites.Count;
        var stateBefore = MiningSiteStateSignature(save.MiningSites);
        Require(MiningSiteService.NextWorldEventUnix(save, beforeDowntime) == downtime,
            "the cached next world event immediately before 11:00 must be the exact downtime boundary");

        foreach (var sampleUnix in new[] { beforeDowntime + 1, beforeDowntime + 30, downtime - 1 })
        {
            MiningSiteService.Tick(save, sampleUnix);
            Require(MiningSiteService.NextWorldEventUnix(save, sampleUnix) == downtime &&
                    save.MiningSites.Count == persistedCount &&
                    MiningSiteStateSignature(save.MiningSites) == stateBefore,
                "repeated pre-deadline Tick/NextWorldEvent calls must neither grow nor mutate hundreds of persisted sites");
        }

        MiningSiteService.Tick(save, downtime);
        Require(representative.InstanceSerial == representativeSerial + 1 &&
                representative.LastRefreshUnix == downtime && representative.NextRefreshUnix == downtime + 24 * 60 * 60 &&
                MiningSiteService.RemainingM3(representative.Asteroids) > 0,
            "the cached deadline must still refresh a persisted static belt at exactly 11:00 UTC");
        Require(MiningSiteService.NextWorldEventUnix(save, downtime) > downtime,
            "processing the cached world deadline must advance the next event beyond the consumed boundary");
    }

    static void SmokeTravelLifecycle()
    {
        var save = SaveService.NewGame();
        Require(TravelService.TryStart(save, "sobaseki-belt-1", out _), "selected starter fleet must begin abstract inter-system travel");
        Require(save.Operation.TravelActive && !save.Operation.Active && save.Operation.TravelShipUids.Count == 9, "travel state must persist exactly nine selected starter ships");
        Require(save.Ships.All(ship => ship.Location == ShipLocation.Transit), "travelling starter ships must expose Transit state");
        var restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));
        Require(restored.Operation.TravelActive && restored.Operation.TravelSecondsLeft > 0 && restored.Operation.TravelShipUids.Count == 9, "travel destination, roster and countdown must survive serialization");
        Require(TravelService.Tick(restored, 999f, out _), "travel countdown must complete deterministically");
        Require(!restored.Operation.TravelActive && restored.Operation.Active && restored.Operation.LocationId == "sobaseki-belt-1", "travel arrival must start the selected destination operation");
        Require(restored.Operation.Fleet.Count == 9 && restored.Ships.All(ship => ship.Location == ShipLocation.Belt), "all persisted travel ships must arrive in the destination belt");

        var blocked = SaveService.NewGame();
        blocked.Operation.IndustrialCoreActive = true;
        blocked.Operation.IndustrialCoreSecondsLeft = 42f;
        Require(!TravelService.TryStart(blocked, "sobaseki-belt-1", out var message) && message.Contains("Industrial Core"), "active Industrial Core must block a route change");

        var race = SaveService.NewGame();
        const string anomalyId = "p3en-e-prospecting-l1";
        Require(TravelService.TryStart(race, anomalyId, out _),
            "availability-race fixture must begin inter-system travel to a live anomaly");
        var anomalyState = race.MiningSites.Single(site => site.LocationId == anomalyId);
        anomalyState.Lifecycle = MiningSiteLifecycle.Cooldown;
        anomalyState.RespawnUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
        anomalyState.Asteroids.Clear();
        Require(TravelService.Tick(race, 999f, out _),
            "inter-system travel must finish safely when its anomaly disappears en route");
        Require(!race.Operation.TravelActive && !race.Operation.Active && race.Ships.All(ship => ship.Location == ShipLocation.Station),
            "a destination availability race must return the persisted travel roster safely to station");
    }

    static void SmokeDeepCoreMercoxit()
    {
        var small = Catalog.GetModule("modulated-deep-core-miner-ii");
        var strip = Catalog.GetModule("modulated-deep-core-strip-miner-ii");
        Require(small?.TypeId == 18068 && strip?.TypeId == 24305, "deep-core module IDs mismatch");
        Require(small.CanMineMercoxit && strip.CanMineMercoxit, "deep-core modules must explicitly unlock Mercoxit");
        RequireNearly(small.BaseYieldM3, 30, .001d, "small deep-core yield mismatch");
        RequireNearly(strip.BaseYieldM3, 80, .001d, "deep-core strip yield mismatch");
        Require(Catalog.GetSkill("deep-core-mining").Prerequisites.Any(requirement => requirement.SkillId == "mining" && requirement.Level == 5), "Deep Core Mining must require Mining V");

        var save = SaveService.NewGame();
        foreach (var candidate in save.Characters) candidate.DeployOnLaunch = false;
        var pilot = save.Characters[9]; GrantAllSkills(pilot);
        var ship = SaveService.CreateShip("smoke-deep-hulk", "hulk");
        var mercoxitPackage = Catalog.GetPackage("hulk-mercoxit-t2-a2");
        Require(mercoxitPackage != null, "Hulk must expose a prepared Mercoxit T2 package");
        PreparedPackageService.ApplyLockedFit(ship, mercoxitPackage);
        save.Ships.Add(ship); pilot.AssignedShipUid = ship.Uid; pilot.DeployOnLaunch = true;
        Require(ship.Modules.Count(module => module.ModuleId == strip.Id) == 2, "Hulk Mercoxit package must materialize both deep-core strips");
        Require(ship.Modules.Where(module => module.ModuleId == strip.Id).All(module => string.IsNullOrWhiteSpace(module.ChargeId)), "Mercoxit Type A profile must remain implicit and wear-free");
        Require(OperationService.CanMineResource(ship, "mercoxit"), "deep-core Hulk must be able to target Mercoxit");
        Require(!OperationService.CanMineResource(save.Ships[0], "mercoxit"), "ordinary Venture and mining drones must reject Mercoxit");

        OperationService.Start(save, "y-zxio-belt-1");
        var asteroid = save.Operation.Asteroids.First(candidate => candidate.OreId == "mercoxit");
        var member = save.Operation.Fleet.Single();
        Require(OperationService.AssignTarget(save, ship.Uid, asteroid.Id), "deep-core Hulk must accept a Mercoxit target");
        member.X = asteroid.X; member.Y = asteroid.Y; member.Z = asteroid.Z;
        OperationService.Tick(save, OperationService.MiningCycleSeconds(pilot, Catalog.GetShip("hulk"), strip, save, member) + .01f);
        Require(OperationService.ItemQuantity(ship.MiningHold, "mercoxit") >= 1, "deep-core cycle must deliver whole 40 m3 Mercoxit units");
    }

    static LocationDefinition ExpectedAutomaticBeltByContract(GameSave save, bool includeCurrent = false)
    {
        var operation = save?.Operation;
        var current = Catalog.GetLocation(operation?.LocationId);
        if (operation?.Active != true || current == null) return null;
        var floor = operation.AutoSecurityFloorInitialized
            ? operation.AutoSecurityFloorTenths
            : MiningSiteService.SecurityTierTenths(current);
        return Catalog.Locations
            .Where(location => location.SiteKind == MiningSiteKind.StaticBelt &&
                               MiningSiteService.SecurityTierTenths(location) >= floor &&
                               (includeCurrent || !string.Equals(location.Id, current.Id, StringComparison.OrdinalIgnoreCase)))
            .Where(location => operation.Fleet.All(member =>
            {
                var ship = save.Ships.Find(candidate => candidate.Uid == member.ShipUid);
                var pilot = save.Characters.Find(candidate => candidate.Id == member.PilotId);
                return ship != null && pilot != null && OperationService.CanFly(pilot, ship) &&
                       OperationService.LocationSupportsShip(location, ship, Catalog.GetShip(ship.HullId));
            }))
            .OrderBy(location => MiningSiteService.SecurityTierTenths(location))
            .ThenBy(location => location.SystemName, StringComparer.Ordinal)
            .ThenBy(location => location.SystemId)
            .ThenBy(location => location.BeltName, StringComparer.Ordinal)
            .ThenBy(location => location.BeltId)
            .ThenBy(location => location.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    static GameSave NewSinglePilotOperation(string locationId, long nowUnix = 0)
    {
        var save = SaveService.NewGame();
        if (nowUnix > 0)
        {
            save.MiningSites.Clear();
            MiningSiteService.Tick(save, nowUnix);
        }
        foreach (var pilot in save.Characters) pilot.DeployOnLaunch = false;
        save.Characters[0].DeployOnLaunch = true;
        OperationService.Start(save, locationId, nowUnix);
        Require(save.Operation.Active && save.Operation.LocationId == locationId, "requested operation location must persist");
        Require(save.Operation.Asteroids.Count > 0, "operation must generate and persist its asteroids");
        Require(save.Operation.Fleet.Count == 1, "single-pilot smoke setup must deploy exactly one ship");
        return save;
    }

    static void GrantAllSkills(CharacterSave pilot)
    {
        foreach (var skill in Catalog.Skills)
        {
            var state = SkillService.GetState(pilot, skill.Id, true);
            state.BookOwned = true;
            state.SkillPoints = SkillService.RequiredSp(skill.Id, 5);
        }
    }

    static string StarterRuntimeSignature(ShipSave ship)
    {
        return JsonUtility.ToJson(new ShipSave
        {
            Uid = ship.Uid,
            Location = ship.Location,
            MiningHold = ship.MiningHold,
            CargoHold = ship.CargoHold,
            FuelHold = ship.FuelHold,
            ShieldHp = ship.ShieldHp,
            ArmorHp = ship.ArmorHp,
            StructureHp = ship.StructureHp
        });
    }

    static string AsteroidStateSignature(IEnumerable<AsteroidSave> asteroids) => string.Join("|", (asteroids ?? Enumerable.Empty<AsteroidSave>()).Select(asteroid =>
        $"{asteroid.Id}:{asteroid.OreId}:{asteroid.RemainingUnits:R}:{asteroid.X:R}:{asteroid.Y:R}:{asteroid.Z:R}:{asteroid.Scale:R}"));

    static string MiningSiteStateSignature(IEnumerable<MiningSiteStateSave> sites) => string.Join("||", (sites ?? Enumerable.Empty<MiningSiteStateSave>())
        .OrderBy(site => site?.LocationId, StringComparer.Ordinal)
        .Select(site => site == null
            ? "null"
            : $"{site.LocationId}:{site.Lifecycle}:{site.InstanceSerial}:{site.LastRefreshUnix}:{site.NextRefreshUnix}:{site.RespawnUnix}:{AsteroidStateSignature(site.Asteroids)}"));

    static string FleetStateSignature(IEnumerable<FleetMemberSave> fleet) => string.Join("|", (fleet ?? Enumerable.Empty<FleetMemberSave>()).Select(member => JsonUtility.ToJson(member)));

    static string EnemyStateSignature(IEnumerable<EnemySave> enemies) => string.Join("|", (enemies ?? Enumerable.Empty<EnemySave>()).Select(enemy => JsonUtility.ToJson(enemy)));

    static void SetSkillLevel(CharacterSave pilot, string skillId, int level)
    {
        var state = SkillService.GetState(pilot, skillId, true);
        state.BookOwned = true;
        state.SkillPoints = level <= 0 ? 0 : SkillService.RequiredSp(skillId, level);
    }

    static void RequireRealLocation(string id, string systemName, int systemId, long beltId)
    {
        var location = Catalog.GetLocation(id);
        Require(location != null && location.SystemName == systemName && location.SystemId == systemId && location.BeltId == beltId,
            $"real belt identity mismatch: {systemName}");
    }

    static void RequireNearly(double actual, double expected, double tolerance, string message)
    {
        Require(Math.Abs(actual - expected) <= tolerance, $"{message}: expected {expected}, got {actual}");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Smoke test failed: " + message);
    }
}
#endif
