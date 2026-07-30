using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using RayTraceAPI;

namespace NadeSystem;

public partial class NadeSystemPlugin : BasePlugin
{
    // ═══════════════════════════════════════════════════════════
    //  Information system: sound trail + vision
    //  Updated every tick for ALL players
    // ═══════════════════════════════════════════════════════════
    // Record a sound point at the player's current origin.
    // * Records an audible position for a player when an enemy can hear it
    private void RecordSoundPoint(CCSPlayerController? player, List<CCSPlayerController>? allPlayers = null)
    {
        if (player == null) return;
        var origin = GetActiveLivePawn(player)?.AbsOrigin;
        if (origin == null) return;

        // Only keep this sound point if at least one enemy is close enough to hear it.
        float ox = origin.X, oy = origin.Y, oz = origin.Z;
        float r2 = SoundHearRadius * SoundHearRadius;
        var candidates = allPlayers
            ?? Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller").ToList();
        bool audibleToEnemy = candidates
            .Any(e =>
            {
                if (!e.IsValid || (int)e.TeamNum == player.TeamNum) return false;
                var ep = GetActiveLivePawn(e)?.AbsOrigin;
                if (ep == null) return false;
                float dx = ep.X - ox, dy = ep.Y - oy, dz = ep.Z - oz;
                return dx*dx + dy*dy + dz*dz <= r2;
            });
        if (!audibleToEnemy) return;

        uint idx = (uint)player.Index;
        if (!_soundPoints.TryGetValue(idx, out var list))
        {
            list = new List<SoundPoint>();
            _soundPoints[idx] = list;
        }
        // Dedup: skip if within 1u of the last point. Repeated sound made in place
        if (list.Count > 0)
        {
            var last = list[^1];
            float ddx = ox - last.X, ddy = oy - last.Y, ddz = oz - last.Z;
            if (ddx * ddx + ddy * ddy + ddz * ddz < 1f) return;
        }
        list.Add(new SoundPoint(ox, oy, oz));
    }

