using UnityEditor;
using UnityEngine;
using VRGame.Items;
using VRGame.Runtime;

namespace VRGame.Manifestation.Editor
{
    public static class BuildingPlacementSelfCheckMenu
    {
        [MenuItem("Tools/VRGame/Items/Run Building Placement Self Checks")]
        public static void RunBuildingPlacementSelfChecks()
        {
            GameObject wallPrefab = CreatePlacedPrefab("SelfCheck_WoodWallPrefab");
            GameObject chairPrefab = CreatePlacedPrefab("SelfCheck_ChairPrefab");
            GameObject generatorPrefab = CreateElectricalPrefab("SelfCheck_GeneratorPrefab", typeof(ElectricalGenerator));
            GameObject wirePrefab = CreatePlacedPrefab("SelfCheck_WireSpoolPrefab");

            ItemDefinition woodWall = CreatePlaceableDefinition(
                "placeable.framework.wood_wall",
                "Placeable > Framework > Wall",
                ItemFlags.Placeable | ItemFlags.Material | ItemFlags.CanBeManifested,
                PlacementMode.FrameworkSnap,
                PlaceableKind.Wall,
                FrameworkPieceKind.Wall,
                wallPrefab);
            ItemDefinition chair = CreatePlaceableDefinition(
                "placeable.furniture.wood_chair",
                "Placeable > Furniture",
                ItemFlags.Placeable | ItemFlags.CanBeManifested,
                PlacementMode.FreeFurniture,
                PlaceableKind.Furniture,
                FrameworkPieceKind.None,
                chairPrefab);
            ItemDefinition generator = CreatePlaceableDefinition(
                "placeable.electrical.generator",
                "Placeable > Electrical > Generator",
                ItemFlags.Placeable | ItemFlags.Electrical | ItemFlags.CanBeManifested,
                PlacementMode.ElectricalDevice,
                PlaceableKind.ElectricalDevice,
                FrameworkPieceKind.None,
                generatorPrefab);
            ItemDefinition wire = CreatePlaceableDefinition(
                "placeable.electrical.wire",
                "Placeable > Electrical > Wire",
                ItemFlags.Placeable | ItemFlags.Electrical | ItemFlags.Material,
                PlacementMode.Wire,
                PlaceableKind.Wire,
                FrameworkPieceKind.None,
                wirePrefab);

            ItemDefinitionDatabase database = CreateDatabase(woodWall, chair, generator, wire);
            PlayerInventoryState inventoryState = new PlayerInventoryState("building_self_check_player");
            TestInventoryProvider provider = new TestInventoryProvider(inventoryState);
            PlayerInventoryOperations.AddStack(inventoryState, database, woodWall.ItemDefId, StackQuantity.FromLong(1));
            PlayerInventoryOperations.AddStack(inventoryState, database, chair.ItemDefId, StackQuantity.FromLong(1));
            PlayerInventoryOperations.AddStack(inventoryState, database, generator.ItemDefId, StackQuantity.FromLong(1));
            PlayerInventoryOperations.AddStack(inventoryState, database, wire.ItemDefId, StackQuantity.FromLong(1));

            GameObject serviceObject = new GameObject("SelfCheck_PlacementService");
            ItemPlacementService placementService = serviceObject.AddComponent<ItemPlacementService>();
            placementService.BindRuntime(database, provider);

            GameObject ground = CreateGround("SelfCheck_Ground");
            PlacementPose groundPose = new PlacementPose
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                SurfaceNormal = Vector3.up,
                SurfaceCollider = ground.GetComponent<Collider>()
            };

            GameObject foundationSupport = CreateFrameworkSupport("SelfCheck_FoundationSupport", FrameworkPieceKind.Foundation, Vector3.zero);
            FrameworkSnapPoint wallSnap = foundationSupport.GetComponentInChildren<FrameworkSnapPoint>();
            PlacementPose wallPose = new PlacementPose
            {
                Position = wallSnap.transform.position,
                Rotation = Quaternion.identity,
                SurfaceNormal = Vector3.up
            };

            int failures = 0;

            PlacementResult wallWithoutSnap = placementService.TryPlace(woodWall.ItemDefId, groundPose);
            failures += ExpectPlacementFailure("Wood Wall rejects placement without a valid snap rule", wallWithoutSnap, PlacementFailureReason.SnapPointRequired);
            failures += ExpectTrue(
                "Failed Wood Wall placement does not consume stack",
                PlayerInventoryOperations.HasStack(inventoryState, woodWall.ItemDefId, StackQuantity.One));

