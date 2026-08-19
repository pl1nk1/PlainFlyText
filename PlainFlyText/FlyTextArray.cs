using System.Runtime.InteropServices;

namespace PlainFlyText;

// Mirrors the native FlyText addon's array of positioning groups. Reused verbatim
// from Aireil/FlyTextFilter (github.com/Aireil/FlyTextFilter,
// FlyTextFilter/Model/FlyTextArray.cs) - real, shipped, third-party code, not
// derived by us.
//
// Group [0] is the player's own healing numbers; [1] is the player's own
// status/damage numbers. Groups [2..9] are documented upstream as
// "unknown/dynamic" and aren't controlled by either plugin.
[StructLayout(LayoutKind.Explicit, Size = 0x30 * 10)]
internal struct FlyTextArray
{
    [StructLayout(LayoutKind.Explicit, Size = 0x30)]
    public struct FlyTextGroup
    {
        [FieldOffset(0x00)] public unsafe long* LinkedList;
        [FieldOffset(0x08)] public long CurrentNbOfNodes;
        [FieldOffset(0x10)] public float X;
        [FieldOffset(0x14)] public float Y;
        [FieldOffset(0x18)] public float HorizontalTranslation;
        [FieldOffset(0x1C)] public float VerticalTranslation;
        [FieldOffset(0x20)] public float VerticalOffset;
        [FieldOffset(0x24)] public short MaxNbOfNodes;
        [FieldOffset(0x26)] public short Priority1;
        [FieldOffset(0x28)] public short Priority2;
    }

    public unsafe FlyTextGroup* this[int i]
    {
        get
        {
            if (i is < 0 or > 9)
            {
                return null;
            }

            fixed (void* ptr = &this)
            {
                return (FlyTextGroup*)ptr + i;
            }
        }
    }
}
