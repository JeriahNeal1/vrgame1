# Risks And Open Questions

This register tracks design and implementation risks for the unified item, inventory, equipment, modifier, enchantment, harvesting, and VR manifestation framework.

## Risks

| Risk | Why It Matters | Mitigation |
| --- | --- | --- |
| ScriptableObject references leaking into save data | Saves and network state become brittle, non-serializable, and hard to validate server-side. | Runtime DTOs store IDs and value records only. Add tests/validators that reject Unity object references in save models. |
| Scene coupling | Inventory and item systems become tied to `OutdoorsScene` or a specific player rig. | Keep core systems in services and ScriptableObjects. Use scene adapters only at boundaries. Avoid scene edits unless the milestone requires them. |
| Hurricane VR API/version differences | Hurricane classes, grab events, or setup requirements may change or differ from docs. | Isolate Hurricane calls in `VRGame.Integration.HurricaneVR`. Validate against imported `Assets/HurricaneVR` code before implementation. |
| OpenXR/XRI setup drift | Package-lock shows OpenXR/XRI dependencies while project settings may be changed by Unity import or plugin setup. | Check `Packages/manifest.json`, `packages-lock.json`, `Assets/XR`, `Assets/XRI`, and ProjectSettings before XR-facing work. Avoid package changes without a setup task. |
| Unlimited inventory performance | Unlimited capacity can produce very large lists, expensive sorting, and slow VR UI rendering. | Store stackables by definition ID, virtualize/paginate UI lists, cache sorted views, and avoid per-frame full inventory scans. |
| Infinitely stackable quantity overflow | A normal integer may eventually overflow in long-running automation/factory economies. | Hide quantity behind a domain type. Start with `long` only if APIs can later swap to string-backed or arbitrary precision quantities. |
| MMO authority and duping risks | Manifested physical objects can create duplication if inventory reservations are not transactional. | Use command/result flows, revisions, manifestation IDs, reservations/locks, and server-owned world item records. |
| Modifier/enchantment explosion | Many modifiers, tiers, enchantments, gems, stations, and skills can become unmaintainable. | Use data-driven definitions, applicability rules, weighted sets, tags, and validators. Keep application logic generic. |
| Reforging/anvil mechanics becoming hard-coded | Future smithing and station interactions need many ways to change modifiers. | Model reforging, hammer strikes, and gem use as generic item action presets with action context and effect handlers. |
| VR UI usability | Three-panel VR inventory can become tiring, crowded, or imprecise. | Prototype rough interactions early, keep lists virtualized, support clear selection feedback, and test grip/release flows in headset when possible. |
| Icon generation editor complexity | Rendering physical prefabs into icons can break with lighting, materials, prefab scale, and HDRP preview setup. | Keep icon generation editor-only, isolated from scenes, and backed by validation/reporting. Start with manual fallback icons if needed. |
| Vendor asset churn | Imported asset folders are large and may be updated from the Asset Store. | Do not modify vendor folders for project behavior. Add wrappers/adapters in first-party folders. |
| Generated project files noise | Unity may rewrite `.csproj`, `.slnx`, and settings during import. | Treat generated files carefully. Do not hand-edit them for architecture. Keep final summaries clear about intended changes. |
| Test coverage lag | Core data bugs can become expensive once VR UI and physical objects rely on them. | Add edit mode tests as soon as core DTOs and services exist. Favor pure rules that can be tested without scene setup. |
| Equipment slot assumptions | Ring count and special slots can be baked into code too early. | Use `EquipmentSlotDefinition` and indexed slot records. Configure rings up to 10 without hard-coding the count into instances. |

## Open Questions

| Question | Current Assumption |
| --- | --- |
| What first-party root namespace should be final? | Use `VRGame` until the user chooses a product name. |
| Should quantities use `long`, `decimal`, or arbitrary precision from day one? | Use a quantity domain type. Initial storage may be string-backed to preserve future arbitrary precision. |
| Should item IDs be generated GUID strings, human-readable slugs, or both? | Use stable GUID-like IDs internally and allow optional human-readable debug slugs. |
| Should item databases use Addressables? | Not initially, because Addressables is not present in the inspected manifest. Add later only when asset loading scale requires it. |
| How should physical prefabs be organized? | Put first-party gameplay prefabs under `Assets/_Project/Prefabs` and reference vendor art as nested assets where appropriate. |
| Which Hurricane rig/player prefab will be the project baseline? | Not selected yet. Inspect Hurricane tech demo prefabs before Milestone 5. |
| How should OpenXR Toolkit Ultimate be represented in project docs/setup? | The repo has OpenXR package/settings, but no explicit inspected asset named OpenXR Toolkit Ultimate. Confirm installation/setup before XR tuning work. |
| What serialization format will saves use? | Keep DTOs format-agnostic. JSON is fine for tests/debug; final save format can change later. |
| How much client prediction is desired for MMO manifestation and harvesting? | Keep command/result APIs prediction-friendly, but implement local authority first. |
| What are the first real item prefabs? | Unknown. Use placeholders only if clearly separated from final content and validated as temporary. |
| Should PlayMaker be used for gameplay logic? | Avoid first-party core inventory logic in PlayMaker. It can call into command services later if desired. |
| What stats are required for the first melee milestone? | Start with melee damage, attack speed, reach, crit chance, knockback/stagger, and any Hurricane hit metadata needed by the adapter. |

## Blockers To Resolve Before Major Implementation

- Confirm the exact first-party namespace/product name if `VRGame` is not desired.
- Confirm whether a first-party asmdef layout should be introduced in Milestone 1.
- Confirm whether placeholder physical prefabs may be created for sample item definitions.
- Confirm the baseline Hurricane VR player rig and grabbable prefab requirements before physical manifestation work.
- Confirm whether OpenXR Toolkit Ultimate is already installed outside the inspected files or still needs setup.
