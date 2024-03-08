using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DeathRoll.Bahamood.TextureHandler;
using DeathRoll.Windows;

namespace DeathRoll.Bahamood;

public record RenderObject(float Depth, Texture[] Texture, Vector2 Size, (int X, int Y) Position, Vector2 UVTop, Vector2 UVBottom)
{
    public RenderObject(float depth, Texture[] texture, Vector2 size, (int X, int Y) position) : this(depth, texture, size, position, Vector2.Zero, Vector2.One)
    {

    }
}

public class Renderer
{
    private readonly Bahamood Game;

    public readonly Dictionary<int, TextureCollection> LoadedTextures;

    public float SkyOffset;

    private bool CreditsReset;
    private float CreditsThrottler;
    private readonly string CreditsText;
    private const float CreditFps = 60.0f;

    public Renderer(Bahamood game)
    {
        Game = game;

        LoadedTextures = LoadWallTexture();
        CreditsText = string.Format(Settings.CreditsTextTemplate, Bahamood.Version);
    }

    public void Draw()
    {
        var drawlist = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();

        DrawBackground(drawlist, p);
        RenderObject(drawlist, p);
        DrawPlayerHealth(drawlist, p);

        Game.Player.CurrentWeapon.Draw(drawlist, p);

        if (Game.Player.ReceivedDamage)
            DrawPlayerDamage(drawlist, p);

        if (Game.Player.CloseToDoor)
            RenderUIText(p);
    }

    private void DrawBackground(ImDrawListPtr drawlist, Vector2 p)
    {
        SkyOffset = Utils.Mod(SkyOffset + 1.5f * Game.Player!.Rel, Settings.Width);
        var drawX = p.X + -SkyOffset;
        var drawY = p.Y;
        drawlist.AddImage(TextureManager.SkyTexture.Tex.ImGuiHandle, new Vector2(drawX, drawY), new Vector2(drawX + TextureManager.SkyTexture.Width, drawY + TextureManager.SkyTexture.Height));

        drawX = p.X + -SkyOffset + Settings.Width;
        drawY = p.Y;
        drawlist.AddImage(TextureManager.SkyTexture.Tex.ImGuiHandle, new Vector2(drawX, drawY), new Vector2(drawX + TextureManager.SkyTexture.Width, drawY + TextureManager.SkyTexture.Height));

        drawX = p.X + 0.0f;
        drawY = p.Y + Settings.HalfHeight;
        drawlist.AddRectFilled(new Vector2(drawX, drawY), new Vector2(drawX + Settings.Width, drawY + Settings.Height), Settings.FloorColor);
    }

    private void RenderObject(ImDrawListPtr drawlist, Vector2 p)
    {
        foreach (var (_, textures, size, pos, uvMin, uvMax) in Game.Raycasting.ObjectsToRender.OrderByDescending(o => o.Depth))
        {
            var drawX = p.X + pos.X;
            var drawY = p.Y + pos.Y;
            foreach (var texture in textures)
            {
                if (texture.SimpleTexture)
                {
                    drawlist.AddImage(texture.Tex.ImGuiHandle, new Vector2(drawX, drawY), new Vector2(drawX + size.X, drawY + size.Y), uvMin, uvMax);
                }
                else
                {
                    var middle = (512.0f - texture.Width) / 2;
                    var correctU = middle / 512.0f;
                    var min = 0.0f + correctU;
                    var max = 1.0f - correctU;

                    if (uvMin.X < min)
                        continue;

                    if (uvMax.X > max)
                        continue;

                    var uMin = (uvMin.X - correctU) * Settings.Scale;
                    var uMax = uMin + (uvMax.X - uvMin.X);

                    drawlist.AddImage(texture.Tex.ImGuiHandle, new Vector2(drawX, drawY), new Vector2(drawX + size.X, drawY + size.Y), new Vector2(uMin, 0), new Vector2(uMax, 1));
                }
            }
        }
    }

    private void DrawPlayerDamage(ImDrawListPtr drawlist, Vector2 p)
    {
        drawlist.AddImage(TextureManager.BloodScreen.Tex.ImGuiHandle, p, new Vector2(p.X + Settings.Width, p.Y + Settings.Height));
    }