            PlacementResult wallWithSnap = placementService.TryPlace(woodWall.ItemDefId, wallPose, wallSnap);
            failures += ExpectPlacementSuccess("Wood Wall places when framework snap passes", wallWithSnap);
            failures += ExpectTrue(
                "Successful Wood Wall placement consumes stack",
                !PlayerInventoryOperations.HasStack(inventoryState, woodWall.ItemDefId, StackQuantity.One));

            PlacementResult chairPlacement = placementService.TryPlace(chair.ItemDefId, new PlacementPose
            {
                Position = new Vector3(3f, 0f, 0f),
                Rotation = Quaternion.Euler(0f, 45f, 0f)
            });
            failures += ExpectPlacementSuccess("Furniture can be placed freely when collision rules pass", chairPlacement);

            PlacementResult generatorPlacement = placementService.TryPlace(generator.ItemDefId, new PlacementPose
            {
                Position = new Vector3(5f, 0f, 0f),
                Rotation = Quaternion.identity
            });
            failures += ExpectPlacementSuccess("Generator electrical device can be placed from item stack", generatorPlacement);

            GameObject generatorObject = new GameObject("SelfCheck_GeneratorDevice");
            ElectricalGenerator generatorComponent = generatorObject.AddComponent<ElectricalGenerator>();
            ElectricalNode generatorOutput = CreateElectricalNode("SelfCheck_GeneratorOutput", generatorObject.transform, new Vector3(0f, 0f, 0f), ElectricalNodeRole.Output, ElectricalNodeKind.GeneratorOutput);

            GameObject switchObject = new GameObject("SelfCheck_SwitchDevice");
            ElectricalSwitch switchComponent = switchObject.AddComponent<ElectricalSwitch>();
            ElectricalNode switchInput = CreateElectricalNode("SelfCheck_SwitchInput", switchObject.transform, new Vector3(1f, 0f, 0f), ElectricalNodeRole.Input, ElectricalNodeKind.SwitchInput);
            ElectricalNode switchOutput = CreateElectricalNode("SelfCheck_SwitchOutput", switchObject.transform, new Vector3(1.2f, 0f, 0f), ElectricalNodeRole.Output, ElectricalNodeKind.SwitchOutput);
            AssignSwitchNodes(switchComponent, switchInput, switchOutput);

            GameObject diodeObject = new GameObject("SelfCheck_DiodeDevice");
            ElectricalDiode diodeComponent = diodeObject.AddComponent<ElectricalDiode>();
            ElectricalNode diodeInput = CreateElectricalNode("SelfCheck_DiodeInput", diodeObject.transform, new Vector3(2f, 0f, 0f), ElectricalNodeRole.Input, ElectricalNodeKind.DiodeInput);
            ElectricalNode diodeOutput = CreateElectricalNode("SelfCheck_DiodeOutput", diodeObject.transform, new Vector3(2.2f, 0f, 0f), ElectricalNodeRole.Output, ElectricalNodeKind.DiodeOutput);
            AssignDiodeNodes(diodeComponent, diodeInput, diodeOutput);

            failures += ExpectTrue(
                "Generator/switch/diode expose electrical nodes",
                generatorComponent.OutputNodes.Count > 0 &&
                switchComponent.InputNode == switchInput &&
                switchComponent.OutputNode == switchOutput &&
                diodeComponent.InputNode == diodeInput &&
                diodeComponent.OutputNode == diodeOutput);

            GameObject wireToolObject = new GameObject("SelfCheck_WireTool");
            ElectricalConnectionRegistry registry = wireToolObject.AddComponent<ElectricalConnectionRegistry>();
            WireToolAction wireTool = wireToolObject.AddComponent<WireToolAction>();
            wireTool.BindRuntime(registry, database, provider, wire.ItemDefId);

            failures += ExpectWireSuccess("Wire tool selects first node", wireTool.SelectFirstNode(generatorOutput));
            WireToolResult connectionResult = wireTool.ReleaseOnNode(switchInput);
            failures += ExpectWireSuccess("Wire tool creates a compatible generator-to-switch connection", connectionResult);
            failures += ExpectTrue(
                "Wire connection data records node IDs and consumes wire stack",
                registry.WireConnections.Count == 1 &&
                connectionResult.Connection != null &&
                !string.IsNullOrWhiteSpace(connectionResult.Connection.FromNodeId) &&
                !PlayerInventoryOperations.HasStack(inventoryState, wire.ItemDefId, StackQuantity.One));

