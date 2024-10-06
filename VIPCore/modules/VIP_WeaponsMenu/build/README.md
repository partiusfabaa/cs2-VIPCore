# Allows a VIP player to select weapons packages

# Config

### in vip.json
```
              "WeaponsMenu": {
                  "CT": {
                      "AK47 + DEAGLE": {
                          "Weapons": [
                              "weapon_ak47",
                              "weapon_deagle"
                          ],
                          "Round": 2
                      },
                      "M4A1 + DEAGLE": {
                          "Weapons": [
                              "weapon_m4a1",
                              "weapon_deagle"
                          ],
                          "Round": 1
                      }
                  },
                  "T": {
                      "AWP + DEAGLE": {
                          "Weapons": [
                              "weapon_awp",
                              "weapon_deagle"
                          ],
                          "Round": 2
                      },
                      "SCOUT + DEAGLE": {
                          "Weapons": [
                              "weapon_ssg08",
                              "weapon_deagle"
                          ],
                          "Round": 1
                      }
                  }
              }
```

# in lang (not necessarily)

EN: 
```json
	"weaponsmenu.title": "Select weapons package:",
	"weaponsmenu.fromround": "[From round {0}]",
    "weaponsmenu.wanttosave": "Do you want to save this set?",
    "weaponsmenu.wanttosave.yes": "Yes",
    "weaponsmenu.wanttosave.no": "No",
    "weaponsmenu.saved": "Your free set has been saved, you will receive it when possible!",
    "weaponsmenu.resetinfo.reset": "Your free set has been reset, choose a new one in the next round!",
    "weaponsmenu.resetinfo.info": "Do you want to change your set? Use the command {orange}!vwmenu{default} to select a new one in the next round.",
    "weaponsmenu.notsaved": "Your free set was not saved, you will be able to choose a new one in the next round."
```