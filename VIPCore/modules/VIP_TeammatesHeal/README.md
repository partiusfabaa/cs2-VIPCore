# Allows a VIP player to heal teammates by shooting them

A shot heals the teammate by a percentage of the shot's damage.

# Config

The config is located at the path: `addons/counterstrikesharp/configs/plugins/VIPCore/Modules/vip_teammates_heal.json`
```json
{
	"WeaponBlacklist": ["weapon_hegrenade", "weapon_molotov"], // Weapons that do not heal teammates
	"MaxHealth": 100, 	// Teammates are healed up to this health. 0 - up to the teammate's maximum health
	"HealPerShot": 25 	// Maximum health restored by one shot. 0 - no limit
}
```

### in vip.json
`"TeammatesHeal": 50` // 50% of the shot's damage is restored to the teammate

The value is read when the VIP player spawns, so a change applies after the next respawn.
