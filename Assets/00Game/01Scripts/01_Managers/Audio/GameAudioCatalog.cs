public static class GameAudioCatalog
{
    public static class Ui
    {
        public const string Move = "Audio/SFX/UI/UI_MOVE_DARK";
        public const string Confirm = "Audio/SFX/UI/UI_CONFIRM_DARK";
        public const string Back = "Audio/SFX/UI/UI_BACK_DARK";
        public const string PauseOpen = "Audio/SFX/UI/UI_PAUSE_OPEN";
        public const string PauseClose = "Audio/SFX/UI/UI_PAUSE_CLOSE";
        public const string PromptAppear = "Audio/SFX/UI/UI_PROMPT_APPEAR";
        public const string DialogueAdvance = "Audio/SFX/UI/UI_DIALOGUE_ADVANCE";
        public const string ItemPickupOpen = "Audio/SFX/UI/UI_ITEMPICKUP_OPEN";
        public const string AreaTitleReveal = "Audio/SFX/UI/UI_AREATITLE_REVEAL";
        public const string YouDiedStinger = "Audio/SFX/UI/UI_YOUDIED_STINGER";
        public const string CheckpointRetry = "Audio/SFX/UI/UI_CHECKPOINT_RETRY";
    }

    public static class Player
    {
        public static readonly string[] AttackSwingPaths =
        {
            "Audio/SFX/Player/Rapier_Thrust_01",
            "Audio/SFX/Player/Rapier_Thrust_02"
        };

        public static readonly string[] BossHitPaths =
        {
            "Audio/SFX/Player/Boss_Hit_01",
            "Audio/SFX/Player/Boss_Hit_02"
        };

        public static readonly string[] IncomingHitPaths =
        {
            "Audio/SFX/Common/Player_Hit_01",
            "Audio/SFX/Common/Player_Hit_02"
        };

        public const string GuardSuccess = "Audio/SFX/Player/Guard_Success_01";
    }

    public static class Boss
    {
        public static readonly string[] SwingPaths =
        {
            "Audio/SFX/Boss/Orlea_Swing_01",
            "Audio/SFX/Boss/Orlea_Swing_02",
            "Audio/SFX/Boss/Orlea_Swing_03",
            "Audio/SFX/Boss/Orlea_Thrust_01"
        };

        public static readonly string[] GroundImpactPaths =
        {
            "Audio/SFX/Boss/Orlea_Impact_01",
            "Audio/SFX/Boss/Orlea_Impact_02"
        };

        public static readonly string[] IncomingHitPaths =
        {
            "Audio/SFX/Player/Boss_Hit_01",
            "Audio/SFX/Player/Boss_Hit_02"
        };

        public static readonly string[] FootstepPaths =
        {
            "Audio/SFX/Boss/Orlea_Footstep_01",
            "Audio/SFX/Boss/Orlea_Footstep_02"
        };

        public static readonly string[] ParriedPaths =
        {
            "Audio/SFX/Boss/Orlea_Parried_01",
            "Audio/SFX/Boss/Orlea_Parried_02",
            "Audio/SFX/Boss/Orlea_Parried_03"
        };
    }

    public static class World
    {
        public const string ChurchBell = "Audio/SFX/Bell/Church_Bell";
    }

    public static class Ambient
    {
        public const string CastleWindLoop = "Audio/Ambient/Castle/Castle_Wind_Loop";
        public const string BonfireLoop = "Audio/Ambient/Castle/Bonfire_Loop";
        public const string FogWallLoop = "Audio/Ambient/Castle/FogWall_Loop";
    }
}
