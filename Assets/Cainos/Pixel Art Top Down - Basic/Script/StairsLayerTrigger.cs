using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;

namespace Cainos.PixelArtTopDown_Basic
{
    //when object enter or exit the trigger, put it to the assigned layer and sorting layers base on the direction
    //used in the stairs objects for player to travel between layers

    public class StairsLayerTrigger : MonoBehaviour
    {
        public Direction direction;                                 //direction of the stairs
        [Space]
        public string layerUpper;
        public string sortingLayerUpper;
        [Space]
        public string layerLower;
        public string sortingLayerLower;

        private static int lastSyncedFrame = -1;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitEditorSync()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying)
                {
                    EnsureSceneView2DMode();
                }
                SyncEastStairsTilemapRegion();
                SyncSpikyWallsPlacement();
                SyncNavMesh2DSetup();
            };
        }

        private static void EnsureSceneView2DMode()
        {
            foreach (UnityEditor.SceneView sv in UnityEditor.SceneView.sceneViews)
            {
                if (sv != null && !sv.in2DMode)
                {
                    sv.in2DMode = true;
                    sv.Repaint();
                }
            }
        }
#endif

        private void Awake()
        {
            if (lastSyncedFrame != Time.frameCount)
            {
                lastSyncedFrame = Time.frameCount;
                SyncEastStairsTilemapRegion();
                SyncSpikyWallsPlacement();
                SyncNavMesh2DSetup();
            }
        }

        public static void SyncNavMesh2DSetup()
        {
            GameObject navMeshObj = GameObject.Find("NavMesh2D");
            if (navMeshObj == null)
                return;

            bool modified = false;

#if UNITY_EDITOR
            UnityEngine.Object[] navSettingsAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (navSettingsAssets != null && navSettingsAssets.Length > 0 && navSettingsAssets[0] != null)
            {
                UnityEditor.SerializedObject navSettingsSO = new UnityEditor.SerializedObject(navSettingsAssets[0]);
                UnityEditor.SerializedProperty settingsProp = navSettingsSO.FindProperty("m_Settings");
                if (settingsProp != null && settingsProp.isArray && settingsProp.arraySize > 0)
                {
                    UnityEditor.SerializedProperty agent0 = settingsProp.GetArrayElementAtIndex(0);
                    UnityEditor.SerializedProperty radiusProp = agent0.FindPropertyRelative("agentRadius");
                    UnityEditor.SerializedProperty heightProp = agent0.FindPropertyRelative("agentHeight");
                    UnityEditor.SerializedProperty minAreaProp = agent0.FindPropertyRelative("minRegionArea");
                    if (radiusProp != null && Mathf.Abs(radiusProp.floatValue - 0.18f) > 0.01f)
                    {
                        radiusProp.floatValue = 0.18f;
                        if (heightProp != null) heightProp.floatValue = 0.5f;
                        if (minAreaProp != null) minAreaProp.floatValue = 0.05f;
                        navSettingsSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
#endif

            Quaternion targetRot = Quaternion.Euler(-90f, 0f, 0f);
            if (Quaternion.Angle(navMeshObj.transform.rotation, targetRot) > 0.1f)
            {
                navMeshObj.transform.rotation = targetRot;
                modified = true;
            }

            Component[] allComps = navMeshObj.GetComponents<Component>();
            foreach (Component comp in allComps)
            {
                if (comp != null && comp.GetType().FullName == "Unity.AI.Navigation.NavMeshSurface")
                {
                    if (Application.isPlaying)
                        Object.Destroy(comp);
                    else
                        Object.DestroyImmediate(comp);
                    modified = true;
                }
            }

            NavMeshSurface surface2D = navMeshObj.GetComponent<NavMeshSurface>();
            if (surface2D == null)
            {
                surface2D = navMeshObj.AddComponent<NavMeshSurface>();
                modified = true;
            }

            CollectSources2d collect2D = navMeshObj.GetComponent<CollectSources2d>();
            if (collect2D != null && surface2D.NavMeshExtensions.Count == 0)
            {
                if (Application.isPlaying)
                    Object.Destroy(collect2D);
                else
                    Object.DestroyImmediate(collect2D);
                collect2D = null;
            }
            if (collect2D == null)
            {
                collect2D = navMeshObj.AddComponent<CollectSources2d>();
                modified = true;
            }

            if (surface2D.collectObjects != CollectObjects.All ||
                surface2D.useGeometry != NavMeshCollectGeometry.PhysicsColliders ||
                !surface2D.overrideVoxelSize ||
                Mathf.Abs(surface2D.voxelSize - 0.05f) > 0.005f ||
                Mathf.Abs(surface2D.minRegionArea - 0.05f) > 0.01f ||
                !surface2D.hideEditorLogs)
            {
                surface2D.collectObjects = CollectObjects.All;
                surface2D.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface2D.defaultArea = 0; // Walkable
                surface2D.overrideVoxelSize = true;
                surface2D.voxelSize = 0.05f;
                surface2D.minRegionArea = 0.05f;
                surface2D.hideEditorLogs = true;
                modified = true;
            }

            string[] groundTilemaps = { "Layer 1 - Grass", "Layer 2 - Grass", "Layer 3 - Grass" };
            foreach (string groundName in groundTilemaps)
            {
                GameObject groundObj = GameObject.Find(groundName);
                if (groundObj != null)
                {
                    if (EnsureModifier2D(groundObj, true, 0, false))
                        modified = true;
                }
            }

            string[] wallTilemaps = { "Layer 1 - Wall", "Layer 2 - Wall", "Layer 3 - Wall" };
            foreach (string wallName in wallTilemaps)
            {
                GameObject wallObj = GameObject.Find(wallName);
                if (wallObj != null)
                {
                    if (EnsureModifier2D(wallObj, true, 1, false))
                        modified = true;
                }
            }

            Collider2D[] allColliders2D = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            foreach (Collider2D col in allColliders2D)
            {
                if (col == null || col.gameObject == null)
                    continue;

                GameObject go = col.gameObject;
                string goName = go.name;

                if (go.GetComponent<Tilemap>() != null)
                    continue;

                if (col.isTrigger)
                {
                    if (EnsureModifier2D(go, false, 0, true))
                        modified = true;
                    continue;
                }

                bool isDynamicOrTarget =
                    goName.Contains("PF Player") ||
                    goName.Contains("Companion") ||
                    goName.Contains("GuardNPC") ||
                    goName.Contains("muhafız") ||
                    go.GetComponent<GuardDialogueTrigger>() != null ||
                    go.GetComponent<TreasureChest>() != null ||
                    goName.Contains("Chest") ||
                    go.GetComponent<StoneGate>() != null ||
                    goName.Contains("Gate") ||
                    go.GetComponent<RuneAltar>() != null ||
                    (go.GetComponent<Rigidbody2D>() != null && go.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic);

                if (isDynamicOrTarget)
                {
                    if (EnsureModifier2D(go, false, 0, true))
                        modified = true;
                    continue;
                }

                if (EnsureModifier2D(go, true, 1, false))
                    modified = true;
            }

            if (Configure2DNavMeshAgents())
            {
                modified = true;
            }

            if (surface2D.navMeshData == null)
            {
                surface2D.BuildNavMesh();
#if UNITY_EDITOR
                if (!Application.isPlaying && surface2D.navMeshData != null && !UnityEditor.EditorUtility.IsPersistent(surface2D.navMeshData))
                {
                    string dir = "Assets/Scenes/MainScene";
                    if (!System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    string assetPath = "Assets/Scenes/MainScene/NavMesh-NavMesh2D.asset";
                    UnityEditor.AssetDatabase.DeleteAsset(assetPath);
                    UnityEditor.AssetDatabase.CreateAsset(surface2D.navMeshData, assetPath);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
#endif
                modified = true;
            }

#if UNITY_EDITOR
            if (modified && !Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(surface2D);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(navMeshObj.scene);
            }
#endif
        }

        private static bool EnsureModifier2D(GameObject go, bool overrideArea, int area, bool ignoreFromBuild)
        {
            bool changed = false;

            Component[] comps = go.GetComponents<Component>();
            foreach (Component c in comps)
            {
                if (c != null && c.GetType().FullName == "Unity.AI.Navigation.NavMeshModifier")
                {
                    if (Application.isPlaying)
                        Object.Destroy(c);
                    else
                        Object.DestroyImmediate(c);
                    changed = true;
                }
            }

            NavMeshModifier mod = go.GetComponent<NavMeshModifier>();
            if (mod == null)
            {
                mod = go.AddComponent<NavMeshModifier>();
                changed = true;
            }

            if (mod.overrideArea != overrideArea ||
                mod.area != area ||
                mod.ignoreFromBuild != ignoreFromBuild)
            {
                mod.overrideArea = overrideArea;
                mod.area = area;
                mod.ignoreFromBuild = ignoreFromBuild;
                changed = true;
            }

            return changed;
        }

        public static bool Configure2DNavMeshAgents()
        {
            bool changed = false;

            GameObject playerObj = GameObject.Find("PF Player");
            Transform keyChestTransform = null;

            TreasureChest[] chests = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
            foreach (TreasureChest chest in chests)
            {
                if (chest != null && chest.transform.position.x > 10f)
                {
                    keyChestTransform = chest.transform;
                    break;
                }
            }
            if (keyChestTransform == null)
            {
                SpriteRenderer[] allSrs = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
                foreach (SpriteRenderer sr in allSrs)
                {
                    if (sr != null && sr.gameObject.name.Contains("Chest") && sr.transform.position.x > 10f)
                    {
                        keyChestTransform = sr.transform;
                        break;
                    }
                }
            }

            UnityEngine.AI.NavMeshAgent[] agents = Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
            foreach (UnityEngine.AI.NavMeshAgent agent in agents)
            {
                if (agent == null)
                    continue;

                GameObject go = agent.gameObject;

                if (agent.updateRotation)
                {
                    agent.updateRotation = false;
                    changed = true;
                }
                if (agent.updateUpAxis)
                {
                    agent.updateUpAxis = false;
                    changed = true;
                }
                if (Mathf.Abs(agent.baseOffset) > 0.001f)
                {
                    agent.baseOffset = 0f;
                    changed = true;
                }

                if (go.GetComponent<AgentOverride2d>() == null)
                {
                    go.AddComponent<AgentOverride2d>();
                    changed = true;
                }

                if (Quaternion.Angle(go.transform.rotation, Quaternion.identity) > 0.1f)
                {
                    go.transform.rotation = Quaternion.identity;
                    changed = true;
                }

                CompanionAgent companion = go.GetComponent<CompanionAgent>();
                if (companion != null || go.name.Contains("Companion"))
                {
                    Vector3 pos = go.transform.position;
                    if (Mathf.Abs(pos.z) > 0.01f)
                    {
                        go.transform.position = new Vector3(pos.x, pos.y, 0f);
                        changed = true;
                    }

                    if (agent.radius > 0.25f)
                    {
                        agent.radius = 0.2f;
                        changed = true;
                    }
                    if (agent.stoppingDistance > 1.2f)
                    {
                        agent.stoppingDistance = 1.0f;
                        changed = true;
                    }

                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        if (!sr.enabled)
                        {
                            sr.enabled = true;
                            changed = true;
                        }
                        if (sr.sortingLayerName != "Layer 3" || sr.sortingOrder < 50)
                        {
                            sr.sortingLayerName = "Layer 3";
                            sr.sortingOrder = 50;
                            changed = true;
                        }
                    }

                    if (companion != null)
                    {
                        if (companion.ajanMotoru == null)
                        {
                            companion.ajanMotoru = agent;
                            changed = true;
                        }
                        if (companion.oyuncuHedefi == null && playerObj != null)
                        {
                            companion.oyuncuHedefi = playerObj.transform;
                            changed = true;
                        }
                        if (companion.sandikHedefi == null && keyChestTransform != null)
                        {
                            companion.sandikHedefi = keyChestTransform;
                            changed = true;
                        }
                    }
                }

                GuardAgent guard = go.GetComponent<GuardAgent>();
                if (guard != null || go.name.Contains("GuardNPC"))
                {
                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    if (mr != null && mr.enabled)
                    {
                        mr.enabled = false;
                        changed = true;
                    }

                    if (GameObject.Find("Hedef") == null && agent.enabled)
                    {
                        agent.enabled = false;
                        changed = true;
                    }
                }

#if UNITY_EDITOR
                if (changed && !Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(go);
                }
#endif
            }

            GameObject guardSpriteObj = GameObject.Find("muhafız - normal_0");
            if (guardSpriteObj != null)
            {
                if (Quaternion.Angle(guardSpriteObj.transform.rotation, Quaternion.identity) > 0.1f)
                {
                    guardSpriteObj.transform.rotation = Quaternion.identity;
                    changed = true;
                }

                Vector3 gPos = guardSpriteObj.transform.position;
                if (Mathf.Abs(gPos.z) > 0.01f || Vector2.Distance(new Vector2(gPos.x, gPos.y), new Vector2(5.74f, -6.758f)) > 0.5f)
                {
                    guardSpriteObj.transform.position = new Vector3(5.74f, -6.758f, 0f);
                    changed = true;
                }

                SpriteRenderer gSr = guardSpriteObj.GetComponent<SpriteRenderer>();
                if (gSr != null)
                {
                    if (!gSr.enabled)
                    {
                        gSr.enabled = true;
                        changed = true;
                    }
                    if (gSr.sortingLayerName != "Layer 3" || gSr.sortingOrder < 20)
                    {
                        gSr.sortingLayerName = "Layer 3";
                        gSr.sortingOrder = 20;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        public static void SyncSpikyWallsPlacement()
        {
            bool modified = false;
            GameObject dirtyRef = null;

            int layer1Physics = LayerMask.NameToLayer("Layer 1");
            if (layer1Physics < 0)
                layer1Physics = 0;

            SpriteRenderer[] allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            foreach (SpriteRenderer sr in allRenderers)
            {
                if (sr == null || sr.gameObject == null)
                    continue;

                string objName = sr.gameObject.name;
                bool isEngeller = objName.StartsWith("engeller") ||
                                  (sr.sprite != null && sr.sprite.name.StartsWith("engeller"));

                if (!isEngeller)
                    continue;

                GameObject wallObj = sr.gameObject;
                dirtyRef = wallObj;

                Vector3 pos = wallObj.transform.position;
                if (Mathf.Abs(pos.z) > 0.001f)
                {
                    wallObj.transform.position = new Vector3(pos.x, pos.y, 0f);
                    modified = true;
                }

                if (wallObj.layer != layer1Physics)
                {
                    wallObj.layer = layer1Physics;
                    modified = true;
                }

                if (sr.sortingLayerName != "Layer 1")
                {
                    sr.sortingLayerName = "Layer 1";
                    modified = true;
                }

                BoxCollider2D boxCol = wallObj.GetComponent<BoxCollider2D>();
                if (boxCol != null && sr.sprite != null)
                {
                    bool wasArtificiallyExpanded =
                        (Vector2.Distance(boxCol.size, new Vector2(8.00f, 4.27f)) < 0.05f && Vector2.Distance(boxCol.offset, new Vector2(-1.10f, -0.55f)) < 0.05f) ||
                        (Vector2.Distance(boxCol.size, new Vector2(6.05f, 5.05f)) < 0.05f && Vector2.Distance(boxCol.offset, new Vector2(0f, -0.50f)) < 0.05f);

                    if (wasArtificiallyExpanded)
                    {
                        boxCol.size = sr.sprite.bounds.size;
                        boxCol.offset = Vector2.zero;
                        modified = true;
                    }
                }
            }

            string[] terraceStoneNames = { "PF Props - Stone 01", "PF Props - Stone 03" };
            foreach (string stoneName in terraceStoneNames)
            {
                GameObject stoneObj = GameObject.Find(stoneName);
                if (stoneObj != null && stoneObj.transform.position.x > 11.5f)
                {
                    if (stoneObj.layer != layer1Physics)
                    {
                        stoneObj.layer = layer1Physics;
                        dirtyRef = stoneObj;
                        modified = true;
                    }

                    BoxCollider2D stoneCol = stoneObj.GetComponent<BoxCollider2D>();
                    if (stoneCol != null &&
                        Vector2.Distance(stoneCol.size, new Vector2(0.75f, 0.55f)) < 0.05f &&
                        Vector2.Distance(stoneCol.offset, new Vector2(0f, 0.25f)) < 0.05f)
                    {
                        if (Application.isPlaying)
                            Object.Destroy(stoneCol);
                        else
                            Object.DestroyImmediate(stoneCol);
                        dirtyRef = stoneObj;
                        modified = true;
                    }
                }
            }

            Physics2D.SyncTransforms();

            GameObject brokenStairsDecal = GameObject.Find("TX Stairs Broken L L");
            if (brokenStairsDecal != null && Mathf.Abs(brokenStairsDecal.transform.position.z) > 0.001f)
            {
                Vector3 p = brokenStairsDecal.transform.position;
                brokenStairsDecal.transform.position = new Vector3(p.x, p.y, 0f);
                dirtyRef = brokenStairsDecal;
                modified = true;
            }

#if UNITY_EDITOR
            if (modified && !Application.isPlaying && dirtyRef != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(dirtyRef.scene);
            }
#endif
        }

        public static void SyncEastStairsTilemapRegion()
        {
            GameObject eastStairs = GameObject.Find("PF Struct - Stairs E 02");
            if (eastStairs == null || eastStairs.transform.position.y > 3.5f)
                return;

            bool modified = false;

            Vector3 stairsPos = eastStairs.transform.position;
            if (Mathf.Abs(stairsPos.x - 12f) > 0.001f || Mathf.Abs(stairsPos.z) > 0.001f)
            {
                eastStairs.transform.position = new Vector3(12f, stairsPos.y, 0f);
                modified = true;
            }

            Tilemap layer2Wall = FindTilemap("Layer 2 - Wall");
            Tilemap layer2Grass = FindTilemap("Layer 2 - Grass");
            Tilemap layer1Grass = FindTilemap("Layer 1 - Grass");
            Tilemap layer1Wall = FindTilemap("Layer 1 - Wall");
            Tilemap layer1Shadow = FindTilemap("Layer 1 - Wall Shadow");

            if (layer2Wall != null)
            {
                TileBase cliffWall2 = layer2Wall.GetTile(new Vector3Int(11, 3, 0));
                if (cliffWall2 != null)
                {
                    if (layer2Wall.GetTile(new Vector3Int(11, 4, 0)) != cliffWall2)
                    {
                        layer2Wall.SetTile(new Vector3Int(11, 4, 0), cliffWall2);
                        modified = true;
                    }
                    if (layer2Wall.GetTile(new Vector3Int(11, 5, 0)) != cliffWall2)
                    {
                        layer2Wall.SetTile(new Vector3Int(11, 5, 0), cliffWall2);
                        modified = true;
                    }
                }

                if (layer2Wall.GetTile(new Vector3Int(11, 1, 0)) != null)
                {
                    layer2Wall.SetTile(new Vector3Int(11, 1, 0), null);
                    modified = true;
                }
                if (layer2Wall.GetTile(new Vector3Int(11, 2, 0)) != null)
                {
                    layer2Wall.SetTile(new Vector3Int(11, 2, 0), null);
                    modified = true;
                }

                if (layer2Wall.GetColliderType(new Vector3Int(10, 1, 0)) != Tile.ColliderType.None)
                {
                    layer2Wall.SetColliderType(new Vector3Int(10, 1, 0), Tile.ColliderType.None);
                    modified = true;
                }

                RefreshTilemapCollider(layer2Wall);
            }

            if (layer2Grass != null)
            {
                TileBase cliffGrass2 = layer2Grass.GetTile(new Vector3Int(11, 3, 0));
                if (cliffGrass2 != null)
                {
                    if (layer2Grass.GetTile(new Vector3Int(11, 4, 0)) != cliffGrass2)
                    {
                        layer2Grass.SetTile(new Vector3Int(11, 4, 0), cliffGrass2);
                        modified = true;
                    }
                    if (layer2Grass.GetTile(new Vector3Int(11, 5, 0)) != cliffGrass2)
                    {
                        layer2Grass.SetTile(new Vector3Int(11, 5, 0), cliffGrass2);
                        modified = true;
                    }
                }

                if (layer2Grass.GetTile(new Vector3Int(11, 1, 0)) != null)
                {
                    layer2Grass.SetTile(new Vector3Int(11, 1, 0), null);
                    modified = true;
                }
                if (layer2Grass.GetTile(new Vector3Int(11, 2, 0)) != null)
                {
                    layer2Grass.SetTile(new Vector3Int(11, 2, 0), null);
                    modified = true;
                }
            }

            if (layer1Grass != null)
            {
                TileBase grassBottom = layer1Grass.GetTile(new Vector3Int(11, 4, 0));
                TileBase grassTop = layer1Grass.GetTile(new Vector3Int(11, 5, 0));

                if (grassBottom != null && layer1Grass.GetTile(new Vector3Int(11, 1, 0)) == null)
                {
                    layer1Grass.SetTile(new Vector3Int(11, 1, 0), grassBottom);
                    modified = true;
                }
                if (grassTop != null && layer1Grass.GetTile(new Vector3Int(11, 2, 0)) == null)
                {
                    layer1Grass.SetTile(new Vector3Int(11, 2, 0), grassTop);
                    modified = true;
                }
            }

            if (layer1Wall != null)
            {
                TileBase standardWall1 = layer1Wall.GetTile(new Vector3Int(11, 3, 0));
                Tile.ColliderType standardCollider1 = layer1Wall.GetColliderType(new Vector3Int(11, 3, 0));
                TileBase tileAt11_4 = layer1Wall.GetTile(new Vector3Int(11, 4, 0));

                if (standardWall1 != null && tileAt11_4 != null && tileAt11_4 != standardWall1)
                {
                    layer1Wall.SetTile(new Vector3Int(11, 1, 0), tileAt11_4);
                    layer1Wall.SetTile(new Vector3Int(11, 4, 0), standardWall1);
                    modified = true;
                }

                layer1Wall.SetColliderType(new Vector3Int(11, 4, 0), standardCollider1);
                layer1Wall.SetColliderType(new Vector3Int(11, 5, 0), standardCollider1);
                layer1Wall.SetColliderType(new Vector3Int(11, 1, 0), Tile.ColliderType.None);
                layer1Wall.SetColliderType(new Vector3Int(11, 2, 0), Tile.ColliderType.None);

                RefreshTilemapCollider(layer1Wall);
            }

            if (layer1Shadow != null)
            {
                TileBase shadowTile = layer1Shadow.GetTile(new Vector3Int(12, 3, 0));
                if (shadowTile != null && layer1Shadow.GetTile(new Vector3Int(12, 4, 0)) == null)
                {
                    layer1Shadow.SetTile(new Vector3Int(12, 4, 0), shadowTile);
                    modified = true;
                }
                if (layer1Shadow.GetTile(new Vector3Int(12, 1, 0)) != null)
                {
                    layer1Shadow.SetTile(new Vector3Int(12, 1, 0), null);
                    modified = true;
                }
            }

#if UNITY_EDITOR
            if (modified && !Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(eastStairs.scene);
            }
#endif
        }

        private static Tilemap FindTilemap(string objectName)
        {
            GameObject obj = GameObject.Find(objectName);
            return obj != null ? obj.GetComponent<Tilemap>() : null;
        }

        private static void RefreshTilemapCollider(Tilemap tilemap)
        {
            TilemapCollider2D tmc = tilemap.GetComponent<TilemapCollider2D>();
            if (tmc != null)
            {
                tmc.ProcessTilemapChanges();
            }

            CompositeCollider2D cc = tilemap.GetComponent<CompositeCollider2D>();
            if (cc != null)
            {
                cc.GenerateGeometry();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (direction == Direction.South && other.transform.position.y < transform.position.y) SetLayerAndSortingLayer(other.gameObject, layerUpper, sortingLayerUpper);
            else
            if (direction == Direction.West && other.transform.position.x < transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerUpper, sortingLayerUpper);
            else
            if (direction == Direction.East && other.transform.position.x > transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerUpper, sortingLayerUpper);

        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (direction == Direction.South && other.transform.position.y < transform.position.y) SetLayerAndSortingLayer(other.gameObject, layerLower, sortingLayerLower);
            else
            if (direction == Direction.West && other.transform.position.x < transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerLower, sortingLayerLower);
            else
            if (direction == Direction.East && other.transform.position.x > transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerLower, sortingLayerLower);
        }

        private void SetLayerAndSortingLayer( GameObject target, string layer, string sortingLayer )
        {
            target.layer = LayerMask.NameToLayer(layer);

            SpriteRenderer rootSr = target.GetComponent<SpriteRenderer>();
            if (rootSr != null)
            {
                rootSr.sortingLayerName = sortingLayer;
            }

            SpriteRenderer[] srs = target.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sr in srs)
            {
                sr.sortingLayerName = sortingLayer;
            }
        }

        public enum Direction
        {
            North,
            South,
            West,
            East
        }    
    }
}
