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

namespace Celeste.Mod.PicoPlayer;

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

[TrackedAs(typeof(Player))]
public class PicoPlayer : Player {
    
    private static readonly MTexture PlayerAtlas = GFX.Game["PicoPlayer/player_atlas"];

    public static readonly Hitbox PicoHitbox = new(6, 5, 1, 3);

    // move
    private new const int MaxRun = 1;
    private const float Deceleration = 0.075f;
    public const float Pico8SpeedUnit = 60;

    internal static readonly Random Rand = new();

    private float _sprite = 1;
    private bool _jumpWasPressed;
    private bool _dashWasPressed;
    private int _grace;
    private int _jumpBuffer;
    private Vector2 _dashTarget;
    private Vector2 _dashAccel;
    private float _spriteOff;
    private bool _wasOnGround;
    private int _lastState;
    private bool _boosting;

    private Hitbox _oldNormalHitbox;
    private Hitbox _oldDuckHitbox;
    private Hitbox _oldStarFlyHitbox;
    private Hitbox _oldNormalHurtbox;
    private Hitbox _oldDuckHurtbox;
    private Hitbox _oldStarFlyHurtbox;
    private Vector2 _oldLightPosition;
    private int _oldHairNodeCount;

    private bool _overriding;
    public bool Overriding {
        get => _overriding;
        set {
            if (value == _overriding) return;
            StateMachine.state = StNormal;
            if (value) {
                _overriding = true;
                StateMachine.RemoveSelf();
                Hair.Active = false;
                Hair.Visible = false;
                Position.Y -= 8;
                Collider = PicoHitbox;
                hurtbox = PicoHitbox;
                _hairCalcPosition = Position;
                _oldLightPosition = Light.Position;
                Light.Position = new Vector2(4, 4);
                _oldHairNodeCount = Hair.Nodes.Count;
                Hair.Nodes = Enumerable.Repeat(Position, 5).ToList();
                OverrideHitboxes();
            } else {
                _overriding = false;
                Add(StateMachine);
                Hair.Active = true;
                Hair.Visible = true;
                Position.Y += 8;
                Collider = normalHitbox;
                hurtbox = normalHurtbox;
                Light.Position = _oldLightPosition;
                Hair.Nodes = Enumerable.Repeat(Position, _oldHairNodeCount).ToList();
                RestoreHitboxes();
            }
        }
    }

    private void OverrideHitboxes() {
        _oldNormalHitbox = (Hitbox) normalHitbox.Clone();
        normalHitbox.Position = PicoHitbox.Position;
        normalHitbox.width = PicoHitbox.width;
        normalHitbox.height = PicoHitbox.height;

        _oldDuckHitbox = (Hitbox) duckHitbox.Clone();
        duckHitbox.Position = PicoHitbox.Position;
        duckHitbox.width = PicoHitbox.width;
        duckHitbox.height = PicoHitbox.height;

        _oldStarFlyHitbox = (Hitbox) starFlyHitbox.Clone();
        starFlyHitbox.Position = PicoHitbox.Position;
        starFlyHitbox.width = PicoHitbox.width;
        starFlyHitbox.height = PicoHitbox.height;

        _oldNormalHurtbox = (Hitbox) normalHurtbox.Clone();
        normalHurtbox.Position = PicoHitbox.Position;
        normalHurtbox.width = PicoHitbox.width;
        normalHurtbox.height = PicoHitbox.height;

        _oldDuckHurtbox = (Hitbox) duckHurtbox.Clone();
        duckHurtbox.Position = PicoHitbox.Position;
        duckHurtbox.width = PicoHitbox.width;
        duckHurtbox.height = PicoHitbox.height;

        _oldStarFlyHurtbox = (Hitbox) starFlyHurtbox.Clone();
        starFlyHurtbox.Position = PicoHitbox.Position;
        starFlyHurtbox.width = PicoHitbox.width;
        starFlyHurtbox.height = PicoHitbox.height;
    }
    
