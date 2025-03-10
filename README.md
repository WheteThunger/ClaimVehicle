## Features

- Allows players to claim ownership of unowned vehicles
- Allows players to relinquish ownership of a vehicle they own, so that another player can claim it

## How it works

By itself, "owning" a vehicle does nothing. However, various plugins enable special features for owned vehicles based on the permissions of the owner. Some examples:
- [Vehicle Decay Protection](https://umod.org/plugins/vehicle-decay-protection) -- Disable decay of vehicles owned by privileged players
- [Vehicle Deployed Locks](https://umod.org/plugins/vehicle-deployed-locks) -- Allow players to deploy locks to only vehicles they own
- [Portable Vehicles](https://umod.org/plugins/portable-vehicles) -- Allow players to pick up vehicles they own
- [Vehicle Storage](https://umod.org/plugins/vehicle-storage) -- Allows adding or increasing storage on vehicles owned by privileged players
- [Car Turrets](https://umod.org/plugins/car-turrets) -- Increase the maximum number of turrets allowed on modular cars owned by privileged players

A vehicle is considered owned if its `OwnerID` property is set to a player's Steam ID. A vehicle is considered unowned if its `OwnerID` is 0. Many plugins that spawn vehicles for a player will set ownership, so this plugin is mostly useful for allowing players to claim vehicles that spawned randomly in the world or at NPC vendors.

Note: If you just want vehicles spawned at NPC vendors to be owned by the player that purchased them, you can accomplish that with the [Vehicle Vendor Options](https://umod.org/plugins/vehicle-vendor-options) plugin.

## Permissions

- `claimvehicle.claim.allvehicles` -- Allows the player to claim unowned vehicles of all supported types.
- `claimvehicle.unclaim` -- Allows the player to relinquish ownership of a vehicle using the `vunclaim` command, so that someone else can claim it. This does *not* reset their cooldown.
- `claimvehicle.nocooldown` -- Allows the player to claim vehicles with no cooldown.

As an alternative to the `claimvehicle.claim.allvehicles` permission, you can grant permissions to claim vehicles by type:

- `claimvehicle.claim.attackhelicopter`
- `claimvehicle.claim.ballista`
- `claimvehicle.claim.batteringram`
- `claimvehicle.claim.caboose`
- `claimvehicle.claim.catapult`
- `claimvehicle.claim.chinook`
- `claimvehicle.claim.duosub`
- `claimvehicle.claim.hotairballoon`
- `claimvehicle.claim.locomotive`
- `claimvehicle.claim.minicopter`
- `claimvehicle.claim.modularcar`
- `claimvehicle.claim.motorbike.sidecar`
- `claimvehicle.claim.motorbike`
- `claimvehicle.claim.pedalbike`
- `claimvehicle.claim.pedaltrike`
- `claimvehicle.claim.rhib`
- `claimvehicle.claim.ridablehorse`
- `claimvehicle.claim.rowboat`
- `claimvehicle.claim.scraptransporthelicopter`
- `claimvehicle.claim.sedan`
- `claimvehicle.claim.sedanrail`
- `claimvehicle.claim.siegetower`
- `claimvehicle.claim.snowmobile`
- `claimvehicle.claim.solosub`
- `claimvehicle.claim.tomaha`
- `claimvehicle.claim.wagona`
- `claimvehicle.claim.wagonb`
- `claimvehicle.claim.wagonc`
- `claimvehicle.claim.workcart`
- `claimvehicle.claim.workcartaboveground`
- `claimvehicle.claim.workcartcovered`

## Commands

- `vclaim` -- Claim ownership of the vehicle you are aiming at. It must be unowned, unmounted, and not have a lock deployed to it by another player. You must also not be building blocked.
- `vunclaim` -- Relinquish ownership of the vehicle you are aiming at. You must already own it.

Note: The above commands can be changed in the configuration. Additionally, you can define multiple commands to perform the same function in case players are accustomed to different commands from other servers.

## Configuration

Default configuration:

```json
{
  "Claim cooldown (seconds)": 3600.0,
  "Claim commands": [
    "vclaim"
  ],
  "Unclaim commands": [
    "vunclaim"
  ]
}
```

- `Claim cooldown (seconds)` -- Determines how long a player must wait after claiming a vehicle before they can claim another.
  - Caution: Before reducing this value, consider other plugins you are running and whether this will allow for griefing potential. For example, a low cooldown may enable players to go around the map and lock or pick up every vehicle they find.
  - Note: Cooldowns are lost on plugin reload (or server restart). Cooldown persistence can be implemented on request.
- `Claim commands` -- Determines which commands can be used to claim vehicles.
- `Unclaim commands` -- Determines which commands can be used to unclaim vehicles.

## Localization

```json
{
  "Generic.Error.NoPermission": "You don't have permission to do that.",
  "Generic.Error.BuildingBlocked": "Error: Cannot do that while building blocked.",
  "Generic.Error.NoSupportedVehicleFound": "Error: No supported vehicle found.",
  "Generic.Error.VehicleDead": "Error: That vehicle is dead.",
  "Generic.Error.Cooldown": "Please wait <color=red>{0}</color> and try again.",
  "Claim.Error.AlreadyOwnedByYou": "You already own that vehicle.",
  "Claim.Error.DifferentOwner": "Error: Someone else already owns that vehicle.",
  "Claim.Error.LockedByAnother": "Error: Someone else placed a lock on that vehicle.",
  "Claim.Error.Mounted": "Error: That vehicle is currently occupied.",
  "Claim.Success": "You now own that vehicle.",
  "Unclaim.Error.NotOwned": "Error: You do not own that vehicle.",
  "Unclaim.Success": "You no longer own that vehicle."
}
```

## Developer Hooks

#### OnVehicleClaim

```csharp
object OnVehicleClaim(BasePlayer player, BaseCombatEntity vehicle)
```

- Called when a player tries to claim a vehicle.
- Returning `false` will prevent the default behavior.
- Returning `null` will result in the default behavior.

#### OnVehicleUnclaim

```csharp
object OnVehicleUnclaim(BasePlayer player, BaseCombatEntity vehicle)
```

- Called when a player tries to relinquish ownership of a vehicle.
- Returning `false` will prevent the default behavior.
- Returning `null` will result in the default behavior.

#### OnVehicleOwnershipChanged

```csharp
void OnVehicleOwnershipChanged(BaseCombatEntity vehicle)
```

This hook is called when a player successfully claims or unclaims a vehicle. This allows other plugins to do things like update vehicle storage capacity based on the new owner.
