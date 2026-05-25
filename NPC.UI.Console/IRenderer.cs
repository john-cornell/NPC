namespace NPC.UI.Console;

using System.Collections.Generic;
using NPC.Library.Character;
using NPC.Application;

using Spectre.Console.Rendering;

public interface IRenderer
{
    IRenderable Build(UIState state);
}
