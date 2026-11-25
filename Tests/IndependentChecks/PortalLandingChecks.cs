using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.Independent;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace IndependentChecks;

public sealed partial class RuntimeChecks
{
    private void CheckPortalLanding(Player owner, Player traveler)
    {
        ModContent.GetInstance<GameManager>().CurrentPhase = GameManager.Phase.Playing;
        int x = Main.spawnTileX + 80;
        int y = Main.spawnTileY - 20;
        Call(typeof(PortalSystem), "Create", null, owner, new Vector2(x * 16, y * 16));
        NPC portal = Main.npc.Single(npc => npc.active && npc.ModNPC is TeamPortal);

        void Fill(int left, int right, bool solid)
        {
            for (int column = left; column <= right; column++)
            for (int row = y - 8; row <= y + 2; row++)
            {
                Tile tile = Main.tile[column, row];
                tile.HasTile = solid;
                tile.TileType = TileID.Stone;
                tile.LiquidAmount = 0;
            }
        }

        Fill(x - 6, x + 6, true);
        Vector2 before = traveler.Center;
        Call(typeof(PortalSystem), "Execute", null, traveler, (byte)2, portal.whoAmI);
        Check(traveler.Center == before, "Portal refuses travel when every nearby landing is blocked");

        Fill(x + 2, x + 6, false);
        Call(typeof(PortalSystem), "Execute", null, traveler, (byte)2, portal.whoAmI);
        Check(traveler.Center.X > portal.Center.X && Vector2.Distance(traveler.Center, portal.Center) <= 80f &&
            !Collision.SolidCollision(traveler.position, traveler.width, traveler.height),
            "Portal finds the open side when all five original vertical landings are blocked");
    }
}
