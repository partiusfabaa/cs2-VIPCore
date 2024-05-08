![Static Badge](https://img.shields.io/badge/ver-1.0.0-darkgreen)
![Static Badge](https://img.shields.io/badge/CS2VIPCore-v1-purple)

## `[VIP] Night VIP`
The `[VIP] Night VIP` plugin assigns players to a VIP group based on specific time periods using the cs2-VIPCore API.

### Features:
- Automatically assigns players to the VIP group if they don't already belong
- Removes VIP status if a player disconnects (only if they belong to the specified group)
- VIPs are only assigned during a specified time window

### `Config`
Place the configuration file at `addons/counterstrikesharp/configs/plugins/VIPCore/Modules/VIP_NightVipConfig.json`.

```json
{
  "VIPGroup": "VIP",
  "PluginStartTime": "20:00:00",
  "PluginEndTime": "06:00:00"
}