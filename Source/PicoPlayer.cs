using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ExtendedVariants.Module;
using ExtendedVariants.Variants;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using MonoMod.Utils;

namespace Celeste.Mod.Picoline;

internal class Smoke : Entity
{
    private static readonly MTexture[] Frames = new MTexture[3] {
        new(GFX.Game["pico8/atlas"], 13 * 8, 1 * 8, 8, 8),
        new(GFX.Game["pico8/atlas"], 14 * 8, 1 * 8, 8, 8),
        new(GFX.Game["pico8/atlas"], 15 * 8, 1 * 8, 8, 8)
    };

    private float _spr;
    private Vector2 _spd;
    private SpriteEffects _fx = SpriteEffects.None;
    
    internal Smoke(Vector2 position) : base(position) {
        _spd.Y = -0.1f;
        _spd.X = -0.1f + PicoPlayer.Rand.NextFloat(.2f);
        X += PicoPlayer.Rand.NextFloat(2) - 1f;
        Y += PicoPlayer.Rand.NextFloat(2) - 1f;
        if (PicoPlayer.Rand.NextSingle() > 0.5)
            _fx |= SpriteEffects.FlipHorizontally;
        if (PicoPlayer.Rand.NextSingle() > 0.5)
            _fx |= SpriteEffects.FlipVertically;
    }
    
    public override void Update()
    {
        Position += _spd * PicoPlayer.Pico8SpeedUnit * Engine.DeltaTime;
        _spr += Engine.DeltaTime * 6;
        if (_spr < 3) return;
        RemoveSelf();
    }

    public override void Render() {
        Frames[Math.Min((int) _spr, 2)].Draw(Position + Vector2.One * 4, Vector2.One * 4, Color.White, 1f, 0f, _fx);
    }
}

public class PicoPlayer
{
    internal Player self;
    
    private static readonly MTexture PlayerAtlas = GFX.Game["PicoPlayer/player_atlas"];

    public static readonly Hitbox PicoHitbox = new(6, 5, 1, 3);

    // move
    private const int MaxRun = 1;
    private const float Deceleration = 0.075f;
    public const float Pico8SpeedUnit = 60;

    internal static readonly Random Rand = new();

    internal float _sprite = 1;
    internal bool _jumpWasPressed;
    internal bool _dashWasPressed;
    internal int _grace;
    internal int _jumpBuffer;
    internal Vector2 _dashTarget;
    internal Vector2 _dashAccel;
    internal float _spriteOff;
    internal bool _wasOnGround;
    internal int _lastState;
    internal bool _boosting;

    internal Hitbox _oldNormalHitbox;
    internal Hitbox _oldDuckHitbox;
    internal Hitbox _oldStarFlyHitbox;
    internal Hitbox _oldNormalHurtbox;
    internal Hitbox _oldDuckHurtbox;
    internal Hitbox _oldStarFlyHurtbox;
    internal Vector2 _oldLightPosition;
    internal int _oldHairNodeCount;

    internal void OverrideHitboxes() {
        _oldNormalHitbox = (Hitbox) self.normalHitbox.Clone();
        self.normalHitbox.Position = PicoHitbox.Position;
        self.normalHitbox.width = PicoHitbox.width;
        self.normalHitbox.height = PicoHitbox.height;

        _oldDuckHitbox = (Hitbox) self.duckHitbox.Clone();
        self.duckHitbox.Position = PicoHitbox.Position;
        self.duckHitbox.width = PicoHitbox.width;
        self.duckHitbox.height = PicoHitbox.height;

        _oldStarFlyHitbox = (Hitbox) self.starFlyHitbox.Clone();
        self.starFlyHitbox.Position = PicoHitbox.Position;
        self.starFlyHitbox.width = PicoHitbox.width;
        self.starFlyHitbox.height = PicoHitbox.height;

        _oldNormalHurtbox = (Hitbox) self.normalHurtbox.Clone();
        self.normalHurtbox.Position = PicoHitbox.Position;
        self.normalHurtbox.width = PicoHitbox.width;
        self.normalHurtbox.height = PicoHitbox.height;

        _oldDuckHurtbox = (Hitbox) self.duckHurtbox.Clone();
        self.duckHurtbox.Position = PicoHitbox.Position;
        self.duckHurtbox.width = PicoHitbox.width;
        self.duckHurtbox.height = PicoHitbox.height;

        _oldStarFlyHurtbox = (Hitbox) self.starFlyHurtbox.Clone();
        self.starFlyHurtbox.Position = PicoHitbox.Position;
        self.starFlyHurtbox.width = PicoHitbox.width;
        self.starFlyHurtbox.height = PicoHitbox.height;
    }
    
