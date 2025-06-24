using System;
using Sandbox.UI;

public partial class SpeedBuildHud : PanelComponent
{
    // a HUD used for Speed Build specific stuff
    GameTimer timer;
    Panel message;

    public static SpeedBuildHud Instance { get; private set; }
    public SpeedBuildHud()
    {
        Instance = this;
    }

    [Sync( SyncFlags.FromHost )]
    public TimeUntil TimerEnd { get; set; } = 0f; // Default to 60 seconds

    [Sync( SyncFlags.FromHost )]
    public float TotalTime { get; set; } = 0f; // Default to 60 seconds
    [Sync( SyncFlags.FromHost )]
    public bool HasTimer { get; set; } = false; // Default to true, indicating that the timer is active
    [Sync( SyncFlags.FromHost )]
    public bool HasReadyCheck { get; set; } = true; // Default to true, indicating that the ready check is active

    [Sync( SyncFlags.FromHost )] public string Message { get; set; } = "Memorize the structure!";

    protected override int BuildHash() => HashCode.Combine(
        HashCode.Combine(
            Message.GetHashCode(),
            TimerEnd.Absolute.GetHashCode(),
            TotalTime.GetHashCode(),
            HasTimer.GetHashCode(),
            HasReadyCheck.GetHashCode() ),
        VoxelPlayer.LocalPlayer.TotalBlockArea.GetHashCode(),
        VoxelPlayer.LocalPlayer.CorrectBlocksPlaced.GetHashCode(),
        VoxelPlayer.LocalPlayer.IncorrectBlocksPlaced.GetHashCode(),
        VoxelPlayer.LocalPlayer.IsReady.GetHashCode() );

    protected override void OnTreeBuilt()
    {
        // Update goodbar and badbar widths based on player stats
        var player = VoxelPlayer.LocalPlayer;
        var goodBar = Panel.Children.First( c => c.Id == "score" ).Children.First( c => c.HasClass( "goodbar" ) );
        var badBar = Panel.Children.First( c => c.Id == "score" ).Children.First( c => c.HasClass( "badbar" ) );

        if ( player.TotalBlockArea > 0 )
        {
            var correctPercentage = (float)player.CorrectBlocksPlaced / player.TotalBlockArea;
            var incorrectPercentage = (float)player.IncorrectBlocksPlaced / player.TotalBlockArea;

            goodBar.Style.Width = Length.Percent( correctPercentage * 100 );
            badBar.Style.Width = Length.Percent( incorrectPercentage * 100 );
        }
        else
        {
            goodBar.Style.Width = Length.Percent( 0 );
            badBar.Style.Width = Length.Percent( 0 );
        }
    }
}
