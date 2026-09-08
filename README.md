# Squad Leader

Quasimorph mod scaffold for squad / ally command features.

## Build

```bat
dotnet build src\Squad_Leader.csproj -c Release
```

Output for Steam upload: `publish\` (contains `Squad_Leader.dll` + `modmanifest.json`).

## First-time Steam Workshop publish (in-game console)

1. Build Release (creates `publish\`).
2. Open console (`` ` ``) with Developer Console mod.
3. Create workshop item:

```text
mod_createworkshopitem E:\SteamLibrary\steamapps\common\Quasimorph\Squad_Leader\publish
```

4. Copy the printed item id into `src\Squad_Leader.csproj` → `<SteamId>...</SteamId>`.
5. Subscribe to your item in Steam / restart the game.
6. Later updates:

```text
mod_updateworkshopitem <item_id> E:\SteamLibrary\steamapps\common\Quasimorph\Squad_Leader\publish true
```

Third argument `true` uploads `thumbnail.png` as the Steam Workshop preview. Use `false` only to update content without changing the cover.
## Verify mod loaded

```text
squad_leader status
```

Aliases: `squadleader`, `sl`.

## Config

`%AppData%\..\LocalLow\Magnum Scriptum Ltd\Quasimorph_ModConfigs\Squad_Leader\config.json`