    internal void RestoreHitboxes() {
        self.normalHitbox.Position = _oldNormalHitbox.Position;
        self.normalHitbox.width = _oldNormalHitbox.width;
        self.normalHitbox.height = _oldNormalHitbox.height;

        self.duckHitbox.Position = _oldDuckHitbox.Position;
        self.duckHitbox.width = _oldDuckHitbox.width;
        self.duckHitbox.height = _oldDuckHitbox.height;

        self.starFlyHitbox.Position = _oldStarFlyHitbox.Position;
        self.starFlyHitbox.width = _oldStarFlyHitbox.width;
        self.starFlyHitbox.height = _oldStarFlyHitbox.height;

        self.normalHurtbox.Position = _oldNormalHurtbox.Position;
        self.normalHurtbox.width = _oldNormalHurtbox.width;
        self.normalHurtbox.height = _oldNormalHurtbox.height;

        self.duckHurtbox.Position = _oldDuckHurtbox.Position;
        self.duckHurtbox.width = _oldDuckHurtbox.width;
        self.duckHurtbox.height = _oldDuckHurtbox.height;

        self.starFlyHurtbox.Position = _oldStarFlyHurtbox.Position;
        self.starFlyHurtbox.width = _oldStarFlyHurtbox.width;
        self.starFlyHurtbox.height = _oldStarFlyHurtbox.height;
    }

    public float BoostTimer;
    
    static PicoPlayer()
    {
        PlayerTextures = new MTexture[24];
        for (var i = 0; i < 24; i++)
        {
            PlayerTextures[i] = new MTexture(PlayerAtlas, i % 8 * 8, i / 8 * 8, 8, 8);
        }
    }

    private static void PicoCircle(Vector2 position, float r, Color color)
    {
        var x = position.X;
        var y = position.Y;
        if (r <= 1.0)
        {
            Draw.Rect(x - 1f, y, 3f, 1f, color);
            Draw.Rect(x, y - 1f, 1f, 3f, color);
        }
        else if (r <= 2.0)
        {
            Draw.Rect(x - 2f, y - 1f, 5f, 3f, color);
            Draw.Rect(x - 1f, y - 2f, 3f, 5f, color);
        }
        else if (r <= 3.0)
        {
            Draw.Rect(x - 3f, y - 1f, 7f, 3f, color);
            Draw.Rect(x - 1f, y - 3f, 3f, 7f, color);
            Draw.Rect(x - 2f, y - 2f, 5f, 5f, color);

        }
        else
        {
            Draw.Circle(position, r + 0.5f, color, 8);
        }
    }

    private static float Approach(float val, float target, float amount)
    {
        return val <= target ? Math.Min(val + amount, target) : Math.Max(val - amount, target);
    }
    
    private void CheckOnGround()
    {
        if (self.SwimCheck()) {
            self.onGround = self.OnSafeGround = false;
        } else if (self.Speed.Y >= 0.0) {
            var platform = (Platform) self.CollideFirst<Solid>(self.Position + Vector2.UnitY) ?? self.CollideFirstOutside<JumpThru>(self.Position + Vector2.UnitY);
            if (platform != null)
            {
                self.onGround = true;
                self.OnSafeGround = platform.Safe;
            }
            else
                self.onGround = self.OnSafeGround = false;
        } else
            self.onGround = self.OnSafeGround = false;


        if (!self.OnSafeGround) return;
        foreach (SafeGroundBlocker component in self.Scene.Tracker.GetComponents<SafeGroundBlocker>())
            if (component.Check(self)) {
                self.OnSafeGround = false;
                break;
            }
    }
    private bool IsSolid(float x, float y)
    {
        return self.CollideCheck<Solid>(self.Position + new Vector2(x, y));
    }

    private Color _randomHairColor = PicoColors.Red;
    
    private Color HairColor() {
        if (self.StateMachine.state == Player.StStarFly) 
            return self.starFlyTransforming ? PicoColors.White : PicoColors.Yellow;
        if (self.StateMachine.state == Player.StDreamDash) return _randomHairColor;
        if (self.DashAttacking) return PicoColors.White;
        
        return self.Dashes switch {
            2 => self.level.TimeActive % 0.3 > 0.15 && !Settings.Instance.DisableFlashes ? PicoColors.White : PicoColors.Green,
            1 => PicoColors.Red,
            0 => self.Inventory.Dashes == 0 ? PicoColors.Red : PicoColors.Cyan,
            _ => _randomHairColor
        };
    }

    private static void Sfx(int sfx) => Audio.Play("event:/classic/sfx" + sfx);

    private IEnumerator _cassetteFlight;
    private float _cassetteFlightTimer;
    
    private DynamicData dynData;

    [MonoModLinkTo("Celeste.Actor", "System.Void Render()")]
    internal static void actor_Render(Actor self) { }

