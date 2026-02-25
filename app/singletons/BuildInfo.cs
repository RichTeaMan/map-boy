using Godot;

public partial class BuildInfo : CanvasLayer
{

    private string commit_hash = "dev";
    private string build_time = "dev";

    private RichTextLabel buildLabel => GetNode<RichTextLabel>("%build_label");

    private RichTextLabel fpsLabel => GetNode<RichTextLabel>("%fps_label");

    public override void _Ready()
    {
        buildLabel.Text = $"Build hash: {commit_hash} | Build date: {build_time}";
    }

    public override void _Process(double _delta)
    {
        fpsLabel.Text = $"FPS: {(int)Engine.GetFramesPerSecond()}";
    }
}
