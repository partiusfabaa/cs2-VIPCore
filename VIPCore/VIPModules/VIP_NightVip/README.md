## `[VIP] Night VIP`
The `[VIP] Night VIP` plugin assigns players to a VIP group based on specific time periods.

### Features:
- Automatically assigns players to the VIP group if they don't already belong
- Removes VIP status if a player disconnects (only if they belong to the specified group)
- VIPs are only assigned during a specified time window

### `Config`
Place the configuration file at `addons/counterstrikesharp/configs/plugins/VIPCore/Modules/vip_night.json`.

```json
{
  "VIPGroup": "VIP",
  "PluginStartTime": "20:00:00",
  "PluginEndTime": "06:00:00",
  "Timezone": "UTC", //default, see timezones.txt for all possible time zones, will also work with "UTC+2" format
  "CheckTimer": 10,
  "VipGrantedMessage": "You are receiving VIP because it's VIP Night time.",
  "Tag": "[NightVIP]"  
}
```
