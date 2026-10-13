using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace TreeTransplant;

internal sealed class HeldTree
{
    private const float GhostAlpha = 0.75f;

    private readonly TerrainFeature ghost;

    public TerrainFeature Source { get; }

    public Vector2 SourceTile { get; }

    public bool IsWildTree => this.Source is Tree;

    public bool IsMature => IsMatureTree(this.Source);

    public bool Flipped
    {
        get => GetFlippedField(this.ghost).Value;
        set => GetFlippedField(this.ghost).Value = value;
    }

    public bool SourceIsFlipped => GetFlippedField(this.Source).Value;

    private HeldTree(TerrainFeature source, Vector2 sourceTile, TerrainFeature ghost)
    {
        this.Source = source;
        this.SourceTile = sourceTile;
        this.ghost = ghost;
    }

    public static bool IsTransplantable([NotNullWhen(true)] TerrainFeature? feature)
    {
        return feature is Tree or FruitTree;
    }

    public static bool IsTapped(TerrainFeature? feature)
    {
        return feature is Tree tree && tree.tapped.Value;
    }

    public static bool IsMatureTree(TerrainFeature? feature)
    {
        return feature switch
        {
            Tree tree => tree.growthStage.Value >= Tree.treeStage,
            FruitTree fruitTree => fruitTree.growthStage.Value >= FruitTree.treeStage,
            _ => false
        };
    }

    public static bool TryCreate(TerrainFeature feature, Vector2 tile, GameLocation location, [NotNullWhen(true)] out HeldTree? held)
    {
        TerrainFeature? ghost = feature switch
        {
            Tree tree => CreateGhost(tree),
            FruitTree fruitTree => CreateGhost(fruitTree),
            _ => null
        };

        if (ghost is null)
        {
            held = null;
            return false;
        }

        ghost.Location = location;
        ghost.Tile = tile;
        held = new HeldTree(feature, tile, ghost);
        return true;
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 tile)
    {
        this.ghost.Tile = tile;
        this.ghost.draw(spriteBatch);
    }

    public void ApplyFlip()
    {
        GetFlippedField(this.Source).Value = this.Flipped;
    }

    private static Tree CreateGhost(Tree source)
    {
        var ghost = new Tree(source.treeType.Value, source.growthStage.Value, source.isTemporaryGreenRainTree.Value);
        ghost.stump.Value = source.stump.Value;
        ghost.flipped.Value = source.flipped.Value;
        ghost.alpha = GhostAlpha;
        return ghost;
    }

    private static FruitTree CreateGhost(FruitTree source)
    {
        var ghost = new FruitTree(source.treeId.Value, source.growthStage.Value);
        ghost.daysUntilMature.Value = source.daysUntilMature.Value;
        ghost.stump.Value = source.stump.Value;
        ghost.flipped.Value = source.flipped.Value;
        ghost.alpha = GhostAlpha;
        return ghost;
    }

    private static NetBool GetFlippedField(TerrainFeature feature)
    {
        return feature switch
        {
            Tree tree => tree.flipped,
            FruitTree fruitTree => fruitTree.flipped,
            _ => throw new InvalidCastException($"Terrain feature '{feature.GetType().FullName}' is not a Tree or FruitTree.")
        };
    }
}
