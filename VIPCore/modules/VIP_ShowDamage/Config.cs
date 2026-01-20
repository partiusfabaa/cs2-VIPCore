using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShowDamage
{
    public class Config : IBasePluginConfig
    {
        public string AdminGroup { get; set; } = "";

        public bool HideDamage { get; set; } = false;

        public bool ShowArmorDmg { get; set; } = true;

        public int Version { get; set; } = 1;
    }
}
