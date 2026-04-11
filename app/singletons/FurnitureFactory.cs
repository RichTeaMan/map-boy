using System;
using Godot;
using MapBoy.Models;

public class FurnitureFactory
{
    public Node3D CreateFurniture(Furniture furniture, Global global)
    {
        using PackedScene scene = FetchScene(furniture.FurnitureType);

        Node3D node = scene.Instantiate<Node3D>();
        node.Name = $"furniture_{furniture.Id}_[{furniture.Uid}]";
        node.Position = global.LatLonToVector3(furniture.Lat, 0.0, furniture.Lon);
        return node;
    }

    private PackedScene FetchScene(FurnitureType furnitureType)
    {
        switch (furnitureType)
        {
            case FurnitureType.Bench:
                return ResourceLoader.Load<PackedScene>("res://assets/furniture/bench.tscn");
            case FurnitureType.Bin:
                return null; // TODO, bin asset
            case FurnitureType.StreetLamp:
                return ResourceLoader.Load<PackedScene>("res://assets/furniture/streetlamp.tscn");
            case FurnitureType.TrafficLight:
                return ResourceLoader.Load<PackedScene>("res://assets/furniture/trafficlight_A.tscn");
            case FurnitureType.Tree:
                return ResourceLoader.Load<PackedScene>("res://assets/furniture/tree_A.tscn");
            default:
                throw new Exception($"Unknown furniture type {furnitureType}");
        }
    }

}