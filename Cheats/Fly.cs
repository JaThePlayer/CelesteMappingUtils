namespace Celeste.Mod.MappingUtils.Cheats;

internal class Fly
{
    private static bool HooksApplied = false;

    public static float SpeedMult = 2f;

    private static bool _Enabled = false;
    public static bool Enabled {
        get => _Enabled;
        set
        {
            _Enabled = value;
            if (value)
                LoadHooksIfNeeded();

            if (!Enabled && (Engine.Scene is Level level))
            {
                var player = level.Tracker.GetEntity<Player>();
                if (player != null)
                {
                    // disable noclip
                    player.Collidable = true;
                    player.StateMachine.Locked = false;
                    player.StateMachine.State = Player.StNormal;
                    player.ForceCameraUpdate = false;
                }
            }
        }
    }

    public static void LoadHooksIfNeeded()
    {
        if (HooksApplied)
            return;

        HooksApplied = true;

        On.Celeste.Player.Update += Player_Update;
        On.Celeste.PlayerCollider.Check += PlayerCollider_Check;

        MappingUtilsModule.OnUnload += Unload;
    }

    private static void Unload()
    {
        if (!HooksApplied)
            return;
        HooksApplied = false;

        On.Celeste.Player.Update -= Player_Update;
        On.Celeste.PlayerCollider.Check -= PlayerCollider_Check;
    }

    private static bool PlayerCollider_Check(On.Celeste.PlayerCollider.orig_Check orig, PlayerCollider self, Player player)
    {
        if (Enabled)
            return false;

        return orig(self, player);
    }

    public static void EnableNoClip(Player self)
    {
        self.Collider = new Hitbox(0, 0);
        self.Collidable = false;
        self.StateMachine.State = Player.StDummy;
        self.StateMachine.Locked = true;
        self.DummyGravity = false;
        self.DummyAutoAnimate = false;
        self.DummyMoving = false;
        self.ForceCameraUpdate = true;
        self.Speed = Vector2.Zero;

        //self.Position += ().Floor();
        self.NaiveMove(Input.GetAimVector(0) * SpeedMult);

        self.SceneAs<Level>().EnforceBounds(self);
    }

    /// <summary>
    /// Handles Flying
    /// </summary>
    private static void Player_Update(On.Celeste.Player.orig_Update orig, Player self)
    {
        if (Enabled)
        {
            EnableNoClip(self);
        }

        orig(self);

        if (Enabled)
        {
            EnableNoClip(self);
        }
    }
}
