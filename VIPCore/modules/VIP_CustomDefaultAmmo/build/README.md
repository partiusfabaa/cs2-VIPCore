# Allows to modify Default Ammo Clip and Reserve for weapons 

# Config

### in vip.json
```json
  "CustomDefaultAmmo": true
```
### in configs/plugins/VIPCore/Modules/vip_custom_default_ammo.json
```json
{
  "WeaponSettings": {
    "weapon_awp": {
      "DefaultClip": 50,
      "DefaultReserve": 100
    },
    "weapon_ak47": {
      "DefaultClip": 30,
      "DefaultReserve": 120
    },
    "weapon_m4a1": {
      "DefaultClip": 20,
      "DefaultReserve": 90
    }
  }
  //add more as needed
}
```

# in lang (not necessarily)


EN: 
```json
"CustomDefaultAmmo" : "Custom Default Ammo"
```

## Many thanks for the base to **https://github.com/1Mack/CS2-CustomDefaultAmmo**