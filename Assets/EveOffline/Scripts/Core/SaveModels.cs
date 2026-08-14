using System;
using System.Collections.Generic;

namespace EveOffline
{
    public enum ShipLocation
    {
        Station,
        Belt,
        Transit
    }

    public enum FleetOrder
    {
        Idle,
        Approaching,
        Mining,
        UnloadAndReturn,
        DockAndStay
    }

    /// <summary>
    /// Persisted visual return phases. Fleet simulation keeps the member at its
    /// exact belt origin while Runtime presents the shared-direction warp.
    /// </summary>
    public enum FleetWarpPhase
    {
        None,
        AligningOut,
        InTransit,
        WarpingIn
    }

    public enum MiningSiteLifecycle
    {
        Available,
        Cooldown
    }

    [Serializable]
    public sealed class GameSave
    {
        public int Version;
        public double Isk;
        public List<CharacterSave> Characters = new();
        public List<ShipSave> Ships = new();
        public List<InventoryStack> StationInventory = new();
        public List<ItemInstanceSave> StationItemInstances = new();
        public MarketPriceCache PriceCache = new();
        public OperationSave Operation = new();
        public List<MiningSiteStateSave> MiningSites = new();
        public long LastSaveUnix;
        public int NextShipSerial = 10;
        public int NextItemSerial = 1;
    }

    [Serializable]
    public sealed class CharacterSave
    {
        public string Id;
        public string Name;
        public string AssignedShipUid;
        public bool DeployOnLaunch;
        public List<CharacterSkillSave> Skills = new();
        public double UnallocatedSkillPoints;
        public List<SkillQueueEntrySave> TrainingQueue = new();
        // Legacy active-entry mirror. Kept serialized so v5/v6 saves and the
        // current station UI continue to work while the full queue is adopted.
        public string TrainingSkillId;
        public int TrainingTargetLevel;
    }

    [Serializable]
    public sealed class SkillQueueEntrySave
    {
        public string SkillId;
        public int TargetLevel;
    }

    [Serializable]
    public sealed class CharacterSkillSave
    {
        public string SkillId;
        public bool BookOwned;
        public double SkillPoints;
    }

    [Serializable]
    public sealed class ShipSave
    {
        public string Uid;
        public string HullId;
        // Ready-to-fly mining package selected for this hull. Empty remains a
        // supported legacy/custom fit so old saves never lose their equipment.
        public string PackageId;
        // Defensive presets are deliberately independent from mining packages.
        // The preset supplies max HP; the three current HP fields remain the
        // persisted damage state.
        public string TankPresetId;
        public ShipLocation Location;
        public List<FittedModuleSave> Modules = new();
        public string CombatDroneId;
        public int CombatDroneCount;
        public string MiningDroneId;
        public int MiningDroneCount;
        public List<InventoryStack> MiningHold = new();
        public List<InventoryStack> CargoHold = new();
        public List<InventoryStack> FuelHold = new();
        public float ShieldHp;
        public float ArmorHp;
        public float StructureHp;
    }

    [Serializable]
    public sealed class FittedModuleSave
    {
        public int Slot;
        public string ModuleId;
        public string ChargeId;
        public string ChargeUid;
        public float ChargeDamage;
        public int ChargeQuantity;
        public bool ChargeQuantityInitialized;
        public float BurstCycleSecondsLeft;
        public bool Active;
    }

    [Serializable]
    public sealed class InventoryStack
    {
        public string ItemId;
        public double Quantity;
    }

    [Serializable]
    public sealed class ItemInstanceSave
    {
        public string Uid;
        public string ItemId;
        public float Damage;
    }