    private void RestoreHitboxes() {
        normalHitbox.Position = _oldNormalHitbox.Position;
        normalHitbox.width = _oldNormalHitbox.width;
        normalHitbox.height = _oldNormalHitbox.height;

        duckHitbox.Position = _oldDuckHitbox.Position;
        duckHitbox.width = _oldDuckHitbox.width;
        duckHitbox.height = _oldDuckHitbox.height;

        starFlyHitbox.Position = _oldStarFlyHitbox.Position;
        starFlyHitbox.width = _oldStarFlyHitbox.width;
        starFlyHitbox.height = _oldStarFlyHitbox.height;

        normalHurtbox.Position = _oldNormalHurtbox.Position;
        normalHurtbox.width = _oldNormalHurtbox.width;
        normalHurtbox.height = _oldNormalHurtbox.height;

        duckHurtbox.Position = _oldDuckHurtbox.Position;
        duckHurtbox.width = _oldDuckHurtbox.width;
        duckHurtbox.height = _oldDuckHurtbox.height;

        starFlyHurtbox.Position = _oldStarFlyHurtbox.Position;
        starFlyHurtbox.width = _oldStarFlyHurtbox.width;
        starFlyHurtbox.height = _oldStarFlyHurtbox.height;
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

    public PicoPlayer(Vector2 position, PlayerSpriteMode mode) : base(position, mode) {
        /*StateMachine.RemoveSelf();
        reflection.RemoveSelf();
        Hair.RemoveSelf();
        Hair = new PlayerHair(new PlayerSprite(PlayerSpriteMode.MadelineNoBackpack)) {
            Nodes = Enumerable.Repeat(position, Math.Max(5, Hair.Nodes.Count)).ToList(),
            Color = Hair.Color,
            Entity = this
        };
        Position = position;
        Collider = PicoHitbox;
        hurtbox = PicoHitbox;
        Light.Position = new Vector2(4, 4); */
        
        Add(dreamSfxLoop = new SoundSource());
    }

    private static float Approach(float val, float target, float amount)
    {
        return val <= target ? Math.Min(val + amount, target) : Math.Max(val - amount, target);
    }
    
    private void CheckOnGround()
    {
        if (SwimCheck()) {
            onGround = OnSafeGround = false;
        } else if (Speed.Y >= 0.0) {
            var platform = (Platform) CollideFirst<Solid>(Position + Vector2.UnitY) ?? CollideFirstOutside<JumpThru>(Position + Vector2.UnitY);
            if (platform != null)
            {
                onGround = true;
                OnSafeGround = platform.Safe;
            }
            else
                onGround = OnSafeGround = false;
        } else
            onGround = OnSafeGround = false;


        if (!OnSafeGround) return;
        foreach (SafeGroundBlocker component in Scene.Tracker.GetComponents<SafeGroundBlocker>())
            if (component.Check(this)) {
                OnSafeGround = false;
                break;
            }
    }
    private bool IsSolid(float x, float y)
    {
        return CollideCheck<Solid>(Position + new Vector2(x, y));
    }

    private Color _randomHairColor = PicoColors.Red;
    
    private Color HairColor() {
        if (StateMachine.state == StStarFly) 
            return starFlyTransforming ? PicoColors.White : PicoColors.Yellow;
        if (StateMachine.state == StDreamDash) return _randomHairColor;
        if (DashAttacking) return PicoColors.White;
        
        return Dashes switch {
            2 => level.TimeActive % 0.3 > 0.15 && !Settings.Instance.DisableFlashes ? PicoColors.White : PicoColors.Green,
            1 => PicoColors.Red,
            0 => Inventory.Dashes == 0 ? PicoColors.Red : PicoColors.Cyan,
            _ => _randomHairColor
        };
    }

    private static void Sfx(int sfx) => Audio.Play("event:/classic/sfx" + sfx);

    [MonoModLinkTo("Celeste.Actor", "Update")]
    private void actor_Update() { }

    private IEnumerator _cassetteFlight;
    private float _cassetteFlightTimer;
    
    public override void Update() {
        if (!_overriding) {
            base.Update();
            return;
        }

        if (
            StateMachine.state is
            StIntroJump or StIntroRespawn or StIntroWalk or StIntroMoonJump or StIntroWakeUp or StIntroThinkForABit
        )
            StateMachine.state = StNormal;

        actor_Update();

        if (StateMachine.state == StFrozen) goto End;
        
        if (StateMachine.state == StCassetteFly) {
            _cassetteFlight ??= CassetteFlyCoroutine();
            if (_cassetteFlightTimer <= 0) {
                if (!_cassetteFlight.MoveNext())
                    goto End;
                _cassetteFlightTimer = (float?) _cassetteFlight.Current ?? 0;
            }
            _cassetteFlightTimer -= Engine.DeltaTime;
            goto EndHair;
        }

        StrawberryCollectResetTimer -= Engine.DeltaTime;
        if (StrawberryCollectResetTimer <= 0)
            StrawberryCollectIndex = 0;
        
        Leader.Position = Vector2.Zero;
        dashCooldownTimer = 0;
        
        // facing
        if (Speed.X != 0)
            Facing = Speed.X < 0 ? Facings.Left : Facings.Right;

        var input = level.InCutscene ? 0 : Input.MoveX.Value;

        if (!InControl) {
            if (StateMachine.State != StDummy) goto EndChecks;
            
            if (!DummyMoving) {
                if (Math.Abs(Speed.X) > 90.0 && DummyMaxspeed)
                    Speed.X = Calc.Approach(Speed.X, 90f * Math.Sign(Speed.X), 2500f * Engine.DeltaTime);
                if (DummyFriction)
                    Speed.X = Calc.Approach(Speed.X, 0.0f, 1000f * Engine.DeltaTime);
            }

            if (DummyGravity) {
                // gravity
                var maxfall = 2f * ExtVarsMaxFall();
                var gravity = 0.21f / 2 * ExtVarsGravityMult();

                if (Math.Abs(Speed.Y) <= 0.15f)
                    gravity *= 0.5f;

                // wall slide
                if (input != 0 && IsSolid(input, 0))
                {
                    maxfall = 0.4f;
                    if (Rand.NextInt64(10) < 2)
                        AddSmoke(X + input * 6, Y);
                }

                if (!onGround)
                    Speed.Y = Approach(Speed.Y / Pico8SpeedUnit, maxfall, gravity * Engine.DeltaTime * 60) * Pico8SpeedUnit;
            }

            if (DummyAutoAnimate) {
                if (!onGround)
                    _sprite = IsSolid(Math.Sign(Speed.X), 0) ? 5 : 3;
                else if (Speed.Y > 0)
                    _sprite = 6;
                else if (Speed.Y < 0)
                    _sprite = 7;
                else if (Speed.X == 0)
                    _sprite = 1;
                else
                    _sprite = 1 + _spriteOff % 4;
            }

            if (Sprite.CurrentAnimationID is "duck" or "sleep" )
                _sprite = 7;
            
            goto EndChecks;
        }

        level.Camera.Position += (CameraTarget - level.Camera.Position) * (1f - (float) Math.Pow(0.01f, Engine.DeltaTime));
        
        UpdateCarry();
        if (minHoldTimer > 0)
            minHoldTimer -= Engine.DeltaTime;
        
        var jump = Input.Jump.Pressed && !_jumpWasPressed && !level.InCutscene;
        _jumpWasPressed = Input.Jump.Pressed;
        if (jump)
            _jumpBuffer = 8;
        else if (_jumpBuffer > 0)
            _jumpBuffer--;

        var dash = Input.Dash.Pressed && !_dashWasPressed && !level.InCutscene;
        _dashWasPressed = Input.Dash.Pressed;

        Ducking = Input.MoveY > 0 && onGround;

        if (dreamDashCanEndTimer > 0) 
            dreamDashCanEndTimer -= Engine.DeltaTime;
        
        // Accessibility is important!!!!
        if (Scene.OnInterval(Settings.Instance.DisableFlashes ? 0.4f : 0.1f)) {
            _randomHairColor = Rand.Choose(
                PicoColors.Red, PicoColors.Orange, PicoColors.Yellow, PicoColors.Green, PicoColors.Cyan, 
                PicoColors.Pink, PicoColors.Purple, PicoColors.White, PicoColors.Brown, PicoColors.DarkBlue, 
                PicoColors.DarkRed, PicoColors.DarkGreen
            );
        }

        if (StateMachine.state == StSummitLaunch) {
            if (_lastState != StSummitLaunch)
                SummitLaunchBegin();
            SummitLaunchUpdate();
            goto EndChecks;
        }

        if (StateMachine.state != StStarFly && (_lastState == StStarFly || starFlyTimer > 0)) {
            StarFlyEnd();
            starFlyTransforming = false;
            starFlyTimer = 0;
        } else if (starFlyTimer > 0) {
            if (dash) {
                PicoDash();
                starFlyTimer = 0;
                StarFlyEnd();
            }
            StateMachine.state = StStarFly;
            if (_lastState != StStarFly) StarFlyBegin();
        }

        if (starFlyTransforming) {
            Speed = Calc.Approach(Speed, Vector2.Zero, 1000f * Engine.DeltaTime);
            starFlyTransforming = Speed.Length() > 1;
            goto EndChecks;
        }

        if (StateMachine.state == StStarFly && !starFlyTransforming && starFlyTimer > 0) {
            StateMachine.state = StarFlyUpdate();
            if (StateMachine.state != StStarFly)
                StarFlyEnd();
            goto EndChecks;
        }

        if (StateMachine.state == StBoost && (BoostTimer > 0 || _lastState != StBoost)) {
            if (_lastState != StBoost) {
                BoostBegin();
                LastBooster = CurrentBooster;
                BoostTimer = 0.4f;
            }
            dashAttackTimer = 0;

            BoostTimer -= Engine.DeltaTime;

            if ((StateMachine.state = BoostUpdate()) != StBoost) {
                _boosting = false;
                BoostTimer = 0;
                dash = true;
            }
        } else if (_lastState == StBoost) {
            _boosting = false;
            BoostTimer = 0;
            StateMachine.state = StDash;
            dash = true;
        }

        _boosting &= LastBooster?.BoostingPlayer ?? false;

        if (StateMachine.state == StAttract) {
            if (Vector2.Distance(attractTo, ExactPosition) <= 1.5) {
                Position = attractTo;
                ZeroRemainderX();
                ZeroRemainderY();
            } else {
                var target = Calc.Approach(ExactPosition, attractTo, 200f * Engine.DeltaTime);
                MoveToX(target.X);
                MoveToY(target.Y);
            }

            goto EndChecks;
        }

        CheckOnGround();
        Hair.Color = HairColor();
        
        if (JustRespawned && Speed != Vector2.Zero)
            JustRespawned = false;

        if (!Dead) Audio.MusicUnderwater = UnderwaterMusicCheck();
        else goto End;

        bool isUnderwater = SwimCheck();

        // TODO: smoke particles
        if (onGround && !_wasOnGround) {
            AddSmoke(X, Y + 4);
        }

        if (onGround || isUnderwater) {
            if (_grace == 0) ExtVarsRefillJumps();
            _grace = 6;
            if (!Inventory.NoRefills && RefillDash()) {
                Sfx(54);
            }
        }
        else if (_grace > 0)
            _grace--;
    
        float accel = 0.6f;

        if (StateMachine.state is StDreamDash or StDash or StRedDash) {
            Speed.X = Approach(Speed.X / Pico8SpeedUnit, _dashTarget.X, _dashAccel.X) * Pico8SpeedUnit;
            Speed.Y = Approach(Speed.Y / Pico8SpeedUnit, _dashTarget.Y, _dashAccel.Y) * Pico8SpeedUnit;
        }
        
        if (StateMachine.state == StDreamDash) {
            Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
            var oldPos = Position;
            NaiveMove(Speed * Engine.DeltaTime);

            RefillDash();
            var block = CollideFirst<DreamBlock>();

            if (block == null) {
                if (DreamDashedIntoSolid()) {
                    if (SaveData.Instance.Assists.Invincible) {
                        Position = oldPos;
                        Speed *= -1;
                        _dashTarget *= -1;
                        Play("event:/game/general/assist_dreamblockbounce");
                    } else Die(Vector2.Zero);
                } else if (dreamDashCanEndTimer <= 0) {
                    Celeste.Freeze(.05f);

                    if (Input.Jump.Pressed && DashDir.X != 0) {
                        Sfx(1);
                        _jumpBuffer = 0;
                        _grace = 0;
                        Speed.Y = -2 * Pico8SpeedUnit;
                    }
                    else
                    {
                        bool left = ClimbCheck(-1);
                        bool right = ClimbCheck(1);

                        if ((DashDir.Y >= 0 || DashDir.X != 0) && ((moveX == 1 && right) || (moveX == -1 && left)))
                        {
                            Facing = (Facings)moveX;
                            Speed.X = 0;
                            dashAttackTimer = 0;
                        }
                    }

                    StateMachine.state = StNormal;
                    
                    Depth = Depths.Player;
                    TreatNaive = false;
                    Stop(dreamSfxLoop);
                    Play("event:/char/madeline/dreamblock_exit");
                }
            }
            else if (level.OnInterval(0.04f)) {
                var disp = level.Displacement.AddBurst(Center, .3f, 0f, 20f);
                disp.WorldClipCollider = block.Collider;
                disp.WorldClipPadding = 2;
            }
            
            
        } else if (DashAttacking) {
            if (StateMachine.state != StRedDash && dashAttackTimer > 0)
                dashAttackTimer -= Engine.DeltaTime * 60;
            if (StateMachine.state == StRedDash && dash)
                PicoDash();
            if (!_boosting && StateMachine.state is not StRedDash) AddSmoke(X, Y);
        } else {
            Stop(dreamSfxLoop);
                
            if (CurrentBooster == null)
                if (isUnderwater) {
                    // swim
                    StateMachine.state = StSwim;
                    if (!level.InCutscene) {
                        var inputVec = Input.Feather.Value.SafeNormalize();

                        if (inputVec.X != 0 || inputVec.Y != 0) {
                            if (inputVec.X != 0)
                                Speed.X = Approach(Speed.X / Pico8SpeedUnit, MaxRun * inputVec.X, accel * Engine.DeltaTime * 60) *
                                          Pico8SpeedUnit;
                            if (inputVec.Y != 0)
                                Speed.Y = Approach(Speed.Y / Pico8SpeedUnit, MaxRun * inputVec.Y, accel * Engine.DeltaTime * 60) *
                                          Pico8SpeedUnit;
                        }
                    }

                    Speed *= (float) Math.Pow(0.02, Engine.DeltaTime);
                } else {
                    StateMachine.state = StNormal;
                    if (!onGround)
                        accel = 0.4f;
                    
                    if (Math.Abs(Speed.X / Pico8SpeedUnit) > MaxRun)
                        Speed.X = Approach(Speed.X / Pico8SpeedUnit, Math.Sign(Speed.X) * MaxRun, Deceleration * Engine.DeltaTime * 60) * Pico8SpeedUnit;
                    else
                        Speed.X = Approach(Speed.X / Pico8SpeedUnit, input * MaxRun, accel * Engine.DeltaTime * 60) * Pico8SpeedUnit;
                    
                    // gravity
                    var maxfall = 2f * ExtVarsMaxFall();
                    var gravity = 0.21f / 2 * ExtVarsGravityMult();
                    if (Holding is { SlowFall: true } && Input.MoveY <= 0) {
                        maxfall *= .3f;
                        gravity *= .5f;
                    }

                    if (Math.Abs(Speed.Y) <= 0.15f)
                        gravity *= 0.5f;

                    // wall slide
                    if (input != 0 && IsSolid(input, 0))
                    {
                        maxfall = 0.4f;
                        if (Rand.NextInt64(10) < 2)
                            AddSmoke(X + input * 6, Y);
                    }

                    if (!onGround)
                        Speed.Y = Approach(Speed.Y / Pico8SpeedUnit, maxfall, gravity * Engine.DeltaTime * 60) * Pico8SpeedUnit;

                    if (Input.GrabCheck && ClimbCheck((int)Facing)) {
                        AddSmoke(X, Y);
                        ClimbTrigger((int) Facing);
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
                            Speed.Y = -2 * Pico8SpeedUnit;
                            AddSmoke(X, Y + 4);
                        }
                        else
                        {
                            // wall jump
                            var wallDir = IsSolid(-3, 0) ? -1 : IsSolid(3, 0) ? 1 : 0;
                            if (wallDir != 0)
                            {
                                Sfx(2);
                                _jumpBuffer = 0;
                                Speed.Y = -2 * Pico8SpeedUnit;
                                Speed.X = -wallDir * (MaxRun + 1) * Pico8SpeedUnit;
                                
                                if (LiftSpeed == Vector2.Zero)
                                {
                                    var solid = CollideFirst<Solid>(Position + Vector2.UnitX * 3f * -wallDir);
                                    if (solid != null) 
                                        LiftSpeed = solid.LiftSpeed;
                                }
                            }
                        }

                        LaunchedBoostCheck();
                        
                        if (liftSpeedTimer > 0)
                            Speed += LiftBoost;
                        
                    }
                }

            if (Dashes > 0 && dash && Holding == null)
                PicoDash();
            else if (dash && Dashes <= 0 && Inventory.Dashes > 0) {
                Sfx(9);
                AddSmoke(X, Y);
            }
        }

        if (StateMachine.state != StDreamDash) {
            if (Input.GrabCheck) {
                if (!Ducking)
                    foreach (Holdable hold in Scene.Tracker.GetComponents<Holdable>())
                        if (hold.Check(this) && Pickup(hold))
                            break;
            } else if (Holding != null) {
                if (Ducking)
                    Drop();
                else
                    Throw();
            }
        }
        
    EndChecks:
        if (StateMachine.state != StDreamDash && StateMachine.State != StAttract) {
            MoveH(Speed.X * Engine.DeltaTime, OnCollideH);
            MoveV(Speed.Y * Engine.DeltaTime, OnCollideV);
        }
        
        UpdateChaserStates();
        
        // animation
        _spriteOff += 0.125f;
        if (StateMachine.State != StDummy || !DummyAutoAnimate) {
            if (!onGround) {
                Sprite.CurrentAnimationID = IsSolid(input, 0) ? "wallslide" : "fallSlow";
                _sprite = IsSolid(input, 0) ? 5 : 3;
            }
            else if (Input.MoveY > 0) {
                Sprite.CurrentAnimationID = "duck";
                _sprite = 6;
            }
            else if (Input.MoveY < 0) {
                Sprite.CurrentAnimationID = "lookUp";
                _sprite = 7;
            }
            else if (Speed.X == 0 || Input.MoveX == 0) {
                Sprite.CurrentAnimationID = "idle";
                _sprite = 1;
            }
            else {
                Sprite.CurrentAnimationID = "runFast";
                _sprite = 1 + _spriteOff % 4;
            }

            if (StateMachine.State == StDreamDash)
                Sprite.CurrentAnimationID = "dreamDashLoop";
            else if (dashAttackTimer > 0)
                Sprite.CurrentAnimationID = "dash";
        }
        
        if (EnforceLevelBounds) level.EnforceBounds(this);
        
        foreach (PlayerCollider pc in Scene.Tracker.GetComponents<PlayerCollider>())
            if (pc.Check(this) && Dead)
                break;
        
        foreach (Trigger trigger in Scene.Tracker.GetEntities<Trigger>())
            if (CollideCheck(trigger)) {
                if (!trigger.Triggered) {
                    trigger.Triggered = true;
                    triggersInside.Add(trigger);
                    trigger.OnEnter(this);
                }
                trigger.OnStay(this);
            }
            else if (trigger.Triggered) {
                triggersInside.Remove(trigger);
                trigger.Triggered = false;
                trigger.OnLeave(this);
            }
        
        EndHair:
        var hairAnchor = new Vector2(X + 4 - (int) Facing * 2, Y + (Input.MoveY.Value > 0 ? 4f : 3f));
        for (var idx = 0; idx < Hair.Nodes.Count; idx++) {
            var node = Hair.Nodes[idx];
            // Approach
            node.X += (float) (hairAnchor.X - (double) node.X) * (1f - (float) Math.Pow(0.5f, Engine.DeltaTime * 60));
            node.Y += (float) (hairAnchor.Y + 0.5 - node.Y) * (1f - (float) Math.Pow(0.5f, Engine.DeltaTime * 60));
            Hair.Nodes[idx] = node;
            hairAnchor = node;
        }
        _hairCalcPosition = Position;
        
        End:
        _wasOnGround = onGround;
        _lastState = StateMachine.state;
    }

