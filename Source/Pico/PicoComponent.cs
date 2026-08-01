using System;
using System.Collections.Generic;
using Celeste.Mod.Roslyn.ModLifecycleAttributes;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System.Collections;
using MonoMod.Utils;
using Microsoft.Xna.Framework.Graphics;
using ExtendedVariants.Module;
using ExtendedVariants.Variants;

namespace Celeste.Mod.Picoline.Pico;

[Tracked]
public class PicoComponent() : Component(false, false) {

    Color RandomHairColor = Utils.Colors.Pink;

    [MonoModLinkTo("Celeste.Actor", "System.Void Update()")]
    [MonoModForceCall]
    static extern void ActorUpdate(Actor self);
    [MonoModLinkTo("Celeste.Entity", "System.Void Render()")]
    [MonoModForceCall]
    static extern void EntityRender(Entity self);

    static readonly Vector2 HackyOffset = new(-4, -8);

    public override void Added(Entity entity) {
        base.Added(entity);
        if (Entity is not Player player) { RemoveSelf(); return; }

        player.StateMachine.State = Player.StNormal;
        player.StateMachine.Locked = false;
        player.StateMachine.Active = false;
        player.Sprite.Visible = false;
        player.Hair.Active = false;
        player.Hair.Visible = false;
        player.Light.Position += new Vector2(3, 6);
        player.carryOffset = new Vector2(4, 1);
        FixHitbox();
        if (player.dreamSfxLoop == null) {
            player.Add(player.dreamSfxLoop = new SoundSource());
        }
    }

    public override void Removed(Entity entity) {
        base.Removed(entity);
        Logger.Log(nameof(Picoline), "Removing component...");
        if (entity is not Player player) { return; }

        player.StateMachine.Locked = false;
        player.StateMachine.state = Player.StNormal;
        player.StateMachine.Active = true;
        player.Sprite.FlipX = false;
        player.Sprite.FlipY = false;
        player.Hair.Active = true;
        player.Sprite.Visible = true;
        player.Hair.Visible = true;
        player.Light.Position -= new Vector2(3, 6);
        player.Collider = new Hitbox(8f, 11f, -4f, -11f);
        player.hurtbox = new Hitbox(8f, 9f, -4f, -11f);
    }

    Player player => (Entity as Player)!;

    // move
    private const int MaxRun = 1;
    private const float Deceleration = 0.075f;
    public const float Pico8SpeedUnit = 60;

    internal static readonly Random Rand = new();

    internal float Spr = 1;
    internal int GraceTimer;
    internal Vector2 DashTarget;
    internal Vector2 _dashAccel;
    internal float SprOff;
    internal bool WasOnGround;
    internal int LastState;
    internal bool Demodashing;

    public float BoostTimer;

    private static float Approach(float val, float target, float amount) {
        return val <= target ? Math.Min(val + amount, target) : Math.Max(val - amount, target);
    }

    private void CheckOnGround() {
        if (player.SwimCheck()) {
            player.onGround = player.OnSafeGround = false;
        } else if (player.Speed.Y >= 0.0) {
            var platform = (Platform)player.CollideFirst<Solid>(player.Position + Vector2.UnitY) ?? player.CollideFirstOutside<JumpThru>(player.Position + Vector2.UnitY);
            if (platform != null) {
                player.onGround = true;
                player.OnSafeGround = platform.Safe;
            } else
                player.onGround = player.OnSafeGround = false;
        } else
            player.onGround = player.OnSafeGround = false;


        if (!player.OnSafeGround) return;
        foreach (SafeGroundBlocker component in player.Scene.Tracker.GetComponents<SafeGroundBlocker>())
            if (component.Check(player)) {
                player.OnSafeGround = false;
                break;
            }
    }
    private bool IsSolid(float x, float y) {
        return player.CollideCheck<Solid>(player.Position + new Vector2(x, y));
    }

    private IEnumerator? CassetteFlightCoroutine;
    private float CassetteFlightTimer;

    private DynamicData? DynData;

