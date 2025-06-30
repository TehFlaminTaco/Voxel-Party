using System;
using Sandbox.UI;

public partial class TelephoneHud : PanelComponent
{
    // a HUD used for Speed Build specific stuff
    GameTimer timer;
    Panel message;

    public static TelephoneHud Instance { get; private set; }
    public TelephoneHud()
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

    [Sync( SyncFlags.FromHost )]
    public int? KillPercentage { get; set; } = null;

    TextEntry textBox;

    [Rpc.Broadcast]
    public void RequestTextBox()
    {
        textBox.Value = "";
        textBox.Focus();
    }

    protected override void OnFixedUpdate()
    {
        if ( VoxelPlayer.LocalPlayer != null )
            VoxelPlayer.LocalPlayer.TextBoxValue = textBox.Value ?? "";
    }

    protected override int BuildHash() => HashCode.Combine(
        HashCode.Combine(
            Message.GetHashCode(),
            TimerEnd.Absolute.GetHashCode(),
            TotalTime.GetHashCode(),
            HasTimer.GetHashCode(),
            HasReadyCheck.GetHashCode() ),
        VoxelPlayer.LocalPlayer?.TotalBlockArea.GetHashCode(),
        VoxelPlayer.LocalPlayer?.CorrectBlocksPlaced.GetHashCode(),
        VoxelPlayer.LocalPlayer?.IncorrectBlocksPlaced.GetHashCode(),
        VoxelPlayer.LocalPlayer?.IsReady.GetHashCode(),
        VoxelPlayer.LocalPlayer?.SpecialMessage.GetHashCode() );

    protected override void OnTreeBuilt()
    {
        // Update goodbar and badbar widths based on player stats
        var player = VoxelPlayer.LocalPlayer;
        var goodBar = Panel.FindChild( "#score", false )?.FindChild( "goodbar" );
        var badBar = Panel.FindChild( "#score", false )?.FindChild( "badbar" );
        var killBar = Panel.FindChild( "failPointer", false );

        if ( goodBar == null || badBar == null || killBar == null ) return;

        if ( KillPercentage.HasValue )
            killBar.Style.Width = Length.Fraction( KillPercentage.Value / 100f );

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