    private void PicoDash()
    {
        var dashInput = Input.GetAimVector(Facing);
        dashInput.Normalize();
        DashDir = dashInput;

        _boosting = CurrentBooster != null;

        if (_boosting) {
            StateMachine.state = CurrentBooster!.red ? StRedDash : StDash;
            dashAttackTimer = 8;
            CurrentBooster.PlayerBoosted(this, DashDir);
            LastBooster = CurrentBooster = null;
        } else {
            AddSmoke(X, Y);
            Dashes--;
            dashAttackTimer = 8;
            StateMachine.state = StDash;
                    
            Sfx(3);
            Celeste.Freeze(0.05f);
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);

            ++SaveData.Instance.TotalDashes;
            ++this.level.Session.Dashes;
            Stats.Increment(Stat.DASHES);
            foreach (DashListener component in Scene.Tracker.GetComponents<DashListener>())
                component.OnDash?.Invoke(DashDir);
        }

        calledDashEvents = true;

        Speed = dashInput * 2.5f * Pico8SpeedUnit;

        if (Scene is Level level)
            level.Shake(6f / 30f);
        _dashTarget.X = 2 * Math.Sign(dashInput.X);
        _dashTarget.Y = 2 * Math.Sign(dashInput.Y);
        if (_boosting) _dashTarget *= 1.5f;
        _dashAccel.X = 1.5f;
        _dashAccel.Y = 1.5f;

