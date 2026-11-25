using System;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.RacePeriod;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace IndependentChecks;

public sealed partial class RuntimeChecks
{
    private void CheckRacePeriod(Player player)
    {
        var config = ModContent.GetInstance<ServerConfig>();
        var game = ModContent.GetInstance<GameManager>();
        var race = player.GetModPlayer<RacePeriodPlayer>();
        config.RacePeriodEnabled = true;
        config.RacePeriodRunSpeed = 8f;
        race.PostUpdateMiscEffects();
        Check(race.InRacePeriod && player.mount.Type == MountID.Bunny, "Spawn region mounts the race bunny while waiting");

        player.velocity = Vector2.Zero;
        race.PreUpdate();
        race.PostUpdateRunSpeeds();
        Check(player.maxRunSpeed == 8f && player.runAcceleration == 0.13f && player.height == Player.defaultHeight,
            "Race bunny uses upstream ground speed, acceleration and player hitbox");
        player.velocity = new Vector2(5f, 0f);
        race.PostUpdateRunSpeeds();
        Check(Math.Abs(player.runAcceleration - 0.026f) < 0.0001f, "Ground acceleration tapers above the upstream threshold");

        player.velocity = new Vector2(8f, 2f);
        player.controlJump = true;
        race.PreUpdate();
        float ordinaryJump = player.velocity.Y;
        Check(ordinaryJump < 0f && player.velocity.X == 8f, "First air jump keeps horizontal momentum");
        player.controlJump = false;
        race.PreUpdate();
        player.controlJump = true;
        race.PreUpdate();
        Check(Math.Abs(player.velocity.X - 8f * 0.66f) < 0.0001f &&
            Math.Abs(player.velocity.Y - ordinaryJump * 2.5f) < 0.0001f, "Powered air jump trades speed for lift");

        game.CurrentPhase = GameManager.Phase.Playing;
        race.PostUpdateMiscEffects();
        Check(race.InRacePeriod, "Race continues when the match starts");
        Vector2 spawn = player.Center;
        player.Center += new Vector2(80 * 16, 0);
        player.mount.Dismount(player);
        race.PostUpdateMiscEffects();
        Check(!race.InRacePeriod, "Dismount clears race protection");
        race.PostUpdateMiscEffects();
        Check(!player.mount.Active, "Players cannot remount outside the spawn region");
        player.Center = spawn;
        race.PostUpdateMiscEffects();
        Check(race.InRacePeriod && player.mount.Type == MountID.Bunny, "Returning to spawn during the match mounts the race bunny");
        player.dead = true;
        race.PostUpdateMiscEffects();
        Check(!player.mount.Active && !race.InRacePeriod, "Dead players cannot enter race state");
        player.dead = false;
        race.PostUpdateMiscEffects();
        Check(race.InRacePeriod && player.mount.Type == MountID.Bunny, "Respawning at world spawn mounts the race bunny");
    }
}