    // * Records weapon fire as sound and recent combat information
    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var p = @event.Userid;
        if (p != null && p.IsValid)
            _botLastFireTime[(uint)p.Index] = Server.CurrentTime;
        RecordSoundPoint(p);
        return HookResult.Continue;
    }

    // * Records weapon reload sound information
    private HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
    {
        RecordSoundPoint(@event.Userid);
        return HookResult.Continue;
    }

    // * Records weapon zoom sound information
    private HookResult OnWeaponZoom(EventWeaponZoom @event, GameEventInfo info)
    {
        RecordSoundPoint(@event.Userid);
        return HookResult.Continue;
    }

    // * Records native grenade throws as sound information
    private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        RecordSoundPoint(@event.Userid);
        return HookResult.Continue;
    }

    // * Records player jumps as sound information
    private HookResult OnPlayerJump(EventPlayerJump @event, GameEventInfo info)
    {
        RecordSoundPoint(@event.Userid);
        return HookResult.Continue;
    }

    // Per-tick maintenance of every player's sound trail.
    // Delete sound points that are now farther than SoundInfoRadius from the player.
    // Add a fresh point if the player is currently making footstep sound (speed > threshold).
    // Pruning + dead-player cleanup run every call; footstep recording is throttled by the caller to every 4 ticks.
    // * Prunes sound trails and optionally records current footsteps
    private void UpdateSoundTrails(bool recordFootsteps)
    {
        var allPlayers = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller").ToList();
        foreach (var p in allPlayers)
        {
            if (!p.IsValid) continue;
            uint idx = (uint)p.Index;

            var pawn = GetActiveLivePawn(p);
            if (pawn == null)
            {
                _soundPoints.Remove(idx);
                continue;
            }
            var origin = pawn.AbsOrigin;
            if (origin == null) continue;

            float cx = origin.X, cy = origin.Y, cz = origin.Z;

            // Delete all kinds of sound points outside the info radius (keep only what is "near here").
            if (_soundPoints.TryGetValue(idx, out var list) && list.Count > 0)
            {
                float r2 = SoundInfoRadius * SoundInfoRadius;
                list.RemoveAll(pt =>
                {
                    float dx = pt.X - cx, dy = pt.Y - cy, dz = pt.Z - cz;
                    return dx * dx + dy * dy + dz * dz > r2;
                });
            }

            // Footstep sound: horizontal speed above threshold.
            if (recordFootsteps)
            {
                var vel = pawn.AbsVelocity;
                if (vel != null)
                {
                    float speed2 = vel.X * vel.X + vel.Y * vel.Y;
                    if (speed2 > FootstepSpeedThreshold * FootstepSpeedThreshold)
                        RecordSoundPoint(p, allPlayers);
                }
            }
        }
    }

    // True if this player currently has any retained sound point near them
    // (Made audible sound here, now or while passing within SoundInfoRadius).
    // * Checks whether a player has retained audible information
    private bool PlayerMadeAudibleSound(CCSPlayerController player)
    {
        if (!player.IsValid) return false;
        return _soundPoints.TryGetValue((uint)player.Index, out var list) && list.Count > 0;
    }

    // True if the given enemy currently sees the target via the official spotting system.
    // Reads the TARGET's SpottedByMask and checks the enemy's slot bit.
    // Falls back to FOV + RayTrace if the schema read is unavailable.
    // * Checks official spotting state before using geometric vision
    private bool EnemySeesTarget(CCSPlayerController enemy, CCSPlayerController target)
    {
        if (!enemy.IsValid || !target.IsValid) return false;
        var targetPawn = GetActiveLivePawn(target);
        if (targetPawn == null || !targetPawn.IsValid) return false;

        try
        {
            var spotted = targetPawn.EntitySpottedState;
            if (spotted != null)
            {
                int slot = enemy.Slot; // entity index - 1
                if (slot >= 0)
                {
                    var mask = spotted.SpottedByMask; // uint[2]
                    int word = slot / 32;
                    int bit  = slot % 32;
                    if (word >= 0 && word < mask.Length)
                        return (mask[word] & (1u << bit)) != 0;
                }
            }
        }
        catch { /* fall through to geometric check */ }

        // Fallback: FOV + RayTrace from enemy eyes to target eyes.
        return EnemySeesTargetGeometric(enemy, target);
    }

    // Geometric vision fallback.
    // * Performs a field-of-view and line-of-sight vision check
    private bool EnemySeesTargetGeometric(CCSPlayerController enemy, CCSPlayerController target)
    {
        var ep = GetActiveLivePawn(enemy);
        var tp = GetActiveLivePawn(target);
        if (ep?.AbsOrigin == null || ep.EyeAngles == null) return false;
        if (tp?.AbsOrigin == null) return false;

        float eyeX = ep.AbsOrigin.X, eyeY = ep.AbsOrigin.Y, eyeZ = ep.AbsOrigin.Z + 64f;
        float tx = tp.AbsOrigin.X, ty = tp.AbsOrigin.Y, tz = tp.AbsOrigin.Z + 64f;

        float dx = tx - eyeX, dy = ty - eyeY, dz = tz - eyeZ;
        float dist2 = dx * dx + dy * dy + dz * dz;
        if (dist2 > 1300f * 1300f) return false;

        float eYawRad   =  ep.EyeAngles.Y * MathF.PI / 180f;
        float ePitchRad = -ep.EyeAngles.X * MathF.PI / 180f;
        float fwdX = MathF.Cos(ePitchRad) * MathF.Cos(eYawRad);
        float fwdY = MathF.Cos(ePitchRad) * MathF.Sin(eYawRad);
        float fwdZ = MathF.Sin(ePitchRad);

        float yawToT   = MathF.Atan2(dy, dx);
        float eyeYaw   = MathF.Atan2(fwdY, fwdX);
        float deltaYaw = MathF.Abs(MathF.Atan2(MathF.Sin(yawToT - eyeYaw),
                                               MathF.Cos(yawToT - eyeYaw)));
        float pitchToT = MathF.Atan2(dz, MathF.Sqrt(dx * dx + dy * dy));
        float eyePitch = MathF.Atan2(fwdZ, MathF.Sqrt(fwdX * fwdX + fwdY * fwdY));
        float deltaPitch = MathF.Abs(pitchToT - eyePitch);
        if (deltaYaw <= 0.927f && deltaPitch <= MathF.PI / 4f) // Horizontal FOV 106° // Vertical FOV 90°
        {
            var tEye = new Vec3 { X = tx, Y = ty, Z = tz };
            return FlashHasLoS(tEye, eyeX, eyeY, eyeZ);
        }
        return false;
    }

    // General-purpose: does `enemy` have information (vision or sound) on `target`?
    // * Combines sound and vision into an enemy information check
    private bool HasInformationOn(CCSPlayerController enemy, CCSPlayerController target)
    {
        if (PlayerMadeAudibleSound(target)) return true;
        if (EnemySeesTarget(enemy, target)) return true;
        return false;
    }

    // Recently fired（used to suppress HE/molotov right after shooting）
    // * Checks whether a player fired within the requested interval
    private bool FiredRecently(CCSPlayerController player, float seconds)
    {
        if (_botLastFireTime.TryGetValue((uint)player.Index, out float t))
            return Server.CurrentTime - t < seconds;
        return false;
    }

}