    private void DrawPlayerHealth(ImDrawListPtr drawlist, Vector2 p)
    {
        var spacing = 40.0f * ImGuiHelpers.GlobalScale;
        var pos = new Vector2(p.X + spacing, p.Y + spacing);

        ImGui.PushFont(Game.Plugin.FontManager.AmazDooMLeft);
        foreach (var digit in Game.Player.Health.ToString().Select(d => d - '0'))
        {
            var digitSize = ImGui.CalcTextSize(digit.ToString()).X;
            drawlist.AddText(pos, Helper.NumberRed, digit.ToString());
            pos.X += digitSize;
        }

        drawlist.AddText(pos, Helper.NumberRed, "HP");
        ImGui.PopFont();
    }

    public void DrawGameOver()
    {
        var drawlist = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();

        var fullSize = new Vector2(p.X + Settings.Width, p.Y + Settings.Height);
        drawlist.AddRectFilled(p, fullSize, Helper.Background);
        drawlist.AddImage(TextureManager.GameOver.Tex.ImGuiHandle, p, fullSize);
    }

    public void DrawVictory()
    {
        var drawlist = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();

        var fullSize = new Vector2(p.X + Settings.Width, p.Y + Settings.Height);
        drawlist.AddRectFilled(p, fullSize, Helper.Background);

        ImGui.PushFont(Game.Plugin.FontManager.AmazDooMLeft);
        var text = "Victory";
        var height = Settings.HalfHeight - (ImGui.CalcTextSize(text).Y / 2);
        ImGuiHelpers.ScaledDummy(height);
        Helper.SetTextCenter(text);
        ImGui.PopFont();
    }

    public void DrawMainMenu()
    {
        var drawlist = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();

        var fullSize = new Vector2(p.X + Settings.Width, p.Y + Settings.Height);
        drawlist.AddRectFilled(p, fullSize, Helper.Background);

        TitleRender();

        ImGui.PushFont(Game.Plugin.FontManager.SourceCode36);
        ImGuiHelpers.ScaledDummy(20.0f);
        if (Helper.CenterButton("Play"))
        {
            Game.InitLevel();
            AudioPlaybackEngine.Instance.FadeOut();
        }

        if (Helper.CenterButton("Credits"))
        {
            CreditsReset = true;
            CreditsThrottler = 0.0f;
            Game.CurrentState = State.Credits;
        }

        ImGui.PopFont();
    }

    public void DrawCredits()
    {
        var drawlist = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();

        var fullSize = new Vector2(p.X + Settings.Width, p.Y + Settings.Height);
        drawlist.AddRectFilled(p, fullSize, Helper.Background);

        var windowSize = ImGui.GetWindowSize();
        ImGui.BeginChild("Scroll", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar);

        if (CreditsReset)
        {
            ImGui.SetScrollY(0);
            CreditsReset = false;
        }

        // Return here to remove flickering ScrollBarY bug
        CreditsThrottler += Bahamood.DeltaTime;
        if (CreditsThrottler < 1000.0f / CreditFps)
        {
            ImGui.EndChild();
            return;
        }

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            ImGuiHelpers.ScaledDummy(new Vector2(0, windowSize.Y + 20.0f));

            TitleRender();

            ImGuiHelpers.ScaledDummy(0, 30.0f);

            var windowX = ImGui.GetWindowSize().X;

            foreach (var creditsLine in CreditsText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            {
                var lineLenX = ImGui.CalcTextSize(creditsLine).X;

                ImGui.Dummy(new Vector2((windowX / 2) - (lineLenX / 2), 0f));
                ImGui.SameLine();
                ImGui.TextUnformatted(creditsLine);
            }

            ImGuiHelpers.ScaledDummy(0, windowSize.Y + 50f);
        }

        var curY = ImGui.GetScrollY();
        var maxY = ImGui.GetScrollMaxY();

        if (curY < maxY - 1)
            ImGui.SetScrollY(curY + 1);
        else
            ImGui.SetScrollY(0);

        ImGui.EndChild();
    }

