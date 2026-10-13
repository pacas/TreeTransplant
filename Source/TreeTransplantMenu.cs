using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;

namespace TreeTransplant;

internal sealed class TreeTransplantMenu : IClickableMenu
{
    private const int TileSize = 64;
    private const float FadeSpeed = 0.02f;

    private readonly Farm farm;
    private readonly Rectangle greenSquare = new(194, 388, 16, 16);
    private readonly Rectangle redSquare = new(210, 388, 16, 16);
    private readonly bool[] validSpot = new bool[9];
    private readonly string defaultText;
    private readonly string placementText;

    private GameLocation? savedLocation;
    private xTile.Dimensions.Location savedViewportLocation;
    private bool isOnFarm;

    private HeldTree? held;
    private bool canPlace = true;

    private ClickableTextureComponent cancelButton = null!;
    private ClickableTextureComponent flipButton = null!;

    public TreeTransplantMenu()
    {
        this.farm = Game1.getFarm();
        this.defaultText = ModEntry.Instance.Helper.Translation.Get("Menu_DefaultText");
        this.placementText = ModEntry.Instance.Helper.Translation.Get("Menu_PlacementText");

        Game1.playSound("dwop");
        Game1.globalFadeToBlack(this.OpenOnFarm, FadeSpeed);
        Game1.player.forceCanMove();

        this.ResetBounds();
    }

    private void OpenOnFarm()
    {
        Game1.currentLocation.cleanupBeforePlayerExit();
        this.savedLocation = Game1.currentLocation;
        this.savedViewportLocation = Game1.viewport.Location;

        Game1.currentLocation = this.farm;
        Game1.player.viewingLocation.Value = this.farm.NameOrUniqueName;
        Game1.currentLocation.resetForPlayerEntry();

        Game1.displayHUD = false;
        Game1.viewportFreeze = true;
        Game1.viewport.Location = this.GetInitialViewportLocation();
        Game1.clampViewportToGameMap();
        Game1.panScreen(0, 0);
        Game1.displayFarmer = false;
        this.isOnFarm = true;

        Game1.globalFadeToClear(null, FadeSpeed);
    }

    private void RestoreLocation()
    {
        if (!this.isOnFarm)
            return;
        this.isOnFarm = false;

        Game1.currentLocation.cleanupBeforePlayerExit();
        if (this.savedLocation != null)
            Game1.currentLocation = this.savedLocation;
        Game1.player.viewingLocation.Value = null;
        Game1.currentLocation.resetForPlayerEntry();

        Game1.displayHUD = true;
        Game1.viewportFreeze = false;
        Game1.viewport.Location = this.savedViewportLocation;
        Game1.displayFarmer = true;
    }

    private void CloseMenu()
    {
        if (!this.readyToClose())
            return;

        this.held = null;
        this.RestoreLocation();
        Game1.globalFadeToClear(null, FadeSpeed);
        this.exitThisMenu(true);
    }

    public override bool readyToClose()
    {
        return base.readyToClose() && this.held == null;
    }

    public override void emergencyShutDown()
    {
        base.emergencyShutDown();
        this.held = null;
        this.RestoreLocation();
    }

    protected override void cleanupBeforeExit()
    {
        base.cleanupBeforeExit();
        this.RestoreLocation();
    }

