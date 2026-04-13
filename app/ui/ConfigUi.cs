using Godot;
using System;

public partial class ConfigUi : Control
{
    private Tree tableNode => GetNode<Tree>("table");

    public override void _Ready()
    {
        base._Ready();

        var config = Config.Fetch();
        foreach (var c in tableNode.GetChildren())
        {
            c.QueueFree();
        }

        var row = tableNode.CreateItem();
        
        row.AddChild(new Label {Text = "f"});
    }
}
