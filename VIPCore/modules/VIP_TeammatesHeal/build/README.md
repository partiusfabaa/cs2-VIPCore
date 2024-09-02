# Allows you to heal allies by shooting at them

# Config

The config is located at the path: `addons/counterstrikesharp/configs/VIPCore/modules/vip_teammatesheal.json`
```json
{
	"WeaponBlacklist": ["weapon_hegrenade", "weapon_molotov"], // A list of weapons that will not heal teammates when used.
	"MaxHealth": 100, 	// The maximum health to which a player can be healed. If set to 0, players can be healed up to their default maximum health.
	"HealPerShot": 20 	// The maximum amount of health that can be restored with a single shot. If set to 0, the healing amount will not be capped and will heal for the full calculated amount.
}
```

### in vip.json
`"TeammatesHeal": 50` // Where 50 is the percentage of potential damage that will be added to the player

# in translations

RU:

`"TeammatesHeal": "Лечение союзников"`

EN:

`"TeammatesHeal": "Teammates Heal"`