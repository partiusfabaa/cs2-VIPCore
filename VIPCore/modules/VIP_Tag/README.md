# Shows a tag on the scoreboard and a coloured tag in chat

Tags can come from the player's VIP group, from named tag sets, or be personal (by SteamID64).
If a player has several tags, they pick one in the menu.

# Tag

```json
{
  "ScoreboardTag": "ADMIN",
  "ChatTag": "[ADMIN]",
  "ColorTag": "red",
  "NameColor": "red",
  "MessageColor": "grey"
}
```

`ScoreboardTag` - plain text shown on the scoreboard (clan tag)

`ChatTag` - prefix shown before the name in chat

`ColorTag` - colour of the chat tag prefix

`NameColor` - colour of the player name in chat

`MessageColor` - colour of the message body in chat

Supported colours: `default, white, darkred, green, lightyellow, lightblue, olive, lime, red, lightpurple, purple, grey, yellow, gold, silver, blue, darkblue, bluegrey, magenta, lightred, orange, teamcolor`

# Config

### configs/plugins/VIPCore/Modules/vip_tag.json

Created automatically on first start.

```json
{
  "TagSets": {
    "VIP1": [
      { "ScoreboardTag": "ALO", "ChatTag": "[alo]", "ColorTag": "gold" },
      { "ScoreboardTag": "ALO2", "ChatTag": "[alo2]", "ColorTag": "green" }
    ],
    "VIP2": [
      { "ScoreboardTag": "ALO3", "ChatTag": "[alo3]", "ColorTag": "gold" }
    ]
  },
  "Players": {
    "76561198000000001": [
      "VIP2",
      { "ScoreboardTag": "KING", "ChatTag": "[KING]", "ColorTag": "gold", "NameColor": "gold" }
    ]
  }
}
```

`TagSets` - named lists of tags that can be reused in VIP groups and personal tags

`Players` - personal tags by SteamID64. They work for any player, VIP or not, and are listed first

### in vip.json

```json
"Tag": ["VIP1", "VIP2"]
```

The value can be a tag set name, a tag, or a list mixing both:

```json
"Tag": ["VIP1", { "ScoreboardTag": "VIP", "ChatTag": "[VIP]", "ColorTag": "lime" }]
```

A single tag still works too:

```json
"Tag": { "ScoreboardTag": "VIP", "ChatTag": "[VIP]", "ColorTag": "lime" }
```

# Menu

`!vip` -> `Tag` or `!tag` opens the tag picker: choose one of your tags or disable it.
The first tag is used until the player picks another one. The choice is saved.

# Commands

`css_tag` - open the tag picker

`css_tag_reload` - reload `vip_tag.json` (`@css/root`)

# Team chat

Messages in team chat are shown as `[TAG] (team) nick: message`.

# Gag / Silence

The chat tag is rendered by this module, so it respects gag/silence. The admin plugin is detected
automatically at load (any/all of these):

- CS2-SimpleAdmin (via its `simpleadmin:api`),
- Iks_Admin (via its `iksadmin:core` capability),
- CS2-AdminPlus (via its `communication_data.json`).

A gagged or silenced player sends nothing through the tag. If none of these admin plugins is
installed, a warning is logged and gag/silence is not enforced for the chat tag.
