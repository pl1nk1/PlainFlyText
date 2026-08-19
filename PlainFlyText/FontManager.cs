using System;
using System.IO;
using Dalamud.Interface.ManagedFontAtlas;

namespace PlainFlyText;

// Owns exactly one IFontHandle at a time, rebuilding (dispose + recreate) whenever the
// configured font path/size actually changes - not per frame, not per keystroke.
// Callers can read CurrentFont.LoadException (e.g. from a Draw loop) to surface
// load failures; this class doesn't log on its own since that requires an async
// wait whose failure-path completion semantics aren't guaranteed.
internal sealed class FontManager : IDisposable
{
    private readonly IFontAtlas fontAtlas;

    private string loadedPath = string.Empty;
    private float loadedSizePx;

    public FontManager(IFontAtlas fontAtlas)
    {
        this.fontAtlas = fontAtlas;
    }

    public IFontHandle? CurrentFont { get; private set; }

    public void EnsureFont(string fontPath, float sizePx)
    {
        if (CurrentFont != null && loadedPath == fontPath && loadedSizePx == sizePx)
        {
            return;
        }

        var oldFont = CurrentFont;
        loadedPath = fontPath;
        loadedSizePx = sizePx;

        CurrentFont = fontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            if (!string.IsNullOrEmpty(fontPath) && File.Exists(fontPath))
            {
                var config = new SafeFontConfig { SizePx = sizePx };
                tk.Font = tk.AddFontFromFile(fontPath, config);
            }
            else
            {
                tk.Font = tk.AddDalamudDefaultFont(sizePx);
            }
        }));

        oldFont?.Dispose();
    }

    public void Dispose() => CurrentFont?.Dispose();
}
