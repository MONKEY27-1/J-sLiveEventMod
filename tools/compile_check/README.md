# Compile check (no game needed)

`tools/compile_check/check.sh` builds the event mod and J CLIENT without Gorilla Tag installed:

- Unity API: `UnityEngine.Modules` 2021.3 reference assemblies from NuGet (the game runs Unity 6; the script renames the
  Unity 6 `Rigidbody.linearVelocity` / `linearDamping` / `angularDamping` to their 2021.3 names in the copies it compiles).
- Harmony: `Lib.Harmony` from NuGet.
- Gorilla Tag, BepInEx, Photon and the Input System: hand-written stubs in `stubs/`. They contain only the members the
  existing code already uses (that code compiles against the real game), so new code that needs a game member that is not
  in the stubs fails here on purpose: check that member exists in the game before adding it to `stubs/GT.cs`.

It catches syntax, type and Unity API mistakes. It cannot catch a wrong guess about a game member's exact type, and it
says nothing about how anything behaves in game: build with `dotnet build -c Release` on the PC and test there.

Needs the .NET 8 SDK (`dotnet`). Output: `...: OK` per project, or the compiler errors.