            PlayerInventoryOperations.AddStack(inventoryState, database, wire.ItemDefId, StackQuantity.One);
            failures += ExpectWireSuccess("Wire tool selects first node for duplicate rejection", wireTool.SelectFirstNode(generatorOutput));
            WireToolResult duplicateConnectionResult = wireTool.ReleaseOnNode(switchInput);
            failures += ExpectWireFailure("Wire tool rejects a duplicate generator-to-switch connection", duplicateConnectionResult);
            failures += ExpectTrue(
                "Duplicate wire rejection refunds wire stack",
                registry.WireConnections.Count == 1 &&
                PlayerInventoryOperations.HasStack(inventoryState, wire.ItemDefId, StackQuantity.One));

            DestroyTemporaryObjects(
                wallPrefab,
                chairPrefab,
                generatorPrefab,
                wirePrefab,
                woodWall,
                chair,
                generator,
                wire,
                database,
                serviceObject,
                ground,
                foundationSupport,
                generatorObject,
                switchObject,
                diodeObject,
                wireToolObject,
                wallWithSnap.PlacedObject,
                chairPlacement.PlacedObject,
                generatorPlacement.PlacedObject);

            if (failures == 0)
            {
                Debug.Log("Building placement self checks passed.");
            }
            else
            {
                Debug.LogError($"Building placement self checks failed with {failures} failure(s).");
            }
        }

        private static int ExpectPlacementSuccess(string label, PlacementResult result)
        {
            if (result != null && result.Success)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - {FormatPlacement(result)}");
            return 1;
        }

        private static int ExpectPlacementFailure(string label, PlacementResult result, PlacementFailureReason expectedReason)
        {
            if (result != null && !result.Success && result.FailureReason == expectedReason)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - expected {expectedReason}, got {FormatPlacement(result)}");
            return 1;
        }

        private static int ExpectWireSuccess(string label, WireToolResult result)
        {
            if (result != null && result.Success)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - {FormatWire(result)}");
            return 1;
        }

        private static int ExpectWireFailure(string label, WireToolResult result)
        {
            if (result != null && !result.Success)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - expected failure, got {FormatWire(result)}");
            return 1;
        }