        if (Speed.Y < 0 && !_boosting)
            _dashTarget.Y *= 0.75f;
        // Manual normalization? 
        if (Speed.Y != 0)
            _dashAccel.X *= 0.70710678118f;
        if (Speed.X != 0)
            _dashAccel.Y *= 0.70710678118f;
    }

    private float ExtVarsGravityMult() => PicoPlayerModule.Instance.ExtVarsLoaded ? __ExtVarsGravityMultUnchecked() : 1;
    
    private float __ExtVarsGravityMultUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.Gravity);
    
    private float ExtVarsMaxFall() => PicoPlayerModule.Instance.ExtVarsLoaded ? __ExtVarsMaxFallUnchecked() : 1;
    
    private float __ExtVarsMaxFallUnchecked() => 
        (float) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(ExtendedVariantsModule.Variant.FallSpeed);
    
    private bool ExtVarsConsumeJump() => PicoPlayerModule.Instance.ExtVarsLoaded && __ExtVarsConsumeJumpUnchecked();

    private bool __ExtVarsConsumeJumpUnchecked() {
        if (IsSolid(-3, 0) || IsSolid(3, 0)) return false;
        int jumpBuffer = JumpCount.GetJumpBuffer();
        if (jumpBuffer <= 0) return false;
        JumpCount.SetJumpCount(--jumpBuffer, false);
        return true;
    }
    
    private bool ExtVarsRefillJumps() => PicoPlayerModule.Instance.ExtVarsLoaded && __ExtVarsRefillJumpsUnchecked();

    private bool __ExtVarsRefillJumpsUnchecked() {
        var jumpCountVar = (JumpCount)
            ExtendedVariantsModule.Instance.VariantHandlers[ExtendedVariantsModule.Variant.JumpCount];
        return jumpCountVar.RefillJumpBuffer();
    }