    public void PicoUpdate() {
        FixHitbox();

        DynData ??= DynamicData.For(player);

        DynData.Set("framesAlive", player.Dead ? 0 : ((int)DynData.Get("framesAlive")!) + 1);

        if (
            player.StateMachine.state is
            Player.StIntroJump or Player.StIntroRespawn or Player.StIntroWalk or Player.StIntroMoonJump or Player.StIntroWakeUp or Player.StIntroThinkForABit
        ) player.StateMachine.state = Player.StNormal;

        if (player.StateMachine.state == Player.StFrozen) goto End;

        if (player.StateMachine.state == Player.StCassetteFly) {
            CassetteFlightCoroutine ??= player.CassetteFlyCoroutine();
            if (CassetteFlightTimer <= 0) {
                if (!CassetteFlightCoroutine.MoveNext())
                    goto End;
                CassetteFlightTimer = (float?)CassetteFlightCoroutine.Current ?? 0;
            }
            CassetteFlightTimer -= Engine.DeltaTime;
            goto EndHair;
        }

        player.StrawberryCollectResetTimer -= Engine.DeltaTime;
        if (player.StrawberryCollectResetTimer <= 0)
            player.StrawberryCollectIndex = 0;

        player.Leader.Position = Vector2.Zero;
        player.dashCooldownTimer = 0;

        FixHitbox();

        ActorUpdate(player);

        var input = player.level.InCutscene ? 0 : Input.MoveX.Value;
        // facing
        if (input != 0)
            player.Facing = input < 0 ? Facings.Left : Facings.Right;

        if (!player.InControl) {
            if (player.StateMachine.State != Player.StDummy) goto EndChecks;

            if (!player.DummyMoving) {
                if (Math.Abs(player.Speed.X) > 90.0 && player.DummyMaxspeed)
                    player.Speed.X = Calc.Approach(player.Speed.X, 90f * Math.Sign(player.Speed.X), 2500f * Engine.DeltaTime);
                if (player.DummyFriction)
                    player.Speed.X = Calc.Approach(player.Speed.X, 0.0f, 1000f * Engine.DeltaTime);
            }

            if (player.DummyGravity) {
                // gravity
                var maxfall = 2f * ExtVarsMaxFall();
                var gravity = 0.21f / 2 * ExtVarsGravityMult();

                if (Math.Abs(player.Speed.Y) <= 0.15f)
                    gravity *= 0.5f;

                // wall slide
                if (input != 0 && IsSolid(input, 0)) {
                    maxfall = 0.4f;
                    if (Rand.NextInt64(10) < 2 && Scene.OnInterval(1 / 30f))
                        AddSmoke(player.X + input * 6, player.Y);
                }

                if (!player.onGround)
                    player.Speed.Y = Approach(player.Speed.Y / Pico8SpeedUnit, maxfall, gravity * Engine.DeltaTime * 60) * Pico8SpeedUnit;
            }

            if (player.DummyAutoAnimate) {
                if (!player.onGround)
                    Spr = IsSolid(Math.Sign(player.Speed.X), 0) ? 5 : 3;
                else if (player.Speed.Y > 0)
                    Spr = 6;
                else if (player.Speed.Y < 0)
                    Spr = 7;
                else if (player.Speed.X == 0)
                    Spr = 1;
                else
                    Spr = 1 + SprOff % 4;
            }

            if (player.Sprite.CurrentAnimationID is "duck" or "sleep")
                Spr = 7;

            goto EndChecks;
        }

        player.level.Camera.Position += (player.CameraTarget - player.level.Camera.Position) * (1f - (float)Math.Pow(0.01f, Engine.DeltaTime));

        player.UpdateCarry();
        if (player.minHoldTimer > 0)
            player.minHoldTimer -= Engine.DeltaTime;

        var jump = Input.Jump.Pressed && !player.level.InCutscene;

        var dash = (Input.Dash.Pressed || Input.CrouchDash.Pressed) && !player.level.InCutscene;
        var demo = Input.CrouchDash.Pressed;

        player.Ducking = player.onGround && Input.MoveY > 0;

        if (player.dreamDashCanEndTimer > 0)
            player.dreamDashCanEndTimer -= Engine.DeltaTime;

        // Accessibility is important!!!!
        if (player.Scene.OnInterval(Settings.Instance.DisableFlashes ? 0.4f : 0.1f)) {
            RandomHairColor = Rand.Choose(
                Utils.Colors.Red, Utils.Colors.Orange, Utils.Colors.Yellow, Utils.Colors.Green, Utils.Colors.Cyan,
                Utils.Colors.Pink, Utils.Colors.Purple, Utils.Colors.White, Utils.Colors.Brown, Utils.Colors.DarkBlue,
                Utils.Colors.DarkRed, Utils.Colors.DarkGreen
            );
        }

        if (player.StateMachine.state == Player.StSummitLaunch) {
            if (LastState != Player.StSummitLaunch)
                player.SummitLaunchBegin();
            player.SummitLaunchUpdate();
            goto EndChecks;
        }

        if (player.StateMachine.state != Player.StStarFly && (LastState == Player.StStarFly || player.starFlyTimer > 0)) {
            player.StarFlyEnd();
            player.starFlyTransforming = false;
            player.starFlyTimer = 0;
        } else if (player.starFlyTimer > 0) {
            player.RefillDash();
            if (dash) {
                PicoDash(demo);
                player.starFlyTimer = 0;
                player.StarFlyEnd();
            }
            player.StateMachine.state = Player.StStarFly;
            if (LastState != Player.StStarFly) player.StarFlyBegin();
        }

        if (player.starFlyTransforming) {
            player.Speed = Calc.Approach(player.Speed, Vector2.Zero, 1000f * Engine.DeltaTime);
            player.starFlyTransforming = player.Speed.Length() > 1;
            goto EndChecks;
        }

        if (player.StateMachine.state == Player.StStarFly && !player.starFlyTransforming && player.starFlyTimer > 0) {
            player.StateMachine.state = player.StarFlyUpdate();
            if (player.StateMachine.state != Player.StStarFly)
                player.StarFlyEnd();
            goto EndChecks;
        }

        if (player.StateMachine.state == Player.StAttract) {
            if (Vector2.Distance(player.attractTo, player.ExactPosition) <= 1.5) {
                player.Position = player.attractTo;
                player.ZeroRemainderX();
                player.ZeroRemainderY();
            } else {
                var target = Calc.Approach(player.ExactPosition, player.attractTo, 200f * Engine.DeltaTime);
                player.MoveToX(target.X);
                player.MoveToY(target.Y);
            }

            goto EndChecks;
        }

        FixHitbox();
        CheckOnGround();

        if (player.JustRespawned && player.Speed != Vector2.Zero)
            player.JustRespawned = false;

        if (!player.Dead) Audio.MusicUnderwater = player.UnderwaterMusicCheck();
        else goto End;

        bool isUnderwater = player.SwimCheck();

        if (player.onGround && !WasOnGround) {
            AddSmoke(player.X, player.Y + 4);
        }

        if (player.onGround || isUnderwater) {
            GraceTimer = 6;
            ExtVarsResetJumps();
            if (!player.Inventory.NoRefills && player.RefillDash()) {
                Utils.PSfx(54);
            }
        } else if (GraceTimer > 0)
            GraceTimer--;

        float accel = 0.6f;

        if (player.StateMachine.state is Player.StDreamDash or Player.StDash or Player.StRedDash) {
            player.Speed.X = Approach(player.Speed.X / Pico8SpeedUnit, DashTarget.X, _dashAccel.X) * Pico8SpeedUnit;
            player.Speed.Y = Approach(player.Speed.Y / Pico8SpeedUnit, DashTarget.Y, _dashAccel.Y) * Pico8SpeedUnit;
        }

        if (player.StateMachine.state == Player.StDreamDash) {
            Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
            var oldPos = player.Position;
            player.NaiveMove(player.Speed * Engine.DeltaTime);

            player.RefillDash();
            var block = player.CollideFirst<DreamBlock>();

            if (block == null) {
                if (player.DreamDashedIntoSolid()) {
                    if (SaveData.Instance.Assists.Invincible) {
                        player.Position = oldPos;
                        player.Speed *= -1;
                        DashTarget *= -1;
                        player.Play("event:/game/general/assist_dreamblockbounce");
                    } else player.Die(Vector2.Zero);
                } else if (player.dreamDashCanEndTimer <= 0) {
                    Celeste.Freeze(.05f);

                    if (Input.Jump.Pressed && player.DashDir.X != 0) {
                        Input.Jump.ConsumePress();
                        Utils.PSfx(1);
                        GraceTimer = 0;
                        player.Speed.Y = -2 * Pico8SpeedUnit;
                    } else {
                        bool left = player.ClimbCheck(-1);
                        bool right = player.ClimbCheck(1);

                        if ((player.DashDir.Y >= 0 || player.DashDir.X != 0) && ((player.moveX == 1 && right) || (player.moveX == -1 && left))) {
                            player.Facing = (Facings)player.moveX;
                            player.Speed.X = 0;
                            player.dashAttackTimer = 0;
                        }
                    }

                    player.StateMachine.state = Player.StNormal;

                    player.Depth = Depths.Player;
                    player.TreatNaive = false;
                    player.Stop(player.dreamSfxLoop);
                    player.Play("event:/char/madeline/dreamblock_exit");
                }
            } else if (player.level.OnInterval(0.04f)) {
                var disp = player.level.Displacement.AddBurst(player.Center, .3f, 0f, 20f);
                disp.WorldClipCollider = block.Collider;
                disp.WorldClipPadding = 2;
            }


        } else if (player.DashAttacking) {
            if (player.StateMachine.state != Player.StRedDash && player.dashAttackTimer > 0)
                player.dashAttackTimer -= Engine.DeltaTime * 60 / ExtVarsDashLength();
            if (player.StateMachine.state == Player.StRedDash && dash) {
                PicoDash(demo);
            }
            AddSmoke(player.X, player.Y);
        } else {
            if (player.dreamSfxLoop is not null) player.Stop(player.dreamSfxLoop);

            if (isUnderwater) {
                // swim
                player.StateMachine.state = Player.StSwim;
                if (!player.level.InCutscene) {
                    var inputVec = Input.Feather.Value.SafeNormalize();

                    if (inputVec.X != 0 || inputVec.Y != 0) {
                        if (inputVec.X != 0)
                            player.Speed.X = Approach(player.Speed.X / Pico8SpeedUnit, MaxRun * inputVec.X, accel * Engine.DeltaTime * 60) * Pico8SpeedUnit;
                        if (inputVec.Y != 0)
                            player.Speed.Y = Approach(player.Speed.Y / Pico8SpeedUnit, MaxRun * inputVec.Y, accel * Engine.DeltaTime * 60) * Pico8SpeedUnit;
                    }
                }

                player.Speed *= (float)Math.Pow(0.02, Engine.DeltaTime);
            } else {
                player.StateMachine.state = Player.StNormal;
                if (!player.onGround) accel = 0.4f;
                else accel *= player.level.CoreMode == Session.CoreModes.Cold ? 0.15f : 1f;

                float maxRun = ExtVarsHorizontalSpeed() * MaxRun;

                if (Math.Abs(player.Speed.X / Pico8SpeedUnit) > maxRun)
                    player.Speed.X = Approach(player.Speed.X / Pico8SpeedUnit, Math.Sign(player.Speed.X) * maxRun, Deceleration * Engine.DeltaTime * 60) * Pico8SpeedUnit;
                else
                    player.Speed.X = Approach(player.Speed.X / Pico8SpeedUnit, input * maxRun, accel * Engine.DeltaTime * 60) * Pico8SpeedUnit;

                // gravity
                var maxfall = 2f * ExtVarsMaxFall();
                var gravity = 0.21f / 2 * ExtVarsGravityMult();
                if (player.Holding is { SlowFall: true } && Input.MoveY <= 0) {
                    maxfall *= .3f;
                    gravity *= .5f;
                }

                if (Math.Abs(player.Speed.Y) <= 0.15f)
                    gravity *= 0.5f;

                // wall slide
                if (input != 0 && IsSolid(input, 0)) {
                    maxfall = 0.4f;
                    if (Rand.NextInt64(10) < 2)
                        AddSmoke(player.X + input * 6, player.Y);
                }

                if (!player.onGround && !player.wallBoosting)
                    player.Speed.Y = Approach(player.Speed.Y / Pico8SpeedUnit, maxfall, gravity * Engine.DeltaTime * 60 * (player.level.InSpace ? Player.SpacePhysicsMult : 1)) * Pico8SpeedUnit;

                if (Input.GrabCheck && player.ClimbCheck((int)player.Facing)) {
                    if (!player.wallBoosting) { AddSmoke(player.X, player.Y); }
                    player.ClimbTrigger((int)player.Facing);
                } else if (player.wallBoosting) {
                    player.wallBoosting = false;
                    if (player.conveyorLoopSfx != null) {
                        player.conveyorLoopSfx.setParameterValue("end", 1);
                        player.conveyorLoopSfx.release();
                        player.conveyorLoopSfx = null;
                    }
                }

                // jump
                if (Input.Jump.Pressed) {
                    if (GraceTimer > 0 || ExtVarsConsumeJump()) {
                        // normal jump
                        Utils.PSfx(1);
                        Input.Jump.ConsumePress();
                        GraceTimer = 0;
                        player.Speed.Y = -2 * Pico8SpeedUnit * ExtVarsJumpHeight();
                        AddSmoke(player.X, player.Y + 4);
                    } else {
                        // wall jump
                        var wallDir = IsSolid(-3, 0) ? -1 : IsSolid(3, 0) ? 1 : 0;
                        if (wallDir != 0) {
                            Utils.PSfx(2);
                            Input.Jump.ConsumePress();
                            player.Speed.Y = -2 * Pico8SpeedUnit * ExtVarsJumpHeight();
                            player.Speed.X = -wallDir * (MaxRun + 1) * Pico8SpeedUnit;
                            player.Facing = (Facings)(-wallDir);

                            if (player.LiftSpeed == Vector2.Zero) {
                                var solid = player.CollideFirst<Solid>(player.Position + Vector2.UnitX * 3f * -wallDir);
                                if (solid != null)
                                    player.LiftSpeed = solid.LiftSpeed;
                            }
                        }
                    }

                    player.LaunchedBoostCheck();

                    if (player.liftSpeedTimer > 0)
                        player.Speed += player.LiftBoost;

                }
            }

            if (player.Dashes > 0 && dash && player.Holding == null) {
                PicoDash(demo);
            } else if (dash && player.Dashes <= 0 && player.Inventory.Dashes > 0) {
                Utils.PSfx(9);
                Input.Dash.ConsumePress();
                Input.CrouchDash.ConsumePress();
                AddSmoke(player.X, player.Y);
            }
        }

        if (player.StateMachine.state != Player.StDreamDash) {
            if (Input.GrabCheck) {
                player.climbTriggerDir = input != 0 ? input : (int)player.Facing;
                if (!player.Ducking && player.Holding == null)
                    foreach (Holdable hold in player.Scene.Tracker.GetComponents<Holdable>())
                        if (hold.Check(player) && player.Pickup(hold)) {
                            Audio.Play("event:/char/madeline/crystaltheo_lift");
                            break;
                        }
                var booster = player.WallBoosterCheck();
                if (booster != null) {
                    player.wallBoosting = true;

                    if (player.conveyorLoopSfx == null)
                        player.conveyorLoopSfx = Audio.Play("event:/game/09_core/conveyor_activate", "end", 0);
                    Audio.Position(player.conveyorLoopSfx, player.Position);

                    player.Speed.Y = Calc.Approach(player.Speed.Y, Player.WallBoosterSpeed, Player.WallBoosterAccel * Engine.DeltaTime);
                    Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
                }
            } else {
                player.climbTriggerDir = 0;
                player.wallBoosting = false;
                if (player.conveyorLoopSfx != null) {
                    player.conveyorLoopSfx.setParameterValue("end", 1);
                    player.conveyorLoopSfx.release();
                    player.conveyorLoopSfx = null;
                }
                if (player.Holding != null) {
                    if (player.Ducking)
                        player.Drop();
                    else
                        player.Throw();
                    player.Holding = null;
                }
            }
        }

    EndChecks:

        if (player.StateMachine.state != Player.StDreamDash && player.StateMachine.State != Player.StAttract) {
            FixHitbox();
            player.MoveH(player.Speed.X * Engine.DeltaTime, OnCollideH);
            player.MoveV(player.Speed.Y * Engine.DeltaTime, OnCollideV);
        }

        player.UpdateChaserStates();

        // animation
        SprOff += 0.125f;
        if (player.StateMachine.State != Player.StDummy || !player.DummyAutoAnimate) {
            if (!player.onGround) {
                player.Sprite.CurrentAnimationID = IsSolid(input, 0) ? "wallslide" : "fallSlow";
                Spr = IsSolid(input, 0) ? 5 : 3;
            } else if (Input.MoveY > 0) {
                player.Sprite.CurrentAnimationID = "duck";
                Spr = 6;
            } else if (Input.MoveY < 0) {
                player.Sprite.CurrentAnimationID = "lookUp";
                Spr = 7;
            } else if (player.Speed.X == 0 || Input.MoveX == 0) {
                player.Sprite.CurrentAnimationID = "idle";
                Spr = 1;
            } else {
                player.Sprite.CurrentAnimationID = "runFast";
                Spr = 1 + SprOff % 4;
            }

            if (player.StateMachine.State == Player.StDreamDash)
                player.Sprite.CurrentAnimationID = "dreamDashLoop";
            else if (player.dashAttackTimer > 0)
                player.Sprite.CurrentAnimationID = "dash";
        }

        if (player.EnforceLevelBounds) player.level.EnforceBounds(player);

        foreach (PlayerCollider pc in player.Scene.Tracker.GetComponents<PlayerCollider>())
            if (pc.Check(player) && player.Dead)
                break;

        foreach (Trigger trigger in player.Scene.Tracker.GetEntities<Trigger>())
            if (player.CollideCheck(trigger)) {
                if (!trigger.Triggered) {
                    trigger.Triggered = true;
                    player.triggersInside.Add(trigger);
                    trigger.OnEnter(player);
                }
                trigger.OnStay(player);
            } else if (trigger.Triggered) {
                player.triggersInside.Remove(trigger);
                trigger.Triggered = false;
                trigger.OnLeave(player);
            }

    EndHair:
        var hairAnchor = new Vector2(player.X + 4 - (int)player.Facing * 2, player.Y + (Input.MoveY.Value > 0 ? 4f : 3f));
        for (var idx = 0; idx < player.Hair.Nodes.Count; idx++) {
            var node = player.Hair.Nodes[idx];
            // Approach
            node.X += (float)(hairAnchor.X - (double)node.X) * (1f - (float)Math.Pow(0.5f, Engine.DeltaTime * 60));
            node.Y += (float)(hairAnchor.Y + 0.5 - node.Y) * (1f - (float)Math.Pow(0.5f, Engine.DeltaTime * 60));
            player.Hair.Nodes[idx] = node;
            hairAnchor = node;
        }
        HairPos = player.Position;

    End:
    
        FixHitbox();
        WasOnGround = player.onGround;
        LastState = player.StateMachine.state;
    }