    public void Update() {
        
        dynData ??= DynamicData.For(self);
        
        if (self.Dead) {
            dynData.Set("framesAlive", 0);
        } else {
            dynData.Set("framesAlive", ((int) dynData.Get("framesAlive")) + 1);
        }

        if (
            self.StateMachine.state is
            Player.StIntroJump or Player.StIntroRespawn or Player.StIntroWalk or Player.StIntroMoonJump or Player.StIntroWakeUp or Player.StIntroThinkForABit
        )
            self.StateMachine.state = Player.StNormal;

        if (self.StateMachine.state == Player.StFrozen) goto End;
        
        if (self.StateMachine.state == Player.StCassetteFly) {
            _cassetteFlight ??= self.CassetteFlyCoroutine();
            if (_cassetteFlightTimer <= 0) {
                if (!_cassetteFlight.MoveNext())
                    goto End;
                _cassetteFlightTimer = (float?) _cassetteFlight.Current ?? 0;
            }
            _cassetteFlightTimer -= Engine.DeltaTime;
            goto EndHair;
        }

        self.StrawberryCollectResetTimer -= Engine.DeltaTime;
        if (self.StrawberryCollectResetTimer <= 0)
            self.StrawberryCollectIndex = 0;
        
        self.Leader.Position = Vector2.Zero;
        self.dashCooldownTimer = 0;

        var input = self.level.InCutscene ? 0 : Input.MoveX.Value;
        // facing
        if (input != 0)
            self.Facing = input < 0 ? Facings.Left : Facings.Right;

        if (!self.InControl) {
            if (self.StateMachine.State != Player.StDummy) goto EndChecks;
            
            if (!self.DummyMoving) {
                if (Math.Abs(self.Speed.X) > 90.0 && self.DummyMaxspeed)
                    self.Speed.X = Calc.Approach(self.Speed.X, 90f * Math.Sign(self.Speed.X), 2500f * Engine.DeltaTime);
                if (self.DummyFriction)
                    self.Speed.X = Calc.Approach(self.Speed.X, 0.0f, 1000f * Engine.DeltaTime);
            }

            if (self.DummyGravity) {
                // gravity
                var maxfall = 2f * ExtVarsMaxFall();
                var gravity = 0.21f / 2 * ExtVarsGravityMult();

                if (Math.Abs(self.Speed.Y) <= 0.15f)
                    gravity *= 0.5f;

                // wall slide
                if (input != 0 && IsSolid(input, 0))
                {
                    maxfall = 0.4f;
                    if (Rand.NextInt64(10) < 2)
                        AddSmoke(self.X + input * 6, self.Y);
                }

                if (!self.onGround)
                    self.Speed.Y = Approach(self.Speed.Y / Pico8SpeedUnit, maxfall, gravity * Engine.DeltaTime * 60) * Pico8SpeedUnit;
            }

            if (self.DummyAutoAnimate) {
                if (!self.onGround)
                    _sprite = IsSolid(Math.Sign(self.Speed.X), 0) ? 5 : 3;
                else if (self.Speed.Y > 0)
                    _sprite = 6;
                else if (self.Speed.Y < 0)
                    _sprite = 7;
                else if (self.Speed.X == 0)
                    _sprite = 1;
                else
                    _sprite = 1 + _spriteOff % 4;
            }

            if (self.Sprite.CurrentAnimationID is "duck" or "sleep" )
                _sprite = 7;
            
            goto EndChecks;
        }

        self.level.Camera.Position += (self.CameraTarget - self.level.Camera.Position) * (1f - (float) Math.Pow(0.01f, Engine.DeltaTime));
        
        self.UpdateCarry();
        if (self.minHoldTimer > 0)
            self.minHoldTimer -= Engine.DeltaTime;
        
        var jump = Input.Jump.Pressed && !_jumpWasPressed && !self.level.InCutscene;
        _jumpWasPressed = Input.Jump.Pressed;
        if (jump)
            _jumpBuffer = 8;
        else if (_jumpBuffer > 0)
            _jumpBuffer--;

        var dash = Input.Dash.Pressed && !_dashWasPressed && !self.level.InCutscene;
        _dashWasPressed = Input.Dash.Pressed;

        self.Ducking = Input.MoveY > 0 && self.onGround;

        if (self.dreamDashCanEndTimer > 0) 
            self.dreamDashCanEndTimer -= Engine.DeltaTime;
        
        // Accessibility is important!!!!
        if (self.Scene.OnInterval(Settings.Instance.DisableFlashes ? 0.4f : 0.1f)) {
            _randomHairColor = Rand.Choose(
                PicoColors.Red, PicoColors.Orange, PicoColors.Yellow, PicoColors.Green, PicoColors.Cyan, 
                PicoColors.Pink, PicoColors.Purple, PicoColors.White, PicoColors.Brown, PicoColors.DarkBlue, 
                PicoColors.DarkRed, PicoColors.DarkGreen
            );
        }

        if (self.StateMachine.state == Player.StSummitLaunch) {
            if (_lastState != Player.StSummitLaunch)
                self.SummitLaunchBegin();
            self.SummitLaunchUpdate();
            goto EndChecks;
        }

        if (self.StateMachine.state != Player.StStarFly && (_lastState == Player.StStarFly || self.starFlyTimer > 0)) {
            self.StarFlyEnd();
            self.starFlyTransforming = false;
            self.starFlyTimer = 0;
        } else if (self.starFlyTimer > 0) {
            if (dash) {
                PicoDash();
                self.starFlyTimer = 0;
                self.StarFlyEnd();
            }
            self.StateMachine.state = Player.StStarFly;
            if (_lastState != Player.StStarFly) self.StarFlyBegin();
        }

        if (self.starFlyTransforming) {
            self.Speed = Calc.Approach(self.Speed, Vector2.Zero, 1000f * Engine.DeltaTime);
            self.starFlyTransforming = self.Speed.Length() > 1;
            goto EndChecks;
        }

        if (self.StateMachine.state == Player.StStarFly && !self.starFlyTransforming && self.starFlyTimer > 0) {
            self.StateMachine.state = self.StarFlyUpdate();
            if (self.StateMachine.state != Player.StStarFly)
                self.StarFlyEnd();
            goto EndChecks;
        }

        if (self.StateMachine.state == Player.StBoost && (BoostTimer > 0 || _lastState != Player.StBoost)) {
            if (_lastState != Player.StBoost) {
                self.BoostBegin();
                self.LastBooster = self.CurrentBooster;
                BoostTimer = 0.4f;
            }
            self.dashAttackTimer = 0;

            BoostTimer -= Engine.DeltaTime;

            if ((self.StateMachine.state = self.BoostUpdate()) != Player.StBoost) {
                _boosting = false;
                BoostTimer = 0;
                dash = true;
            }
        } else if (_lastState == Player.StBoost) {
            _boosting = false;
            BoostTimer = 0;
            self.StateMachine.state = Player.StDash;
            dash = true;
        }

        _boosting &= self.LastBooster?.BoostingPlayer ?? false;

        if (self.StateMachine.state == Player.StAttract) {
            if (Vector2.Distance(self.attractTo, self.ExactPosition) <= 1.5) {
                self.Position = self.attractTo;
                self.ZeroRemainderX();
                self.ZeroRemainderY();
            } else {
                var target = Calc.Approach(self.ExactPosition, self.attractTo, 200f * Engine.DeltaTime);
                self.MoveToX(target.X);
                self.MoveToY(target.Y);
            }

            goto EndChecks;
        }

        CheckOnGround();
        self.Hair.Color = HairColor();
        
        if (self.JustRespawned && self.Speed != Vector2.Zero)
            self.JustRespawned = false;

        if (!self.Dead) Audio.MusicUnderwater = self.UnderwaterMusicCheck();
        else goto End;

        bool isUnderwater = self.SwimCheck();

        if (self.onGround && !_wasOnGround) {
            AddSmoke(self.X, self.Y + 4);
        }

        if (self.onGround || isUnderwater) {
            if (_grace == 0) ExtVarsRefillJumps();
            _grace = 6;
            if (!self.Inventory.NoRefills && self.RefillDash()) {
                Sfx(54);
            }
        }
        else if (_grace > 0)
            _grace--;
    
        float accel = 0.6f;

        if (self.StateMachine.state is Player.StDreamDash or Player.StDash or Player.StRedDash) {
            self.Speed.X = Approach(self.Speed.X / Pico8SpeedUnit, _dashTarget.X, _dashAccel.X) * Pico8SpeedUnit;
            self.Speed.Y = Approach(self.Speed.Y / Pico8SpeedUnit, _dashTarget.Y, _dashAccel.Y) * Pico8SpeedUnit;
        }
        
        if (self.StateMachine.state == Player.StDreamDash) {
            Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
            var oldPos = self.Position;
            self.NaiveMove(self.Speed * Engine.DeltaTime);

            self.RefillDash();
            var block = self.CollideFirst<DreamBlock>();

            if (block == null) {
                if (self.DreamDashedIntoSolid()) {
                    if (SaveData.Instance.Assists.Invincible) {
                        self.Position = oldPos;
                        self.Speed *= -1;
                        _dashTarget *= -1;
                        self.Play("event:/game/general/assist_dreamblockbounce");
                    } else self.Die(Vector2.Zero);
                } else if (self.dreamDashCanEndTimer <= 0) {
                    Celeste.Freeze(.05f);

                    if (Input.Jump.Pressed && self.DashDir.X != 0) {
                        Sfx(1);
                        _jumpBuffer = 0;
                        _grace = 0;
                        self.Speed.Y = -2 * Pico8SpeedUnit;
                    }
                    else
                    {
                        bool left = self.ClimbCheck(-1);
                        bool right = self.ClimbCheck(1);

                        if ((self.DashDir.Y >= 0 || self.DashDir.X != 0) && ((self.moveX == 1 && right) || (self.moveX == -1 && left)))
                        {
                            self.Facing = (Facings)self.moveX;
                            self.Speed.X = 0;
                            self.dashAttackTimer = 0;
                        }
                    }

                    self.StateMachine.state = Player.StNormal;
                    
                    self.Depth = Depths.Player;
                    self.TreatNaive = false;
                    self.Stop(self.dreamSfxLoop);
                    self.Play("event:/char/madeline/dreamblock_exit");
                }
            }
            else if (self.level.OnInterval(0.04f)) {
                var disp = self.level.Displacement.AddBurst(self.Center, .3f, 0f, 20f);
                disp.WorldClipCollider = block.Collider;
                disp.WorldClipPadding = 2;
            }
            
            
        } else if (self.DashAttacking) {
            if (self.StateMachine.state != Player.StRedDash && self.dashAttackTimer > 0)
                self.dashAttackTimer -= Engine.DeltaTime * 60 / ExtVarsDashLength();
            if (self.StateMachine.state == Player.StRedDash && dash)
                PicoDash();
            if (!_boosting && self.StateMachine.state is not Player.StRedDash) AddSmoke(self.X, self.Y);
        } else {
            self.Stop(self.dreamSfxLoop);
                
            if (self.CurrentBooster == null)
                if (isUnderwater) {
                    // swim
                    self.StateMachine.state = Player.StSwim;
                    if (!self.level.InCutscene) {
                        var inputVec = Input.Feather.Value.SafeNormalize();

                        if (inputVec.X != 0 || inputVec.Y != 0) {
                            if (inputVec.X != 0)
                                self.Speed.X = Approach(self.Speed.X / Pico8SpeedUnit, MaxRun * inputVec.X, accel * Engine.DeltaTime * 60) *
                                          Pico8SpeedUnit;
                            if (inputVec.Y != 0)
                                self.Speed.Y = Approach(self.Speed.Y / Pico8SpeedUnit, MaxRun * inputVec.Y, accel * Engine.DeltaTime * 60) *
                                          Pico8SpeedUnit;
                        }
                    }

                    self.Speed *= (float) Math.Pow(0.02, Engine.DeltaTime);
                } else {
                    self.StateMachine.state = Player.StNormal;
                    if (!self.onGround)
                        accel = 0.4f;
                    else
                        accel *= self.level.CoreMode == Session.CoreModes.Cold ? 0.15f : 1f;
                    
                    float maxRun = ExtVarsHorizontalSpeed() * MaxRun;
                    
                    if (Math.Abs(self.Speed.X / Pico8SpeedUnit) > maxRun)
                        self.Speed.X = Approach(self.Speed.X / Pico8SpeedUnit, Math.Sign(self.Speed.X) * maxRun, Deceleration * Engine.DeltaTime * 60) * Pico8SpeedUnit;
                    else
                        self.Speed.X = Approach(self.Speed.X / Pico8SpeedUnit, input * maxRun, accel * Engine.DeltaTime * 60) * Pico8SpeedUnit;
                    
                    // gravity
                    var maxfall = 2f * ExtVarsMaxFall();
                    var gravity = 0.21f / 2 * ExtVarsGravityMult();
                    if (self.Holding is { SlowFall: true } && Input.MoveY <= 0) {
                        maxfall *= .3f;
                        gravity *= .5f;
                    }

                    if (Math.Abs(self.Speed.Y) <= 0.15f)
                        gravity *= 0.5f;

                    // wall slide
                    if (input != 0 && IsSolid(input, 0))
                    {
                        maxfall = 0.4f;
                        if (Rand.NextInt64(10) < 2)
                            AddSmoke(self.X + input * 6, self.Y);
                    }

                    if (!self.onGround && !self.wallBoosting)
                        self.Speed.Y = Approach(self.Speed.Y / Pico8SpeedUnit, maxfall, gravity * Engine.DeltaTime * 60 * (self.level.InSpace ? Player.SpacePhysicsMult : 1)) * Pico8SpeedUnit;

                    if (Input.GrabCheck && self.ClimbCheck((int)self.Facing)) {
                        if (!self.wallBoosting) { AddSmoke(self.X, self.Y); }
                        self.ClimbTrigger((int) self.Facing);
                    } else if (self.wallBoosting) {
                        self.wallBoosting = false;
                        if (self.conveyorLoopSfx != null)
                        {
                            self.conveyorLoopSfx.setParameterValue("end", 1);
                            self.conveyorLoopSfx.release();
                            self.conveyorLoopSfx = null;
                        }
                    }
                    
                    // jump
                    if (_jumpBuffer > 0)
                    {
                        if (_grace > 0 || ExtVarsConsumeJump())
                        {
                            // normal jump
                            Sfx(1);
                            _jumpBuffer = 0;
                            _grace = 0;
                            self.Speed.Y = -2 * Pico8SpeedUnit * ExtVarsJumpHeight();
                            AddSmoke(self.X, self.Y + 4);
                        }
                        else
                        {
                            // wall jump
                            var wallDir = IsSolid(-3, 0) ? -1 : IsSolid(3, 0) ? 1 : 0;
                            if (wallDir != 0)
                            {
                                Sfx(2);
                                _jumpBuffer = 0;
                                self.Speed.Y = -2 * Pico8SpeedUnit * ExtVarsJumpHeight();
                                self.Speed.X = -wallDir * (MaxRun + 1) * Pico8SpeedUnit;
                                self.Facing = (Facings) (-wallDir);
                                
                                if (self.LiftSpeed == Vector2.Zero)
                                {
                                    var solid = self.CollideFirst<Solid>(self.Position + Vector2.UnitX * 3f * -wallDir);
                                    if (solid != null) 
                                        self.LiftSpeed = solid.LiftSpeed;
                                }
                            }
                        }

                        self.LaunchedBoostCheck();
                        
                        if (self.liftSpeedTimer > 0)
                            self.Speed += self.LiftBoost;
                        
                    }
                }

            if (self.Dashes > 0 && dash && self.Holding == null)
                PicoDash();
            else if (dash && self.Dashes <= 0 && self.Inventory.Dashes > 0) {
                Sfx(9);
                AddSmoke(self.X, self.Y);
            }
        }

        if (self.StateMachine.state != Player.StDreamDash) {
            if (Input.GrabCheck) {
                if (!self.Ducking)
                    foreach (Holdable hold in self.Scene.Tracker.GetComponents<Holdable>())
                        if (hold.Check(self) && self.Pickup(hold))
                            break;
                            var booster = self.WallBoosterCheck();
                if (booster != null) {
                    self.wallBoosting = true;
    
                    if (self.conveyorLoopSfx == null)
                        self.conveyorLoopSfx = Audio.Play("event:/game/09_core/conveyor_activate", "end", 0);
                    Audio.Position(self.conveyorLoopSfx, self.Position);
                    
                    self.Speed.Y = Calc.Approach(self.Speed.Y, Player.WallBoosterSpeed, Player.WallBoosterAccel * Engine.DeltaTime);
                    self.LiftSpeed = Vector2.UnitY * Math.Max(self.Speed.Y, Player.WallBoosterLiftSpeed);
                    Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
                }
            } else {
                self.wallBoosting = false;
                if (self.conveyorLoopSfx != null)
                {
                    self.conveyorLoopSfx.setParameterValue("end", 1);
                    self.conveyorLoopSfx.release();
                    self.conveyorLoopSfx = null;
                }
                if (self.Holding != null) {
                    if (self.Ducking)
                        self.Drop();
                    else
                        self.Throw();
                }
            }
        }
        
    EndChecks:
        if (self.StateMachine.state != Player.StDreamDash && self.StateMachine.State != Player.StAttract) {
            self.MoveH(self.Speed.X * Engine.DeltaTime, OnCollideH);
            self.MoveV(self.Speed.Y * Engine.DeltaTime, OnCollideV);
        }
        
        self.UpdateChaserStates();
        
        // animation
        _spriteOff += 0.125f;
        if (self.StateMachine.State != Player.StDummy || !self.DummyAutoAnimate) {
            if (!self.onGround) {
                self.Sprite.CurrentAnimationID = IsSolid(input, 0) ? "wallslide" : "fallSlow";
                _sprite = IsSolid(input, 0) ? 5 : 3;
            }
            else if (Input.MoveY > 0) {
                self.Sprite.CurrentAnimationID = "duck";
                _sprite = 6;
            }
            else if (Input.MoveY < 0) {
                self.Sprite.CurrentAnimationID = "lookUp";
                _sprite = 7;
            }
            else if (self.Speed.X == 0 || Input.MoveX == 0) {
                self.Sprite.CurrentAnimationID = "idle";
                _sprite = 1;
            }
            else {
                self.Sprite.CurrentAnimationID = "runFast";
                _sprite = 1 + _spriteOff % 4;
            }

            if (self.StateMachine.State == Player.StDreamDash)
                self.Sprite.CurrentAnimationID = "dreamDashLoop";
            else if (self.dashAttackTimer > 0)
                self.Sprite.CurrentAnimationID = "dash";
        }
        
        if (self.EnforceLevelBounds) self.level.EnforceBounds(self);
        
        foreach (PlayerCollider pc in self.Scene.Tracker.GetComponents<PlayerCollider>())
            if (pc.Check(self) && self.Dead)
                break;
        
        foreach (Trigger trigger in self.Scene.Tracker.GetEntities<Trigger>())
            if (self.CollideCheck(trigger)) {
                if (!trigger.Triggered) {
                    trigger.Triggered = true;
                    self.triggersInside.Add(trigger);
                    trigger.OnEnter(self);
                }
                trigger.OnStay(self);
            }
            else if (trigger.Triggered) {
                self.triggersInside.Remove(trigger);
                trigger.Triggered = false;
                trigger.OnLeave(self);
            }
        
        EndHair:
        var hairAnchor = new Vector2(self.X + 4 - (int) self.Facing * 2, self.Y + (Input.MoveY.Value > 0 ? 4f : 3f));
        for (var idx = 0; idx < self.Hair.Nodes.Count; idx++) {
            var node = self.Hair.Nodes[idx];
            // Approach
            node.X += (float) (hairAnchor.X - (double) node.X) * (1f - (float) Math.Pow(0.5f, Engine.DeltaTime * 60));
            node.Y += (float) (hairAnchor.Y + 0.5 - node.Y) * (1f - (float) Math.Pow(0.5f, Engine.DeltaTime * 60));
            self.Hair.Nodes[idx] = node;
            hairAnchor = node;
        }
        _hairCalcPosition = self.Position;
        
        End:
        _wasOnGround = self.onGround;
        _lastState = self.StateMachine.state;
    }

