using Godot;

public partial class BuildInfo : CanvasLayer
{

    private string commitHash = "dev";
    private string buildTime = "dev";

    private RichTextLabel BuildLabel => GetNode<RichTextLabel>("%build_label");

    private RichTextLabel FpsLabel => GetNode<RichTextLabel>("%fps_label");

    public override void _Ready()
    {
        BuildLabel.Text = $"Build hash: {commitHash} | Build date: {buildTime}";
    }

    public override void _Process(double _delta)
    {
        FpsLabel.Text = $"FPS: {(int)Engine.GetFramesPerSecond()}";
    }
}