    private void FixHitbox() {
        bool duckBox = (Demodashing && player.StateMachine == Player.StDash) || player.Ducking;
        if (player.Collider is Hitbox hitbox) {
            player.Collider = new Hitbox(6, duckBox ? 4 : 5, 1 + HackyOffset.X, 3 + (duckBox ? 1 : 0) + HackyOffset.Y);
        }
        if (player.hurtbox is Hitbox hurtbox) {
            player.hurtbox = new Hitbox(6, duckBox ? 4 : 5, 1 + HackyOffset.X, 3 + (duckBox ? 1 : 0) + HackyOffset.Y);
        }
    }

    private void PicoDash(bool demo = false) {
        Input.Dash.ConsumePress();
        Input.CrouchDash.ConsumePress();
        var dashInput = Input.GetAimVector(player.Facing);
        dashInput.Normalize();
        player.DashDir = dashInput;
        Demodashing = demo;

        AddSmoke(player.X, player.Y);
        player.Dashes--;
        player.dashAttackTimer = 8;
        player.StateMachine.state = Player.StDash;

        Utils.PSfx(3);
        Celeste.Freeze(0.05f);
        Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);

        ++SaveData.Instance.TotalDashes;
        ++player.level.Session.Dashes;
        Stats.Increment(Stat.DASHES);
        foreach (DashListener component in player.Scene.Tracker.GetComponents<DashListener>())
            component.OnDash?.Invoke(player.DashDir);

