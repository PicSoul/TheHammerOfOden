# PatchCheck

Verifies that every Harmony patch target in the built mod still exists in the installed
Valheim assembly. Run it after a Valheim update, before launching the game: a string-named
patch target that no longer exists throws at patch time and takes the whole mod down with it.

Usage (paths are for this machine; adjust as needed):

```
dotnet run --project tools/PatchCheck/patchcheck.csproj -c Release -- \
  src/MarkOfOden/bin/Release/MarkOfOden.dll \
  "X:/SteamLibrary/steamapps/common/Valheim/valheim_Data/Managed" \
  "$APPDATA/r2modmanPlus-local/Valheim/profiles/1.0 Release V0.1/BepInEx/core"
```

It also checks ServerSync's own patches, so it doubles as a compatibility check on the vendored copy.

Exit code is non-zero when anything is missing or ambiguous.
