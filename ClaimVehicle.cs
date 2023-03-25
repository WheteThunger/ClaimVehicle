using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Claim Vehicle", "WhiteThunder", "1.4.0")]
    [Description("Allows players to claim ownership of unowned vehicles.")]
    internal class ClaimVehicle : CovalencePlugin
    {
        #region Fields

        private Configuration _config;

        private const string Permission_Claim_AllVehicles = "claimvehicle.claim.allvehicles";
        private const string Permission_Claim_Chinook = "claimvehicle.claim.chinook";
        private const string Permission_Claim_DuoSub = "claimvehicle.claim.duosub";
        private const string Permission_Claim_HotAirBalloon = "claimvehicle.claim.hotairballoon";
        private const string Permission_Claim_MiniCopter = "claimvehicle.claim.minicopter";
        private const string Permission_Claim_ModularCar = "claimvehicle.claim.modularcar";
        private const string Permission_Claim_RHIB = "claimvehicle.claim.rhib";
        private const string Permission_Claim_RidableHorse = "claimvehicle.claim.ridablehorse";
        private const string Permission_Claim_Rowboat = "claimvehicle.claim.rowboat";
        private const string Permission_Claim_ScrapHeli = "claimvehicle.claim.scraptransporthelicopter";
        private const string Permission_Claim_Sedan = "claimvehicle.claim.sedan";
        private const string Permission_Claim_Snowmobile = "claimvehicle.claim.snowmobile";
        private const string Permission_Claim_SoloSub = "claimvehicle.claim.solosub";
        private const string Permission_Claim_Tomaha = "claimvehicle.claim.tomaha";
        private const string Permission_Claim_Workcart = "claimvehicle.claim.workcart";

        private const string Permission_Unclaim = "claimvehicle.unclaim";
        private const string Permission_NoClaimCooldown = "claimvehicle.nocooldown";

        private const string SnowmobileShortPrefabName = "snowmobile";
        private const string TomahaShortPrefabName = "tomahasnowmobile";

        private CooldownManager _cooldownManager;

        #endregion

        #region Hooks

        private void Init()
        {
            _cooldownManager = new CooldownManager(_config.ClaimCooldownSeconds);

            permission.RegisterPermission(Permission_Claim_AllVehicles, this);
            permission.RegisterPermission(Permission_Claim_Chinook, this);
            permission.RegisterPermission(Permission_Claim_DuoSub, this);
            permission.RegisterPermission(Permission_Claim_HotAirBalloon, this);
            permission.RegisterPermission(Permission_Claim_MiniCopter, this);
            permission.RegisterPermission(Permission_Claim_ModularCar, this);
            permission.RegisterPermission(Permission_Claim_RHIB, this);
            permission.RegisterPermission(Permission_Claim_RidableHorse, this);
            permission.RegisterPermission(Permission_Claim_Rowboat, this);
            permission.RegisterPermission(Permission_Claim_ScrapHeli, this);
            permission.RegisterPermission(Permission_Claim_Sedan, this);
            permission.RegisterPermission(Permission_Claim_Snowmobile, this);
            permission.RegisterPermission(Permission_Claim_SoloSub, this);
            permission.RegisterPermission(Permission_Claim_Tomaha, this);
            permission.RegisterPermission(Permission_Claim_Workcart, this);

            permission.RegisterPermission(Permission_Unclaim, this);
            permission.RegisterPermission(Permission_NoClaimCooldown, this);
        }

        #endregion

        #region Exposed Hooks

        private static class ExposedHooks
        {
            public static object OnVehicleClaim(BasePlayer player, BaseCombatEntity vehicle)
            {
                return Interface.CallHook("OnVehicleClaim", player, vehicle);
            }

            public static object OnVehicleUnclaim(BasePlayer player, BaseCombatEntity vehicle)
            {
                return Interface.CallHook("OnVehicleUnclaim", player, vehicle);
            }

            public static void OnVehicleOwnershipChanged(BaseCombatEntity vehicle)
            {
                Interface.CallHook("OnVehicleOwnershipChanged", vehicle);
            }
        }

        #endregion

        #region Commands

        [Command("vclaim")]
        private void ClaimVehicleCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            var basePlayer = player.Object as BasePlayer;
            string perm = null;
            BaseCombatEntity vehicle = null;

            if (!VerifySupportedVehicleFound(player, GetLookEntity(basePlayer), ref vehicle, ref perm) ||
                !VerifyPermissionAny(player, Permission_Claim_AllVehicles, perm) ||
                !VerifyVehicleIsNotDead(player, vehicle) ||
                !VerifyNotOwned(player, vehicle) ||
                !VerifyOffCooldown(player) ||
                !VerifyCanBuild(player) ||
                !VerifyNoLockRestriction(player, vehicle) ||
                !VerifyNotMounted(player, vehicle) ||
                ClaimWasBlocked(basePlayer, vehicle))
                return;

            ChangeVehicleOwnership(vehicle, basePlayer.userID);
            _cooldownManager.UpdateLastUsedForPlayer(basePlayer.userID);
            ReplyToPlayer(player, "Claim.Success");
        }

        [Command("vunclaim")]
        private void UnclaimVehicleCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer || !VerifyPermissionAny(player, Permission_Unclaim))
                return;

            var basePlayer = player.Object as BasePlayer;
            string perm = null;
            BaseCombatEntity vehicle = null;

            if (!VerifySupportedVehicleFound(player, GetLookEntity(basePlayer), ref vehicle, ref perm) ||
                !VerifyCurrentlyOwned(player, vehicle) ||
                UnclaimWasBlocked(basePlayer, vehicle))
                return;

            ChangeVehicleOwnership(vehicle, 0);
            ReplyToPlayer(player, "Unclaim.Success");
        }

        #endregion

        #region Helper Methods

        private static bool ClaimWasBlocked(BasePlayer player, BaseCombatEntity vehicle)
        {
            var hookResult = ExposedHooks.OnVehicleClaim(player, vehicle);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool UnclaimWasBlocked(BasePlayer player, BaseCombatEntity vehicle)
        {
            var hookResult = ExposedHooks.OnVehicleUnclaim(player, vehicle);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static RidableHorse GetClosestHorse(HitchTrough hitchTrough, BasePlayer player)
        {
            var closestDistance = 1000f;
            RidableHorse closestHorse = null;

            for (var i = 0; i < hitchTrough.hitchSpots.Length; i++)
            {
                var hitchSpot = hitchTrough.hitchSpots[i];
                if (!hitchSpot.IsOccupied())
                    continue;

                var distance = Vector3.Distance(player.transform.position, hitchSpot.spot.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHorse = hitchSpot.horse.Get(serverside: true) as RidableHorse;
                }
            }

            return closestHorse;
        }

        private static BaseEntity GetLookEntity(BasePlayer player, float maxDistance = 5)
        {
            RaycastHit hit;
            return Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }

        private static void ChangeVehicleOwnership(BaseCombatEntity vehicle, ulong userId)
        {
            vehicle.OwnerID = userId;
            ExposedHooks.OnVehicleOwnershipChanged(vehicle);
        }

        private static string FormatDuration(double seconds)
        {
            return TimeSpan.FromSeconds(Math.Ceiling(seconds)).ToString("g");
        }

        private bool VerifySupportedVehicleFound(IPlayer player, BaseEntity entity, ref BaseCombatEntity vehicle, ref string perm)
        {
            vehicle = GetSupportedVehicle(entity, player.Object as BasePlayer, ref perm);
            if (vehicle != null)
                return true;

            ReplyToPlayer(player, "Generic.Error.NoSupportedVehicleFound");
            return false;
        }

        private bool VerifyPermissionAny(IPlayer player, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
            {
                if (permission.UserHasPermission(player.Id, perm))
                    return true;
            }

            ReplyToPlayer(player, "Generic.Error.NoPermission");
            return false;
        }

        private bool VerifyVehicleIsNotDead(IPlayer player, BaseCombatEntity vehicle)
        {
            if (!vehicle.IsDead())
                return true;

            ReplyToPlayer(player, "Generic.Error.VehicleDead");
            return false;
        }

        private bool VerifyNotOwned(IPlayer player, BaseEntity vehicle)
        {
            if (vehicle.OwnerID == 0)
                return true;

            var basePlayer = player.Object as BasePlayer;
            if (vehicle.OwnerID == basePlayer.userID)
            {
                ReplyToPlayer(player, "Claim.Error.AlreadyOwnedByYou");
            }
            else
            {
                ReplyToPlayer(player, "Claim.Error.DifferentOwner");
            }

            return false;
        }

        private bool VerifyOffCooldown(IPlayer player)
        {
            if (player.HasPermission(Permission_NoClaimCooldown))
                return true;

            var basePlayer = player.Object as BasePlayer;
            var secondsRemaining = _cooldownManager.GetSecondsRemaining(basePlayer.userID);
            if (secondsRemaining > 0)
            {
                ReplyToPlayer(player, "Generic.Error.Cooldown", FormatDuration(secondsRemaining));
                return false;
            }

            return true;
        }

        private bool VerifyCanBuild(IPlayer player)
        {
            if ((player.Object as BasePlayer).CanBuild())
                return true;

            ReplyToPlayer(player, "Generic.Error.BuildingBlocked");
            return false;
        }

        private bool VerifyNoLockRestriction(IPlayer player, BaseCombatEntity vehicle)
        {
            var basePlayer = player.Object as BasePlayer;
            var baseLock = vehicle.GetSlot(BaseEntity.Slot.Lock);
            if (baseLock == null || baseLock.OwnerID == basePlayer.userID)
                return true;

            ReplyToPlayer(player, "Claim.Error.LockedByAnother");
            return false;
        }

        private bool VerifyNotMounted(IPlayer player, BaseCombatEntity entity)
        {
            var vehicle = entity as BaseVehicle;
            if (vehicle == null || !vehicle.AnyMounted())
                return true;

            ReplyToPlayer(player, "Claim.Error.Mounted");
            return false;
        }

        private bool VerifyCurrentlyOwned(IPlayer player, BaseCombatEntity vehicle)
        {
            var basePlayer = player.Object as BasePlayer;
            if (vehicle.OwnerID == basePlayer.userID)
                return true;

            ReplyToPlayer(player, "Unclaim.Error.NotOwned");
            return false;
        }

        private BaseCombatEntity GetSupportedVehicle(BaseEntity entity, BasePlayer player, ref string perm)
        {
            var ch47 = entity as CH47Helicopter;
            if (!ReferenceEquals(ch47, null))
            {
                perm = Permission_Claim_Chinook;
                return ch47;
            }

            var hab = entity as HotAirBalloon;
            if (!ReferenceEquals(hab, null))
            {
                perm = Permission_Claim_HotAirBalloon;
                return hab;
            }

            var ridableHorse = entity as RidableHorse;
            if (!ReferenceEquals(ridableHorse, null))
            {
                perm = Permission_Claim_RidableHorse;
                return ridableHorse;
            }

            var hitchTrough = entity as HitchTrough;
            if (!ReferenceEquals(hitchTrough, null))
            {
                perm = Permission_Claim_RidableHorse;
                return GetClosestHorse(hitchTrough, player);
            }

            var sedan = entity as BasicCar;
            if (!ReferenceEquals(sedan, null))
            {
                perm = Permission_Claim_Sedan;
                return sedan;
            }

            var car = entity as ModularCar;
            if (!ReferenceEquals(car, null))
            {
                perm = Permission_Claim_ModularCar;
                return car;
            }

            var vehicleModule = entity as BaseVehicleModule;
            if (!ReferenceEquals(vehicleModule, null))
            {
                perm = Permission_Claim_ModularCar;
                return vehicleModule.Vehicle;
            }

            var carLift = entity as ModularCarGarage;
            if (!ReferenceEquals(carLift, null))
            {
                perm = Permission_Claim_ModularCar;
                return carLift.carOccupant;
            }

            // Must go before MiniCopter.
            var scrapHeli = entity as ScrapTransportHelicopter;
            if (!ReferenceEquals(scrapHeli, null))
            {
                perm = Permission_Claim_ScrapHeli;
                return scrapHeli;
            }

            var minicopter = entity as MiniCopter;
            if (!ReferenceEquals(minicopter, null))
            {
                perm = Permission_Claim_MiniCopter;
                return minicopter;
            }

            // Must go before MotorRowboat.
            var rhib = entity as RHIB;
            if (!ReferenceEquals(rhib, null))
            {
                perm = Permission_Claim_RHIB;
                return rhib;
            }

            var rowboat = entity as MotorRowboat;
            if (!ReferenceEquals(rowboat, null))
            {
                perm = Permission_Claim_Rowboat;
                return rowboat;
            }

            var workcart = entity as TrainEngine;
            if (!ReferenceEquals(workcart, null))
            {
                perm = Permission_Claim_Workcart;
                return workcart;
            }

            // Must go before BaseSubmarine.
            var duoSub = entity as SubmarineDuo;
            if (!ReferenceEquals(duoSub, null))
            {
                perm = Permission_Claim_DuoSub;
                return duoSub;
            }

            var soloSub = entity as BaseSubmarine;
            if (!ReferenceEquals(soloSub, null))
            {
                perm = Permission_Claim_SoloSub;
                return soloSub;
            }

            var snowmobile = entity as Snowmobile;
            if (!ReferenceEquals(snowmobile, null))
            {
                if (snowmobile.ShortPrefabName == SnowmobileShortPrefabName)
                {
                    perm = Permission_Claim_Snowmobile;
                    return snowmobile;
                }
                if (snowmobile.ShortPrefabName == TomahaShortPrefabName)
                {
                    perm = Permission_Claim_Tomaha;
                    return snowmobile;
                }
            }

            return null;
        }

        #endregion

        #region Helper Classes

        private class CooldownManager
        {
            private readonly Dictionary<ulong, float> CooldownMap = new Dictionary<ulong, float>();
            private readonly float CooldownDuration;

            public CooldownManager(float duration)
            {
                CooldownDuration = duration;
            }

            public void UpdateLastUsedForPlayer(ulong userId)
            {
                if (CooldownMap.ContainsKey(userId))
                    CooldownMap[userId] = Time.realtimeSinceStartup;
                else
                    CooldownMap.Add(userId, Time.realtimeSinceStartup);
            }

            public float GetSecondsRemaining(ulong userId)
            {
                if (!CooldownMap.ContainsKey(userId))
                    return 0;

                return CooldownMap[userId] + CooldownDuration - Time.realtimeSinceStartup;
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("ClaimCooldownSeconds")]
            public float ClaimCooldownSeconds = 3600;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Helpers

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, player.Id);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Generic.Error.NoPermission"] = "You don't have permission to do that.",
                ["Generic.Error.BuildingBlocked"] = "Error: Cannot do that while building blocked.",
                ["Generic.Error.NoSupportedVehicleFound"] = "Error: No supported vehicle found.",
                ["Generic.Error.VehicleDead"] = "Error: That vehicle is dead.",
                ["Generic.Error.Cooldown"] = "Please wait <color=red>{0}</color> and try again.",
                ["Claim.Error.AlreadyOwnedByYou"] = "You already own that vehicle.",
                ["Claim.Error.DifferentOwner"] = "Error: Someone else already owns that vehicle.",
                ["Claim.Error.LockedByAnother"] = "Error: Someone else placed a lock on that vehicle.",
                ["Claim.Error.Mounted"] = "Error: That vehicle is currently occupied.",
                ["Claim.Success"] = "You now own that vehicle.",
                ["Unclaim.Error.NotOwned"] = "Error: You do not own that vehicle.",
                ["Unclaim.Success"] = "You no longer own that vehicle.",
            }, this, "en");
        }

        #endregion
    }
}