    public void DrawNextStage()
    {
        var drawlist = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();

        var fullSize = new Vector2(p.X + Settings.Width, p.Y + Settings.Height);
        drawlist.AddRectFilled(p, fullSize, Helper.Background);

        ImGui.PushFont(Game.Plugin.FontManager.AmazDooMLeft);
        var text = Game.CurrentLevel?.LevelName ?? "Loading ...";
        var height = Settings.HalfHeight - (ImGui.CalcTextSize(text).Y / 2);
        ImGuiHelpers.ScaledDummy(height);
        Helper.SetTextCenter(text);
        ImGui.PopFont();
    }

    private void RenderUIText(Vector2 p)
    {
        ImGui.PushFont(Game.Plugin.FontManager.AmazDooMLeft);
        var text = "Open Door [F]";
        var height = Settings.HalfHeight - (ImGui.CalcTextSize(text).Y / 2);
        ImGuiHelpers.ScaledDummy(height);
        Helper.SetTextCenter(text);
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(p);
    }

    private void TitleRender()
    {
        ImGuiHelpers.ScaledDummy(50.0f);
        ImGui.PushFont(Game.Plugin.FontManager.AmazDooMLeft);
        ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize("BAHAMOOD").X + 5.0f) * 0.5f);
        ImGui.TextUnformatted("BAHA");
        ImGui.PopFont();
        ImGui.SameLine(0, 5.0f);
        ImGui.PushFont(Game.Plugin.FontManager.AmazDooMRight);
        ImGui.TextUnformatted("MOOD");
        ImGui.PopFont();
    }

    private Dictionary<int, TextureCollection> LoadWallTexture()
    {
        return new()
        {
            { 1, TextureCollection.SimpleCol(new [] { TextureManager.LimsaRock1 }) },
            { 2, TextureCollection.SimpleCol(new [] { TextureManager.LimsaStuc1 }) },
            { 3, TextureCollection.SimpleCol(new [] { TextureManager.LimsaStpv1 }) },
            { 4, TextureCollection.SimpleCol(new [] { TextureManager.LimsaWall1, TextureManager.Vines1 }) },
            { 5, TextureCollection.SimpleCol(new [] { TextureManager.LimsaWood3, TextureManager.GarleanFlag, TextureManager.Vines2 }) },
            { 6, TextureCollection.WestFacing(new [] { TextureManager.LimsaWood3 }, new [] { TextureManager.LimsaWall1 }) },
            { 7, TextureCollection.WestFacing(new [] { TextureManager.LimsaWood3, TextureManager.GarleanFlag}, new [] { TextureManager.LimsaWall1 }) },
            { 8, TextureCollection.Facings(new [] { TextureManager.LimsaWood3 }, new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWall1 }) },
            { 9, TextureCollection.Facings(new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWood3 }, new [] { TextureManager.LimsaWall1 }) },
            { 10, TextureCollection.Facings(new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWood3 }) },
            { 11, TextureCollection.Facings(new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWood3 }, new [] { TextureManager.LimsaWall1, TextureManager.LimsaDoor.Right }, new [] { TextureManager.LimsaWall1 }) },
            { 12, TextureCollection.Facings(new [] { TextureManager.LimsaWall1, TextureManager.LimsaDoor.Left }, new [] { TextureManager.LimsaWood3 }, new [] { TextureManager.LimsaWall1 }, new [] { TextureManager.LimsaWall1 }) },
            { 13, TextureCollection.SimpleCol(new [] { TextureManager.LimsaDoor.Full }, true) },
            { 14, TextureCollection.EastFacing(new [] { TextureManager.LimsaWall1, TextureManager.VinesLeft }, new []{ TextureManager.LimsaWall1 }) },
            { 15, TextureCollection.EastFacing(new [] { TextureManager.LimsaWall1, TextureManager.Vines1 }, new []{ TextureManager.LimsaWall1 }) },
            { 16, TextureCollection.EastFacing(new [] { TextureManager.LimsaWall1, TextureManager.VinesRight }, new []{ TextureManager.LimsaWall1 }) },
        };
    }
}