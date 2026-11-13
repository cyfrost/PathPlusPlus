using System;

namespace PathPlusPlus.Core
{
    [Flags]
    public enum EntryStatus
    {
        Ok = 0,
        Empty = 1,
        Missing = 2,
        Duplicate = 4,
        AlsoInOtherScope = 8,
        NotAbsolute = 16,
        UnresolvedVariable = 32,
    }
}
