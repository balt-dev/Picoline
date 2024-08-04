using System;
using System.Linq;
using System.Reflection;
using ExtendedVariants.Module;
using ExtendedVariants.Variants;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.PicoPlayer;

internal static class PicoColors {
    public static readonly Color Black = Calc.HexToColor("000000");
    public static readonly Color DarkBlue = Calc.HexToColor("1d2b53");
    public static readonly Color DarkRed = Calc.HexToColor("7e2553");
    public static readonly Color DarkGreen = Calc.HexToColor("008751");
    public static readonly Color Brown = Calc.HexToColor("ab5236");
    public static readonly Color Grey = Calc.HexToColor("5f574f");
    public static readonly Color LightGrey = Calc.HexToColor("c2c3c7");
    public static readonly Color White = Calc.HexToColor("fff1e8");
    public static readonly Color Red = Calc.HexToColor("ff004d");
    public static readonly Color Orange = Calc.HexToColor("ffa300");
    public static readonly Color Yellow = Calc.HexToColor("ffec27");
    public static readonly Color Green = Calc.HexToColor("00e436");
    public static readonly Color Cyan = Calc.HexToColor("29adff");
    public static readonly Color Purple = Calc.HexToColor("83769c");
    public static readonly Color Pink = Calc.HexToColor("ff77a8");
    public static readonly Color Cream = Calc.HexToColor("ffccaa");
}

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
        _spd.X = 0.3f + PicoPlayer.Rand.NextFloat(.2f);
        X += PicoPlayer.Rand.NextFloat(2) - 1f;
        Y += PicoPlayer.Rand.NextFloat(2) - 1f;
        if (PicoPlayer.Rand.NextSingle() > 0.5)
            _fx |= SpriteEffects.FlipHorizontally;
        if (PicoPlayer.Rand.NextSingle() > 0.5)
            _fx |= SpriteEffects.FlipVertically;
    }
    
    private bool _updatedLastFrame;
    public override void Update()
    {
        _updatedLastFrame = !_updatedLastFrame;
        if (_updatedLastFrame) return;
        Position += _spd;
        _spr += 0.2f;
        if (_spr < 3) return;
        RemoveSelf();
    }

    public override void Render() {
        Frames[Math.Min((int) _spr, 2)].Draw(Position + Vector2.One * 4, Vector2.One * 4, Color.White, 1f, 0f, _fx);
    }
}

[TrackedAs(typeof(Player))]
public class PicoPlayer : Player
{
    
    private static readonly MTexture PlayerAtlas = GFX.Game["PicoPlayer/player_atlas"];

    public static readonly Hitbox PicoHitbox = new(6, 5, 1, 3);

    // move
    private new const int MaxRun = 1;
    private const float Deceleration = 0.075f;
    private const float Pico8SpeedUnit = 60;

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
    private bool _dreamDashing;
    private int _lastState;

