# Shows a tag on the scoreboard and a coloured tag in chat for the VIP player

# Config

### in vip.json

```json
"Tag": {
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

# Gag / Silence

The chat tag is rendered by this module, so it respects gag/silence. The admin plugin is detected
automatically at load (any/all of these):

- CS2-SimpleAdmin (via its `simpleadmin:api`),
- Iks_Admin (via its `iksadmin:core` capability),
- CS2-AdminPlus (via its `communication_data.json`).

A gagged or silenced player sends nothing through the tag. If none of these admin plugins is
installed, a warning is logged and gag/silence is not enforced for the chat tag.

# Menu

The feature is a toggle in `!vip` - the player can Enable or Disable their tag.
