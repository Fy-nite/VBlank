// Support command-line flag `--sdl` to force the Silk/SDL backend instead of MonoGame.
using System;

bool useSdl = false;
if (args != null)
{
    foreach (var a in args)
    {
        if (string.Equals(a, "--sdl", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-sdl", StringComparison.OrdinalIgnoreCase))
        {
            useSdl = true;
            break;
        }
    }
}
if (useSdl)
{
    try { Environment.SetEnvironmentVariable("ASMO_BACKEND", "SDL"); } catch { }
}

using var game = new VBlank.AsmoGameEngine("VBlankTest", 800, 600);
game.LoadEntry("Content\\entry.oir");
game.Run();
