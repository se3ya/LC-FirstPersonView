using System;

namespace FirstPersonView;

[Flags]
public enum ItemDitherMode
{
    Vanilla = 1,        // fade the item while the vanilla first-person arms hold it
    ThirdPerson = 2,        // fade the item while the body's own arms hold it
}