        player.calledDashEvents = true;

        player.Speed = dashInput * 2.5f * Pico8SpeedUnit;

        if (player.Scene is Level level)
            level.Shake(6f / 30f);
        DashTarget.X = 2 * Math.Sign(dashInput.X);
        DashTarget.Y = 2 * Math.Sign(dashInput.Y);
        DashTarget *= ExtVarsDashSpeed();
        _dashAccel.X = 1.5f;
        _dashAccel.Y = 1.5f;

        if (player.Speed.Y < 0)
            DashTarget.Y *= 0.75f;
        // Manual normalization?
        if (player.Speed.Y != 0)
            _dashAccel.X *= 0.70710678118f;
        if (player.Speed.X != 0)
            _dashAccel.Y *= 0.70710678118f;
    }

    private float ExtVarsGravityMult() => PicolineModule.ExtVarsLoaded ? __ExtVarsGravityMultUnchecked() : 1;

    private float __ExtVarsGravityMultUnchecked() =>
        (float)ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.Gravity);

    private float ExtVarsMaxFall() => PicolineModule.ExtVarsLoaded ? __ExtVarsMaxFallUnchecked() : 1;

    private float __ExtVarsMaxFallUnchecked() =>
        (float)ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.FallSpeed);

    private float ExtVarsDashSpeed() => PicolineModule.ExtVarsLoaded ? __ExtVarsDashSpeedUnchecked() : 1;

    private float __ExtVarsDashSpeedUnchecked() =>
        (float)ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.DashSpeed);

    private float ExtVarsDashLength() => PicolineModule.ExtVarsLoaded ? __ExtVarsDashLengthUnchecked() : 1;

    private float __ExtVarsDashLengthUnchecked() =>
        (float)ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.DashLength);

    private float ExtVarsJumpHeight() => PicolineModule.ExtVarsLoaded ? __ExtVarsJumpHeightUnchecked() : 1;

    private float __ExtVarsJumpHeightUnchecked() =>
        (float)ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.JumpHeight);

    private float ExtVarsHorizontalSpeed() => PicolineModule.ExtVarsLoaded ? __ExtVarsHorizontalSpeedUnchecked() : 1;

    private float __ExtVarsHorizontalSpeedUnchecked() =>
        (float)ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.SpeedX);

    private bool ExtVarsConsumeJump() => PicolineModule.ExtVarsLoaded && __ExtVarsConsumeJumpUnchecked();

    private bool __ExtVarsConsumeJumpUnchecked() {
        if (IsSolid(-3, 0) || IsSolid(3, 0)) return false;
        int jumpBuffer = JumpCount.GetJumpBuffer();
        if (jumpBuffer <= 0) return false;
        JumpCount.SetJumpCount(--jumpBuffer, false);
        return true;
    }
    
    private void ExtVarsResetJumps() { if (PicolineModule.ExtVarsLoaded) { __ExtVarsResetJumpsUnchecked(); } }

    private void __ExtVarsResetJumpsUnchecked() {
        if ((bool) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.ResetJumpCountOnGround))
        JumpCount.SetJumpCount((int) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.JumpCount) - 1, true);
    }


    private bool ShouldBeSilhouette => player.StateMachine.State == Player.StDreamDash ||
        (PicolineModule.ExtVarsLoaded && __ExtVarsDrawAsSilhouetteUnchecked());

    private bool __ExtVarsDrawAsSilhouetteUnchecked() => (
            ExtendedVariantsModule.Instance.MaxHelpingHandInstalled ||
            ExtendedVariantsModule.Instance.SpringCollab2020Installed
        ) &&
        (bool) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(
            ExtendedVariantsModule.Variant.MadelineIsSilhouette
        );

