using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.Travel;

internal sealed class BossProjectileSource : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    public bool IsFromBoss { get; private set; }

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (!projectile.hostile || source is not EntitySource_Parent parent)
            return;

        IsFromBoss = parent.Entity switch
        {
            NPC npc => npc.boss || (npc.realLife >= 0 && npc.realLife < Main.maxNPCs &&
                Main.npc[npc.realLife] is { active: true, boss: true }),
            Projectile projectileParent => projectileParent.GetGlobalProjectile<BossProjectileSource>().IsFromBoss,
            _ => false
        };
    }

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter) =>
        bitWriter.WriteBit(IsFromBoss);

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) =>
        IsFromBoss = bitReader.ReadBit();
}