    [Serializable]
    public sealed class OperationSave
    {
        public bool Active;
        public string LocationId;
        public int SiteInstanceSerial;
        public int BeltSeed = 1337;
        public int VisitNumber;
        public float ElapsedSeconds;
        public float RaidTimerSeconds;
        public bool AutoUnload;
        public bool AutoRetarget;
        public bool AutoNextBelt;
        // The lowest security tier the automatic mining route may enter. The
        // value is the displayed security multiplied by ten (0.5 => 5). It is
        // committed only after a player-requested journey arrives successfully;
        // automatic belt changes never rewrite it.
        public int AutoSecurityFloorTenths;
        public bool AutoSecurityFloorInitialized;
        public bool StopAfterFleetWarp;
        public bool BurstsActive;
        public string BurstShipUid;
        public bool IndustrialCoreActive;
        public bool IndustrialCoreStopRequested;
        public float IndustrialCoreSecondsLeft;
        public string IndustrialCoreShipUid;
        // Automatic roaming must stop an active core at its cycle boundary, but
        // remembers the player's intent and starts a fresh cycle after arrival.
        public bool AutoRestartIndustrialCore;
        public string AutoRestartIndustrialCoreShipUid;
        public List<BurstRecipientEffectSave> BurstEffects = new();
        public bool TravelActive;
        public string TravelSourceLocationId;
        public string TravelDestinationLocationId;
        public float TravelSecondsLeft;
        public float TravelTotalSeconds;
        public List<string> TravelShipUids = new();
        public bool TravelIsAutomatic;
        // A local belt-to-belt warp deliberately has its own state. Unlike the
        // station/inter-system journey above it keeps the operation roster,
        // damage and every ship hold exactly as they are.
        public bool BeltWarpActive;
        public string BeltWarpDestinationLocationId;
        public float BeltWarpSecondsLeft;
        public float BeltWarpTotalSeconds;
        public bool BeltWarpIsAutomatic;
        public List<AsteroidSave> Asteroids = new();
        public List<FleetMemberSave> Fleet = new();
        public List<EnemySave> Enemies = new();
        public List<OperationLogSave> Log = new();
        public bool WarpPointInitialized;
        public float WarpPointX;
        public float WarpPointY;
        public float WarpPointZ;
    }

    [Serializable]
    public sealed class MiningSiteStateSave
    {
        public string LocationId;
        public MiningSiteLifecycle Lifecycle;
        public int InstanceSerial;
        public long LastRefreshUnix;
        public long NextRefreshUnix;
        public long RespawnUnix;
        public List<AsteroidSave> Asteroids = new();
    }

    [Serializable]
    public sealed class AsteroidSave
    {
        public string Id;
        public string OreId;
        public double RemainingUnits;
        public float X;
        public float Y;
        public float Z;
        public float Scale;
    }

    [Serializable]
    public sealed class FleetMemberSave
    {
        public string PilotId;
        public string ShipUid;
        public FleetOrder Order;
        public string TargetAsteroidId;
        public float PreferredRangeKm;
        public float X;
        public float Y;
        public float Z;
        public float CycleProgressSeconds;
        public float DroneCycleProgressSeconds;
        public List<MiningCycleSave> MiningCycles = new();
        public float TransitSecondsLeft;
        public bool ReturnAfterUnload;
        public bool CoreReturnQueued;
        public bool CoreReturnAfterUnload;
        public FleetWarpPhase WarpPhase;
        public float WarpPhaseSecondsLeft;
        public FleetOrder ResumeOrder;
        public string ResumeTargetAsteroidId;
        public float WarpOriginX;
        public float WarpOriginY;
        public float WarpOriginZ;
    }

    [Serializable]
    public sealed class BurstRecipientEffectSave
    {
        public string ShipUid;
        public string SourceShipUid;
        public string ChargeId;
        public float Strength;
        public float SecondsLeft;
    }

    [Serializable]
    public sealed class FleetCommandResult
    {
        public bool Success;
        public int RequestedCount;
        public int ImmediateCount;
        public int QueuedCount;
        public int FailedCount;
        public List<string> Messages = new();

        public string Summary => Messages == null || Messages.Count == 0
            ? (Success ? "Команда принята." : "Команда не выполнена.")
            : string.Join("\n", Messages);
    }

    [Serializable]
    public sealed class MiningCycleSave
    {
        public int Slot;
        public float ProgressSeconds;
    }

    [Serializable]
    public sealed class EnemySave
    {
        public string Id;
        public string Name;
        public float X;
        public float Y;
        public float Z;
        public float ShieldHp;
        public float ArmorHp;
        public float StructureHp;
        public float Dps;
        public float SpeedKmPerSecond;
        public string TargetShipUid;
    }

    [Serializable]
    public sealed class OperationLogSave
    {
        public float AtSeconds;
        public string Message;
    }

    [Serializable]
    public sealed class MarketPriceCache
    {
        public int StationId = MarketService.JitaStationId;
        public string Source = "Встроенный снимок";
        public string SourceScope = "seed";
        public long UpdatedUnix;
        public List<MarketPriceEntry> Entries = new();

        public MarketPriceEntry Find(int typeId)
        {
            return Entries?.Find(entry => entry != null && entry.TypeId == typeId);
        }
    }

    [Serializable]
    public sealed class MarketPriceEntry
    {
        public int TypeId;
        public double BuyPrice;
        public double SellPrice;
        public long UpdatedUnix;
        public string SourceScope;
    }
}