#nullable disable


    private void OnCollideH(CollisionData data) {
        if (player.StateMachine.State == Player.StDreamDash)
            return;

        if (player.StateMachine.State == Player.StStarFly) {
            if (player.starFlyTimer < Player.StarFlyEndNoBounceTime)
                player.Speed.X = 0;
            else {
                player.Play("event:/game/06_reflection/feather_state_bump");
                Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
                player.Speed.X *= Player.StarFlyWallBounce;
            }
            return;
        }

        if (
            player.DashAttacking && data.Hit is { OnDashCollide: not null } &&
            Math.Abs(data.Direction.X - Math.Sign(player.DashDir.X)) < 0.01
        ) {
            var collisionResults = data.Hit.OnDashCollide(player, data.Direction);
            if (collisionResults == DashCollisionResults.NormalOverride)
                collisionResults = DashCollisionResults.NormalCollision;
            switch (collisionResults) {
                case DashCollisionResults.Rebound:
                    player.Speed.X *= -0.5f;
                    player.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Bounce:
                    player.Speed.X = Math.Sign(player.Speed.X) * -1 * 240;
                    player.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Ignore:
                    return;
            }
        }

        if (player.DreamDashCheck(Vector2.UnitX * Math.Sign(player.Speed.X))) {
            player.StateMachine.state = Player.StDreamDash;
            player.Play("event:/char/madeline/dreamblock_enter");
            player.Loop(player.dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            player.Depth = Depths.PlayerDreamDashing;
            player.TreatNaive = true;
            player.dashAttackTimer = 0;
            return;
        }

        if (player.StateMachine.state == Player.StRedDash)
            player.StateMachine.state = Player.StNormal;

        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);

        player.Speed.X = 0;
        player.dashAttackTimer = 0;
    }

    private void OnCollideV(CollisionData data) {
        if (player.StateMachine.State == Player.StDreamDash)
            return;

        if (player.StateMachine.State == Player.StStarFly) {
            if (player.starFlyTimer < Player.StarFlyEndNoBounceTime)
                player.Speed.Y = 0;
            else {
                player.Play("event:/game/06_reflection/feather_state_bump");
                Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
                player.Speed.Y *= Player.StarFlyWallBounce;
            }
            return;
        }

        if (
            player.DashAttacking && data.Hit is { OnDashCollide: not null }
                          && Math.Abs(data.Direction.Y - Math.Sign(player.DashDir.Y)) < 0.01
        ) {
            var collisionResults = data.Hit.OnDashCollide(player, data.Direction);
            if (collisionResults == DashCollisionResults.NormalOverride)
                collisionResults = DashCollisionResults.NormalCollision;
            switch (collisionResults) {
                case DashCollisionResults.Rebound:
                    player.Speed.Y *= -1;
                    player.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Bounce:
                    player.Speed.Y = Math.Sign(player.Speed.Y) * -1 * 240;
                    player.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Ignore:
                    return;
            }
        }

        if (player.DreamDashCheck(Vector2.UnitY * Math.Sign(player.Speed.Y))) {
            player.StateMachine.state = Player.StDreamDash;
            player.Play("event:/char/madeline/dreamblock_enter");
            player.Loop(player.dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            player.dashAttackTimer = 0;
            player.Depth = Depths.PlayerDreamDashing;
            player.TreatNaive = true;
            return;
        }

        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);

        if (player.StateMachine.state == Player.StRedDash)
            player.StateMachine.state = Player.StNormal;

        player.Speed.Y = 0;
        player.dashAttackTimer = 0;
    }

    private void AddSmoke(float x, float y) => player.level.Add(new Smoke(new Vector2(x, y) + HackyOffset));

    internal Vector2 HairPos;

    Color HairColor = Color.White;

    private void DrawHair(Player self, Vector2 offset = new()) {
        for (int i = self.Hair.Nodes.Count - 1; i >= 0; i--) {
            Vector2 node = self.Hair.Nodes[i];
            Utils.DrawCircle(node + offset + HackyOffset, i > 1 ? 1 : 2, HairColor);
        }
    }

    public void PicoRender() {

        HairColor = Color.Black;
        DrawHair(player, -Vector2.UnitX);
        DrawHair(player, Vector2.UnitX);
        DrawHair(player, -Vector2.UnitY);
        DrawHair(player, Vector2.UnitY);
        DrawPlayer(player, -Vector2.UnitX, Color.Black, true);
        DrawPlayer(player, Vector2.UnitX, Color.Black, true);
        DrawPlayer(player, -Vector2.UnitY, Color.Black, true);
        DrawPlayer(player, Vector2.UnitY, Color.Black, true);
        HairColor = GetHairColor(player.Dashes);
        DrawHair(player);
        DrawPlayer(player, Vector2.Zero, HairColor, ShouldBeSilhouette);
    }

    private void DrawPlayer(Player player, Vector2 offset, Color color, bool silhouette) {
        if (player.StateMachine == Player.StStarFly) {
            Utils.DrawCircle(player.Hair.Nodes[0] + offset + HackyOffset, 3, color);
        } else {
            Utils.PlayerSpr((int)Spr, player.Position + offset + HackyOffset, player.Facing == Facings.Left, player.Sprite.FlipY, color, silhouette);
        }
    }

    private Color GetHairColor(int dashes) => dashes switch {
        _ when player.StateMachine == Player.StDreamDash => RandomHairColor,
        _ when player.StateMachine == Player.StStarFly => Utils.Colors.Yellow,
        0 => Utils.Colors.Cyan,
        1 => Utils.Colors.Red,
        2 when Scene.TimeActive % 0.3 > 0.15 && !Settings.Instance.DisableFlashes => Utils.Colors.White,
        2 => Utils.Colors.Green,
        _ => RandomHairColor
    };
}
