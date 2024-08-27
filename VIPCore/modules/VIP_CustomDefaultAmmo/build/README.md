# Allows to modify Default Ammo Clip and Reserve for weapons 

# Config

### in vip.json
```json
  "CustomDefaultAmmo": "VIPGroup1"
```
### in configs/plugins/VIPCore/Modules/vip_custom_default_ammo.json
```json
{
    "VIPGroup1": {  //can be named anyway
        "weapon_awp": {
            "DefaultClip": 50,
            "DefaultReserve": 100
        },
        "weapon_ak47": {
            "DefaultClip": 35,
            "DefaultReserve": 120
        }
    },
    "VIPGroup2": {
        "weapon_awp": {
            "DefaultClip": 30,
            "DefaultReserve": 90
        },
        "weapon_m4a1": {
            "DefaultClip": 30,
            "DefaultReserve": 90
        }
    },
    "VIPGroup3": {
        "weapon_awp": {
            "DefaultClip": 40,
            "DefaultReserve": 100
        },
        "weapon_deagle": {
            "DefaultClip": 7,
            "DefaultReserve": 35
        }
    }
    //add more as needed
}
  
```
![image](https://github.com/user-attachments/assets/c01b41d6-fb9f-43d9-893f-46c90f0f17d2)

# in lang (not necessarily)


EN: 
```json
"CustomDefaultAmmo" : "Custom Default Ammo"
```

## Many thanks for the base to **https://github.com/1Mack/CS2-CustomDefaultAmmo**
