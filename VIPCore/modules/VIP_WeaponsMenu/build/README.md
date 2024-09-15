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
```