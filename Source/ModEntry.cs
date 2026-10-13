using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace TreeTransplant;

public class ModEntry : Mod
{
    private const string CarpenterQuestionKey = "carpenter";
    private const string TransplantResponseKey = "TreeTransplant";

    internal static ModEntry Instance { get; private set; } = null!;

    internal Texture2D FlipTexture { get; private set; } = null!;


    public override void Entry(IModHelper helper)
    {
        Instance = this;

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.Display.MenuChanged += this.OnMenuChanged;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.FlipTexture = this.Helper.ModContent.Load<Texture2D>("assets/flip.png");
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;

        if (e.NewMenu is DialogueBox dialogue && Game1.currentLocation?.lastQuestionKey == CarpenterQuestionKey && IsCarpenterDialogue(dialogue))
            this.AddTransplantOption(Game1.currentLocation, dialogue);
    }

    private static bool IsCarpenterDialogue(DialogueBox dialogue)
    {
        return
            dialogue.isQuestion
            && dialogue.characterDialogue is null
            && dialogue.responses.Any(response => response.responseKey is "Shop" or "Construct");
    }

    private void AddTransplantOption(GameLocation location, DialogueBox dialogue)
    {
        if (dialogue.responses.Any(response => response.responseKey == TransplantResponseKey))
            return;

        var responses = dialogue.responses.ToList();
        int insertAt = responses.FindIndex(response => response.responseKey == "Construct");
        if (insertAt < 0)
            insertAt = responses.FindIndex(response => response.responseKey == "Leave");
        if (insertAt < 0)
            insertAt = responses.Count;
        responses.Insert(insertAt, new Response(TransplantResponseKey, this.Helper.Translation.Get("Carpenter_Option")));

        string question = dialogue.dialogues.FirstOrDefault() ?? string.Empty;
        location.createQuestionDialogue(question, responses.ToArray(), this.OnCarpenterAnswer, null);
    }

    private void OnCarpenterAnswer(Farmer who, string answerKey)
    {
        if (answerKey == TransplantResponseKey)
        {
            Game1.activeClickableMenu = new TreeTransplantMenu();
            return;
        }

        GameLocation location = Game1.currentLocation;
        location.lastQuestionKey = CarpenterQuestionKey;
        location.answerDialogueAction($"{CarpenterQuestionKey}_{answerKey}", new[] { CarpenterQuestionKey });
    }
}
