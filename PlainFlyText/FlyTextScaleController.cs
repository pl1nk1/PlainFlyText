using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace PlainFlyText;

// Scales the native FlyText addon window via AtkUnitBase.SetScale, fetched each tick
// through Dalamud's stable, documented IGameGui.GetAddonByName - no native hooking or
// signature scanning at all for this one.
//
// Caveat (untested by us, flagged to the user before building): this scales the whole
// shared FlyText window from its own anchor, not each number individually. Since every
// flytext entry is positioned as a child offset within that one window, scaling it may
// visibly shift entries away from the character they belong to, more so the further
// they are from the window's anchor. That's why this ships as an opt-in toggle.
internal sealed unsafe class FlyTextScaleController : IDisposable
{
    private readonly Configuration config;
    private readonly IGameGui gameGui;
    private readonly IFramework framework;

    public FlyTextScaleController(Configuration config, IGameGui gameGui, IFramework framework)
    {
        this.config = config;
        this.gameGui = gameGui;
        this.framework = framework;
        framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        var addon = gameGui.GetAddonByName<AddonFlyText>("FlyText");
        if (addon == null)
        {
            return;
        }

        var targetScale = config.SizeScalingEnabled ? config.SizeMultiplier : 1.0f;
        addon->SetScale(targetScale, false);
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;

        // Don't leave native flytext stuck at a non-default scale if the plugin
        // unloads/disables while the toggle was on.
        var addon = gameGui.GetAddonByName<AddonFlyText>("FlyText");
        if (addon != null)
        {
            addon->SetScale(1.0f, false);
        }
    }
}
