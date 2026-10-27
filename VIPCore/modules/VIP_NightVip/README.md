![Static Badge](https://img.shields.io/badge/ver-1.3-darkgreen)

## `[VIP] Night VIP`
Gives players a temporary VIP group during a specific time window (for example at night).

### How it works
- Every `CheckTimer` seconds every alive player without VIP gets the `VIPGroup` group
- The VIP is temporary: it is not saved to the database and is gone after the player leaves the server
- The VIP ends when the time window ends. A player who reconnects during the window gets it again
- Players who already have VIP are not touched

VIPCore passes the VIP time in minutes, so set `"TimeMode": 1` in `vip_core.json`.
With `TimeMode` 0 (seconds) the VIP expires almost immediately.

### `Config`
`addons/counterstrikesharp/configs/plugins/VIPCore/Modules/vip_night.json` - created automatically on first start.

```json
{
  "VIPGroup": "vip",
  "PluginStartTime": "20:00:00",
  "PluginEndTime": "08:00:00",
  "Timezone": "UTC",
  "CheckTimer": 10,
  "VipGrantedMessage": "You are receiving VIP because it's VIP Night time.",
  "Tag": "[NightVIP]",
  "Debug": false
}
```

`VIPGroup` - VIP group from `vip.json` that is given

`PluginStartTime` / `PluginEndTime` - time window in `HH:mm:ss`. The window can cross midnight (`20:00:00` - `08:00:00`)

`Timezone` - time zone of the window, a name from `timezones.txt` (for example `Europe/Berlin`). An unknown name falls back to `UTC`

`CheckTimer` - how often players are checked, in seconds

`VipGrantedMessage` - chat message shown when the VIP is given

`Tag` - prefix of the chat message and of the console logs

`Debug` - extra console logs

The module rewrites this file on load, so comments in it are removed.