    public override void receiveKeyPress(Keys key)
    {
        if (Game1.IsFading())
            return;

        if (Game1.options.doesInputListContain(Game1.options.menuButton, key))
            this.HandleCancelAction();

        if (!Game1.options.SnappyMenus)
        {
            if (Game1.options.doesInputListContain(Game1.options.moveDownButton, key))
                Game1.panScreen(0, 4);
            if (Game1.options.doesInputListContain(Game1.options.moveUpButton, key))
                Game1.panScreen(0, -4);
            if (Game1.options.doesInputListContain(Game1.options.moveRightButton, key))
                Game1.panScreen(4, 0);
            if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, key))
                Game1.panScreen(-4, 0);
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);
        if (Game1.globalFade)
            return;

        if (this.cancelButton.containsPoint(x, y))
        {
            this.HandleCancelAction();
            return;
        }

        if (this.flipButton.containsPoint(x, y))
        {
            this.FlipTree();
            return;
        }

        Vector2 tile = this.GetTileUnderMouse();

        if (this.held is null)
        {
            if (this.farm.terrainFeatures.TryGetValue(tile, out TerrainFeature? feature) && HeldTree.IsTransplantable(feature))
            {
                if (HeldTree.IsTapped(feature))
                {
                    Notify(ModEntry.Instance.Helper.Translation.Get("Menu_InvalidCap"));
                    Game1.playSound("cancel");
                }
                else if (HeldTree.TryCreate(feature, tile, this.farm, out HeldTree? picked))
                {
                    this.held = picked;
                    Game1.playSound("hoeHit");
                }
            }
            return;
        }

        if (!this.canPlace)
        {
            Notify(ModEntry.Instance.Helper.Translation.Get("Menu_InvalidPlace"));
            Game1.playSound("cancel");
            return;
        }

        if (tile == this.held.SourceTile)
        {
            Game1.playSound(this.held.SourceIsFlipped == this.held.Flipped ? "shwip" : "dirtyHit");
            this.held.ApplyFlip();
            this.held = null;
            return;
        }

        this.PlaceHeldTree(tile);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        this.FlipTree();
    }

    public override void performHoverAction(int x, int y)
    {
        this.cancelButton.tryHover(x, y, 0.1f);
        this.flipButton.tryHover(x, y, 0.1f);
        base.performHoverAction(x, y);
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        this.ResetBounds();
    }

    private void HandleCancelAction()
    {
        if (this.readyToClose())
        {
            Game1.globalFadeToBlack(this.CloseMenu, FadeSpeed);
        }
        else if (this.held != null)
        {
            this.held = null;
            Game1.playSound("shwip");
        }
    }

    private void FlipTree()
    {
        if (this.held != null)
            this.held.Flipped = !this.held.Flipped;
    }

    private void PlaceHeldTree(Vector2 tile)
    {
        if (this.held is null)
            return;

        TerrainFeature tree = this.held.Source;
        this.held.ApplyFlip();

        this.farm.terrainFeatures.Remove(this.held.SourceTile);
        if (this.farm.terrainFeatures.ContainsKey(tile))
            this.farm.terrainFeatures.Remove(tile); // grass
        this.farm.terrainFeatures.Add(tile, tree);

        Game1.playSound("dirtyHit");
        this.held = null;
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (Game1.globalFade)
            return;

        int mouseX = Game1.getOldMouseX(false) + Game1.viewport.X;
        int mouseY = Game1.getOldMouseY(false) + Game1.viewport.Y;

        if (mouseX - Game1.viewport.X < 64)
            Game1.panScreen(-8, 0);
        else if (mouseX - (Game1.viewport.X + Game1.viewport.Width) >= -128)
            Game1.panScreen(8, 0);

        if (mouseY - Game1.viewport.Y < 64)
            Game1.panScreen(0, -8);
        else if (mouseY - (Game1.viewport.Y + Game1.viewport.Height) >= -64)
            Game1.panScreen(0, 8);

        this.CalculateValidPlacement();
    }

    public override void draw(SpriteBatch b)
    {
        base.draw(b);
        if (Game1.globalFade)
            return;

        Vector2 tile = this.GetTileUnderMouse();

        Game1.StartWorldDrawInUI(b);
        if (this.held != null)
        {
            this.held.Draw(b, tile);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    Vector2 squareTile = new(tile.X + col - 1, tile.Y + row - 1);
                    b.Draw(
                        Game1.mouseCursors,
                        Game1.GlobalToLocal(Game1.viewport, squareTile * TileSize),
                        this.validSpot[col + row * 3] ? this.greenSquare : this.redSquare,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        4f,
                        SpriteEffects.None,
                        0.999f
                    );
                }
            }
        }
        Game1.EndWorldDrawInUI(b);

        this.cancelButton.draw(b);
        this.flipButton.draw(b);

        string text = this.held is null ? this.defaultText : this.placementText;
        SpriteText.drawStringWithScrollBackground(b, text, Game1.uiViewport.Width / 2 - SpriteText.getWidthOfString(text, 999999) / 2, 16);

        this.drawMouse(b);
    }

    private void CalculateValidPlacement()
    {
        if (this.held is null)
            return;

        this.canPlace = PlacementRules.Evaluate(this.farm, this.held, this.GetTileUnderMouse(), this.validSpot);
    }

    private Vector2 GetTileUnderMouse()
    {
        return new Vector2(
            (Game1.viewport.X + Game1.getOldMouseX(false)) / TileSize,
            (Game1.viewport.Y + Game1.getOldMouseY(false)) / TileSize
        );
    }

    private xTile.Dimensions.Location GetInitialViewportLocation()
    {
        Building? house = this.farm.getBuildingByName("FarmHouse") ?? this.farm.buildings.FirstOrDefault();

        int centerTileX;
        int centerTileY;
        if (house != null)
        {
            centerTileX = house.tileX.Value + house.tilesWide.Value / 2;
            centerTileY = house.tileY.Value + house.tilesHigh.Value / 2;
        }
        else
        {
            xTile.Layers.Layer layer = this.farm.Map.Layers[0];
            centerTileX = layer.LayerWidth / 2;
            centerTileY = layer.LayerHeight / 2;
        }

        return new xTile.Dimensions.Location(
            centerTileX * TileSize - Game1.viewport.Width / 2,
            centerTileY * TileSize - Game1.viewport.Height / 2
        );
    }

    private void ResetBounds()
    {
        int right = this.xPositionOnScreen + Game1.uiViewport.Width - borderWidth - spaceToClearSideBorder;
        int bottom = this.yPositionOnScreen + Game1.uiViewport.Height - 128;

        this.cancelButton = new ClickableTextureComponent(
            new Rectangle(right - 64, bottom, 64, 64),
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47, -1, -1),
            1f
        );
        this.flipButton = new ClickableTextureComponent(
            new Rectangle(right - 144, bottom, 64, 64),
            ModEntry.Instance.FlipTexture,
            new Rectangle(0, 0, 64, 64),
            1f
        );
    }

    private static void Notify(string message)
    {
        Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type) { timeLeft = 3500f });
    }
}