    private void PicoDash()
    {
        var dashInput = Input.GetAimVector(self.Facing);
        dashInput.Normalize();
        self.DashDir = dashInput;

        _boosting = self.CurrentBooster != null;

        if (_boosting) {
            self.StateMachine.state = self.CurrentBooster!.red ? Player.StRedDash : Player.StDash;
            self.dashAttackTimer = 8;
            self.CurrentBooster.PlayerBoosted(self, self.DashDir);
            self.LastBooster = self.CurrentBooster = null;
        } else {
            AddSmoke(self.X, self.Y);
            self.Dashes--;
            self.dashAttackTimer = 8;
            self.StateMachine.state = Player.StDash;
                    
            Sfx(3);
            Celeste.Freeze(0.05f);
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);

            ++SaveData.Instance.TotalDashes;
            ++self.level.Session.Dashes;
            Stats.Increment(Stat.DASHES);
            foreach (DashListener component in self.Scene.Tracker.GetComponents<DashListener>())
                component.OnDash?.Invoke(self.DashDir);
        }

        self.calledDashEvents = true;

        self.Speed = dashInput * 2.5f * Pico8SpeedUnit;

        if (self.Scene is Level level)
            level.Shake(6f / 30f);
        _dashTarget.X = 2 * Math.Sign(dashInput.X);
        _dashTarget.Y = 2 * Math.Sign(dashInput.Y);
        if (_boosting) _dashTarget *= 1.5f;
        _dashTarget *= ExtVarsDashSpeed();
        _dashAccel.X = 1.5f;
        _dashAccel.Y = 1.5f;