#nullable disable
    

    private new void OnCollideH(CollisionData data) {
        if (StateMachine.State == StDreamDash)
            return;

        if (StateMachine.State == StStarFly) {
            if (starFlyTimer < StarFlyEndNoBounceTime)
                Speed.X = 0;
            else {
                Play("event:/game/06_reflection/feather_state_bump");
                Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
                Speed.X *= StarFlyWallBounce;
            }
            return;
        }
        
        if (
            DashAttacking && data.Hit is { OnDashCollide: not null } &&
            Math.Abs(data.Direction.X - Math.Sign(DashDir.X)) < 0.01
        ) {
            var collisionResults = data.Hit.OnDashCollide(this, data.Direction);
            if (collisionResults == DashCollisionResults.NormalOverride)
                collisionResults = DashCollisionResults.NormalCollision;
            switch (collisionResults) {
                case DashCollisionResults.Rebound:
                    Speed.X *= -0.5f;
                    dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Bounce:
                    Speed.X = Math.Sign(Speed.X) * -1 * 240;
                    dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Ignore:
                    return;
            }
        }
        
        if (DreamDashCheck(Vector2.UnitX * Math.Sign(Speed.X))) {
            StateMachine.state = StDreamDash;
            Play("event:/char/madeline/dreamblock_enter");
            Loop(dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            Depth = Depths.PlayerDreamDashing;
            TreatNaive = true;
            dashAttackTimer = 0;
            return;
        }

        if (StateMachine.state == StRedDash)
            StateMachine.state = StNormal;
        
        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);
        
        Speed.X = 0;
        dashAttackTimer = 0;
    }
    
    private new void OnCollideV(CollisionData data) {
        if (StateMachine.State == StDreamDash)
            return;
        
        if (StateMachine.State == StStarFly) {
            if (starFlyTimer < StarFlyEndNoBounceTime)
                Speed.Y = 0;
            else {
                Play("event:/game/06_reflection/feather_state_bump");
                Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
                Speed.Y *= StarFlyWallBounce;
            }
            return;
        }
        
        if (
            DashAttacking && data.Hit is { OnDashCollide: not null }
                          && Math.Abs(data.Direction.Y - Math.Sign(DashDir.Y)) < 0.01
        ) {
            var collisionResults = data.Hit.OnDashCollide(this, data.Direction);
            if (collisionResults == DashCollisionResults.NormalOverride)
                collisionResults = DashCollisionResults.NormalCollision;
            switch (collisionResults) {
                case DashCollisionResults.Rebound:
                    Speed.Y *= -1;
                    dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Bounce:
                    Speed.Y = Math.Sign(Speed.Y) * -1 * 240;
                    dashAttackTimer = 0;
                    return;
                case DashCollisionResults.Ignore:
                    return;
            }
        }
        
        if (DreamDashCheck(Vector2.UnitY * Math.Sign(Speed.Y))) {
            StateMachine.state = StDreamDash;
            Play("event:/char/madeline/dreamblock_enter");
            Loop(dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            dashAttackTimer = 0;
            Depth = Depths.PlayerDreamDashing;
            TreatNaive = true;
            return;
        }
        
        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);
        
        if (StateMachine.state == StRedDash)
            StateMachine.state = StNormal;
        
        Speed.Y = 0;
        dashAttackTimer = 0;
    }
    
    private void AddSmoke(float x, float y) => level.Add(new Smoke(new Vector2(x, y)));

    private static readonly MTexture[] PlayerTextures;
    
    // By all means, this should be done in Update().
    // However, for some reason, doing this there makes the hair one frame behind.
    private Vector2 _hairCalcPosition;

    private bool _drawAsSilhouette =>
        (PicoPlayerModule.Instance.ExtVarsLoaded && __ExtVarsDrawAsSilhouetteUnchecked())
        || StateMachine.State == StDreamDash;

    private bool __ExtVarsDrawAsSilhouetteUnchecked() =>
        (
            ExtendedVariantsModule.Instance.MaxHelpingHandInstalled ||
            ExtendedVariantsModule.Instance.SpringCollab2020Installed
        ) &&
        (bool) ExtendedVariantsModule.Instance.TriggerManager.GetCurrentVariantValue(
            ExtendedVariantsModule.Variant.MadelineIsSilhouette
        );

    public override void Render() {
        if (!_overriding) {
            base.Render();
            return;
        }
        if (_boosting) return;
        
        var playerTexture = PlayerTextures[(int) _sprite + (_drawAsSilhouette ? 16 : 0)];
        var hairTexture = PlayerTextures[(int) _sprite + 8];
        var flip = SpriteEffects.None;
        if (Facing == Facings.Left) flip |= SpriteEffects.FlipHorizontally;

        var hairColor = HairColor();

        if (StateMachine.state != StRedDash) {
            var i = 0;
            foreach (var node in Hair.Nodes)
            {
                var hairSize = StateMachine.State == StStarFly && i == 0 ? 3 : i < 2 ? 2 : 1;
                PicoCircle(new Vector2(node.X, node.Y), hairSize, hairColor);
                i++;
            }
        }

        if (StateMachine.state == StStarFly) return;
        playerTexture.Draw(_hairCalcPosition, Vector2.Zero, _drawAsSilhouette ? hairColor : Color.White, 1f, 0f, flip);
        hairTexture.Draw(_hairCalcPosition, Vector2.Zero, hairColor, 1f, 0f, flip);
        
    }
}