using System;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace TreeTransplant;

internal static class PlacementRules
{
    private static readonly Vector2[] AdjacentOffsets =
    {
        new(-1, 0), new(1, 0), new(0, 1), new(0, -1)
    };

    private static readonly Vector2[] SurroundingOffsets =
    {
        new(-1, 0), new(1, 0), new(0, 1), new(0, -1),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)
    };

    public static bool Evaluate(GameLocation location, HeldTree held, Vector2 tile, bool[] spots)
    {
        Array.Fill(spots, true);
        bool canPlace = true;

        if (!IsTileOpen(location, held, tile))
        {
            canPlace = false;
            spots[4] = false;
        }

        bool onlyAvoidTrees = held.IsWildTree || held.IsMature;
        foreach (Vector2 offset in held.IsWildTree ? AdjacentOffsets : SurroundingOffsets)
        {
            Vector2 neighbour = tile + offset;
            bool isValid = onlyAvoidTrees
                ? !IsBlockedByTree(location, held, neighbour)
                : !IsBlockingSaplingGrowth(location, held, neighbour);

            spots[(int)(offset.X + 1) + (int)(offset.Y + 1) * 3] = isValid;
            if (!isValid)
                canPlace = false;
        }

        return canPlace;
    }

    private static bool IsTileOpen(GameLocation location, HeldTree held, Vector2 tile)
    {
        if (tile == held.SourceTile)
            return true;

        int x = (int)tile.X;
        int y = (int)tile.Y;

        if (!location.isTileOnMap(tile) || location.isOpenWater(x, y) || !location.isTilePassable(tile))
            return false;

        if (location.doesTileHaveProperty(x, y, "NoSpawn", "Back", false) == "All")
            return false;

        // grass is replaced when the tree is placed
        if (location.terrainFeatures.TryGetValue(tile, out TerrainFeature? feature) && feature is not Grass)
            return false;

        return location.CanItemBePlacedHere(tile);
    }

    private static bool IsBlockedByTree(GameLocation location, HeldTree held, Vector2 tile)
    {
        if (tile == held.SourceTile)
            return false;

        if (!location.terrainFeatures.TryGetValue(tile, out TerrainFeature? feature))
            return false;

        return feature is FruitTree || (feature is Tree && HeldTree.IsMatureTree(feature));
    }

    private static bool IsBlockingSaplingGrowth(GameLocation location, HeldTree held, Vector2 tile)
    {
        if (tile == held.SourceTile)
            return false;

        if (location.IsTileOccupiedBy(tile, CollisionMask.Objects, CollisionMask.None, false))
        {
            string? itemId = location.objects.TryGetValue(tile, out StardewValley.Object? obj) ? obj.QualifiedItemId : null;
            if (itemId != "(O)590" && itemId != "(O)SeedSpot") // artifact spots and seed spots don't block growth
                return true;
        }

        if (location.IsTileOccupiedBy(tile, CollisionMask.TerrainFeatures, CollisionMask.None, false))
        {
            location.terrainFeatures.TryGetValue(tile, out TerrainFeature? feature);
            if (feature is HoeDirt dirt)
            {
                if (dirt.crop != null)
                    return true;
            }
            else if (feature is not Grass)
                return true;
        }

        return location.IsTileOccupiedBy(tile, CollisionMask.Buildings | CollisionMask.Furniture | CollisionMask.Flooring | CollisionMask.LocationSpecific, CollisionMask.None, false);
    }
}