        if (self.Speed.Y < 0 && !_boosting)
            _dashTarget.Y *= 0.75f;
        // Manual normalization? 
        if (self.Speed.Y != 0)
            _dashAccel.X *= 0.70710678118f;
        if (self.Speed.X != 0)
            _dashAccel.Y *= 0.70710678118f;
    }

    private float ExtVarsGravityMult() => PicolineModule.Instance.ExtVarsLoaded ? __ExtVarsGravityMultUnchecked() : 1;
    
    private float __ExtVarsGravityMultUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.Gravity);
    
    private float ExtVarsMaxFall() => PicolineModule.Instance.ExtVarsLoaded ? __ExtVarsMaxFallUnchecked() : 1;
    
    private float __ExtVarsMaxFallUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.FallSpeed);
        
    private float ExtVarsDashSpeed() => PicolineModule.Instance.ExtVarsLoaded ? __ExtVarsDashSpeedUnchecked() : 1;
    
    private float __ExtVarsDashSpeedUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.DashSpeed);
        
    private float ExtVarsDashLength() => PicolineModule.Instance.ExtVarsLoaded ? __ExtVarsDashLengthUnchecked() : 1;
    
    private float __ExtVarsDashLengthUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.DashLength);

    private float ExtVarsJumpHeight() => PicolineModule.Instance.ExtVarsLoaded ? __ExtVarsJumpHeightUnchecked() : 1;
    
    private float __ExtVarsJumpHeightUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.JumpHeight);

    private float ExtVarsHorizontalSpeed() => PicolineModule.Instance.ExtVarsLoaded ? __ExtVarsHorizontalSpeedUnchecked() : 1;
    
    private float __ExtVarsHorizontalSpeedUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.SpeedX);
    
    private bool ExtVarsConsumeJump() => PicolineModule.Instance.ExtVarsLoaded && __ExtVarsConsumeJumpUnchecked();

    private bool __ExtVarsConsumeJumpUnchecked() {
        if (IsSolid(-3, 0) || IsSolid(3, 0)) return false;
        int jumpBuffer = JumpCount.GetJumpBuffer();
        if (jumpBuffer <= 0) return false;
        JumpCount.SetJumpCount(--jumpBuffer, false);
        return true;
    }
    
    private bool ExtVarsRefillJumps() => PicolineModule.Instance.ExtVarsLoaded && __ExtVarsRefillJumpsUnchecked();

    private bool __ExtVarsRefillJumpsUnchecked() {
        return JumpCount.RefillJumpBuffer();
    }

