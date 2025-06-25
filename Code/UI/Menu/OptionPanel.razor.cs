using Sandbox.UI;

public partial class OptionPanel : Panel
{
    public Dropdown Source { get; set; } = null;
    public OptionPanel()
    {
        this.AcceptsFocus = true;
    }
}