    static PicoPlayer()
    {
        PlayerTextures = new MTexture[16];
        for (var i = 0; i < 16; i++)
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

    public PicoPlayer(Vector2 position, PlayerSpriteMode mode) : base(position, mode)
    {
        StateMachine.RemoveSelf();
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
        Light.Position = new Vector2(4, 4); 
        dreamSfxLoop = new SoundSource();
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
        
        
        if (OnSafeGround)
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

    private DateTime _lastRandHairUpdate = DateTime.UnixEpoch;
    private Color _randomHairColor = PicoColors.Red;
    
    private Color HairColor() {
        if (dashAttackTimer > 0) return PicoColors.White;
        return Dashes switch {
            2 => level.TimeActive % 0.3 > 0.15 && !Settings.Instance.DisableFlashes ? PicoColors.White : PicoColors.Green,
            1 => PicoColors.Red,
            0 => PicoColors.Cyan,
            _ => _randomHairColor
        };
    }

    private static void Sfx(int sfx) => Audio.Play("event:/classic/sfx" + sfx);
    
    public override void Update()
    {
        if (Scene?.Paused ?? true) return;
        
        level.Camera.Position = CameraTarget;
        Leader.Position = Position;

        Components.Update();
        var input = Input.MoveX.Value;

        if (dreamDashCanEndTimer > 0) 
            dreamDashCanEndTimer -= Engine.DeltaTime;
        
        LiftSpeed = Vector2.Zero;
        if (liftSpeedTimer > 0.0)
            liftSpeedTimer -= Engine.DeltaTime;
        
        // Accessibility is important!!!!
        if (Scene.OnInterval(Settings.Instance.DisableFlashes ? 0.4f : 0.1f)) {
            _lastRandHairUpdate = DateTime.UtcNow;
            _randomHairColor = Rand.Choose(
                PicoColors.Red, PicoColors.Orange, PicoColors.Yellow, PicoColors.Green, PicoColors.Cyan, 
                PicoColors.Pink, PicoColors.Purple, PicoColors.White, PicoColors.Brown, PicoColors.DarkBlue, 
                PicoColors.DarkRed, PicoColors.DarkGreen
            );
        }

        if (starFlyTimer > 0) {
            StateMachine.State = StStarFly;
            if (_lastState != StStarFly) StarFlyBegin();
        } else {
            if (_lastState == StStarFly) StarFlyEnd();
            StateMachine.State = StNormal;
        }

        if (starFlyTransforming) {
            Speed = Calc.Approach(Speed, Vector2.Zero, 1000f * Engine.DeltaTime);
            starFlyTransforming = Speed.Length() > 1;
            goto EndSpeed;
        }

        if (StateMachine.State == StStarFly) {
            StarFlyUpdate();
            _dreamDashing = false;
            goto EndSpeed;
        }

        if (StateMachine.State == StAttract) {
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

        if (CollideCheck<Killbox>(Position)) {
            Die(Vector2.Zero);
            goto End;
        }

        bool isUnderwater = SwimCheck();

        // TODO: smoke particles
        if (onGround && !_wasOnGround) {
            AddSmoke(X, Y + 4);
        }

        var jump = Input.Jump.Pressed && !_jumpWasPressed;
        _jumpWasPressed = Input.Jump.Pressed;
        if (jump)
            _jumpBuffer = 8;
        else if (_jumpBuffer > 0)
            _jumpBuffer--;

        var dash = Input.Dash.Pressed && !_dashWasPressed;
        _dashWasPressed = Input.Dash.Pressed;

        if (onGround || isUnderwater) {
            if (_grace == 0) ExtVarsRefillJumps();
            _grace = 6;
            if (RefillDash()) {
                Sfx(54);
            }
        }
        else if (_grace > 0)
            _grace--;
    
        float accel = 0.6f;

        if (StateMachine.State is StDreamDash or StDash) {
            Speed.X = Approach(Speed.X / Pico8SpeedUnit, _dashTarget.X, _dashAccel.X) * Pico8SpeedUnit;
            Speed.Y = Approach(Speed.Y / Pico8SpeedUnit, _dashTarget.Y, _dashAccel.Y) * Pico8SpeedUnit;
        }
        
        if (_dreamDashing) {
            dashAttackTimer = 0;
            Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
            var oldPos = Position;

            if (_dashTarget.LengthSquared() < 1)
                _dashTarget = Input.Feather.value;
            if (_dashTarget.LengthSquared() < 1)
                _dashTarget = Vector2.UnitX;
            
            var block = CollideFirst<DreamBlock>(); 
            if (block == null) {
                if (DreamDashedIntoSolid()) {
                    if (SaveData.Instance.AssistMode && SaveData.Instance.Assists.Invincible) {
                        Position = oldPos;
                        Speed *= -1;
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

                    StateMachine.State = StNormal;
                    
                    Stop(dreamSfxLoop);
                    Play("event:/char/madeline/dreamblock_exit");
                }
            }
            else
            {
                if (Scene.OnInterval(0.1f))
                    CreateTrail();

                //Displacement effect
                if (level.OnInterval(0.04f))
                    level.Displacement.AddBurst(Center, .3f, 0f, 20f);
            }
        } else if (dashAttackTimer > 0) {
            dashAttackTimer--;
            StateMachine.State = StDash;
            AddSmoke(X, Y);
        } else {
            // facing
            if (Speed.X != 0)
                Facing = Speed.X < 0 ? Facings.Left : Facings.Right;

            if (isUnderwater) {
                // swim
                StateMachine.State = StSwim;
                var inputVec = Input.Feather.Value.SafeNormalize();
                
                if (inputVec.X != 0 || inputVec.Y != 0) {
                    if (inputVec.X != 0)
                        Speed.X = Approach(Speed.X / Pico8SpeedUnit, MaxRun * inputVec.X, accel) * Pico8SpeedUnit;
                    if (inputVec.Y != 0)
                        Speed.Y = Approach(Speed.Y / Pico8SpeedUnit, MaxRun * inputVec.Y, accel) * Pico8SpeedUnit;
                }
                Speed *= (float) Math.Pow(0.02, Engine.DeltaTime);
            } else {
                StateMachine.State = StNormal;
                if (!onGround)
                    accel = 0.4f;

                if (Math.Abs(Speed.X / Pico8SpeedUnit) > MaxRun)
                    Speed.X = Approach(Speed.X / Pico8SpeedUnit, Math.Sign(Speed.X) * MaxRun, Deceleration) * Pico8SpeedUnit;
                else
                    Speed.X = Approach(Speed.X / Pico8SpeedUnit, input * MaxRun, accel) * Pico8SpeedUnit;
                
                // gravity
                var maxfall = 2f;
                var gravity = 0.21f / 2;

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
                    Speed.Y = Approach(Speed.Y / Pico8SpeedUnit, maxfall, gravity) * Pico8SpeedUnit;

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

            if (Dashes > 0 && dash)
            {
                AddSmoke(X, Y);
                Dashes--;
                dashAttackTimer = 8;
                
                var dashInput = Input.GetAimVector(Facing);
                dashInput.Normalize();
                DashDir = dashInput;

                ++SaveData.Instance.TotalDashes;
                ++this.level.Session.Dashes;
                Stats.Increment(Stat.DASHES);
                foreach (DashListener component in Scene.Tracker.GetComponents<DashListener>())
                    component.OnDash?.Invoke(DashDir);

                calledDashEvents = true;

                Speed = dashInput * 5f * Pico8SpeedUnit;

                Sfx(3);
                Celeste.Freeze(0.05f);
                Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
                if (Scene is Level level)
                    level.Shake(6f / 30f);
                _dashTarget.X = 2 * Math.Sign(Speed.X);
                _dashTarget.Y = 2 * Math.Sign(Speed.Y);
                _dashAccel.X = 1.5f;
                _dashAccel.Y = 1.5f;

                if (Speed.Y < 0)
                    _dashTarget.Y *= 0.75f;
                // Manual normalization? 
                if (Speed.Y != 0)
                    _dashAccel.X *= 0.70710678118f;
                if (Speed.X != 0)
                    _dashAccel.Y *= 0.70710678118f;
            }
            else if (dash && Dashes <= 0)
            {
                Sfx(9);
                AddSmoke(X, Y);
            }
        }
        
    EndSpeed:
        if (StateMachine.State == StDreamDash)
            NaiveMove(Speed * Engine.DeltaTime);
        else {
            MoveH(Speed.X * Engine.DeltaTime, OnCollideX);
            MoveV(Speed.Y * Engine.DeltaTime, OnCollideY);
        }
        
        if (varJumpTimer > 0) {
            varJumpTimer -= Engine.DeltaTime;
            if (AutoJump || Input.Jump.Check)
                Speed.Y = Math.Min(Speed.Y, varJumpSpeed);
            else
                varJumpTimer = 0;
        }
        
    EndChecks:
        // animation
        _spriteOff += 0.125f;
        if (!onGround)
            _sprite = IsSolid(input, 0) ? 5 : 3;
        else if (Input.MoveY > 0)
            _sprite = 6;
        else if (Input.MoveY < 0)
            _sprite = 7;
        else if (Speed.X == 0 || Input.MoveX == 0)
            _sprite = 1;
        else
            _sprite = 1 + _spriteOff % 4;


        if (!AutoJump && EnforceLevelBounds)
            level.EnforceBounds(this);

        if (AutoJumpTimer > 0)
        {
            if (AutoJump)
            {
                AutoJumpTimer -= Engine.DeltaTime;
                if (AutoJumpTimer <= 0) AutoJump = false;
            }
            else AutoJumpTimer = 0;
        }
        else AutoJump = false;

        var hairAnchor = new Vector2(X + 4 - (int) Facing * 2, Y + (Input.MoveY.Value > 0 ? 4f : 3f));
        for (var idx = 0; idx < Hair.Nodes.Count; idx++) {
            var node = Hair.Nodes[idx];
            // Approach
            node.X += (float) ((hairAnchor.X - (double) node.X) / 2);
            node.Y += (float) ((hairAnchor.Y + 0.5 - node.Y) / 2);
            Hair.Nodes[idx] = node;
            hairAnchor = node;
        }
        _hairCalcPosition = Position;
        
        foreach (PlayerCollider pc in Scene.Tracker.GetComponents<PlayerCollider>())
            if (pc.Check(this) && Dead)
                break;

    End:
        _wasOnGround = onGround;
        _lastState = StateMachine.State;
    }
    
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
    

    private void OnCollideX(CollisionData data) {
        if (
            dashAttackTimer > 0 && data.Hit is { OnDashCollide: not null } &&
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
        
        if (StateMachine.state != StDreamDash && DreamDashCheck(Vector2.UnitX * Math.Sign(Speed.X))) {
            Play("event:/char/madeline/dreamblock_enter");
            Loop(dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            StateMachine.State = StDreamDash;
            dreamDashCanEndTimer = 0.1f;
            return;
        }
        
        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);
        
        Speed.X = 0;
        dashAttackTimer = 0;
    }
    
    private void OnCollideY(CollisionData data) {
        if (
            dashAttackTimer > 0 && data.Hit is { OnDashCollide: not null }
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
        
        if (!_dreamDashing && DreamDashCheck(Vector2.UnitY * Math.Sign(Speed.Y))) {
            Play("event:/char/madeline/dreamblock_enter");
            Loop(dreamSfxLoop, "event:/char/madeline/dreamblock_travel");
            _dreamDashing = true;
            dreamDashCanEndTimer = 0.1f;
            return;
        }
        
        if (data.Hit is { OnCollide: not null })
            data.Hit.OnCollide(data.Direction);
        
        Speed.Y = 0;
        dashAttackTimer = 0;
    }

    private void AddSmoke(float x, float y) => level.Add(new Smoke(new Vector2(x, y)));

    private static readonly MTexture[] PlayerTextures;
    
    // By all means, this should be done in Update().
    // However, for some reason, doing this there makes the hair one frame behind.
    private Vector2 _hairCalcPosition;

    public override void Render() {
        
        var playerTexture = PlayerTextures[(int) _sprite];
        var hairTexture = PlayerTextures[(int) _sprite + 8];
        var flip = SpriteEffects.None;
        if (Facing == Facings.Left) flip |= SpriteEffects.FlipHorizontally;
        
        int i = 0;
        foreach (var node in Hair.Nodes) {
            var dashColor = Hair.GetHairColor(i);
            PicoCircle(new Vector2(node.X, node.Y), i < 2 ? 2 : 1, StateMachine.State == StStarFly ? PicoColors.Yellow : dashColor);
            i++;
        }

        if (StateMachine.State == StStarFly) 
            PicoCircle(_hairCalcPosition + Vector2.One * 5, 3, PicoColors.Yellow);
        else {
            playerTexture.Draw(_hairCalcPosition, Vector2.Zero, Color.White, 1f, 0f, flip);
            hairTexture.Draw(_hairCalcPosition, Vector2.Zero, Hair.Color, 1f, 0f, flip);
        }
    }
}