#nullable disable
    

    private void OnCollideH(CollisionData data) {
        if (self.StateMachine.State == Player.StDreamDash)
            return;

        if (self.StateMachine.State == Player.StStarFly) {
            if (self.starFlyTimer < Player.StarFlyEndNoBounceTime)
                self.Speed.X = 0;
            else {
                self.Play("event:/game/06_reflection/feather_state_bump");
                Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
                self.Speed.X *= Player.StarFlyWallBounce;
            }
            return;
        }
        
        if (
            self.DashAttacking && data.Hit is { OnDashCollide: not null } &&
            Math.Abs(data.Direction.X - Math.Sign(self.DashDir.X)) < 0.01
        ) {
            var collisionResults = data.Hit.OnDashCollide(self, data.Direction);
            if (collisionResults == DashCollisionResults.NormalOverride)
                collisionResults = DashCollisionResults.NormalCollision;
            switch (collisionResults) {
                case DashCollisionResults.Rebound:
                    self.Speed.X *= -0.5f;
                    self.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Bounce:
                    self.Speed.X = Math.Sign(self.Speed.X) * -1 * 240;
                    self.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Ignore:
                    return;
            }
        }
        
        if (self.DreamDashCheck(Vector2.UnitX * Math.Sign(self.Speed.X))) {
            self.StateMachine.state = Player.StDreamDash;
            self.Play("event:/char/madeline/dreamblock_enter");
            self.Loop(self.dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            self.Depth = Depths.PlayerDreamDashing;
            self.TreatNaive = true;
            self.dashAttackTimer = 0;
            return;
        }

        if (self.StateMachine.state == Player.StRedDash)
            self.StateMachine.state = Player.StNormal;
        
        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);
        
        self.Speed.X = 0;
        self.dashAttackTimer = 0;
    }
    
    private void OnCollideV(CollisionData data) {
        if (self.StateMachine.State == Player.StDreamDash)
            return;
        
        if (self.StateMachine.State == Player.StStarFly) {
            if (self.starFlyTimer < Player.StarFlyEndNoBounceTime)
                self.Speed.Y = 0;
            else {
                self.Play("event:/game/06_reflection/feather_state_bump");
                Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
                self.Speed.Y *= Player.StarFlyWallBounce;
            }
            return;
        }
        
        if (
            self.DashAttacking && data.Hit is { OnDashCollide: not null }
                          && Math.Abs(data.Direction.Y - Math.Sign(self.DashDir.Y)) < 0.01
        ) {
            var collisionResults = data.Hit.OnDashCollide(self, data.Direction);
            if (collisionResults == DashCollisionResults.NormalOverride)
                collisionResults = DashCollisionResults.NormalCollision;
            switch (collisionResults) {
                case DashCollisionResults.Rebound:
                    self.Speed.Y *= -1;
                    self.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Bounce:
                    self.Speed.Y = Math.Sign(self.Speed.Y) * -1 * 240;
                    self.dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Ignore:
                    return;
            }
        }
        
        if (self.DreamDashCheck(Vector2.UnitY * Math.Sign(self.Speed.Y))) {
            self.StateMachine.state = Player.StDreamDash;
            self.Play("event:/char/madeline/dreamblock_enter");
            self.Loop(self.dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            self.dashAttackTimer = 0;
            self.Depth = Depths.PlayerDreamDashing;
            self.TreatNaive = true;
            return;
        }
        
        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);
        
        if (self.StateMachine.state == Player.StRedDash)
            self.StateMachine.state = Player.StNormal;
        
        self.Speed.Y = 0;
        self.dashAttackTimer = 0;
    }
    
    private void AddSmoke(float x, float y) => self.level.Add(new Smoke(new Vector2(x, y)));

    private static readonly MTexture[] PlayerTextures;
    
    // By all means, this should be done in Update().
    // However, for some reason, doing this there makes the hair one frame behind.
    internal Vector2 _hairCalcPosition;

    public PicoPlayer(Player self)  {
        this.self = self;
        if (self.dreamSfxLoop == null) {
            self.Add(self.dreamSfxLoop = new SoundSource());
        }
    }

    private bool _drawAsSilhouette =>
        (PicolineModule.Instance.ExtVarsLoaded && __ExtVarsDrawAsSilhouetteUnchecked())
        || self.StateMachine.State == Player.StDreamDash;

    private bool __ExtVarsDrawAsSilhouetteUnchecked() =>
        (
            ExtendedVariantsModule.Instance.MaxHelpingHandInstalled ||
            ExtendedVariantsModule.Instance.SpringCollab2020Installed
        ) &&
        (bool) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(
            ExtendedVariantsModule.Variant.MadelineIsSilhouette
        );

    public void Render() {
        if (self.CurrentBooster != null) return;
        
        var playerTexture = PlayerTextures[(int) _sprite + (_drawAsSilhouette ? 16 : 0)];
        var hairTexture = PlayerTextures[(int) _sprite + 8];
        var flip = SpriteEffects.None;
        if (self.Facing == Facings.Left) flip |= SpriteEffects.FlipHorizontally;

        var hairColor = HairColor();

        if (self.StateMachine.state != Player.StStarFly) {
            playerTexture.Draw(_hairCalcPosition - Vector2.UnitX, Vector2.Zero, Color.Black, 1f, 0f, flip);
            playerTexture.Draw(_hairCalcPosition - Vector2.UnitY, Vector2.Zero, Color.Black, 1f, 0f, flip);
            playerTexture.Draw(_hairCalcPosition + Vector2.UnitX, Vector2.Zero, Color.Black, 1f, 0f, flip);
            playerTexture.Draw(_hairCalcPosition + Vector2.UnitY, Vector2.Zero, Color.Black, 1f, 0f, flip);
            hairTexture.Draw(_hairCalcPosition - Vector2.UnitX, Vector2.Zero, Color.Black, 1f, 0f, flip);
            hairTexture.Draw(_hairCalcPosition - Vector2.UnitY, Vector2.Zero, Color.Black, 1f, 0f, flip);
            hairTexture.Draw(_hairCalcPosition + Vector2.UnitX, Vector2.Zero, Color.Black, 1f, 0f, flip);
            hairTexture.Draw(_hairCalcPosition + Vector2.UnitY, Vector2.Zero, Color.Black, 1f, 0f, flip);
        }

        if (self.StateMachine.state != Player.StRedDash) {
            var i = 0;
            foreach (var node in self.Hair.Nodes)
            {
                var hairSize = self.StateMachine.State == Player.StStarFly && i == 0 ? 3 : i < 2 ? 2 : 1;
                PicoCircle(new Vector2(node.X - 1, node.Y), hairSize, Color.Black);
                PicoCircle(new Vector2(node.X + 1, node.Y), hairSize, Color.Black);
                PicoCircle(new Vector2(node.X, node.Y - 1), hairSize, Color.Black);
                PicoCircle(new Vector2(node.X, node.Y + 1), hairSize, Color.Black);
                i++;
            }
            i = 0;
            foreach (var node in self.Hair.Nodes)
            {
                var hairSize = self.StateMachine.State == Player.StStarFly && i == 0 ? 3 : i < 2 ? 2 : 1;
                PicoCircle(new Vector2(node.X, node.Y), hairSize, hairColor);
                i++;
            }
        }

        if (self.StateMachine.state == Player.StStarFly) return;
        playerTexture.Draw(_hairCalcPosition, Vector2.Zero, _drawAsSilhouette ? hairColor : Color.White, 1f, 0f, flip);
        hairTexture.Draw(_hairCalcPosition, Vector2.Zero, hairColor, 1f, 0f, flip);
        
    }
}