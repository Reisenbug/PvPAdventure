using System;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.Spawnbox;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

public sealed class RacePeriodPlayer : ModPlayer
{
    private const int RaceMount = MountID.Bunny;
    private const float RunAcceleration = 0.13f;
    private const float SlowRunAcceleration = RunAcceleration / 5f;
    private const float RunSlowdown = 0.6f;
    private const int MaxAirJumps = 20;
    private const float AirJumpHeightMultiplier = 2.5f;
    private const int AirJumpCooldownFrames = 40;

    private static float FullRunSpeed => ModContent.GetInstance<ServerConfig>().RacePeriodRunSpeed;
    private static float BaseRunSpeed => FullRunSpeed * 22f / 40f;
    private static float JumpSpeed => Mount.mounts[RaceMount].jumpSpeed;
    private static int JumpHeight => Mount.mounts[RaceMount].jumpHeight;
    private static bool FeatureEnabled => ModContent.GetInstance<ServerConfig>().RacePeriodEnabled;
    private static bool Waiting => ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Waiting;

    private bool raceMountApplied;
    private bool wasJumpPressed;
    private bool wasAirborne;
    private int airJumpCount;
    private bool isGrounded;
    private int jumpCooldown;
    private bool wasWaiting;

    public bool InRacePeriod => FeatureEnabled && raceMountApplied && !Player.dead && !Player.ghost;
    internal bool IsRacing => InRacePeriod && Player.mount.Active && Player.mount.Type == RaceMount;
    private bool InEntryArea => FeatureEnabled && Waiting && Player.active && !Player.dead && !Player.ghost &&
        ModContent.GetInstance<RegionManager>().GetRegionContaining(Player.Center.ToTileCoordinates()) != null;

    public override void Load() => On_Player.QuickMount += BlockMountToggleWhileWaiting;
    public override void Unload() => On_Player.QuickMount -= BlockMountToggleWhileWaiting;

    private static void BlockMountToggleWhileWaiting(On_Player.orig_QuickMount orig, Player player)
    {
        if (!player.GetModPlayer<RacePeriodPlayer>().InEntryArea)
            orig(player);
    }

    public override void SetControls()
    {
        if (InRacePeriod)
        {
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }
        if (InEntryArea)
            Player.controlHook = false;
    }

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool pvp) => InRacePeriod;

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) =>
        ClearRaceState();

    public override void PreUpdate()
    {
        isGrounded = Player.velocity.Y == 0f;
        bool jumpJustPressed = Player.controlJump && !wasJumpPressed;

        if (wasAirborne && isGrounded)
            airJumpCount = 0;

        if (jumpCooldown > 0)
            jumpCooldown--;

        bool cooldownGate = airJumpCount == 0 || jumpCooldown == 0;
        bool underJumpLimit = airJumpCount <= MaxAirJumps;
        bool wantsAirJump = InRacePeriod && !isGrounded && jumpJustPressed && cooldownGate && underJumpLimit;

        if (wantsAirJump)
        {
            if (airJumpCount == 0)
            {
                Player.velocity.Y = -JumpSpeed;
                Player.jump = JumpHeight;
            }
            else
            {
                Player.velocity.X *= 0.66f;
                Player.velocity.Y = -JumpSpeed * AirJumpHeightMultiplier;
                Player.jump = 0;
                if (!Main.dedServ)
                    SpawnJumpPoof();

                if (airJumpCount > 1)
                    jumpCooldown = AirJumpCooldownFrames;
            }

            airJumpCount++;
        }

        wasJumpPressed = Player.controlJump;
        wasAirborne = !isGrounded;
    }

    private void ResetMountHitbox()
    {
        int boost = Player.height - Player.defaultHeight;
        if (boost == 0)
            return;

        Player.position.Y += boost;
        Player.height = Player.defaultHeight;
    }

    private void SpawnJumpPoof()
    {
        for (int i = 0; i < 6; i++)
        {
            Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                DustID.Cloud, 0f, 0f, 100, default, 1.5f);
            dust.velocity *= 0.4f;
            dust.velocity.Y -= Main.rand.NextFloat(0.5f, 1.5f);
            dust.noGravity = false;
            dust.fadeIn = 0.5f;
        }
    }

    public override void PostUpdateRunSpeeds()
    {
        if (!InRacePeriod)
            return;

        ResetMountHitbox();

        float fullSpeed = FullRunSpeed;
        float baseSpeed = BaseRunSpeed;
        float fastAccel = RunAcceleration;
        float currentAbsSpeed = Math.Abs(Player.velocity.X);

        if (isGrounded)
        {
            bool pressingOpposite = (Player.controlRight && Player.velocity.X < 0f)
                                 || (Player.controlLeft && Player.velocity.X > 0f);

            Player.maxRunSpeed = fullSpeed;
            Player.accRunSpeed = fullSpeed;
            Player.runAcceleration = pressingOpposite || currentAbsSpeed < baseSpeed
                ? fastAccel
                : SlowRunAcceleration;
            Player.runSlowdown = RunSlowdown;
        }
        else if (airJumpCount <= 1)
        {
            Player.maxRunSpeed = Math.Max(baseSpeed, currentAbsSpeed);
            Player.accRunSpeed = Player.maxRunSpeed;
            Player.runAcceleration = fastAccel;
            Player.runSlowdown = 0f;
        }
        else
        {
            float floorCap = fullSpeed * 5f / 40f;
            Player.maxRunSpeed = Math.Max(floorCap, currentAbsSpeed);
            Player.accRunSpeed = Player.maxRunSpeed;
            Player.runAcceleration = fastAccel;
        }

        Player.noFallDmg = true;

        Player.jumpSpeed = JumpSpeed;
        Player.jumpHeight = JumpHeight;
    }

    public override void PostUpdate()
    {
        if (!IsRacing)
            return;

        Mount mount = Player.mount;
        mount._flyTime = 0;
        mount._abilityCharge = 0;
        mount._abilityCharging = false;
        mount._abilityActive = false;
    }

    public override void PostUpdateMiscEffects()
    {
        bool waiting = Waiting;
        if (!FeatureEnabled || Player.dead || Player.ghost || waiting && !wasWaiting)
        {
            if (raceMountApplied && Player.mount.Active && Player.mount.Type == RaceMount)
                Player.mount.Dismount(Player);
            ClearRaceState();
        }

        wasWaiting = waiting;
        if (raceMountApplied)
        {
            if (!Player.mount.Active || Player.mount.Type != RaceMount)
                ClearRaceState();
            return;
        }

        if (!InEntryArea)
            return;

        Player.mount.SetMount(RaceMount, Player);
        raceMountApplied = Player.mount.Active && Player.mount.Type == RaceMount;
    }

    private void ClearRaceState()
    {
        raceMountApplied = false;
        airJumpCount = 0;
        jumpCooldown = 0;
    }
}

public sealed class RacePeriodItemBlock : GlobalItem
{
    private static bool Restricted(Player player) =>
        player.GetModPlayer<RacePeriodPlayer>().InRacePeriod;

    public override bool CanUseItem(Item item, Player player) => !Restricted(player);

    public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded) =>
        !Restricted(player);

    public override bool? CanBeChosenAsAmmo(Item ammo, Item weapon, Player player) =>
        Restricted(player) ? false : null;
}

public sealed class RacePeriodSpawnSuppressor : GlobalNPC
{
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (player.GetModPlayer<RacePeriodPlayer>().InRacePeriod)
            maxSpawns = 0;
    }
}