        private static int ExpectTrue(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label}");
            return 1;
        }

        private static string FormatPlacement(PlacementResult result)
        {
            return result == null ? "null result" : $"{result.FailureReason}: {result.Message}";
        }

        private static string FormatWire(WireToolResult result)
        {
            return result == null ? "null result" : $"{result.FailureReason}: {result.Message}";
        }

        private static GameObject CreatePlacedPrefab(string objectName)
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = objectName;
            return prefab;
        }

        private static GameObject CreateElectricalPrefab(string objectName, System.Type componentType)
        {
            GameObject prefab = CreatePlacedPrefab(objectName);
            prefab.AddComponent(componentType);
            CreateElectricalNode(objectName + "_Node", prefab.transform, Vector3.zero, ElectricalNodeRole.Output, ElectricalNodeKind.GeneratorOutput);
            return prefab;
        }

        private static GameObject CreateGround(string objectName)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = objectName;
            ground.transform.position = new Vector3(0f, -0.55f, 0f);
            ground.transform.localScale = new Vector3(10f, 0.1f, 10f);
            return ground;
        }

        private static GameObject CreateFrameworkSupport(string objectName, FrameworkPieceKind pieceKind, Vector3 position)
        {
            GameObject support = new GameObject(objectName);
            support.transform.position = position;
            FrameworkStructurePiece piece = support.AddComponent<FrameworkStructurePiece>();
            piece.Bind(objectName, pieceKind);
            GameObject snapObject = new GameObject(objectName + "_Snap");
            snapObject.transform.SetParent(support.transform, false);
            snapObject.transform.localPosition = Vector3.zero;
            FrameworkSnapPoint snapPoint = snapObject.AddComponent<FrameworkSnapPoint>();
            snapPoint.BindOwner(piece);
            return support;
        }

        private static ElectricalNode CreateElectricalNode(string objectName, Transform parent, Vector3 position, ElectricalNodeRole roles, ElectricalNodeKind kind)
        {
            GameObject nodeObject = new GameObject(objectName);
            nodeObject.transform.SetParent(parent, false);
            nodeObject.transform.position = position;
            ElectricalNode node = nodeObject.AddComponent<ElectricalNode>();

            SerializedObject serializedObject = new SerializedObject(node);
            serializedObject.FindProperty("roles").intValue = (int)roles;
            serializedObject.FindProperty("nodeKind").enumValueIndex = (int)kind;
            serializedObject.FindProperty("maxWireDistance").floatValue = 10f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return node;
        }

        private static ItemDefinition CreatePlaceableDefinition(
            string itemDefId,
            string categoryPath,
            ItemFlags flags,
            PlacementMode placementMode,
            PlaceableKind kind,
            FrameworkPieceKind frameworkPieceKind,
            GameObject placedPrefab)
        {
            ItemDefinition itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            itemDefinition.name = itemDefId;

            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("itemDefId").FindPropertyRelative("value").stringValue = itemDefId;
            serializedObject.FindProperty("displayName").stringValue = itemDefId;
            serializedObject.FindProperty("flags").intValue = (int)flags;
            serializedObject.FindProperty("hasPlaceableProfile").boolValue = true;
            SetCategoryPath(serializedObject.FindProperty("categoryPath").FindPropertyRelative("segments"), categoryPath);

            SerializedProperty profile = serializedObject.FindProperty("placeableProfile");
            profile.FindPropertyRelative("placementMode").enumValueIndex = (int)placementMode;
            profile.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            profile.FindPropertyRelative("placedPrefab").objectReferenceValue = placementMode == PlacementMode.Wire ? null : placedPrefab;
            profile.FindPropertyRelative("previewPrefab").objectReferenceValue = placementMode == PlacementMode.Wire ? null : placedPrefab;
            profile.FindPropertyRelative("consumedItemQuantity").FindPropertyRelative("value").longValue = 1;
            profile.FindPropertyRelative("surfaceSnapMode").enumValueIndex = (int)PlacementSurfaceSnapMode.Optional;
            profile.FindPropertyRelative("frameworkPieceKind").enumValueIndex = (int)frameworkPieceKind;
            profile.FindPropertyRelative("validGroundLayers").intValue = ~0;
            profile.FindPropertyRelative("collisionRules").FindPropertyRelative("requireNoBlockingOverlap").boolValue = false;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return itemDefinition;
        }

        private static ItemDefinitionDatabase CreateDatabase(params ItemDefinition[] itemDefinitions)
        {
            ItemDefinitionDatabase database = ScriptableObject.CreateInstance<ItemDefinitionDatabase>();
            database.name = "BuildingPlacementSelfCheckDatabase";

            SerializedObject serializedObject = new SerializedObject(database);
            SerializedProperty definitionsProperty = serializedObject.FindProperty("itemDefinitions");
            definitionsProperty.arraySize = itemDefinitions.Length;
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                definitionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = itemDefinitions[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            database.RebuildLookup();
            return database;
        }

        private static void AssignSwitchNodes(ElectricalSwitch electricalSwitch, ElectricalNode inputNode, ElectricalNode outputNode)
        {
            SerializedObject serializedObject = new SerializedObject(electricalSwitch);
            serializedObject.FindProperty("inputNode").objectReferenceValue = inputNode;
            serializedObject.FindProperty("outputNode").objectReferenceValue = outputNode;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDiodeNodes(ElectricalDiode diode, ElectricalNode inputNode, ElectricalNode outputNode)
        {
            SerializedObject serializedObject = new SerializedObject(diode);
            serializedObject.FindProperty("inputNode").objectReferenceValue = inputNode;
            serializedObject.FindProperty("outputNode").objectReferenceValue = outputNode;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCategoryPath(SerializedProperty segmentsProperty, string categoryPath)
        {
            string[] segments = categoryPath.Split('>');
            segmentsProperty.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                segmentsProperty.GetArrayElementAtIndex(i).stringValue = segments[i].Trim();
            }
        }

        private static void DestroyTemporaryObjects(params Object[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private sealed class TestInventoryProvider : IPlayerInventoryStateProvider
        {
            public TestInventoryProvider(PlayerInventoryState inventoryState)
            {
                InventoryState = inventoryState;
            }

            public PlayerInventoryState InventoryState { get; }
        }
    }
}
