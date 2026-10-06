# In-Game Multiplayer Implementation Changes

This document details all modifications and additions made to wire up the in-game side (**Game** scene) for Unity + Netcode for GameObjects (NGO) with Relay and Lobby support. **Zero `.unity` scene files were modified.**

---

## 1. Overview of Tasks Completed

1. **Player Spawning & Late Joiners**: Server spawns individual kart instances for each connected client with ownership (`SpawnWithOwnership(clientId, true)`). Includes a synchronized `HashSet<ulong>` reservation check so `"Player spawned: {clientId}"` is never logged twice.
2. **Non-Overlapping Spawn Points**: Staggered 2-column starting grid layout calculated along the track starting line from track origin `(609f, -89.9f, -2761f)`. Dynamically discovers in-scene placeholder `base_basic_shaded`, `RespawnManager`, or objects tagged `"Respawn"`.
3. **Ownership Isolation**: Owners handle camera, audio listener, Cinemachine, and input; non-owners are kinematic and driven via `NetworkTransform`.
4. **Lobby Lock & Connection Approval**:
   - Server locks the lobby on match start (`IsLocked = true`) to hide the lobby from public listings.
   - Registers `NetworkManager.ConnectionApprovalCallback` to block mid-race joins from anyone attempting to connect directly with the Relay code.
5. **Disconnect Handling**:
   - Client disconnects: cleanly despawned without errors on the host.
   - Host disconnects: clients cleanly shut down and return to Main Menu with a disconnect banner.
6. **In-Game Leave / Quit**: Client shuts down and loads Main Menu; host deletes lobby, stops heartbeat, shuts down, and loads Main Menu.
7. **Room Code Display**: Small HUD display (`RoomCode: XXX-XXX`) inside the pause menu, with duplicate label protection.
8. **Required Logs**:
   - `Player spawned: {clientId}`
   - `Player disconnected: {clientId}`
   - `Lobby locked`

---

## 2. File-by-File Summary

| File | Status | Core Changes |
|---|---|---|
| [`InGameNetworkManager.cs`](Assets/Scripts/Network/InGameNetworkManager.cs) | **Created** | Manages player spawning, starting grid slots, lobby locking, `ConnectionApprovalCallback`, disconnect callbacks, leave coordinator, and room code caching. |
| [`Player Kart.prefab`](Assets/Prefabs/Player%20Kart.prefab) | **Modified** | Stripped obsolete Mirror component; added `NetworkObject` and `NetworkTransform`. |
| `Assets/Resources/Player Kart.prefab` | **Created** | Resources fallback copy so `Resources.Load` succeeds anywhere without scene references. |
| [`DefaultNetworkPrefabs.asset`](Assets/DefaultNetworkPrefabs.asset) | **Modified** | Registered `Player Kart`, `Boom`, `Mine`, and `Pickup` in NGO's `NetworkPrefabsList`. |
| [`Boom.prefab`](Assets/Prefabs/Boom.prefab) | **Modified** | Replaced legacy Mirror identity with `NetworkObject`. |
| [`Mine.prefab`](Assets/Prefabs/Mine.prefab) | **Modified** | Replaced legacy Mirror identity with `NetworkObject`. |
| [`Pickup.prefab`](Assets/Prefabs/Pickup.prefab) | **Modified** | Replaced legacy Mirror identity with `NetworkObject`. |
| [`PauseMenu.cs`](Assets/Scripts/UI/PauseMenu.cs) | **Modified** | Auto-formats and displays room code as `XXX-XXX` with duplicate creation guards; coordinates leave routine. |
| [`ClientManager.cs`](Assets/Scripts/Network/ClientManager.cs) | **Modified** | Added `DisconnectReason` static property for inter-scene status messaging. |
| [`MainMenuManager.cs`](Assets/Scripts/Network/MainMenuManager.cs) | **Modified** | Displays `ClientManager.DisconnectReason` when returning from a disconnected game. |
| [`RespawnManager.cs`](Assets/Scripts/Game/RespawnManager.cs) | **Modified** | Updated to modern NGO `[Rpc]` syntax and triggers `TemporaryInvulnerability`. |
| [`MirrorKartController.cs`](Assets/Scripts/Kart/MirrorKartController.cs) | **Modified** | Enforces ownership: disables camera, audio listener, and Cinemachine on non-owners; sets `rb.isKinematic = true` on remote instances. |
| [`BoomerangProjectile.cs`](Assets/Scripts/Weapons/BoomerangProjectile.cs) | **Modified** | Overrode `OnDestroy()` with `base.OnDestroy()` for NGO lifecycle compliance. |
| [`Menu.cs`](Assets/Menu.cs) | **Modified** | Cleaned legacy Mirror references and linked to `Unity.Netcode`. |
| [`AnimatedElement_Inspector.cs`](Assets/Airy%20UI/Editor/AnimatedElement_Inspector.cs) | **Modified** | Fixed Unity 6 deprecation: updated `Selection.instanceIDs` to `Selection.entityIds`. |
| [`CustomAnimatedElement_Inspector.cs`](Assets/Airy%20UI/Editor/CustomAnimatedElement_Inspector.cs) | **Modified** | Fixed Unity 6 deprecation: updated `Selection.instanceIDs` to `Selection.entityIds`. |

---

## 3. Inspector & Configuration Checklist

1. **Network Prefabs Registration**:
   - `Assets/DefaultNetworkPrefabs.asset` has `Player Kart`, `Boom`, `Mine`, and `Pickup` registered.
   - Verify that your in-scene `NetworkManager` component has **Network Prefabs** assigned to `DefaultNetworkPrefabs`.
2. **Pause Menu Room Code Label (Optional)**:
   - If your Pause Menu already has a dedicated room code text element, you can assign it to the `Room Code Text` field on `PauseMenu`. Otherwise, it automatically creates/reuses a clean text label anchored in the top-right corner.
3. **Leave Button**:
   - Ensure your pause menu's "Leave" or "Quit" button triggers `PauseMenu.QuitToMainMenu()` or `InGameNetworkManager.Instance.LeaveGame()`.
