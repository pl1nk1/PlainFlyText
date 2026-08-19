using System;
using System.Numerics;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace PlainFlyText;

// Captures per-actor position for custom-rendered flytext by hooking a native function
// deeper than Dalamud's own IFlyTextGui.FlyTextCreated event, which carries no actor
// identity at all. FlyTextCreated (see Plugin.OnFlyTextCreated) and this hook are
// intentionally NOT correlated 1:1 - they both fire synchronously within the same
// native call stack for a given hit, so "always suppress via the safe event for kinds
// we own" plus "always capture via this hook for kinds we own" just works independently.
internal sealed unsafe class ScreenLogHook : IDisposable
{
    // Signature for the native AddScreenLogWithKind function, reused verbatim from
    // cultbaus/CBT (github.com/cultbaus/CBT, CBT/PluginAddressResolver.cs), a real,
    // shipped, third-party Dalamud plugin - not derived by us. This is an unofficial,
    // unsupported memory signature with no compatibility guarantee: it WILL eventually
    // break on a game patch that changes the bytes around this function. Initialize()
    // treats that as an expected, recoverable failure, not an exceptional one.
    private const string AddScreenLogWithKindSignature = "E8 ?? ?? ?? ?? BF ?? ?? ?? ?? EB 39";

    private Hook<AddScreenLogWithKindDelegate>? hook;

    public bool IsAvailable { get; private set; }

    public event Action<ActiveFlyText>? FlyTextCaptured;

    private delegate void AddScreenLogWithKindDelegate(
        Character* target,
        Character* source,
        FlyTextKind kind,
        int option,
        int actionKind,
        int actionId,
        int val1,
        int val2,
        int val3,
        int val4);

    public void Initialize(IGameInteropProvider gameInteropProvider, IPluginLog log)
    {
        try
        {
            hook = gameInteropProvider.HookFromSignature<AddScreenLogWithKindDelegate>(AddScreenLogWithKindSignature, Detour);
            hook.Enable();
            IsAvailable = true;
            log.Information("PlainFlyText: custom rendering hook installed.");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "PlainFlyText: failed to install the native flytext hook; falling back to blank-label-only mode.");
            IsAvailable = false;
            hook = null;
        }
    }

    public void Dispose()
    {
        hook?.Disable();
        hook?.Dispose();
    }

    private void Detour(
        Character* target,
        Character* source,
        FlyTextKind kind,
        int option,
        int actionKind,
        int actionId,
        int val1,
        int val2,
        int val3,
        int val4)
    {
        // Always call through unmodified first - this hook only observes, it never
        // alters native behavior.
        hook!.Original(target, source, kind, option, actionKind, actionId, val1, val2, val3, val4);

        if (target == null || !FlyTextKindSet.NumberWithLabel.Contains(kind))
        {
            return;
        }

        var entry = new ActiveFlyText
        {
            Kind = kind,
            Val1 = val1,
            WorldPosition = target->Position,
            Color = ColorFor(kind),
        };

        FlyTextCaptured?.Invoke(entry);
    }

    // Approximate native palette (white for normal, yellow-ish for crit/DH variants,
    // green for healing) - the AddScreenLogWithKind hook doesn't carry a color, and per
    // design the two hook paths aren't correlated, so this is a scoped simplification
    // rather than a pixel-perfect match of native flytext coloring.
    private static Vector4 ColorFor(FlyTextKind kind) => kind switch
    {
        FlyTextKind.DamageCrit or FlyTextKind.DamageCritDh
            or FlyTextKind.AutoAttackOrDotCrit or FlyTextKind.AutoAttackOrDotCritDh
            => new Vector4(1f, 0.85f, 0.2f, 1f),
        FlyTextKind.Healing or FlyTextKind.HealingCrit
            => new Vector4(0.4f, 1f, 0.4f, 1f),
        FlyTextKind.HpDrain or FlyTextKind.MpDrain
            => new Vector4(0.8f, 0.4f, 1f, 1f),
        _ => Vector4.One,
    };
}
