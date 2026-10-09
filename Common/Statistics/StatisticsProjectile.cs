using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.Statistics;

public class StatisticsProjectile : GlobalProjectile
{
    private Item sourceItem;

    public override bool InstancePerEntity => true;

    public override void Load()
    {
        On_PlayerDeathReason.ByProjectile += OnPlayerDeathReasonByProjectile;
    }

    public override void Unload()
    {
        On_PlayerDeathReason.ByProjectile -= OnPlayerDeathReasonByProjectile;
    }

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        // keep track of the item that came with our entity source.
        // if that source is our parent projectile, take its item.
        var item = source switch
        {
            IEntitySource_WithStatsFromItem sourceItemUse when sourceItemUse.Player.whoAmI == projectile.owner => sourceItemUse.Item,
            EntitySource_Parent sourceParent when sourceParent.Entity is Projectile parentProjectile &&
                                                  parentProjectile.whoAmI != projectile.whoAmI &&
                                                  parentProjectile.owner == projectile.owner => parentProjectile
                .GetGlobalProjectile<StatisticsProjectile>()
                .sourceItem,
            _ => null
        };

        sourceItem = item?.Clone();
    }

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        ItemIO.Send(sourceItem ?? new Item(), binaryWriter);
    }

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
    {
        sourceItem = ItemIO.Receive(binaryReader);
    }

    private PlayerDeathReason OnPlayerDeathReasonByProjectile(On_PlayerDeathReason.orig_ByProjectile orig,
        int playerindex, int projectileindex)
    {
        var self = orig(playerindex, projectileindex);

        Projectile projectile = Main.projectile[projectileindex];
        if (projectile.owner == playerindex)
            self.SourceItem = projectile.GetGlobalProjectile<StatisticsProjectile>().sourceItem;

        return self;
    }
}
