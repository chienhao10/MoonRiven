namespace MoonRiven
{
    using System;
    using LeagueSharp;
    using LeagueSharp.Common;

    internal class Program
    {
        private static void Main(string[] Args)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        private static void OnGameLoad(EventArgs Args)
        {
            if (ObjectManager.Player.ChampionName != "Riven")
            {
                return;
            }

            Riven.LoadAssembly();
        }
    }
}
