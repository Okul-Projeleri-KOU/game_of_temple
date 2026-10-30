using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
                    SyncEastStairsTilemapRegion();
                    SyncSpikyWallsPlacement();
                }
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
            }
        }

        /// <summary>
        /// Doğu terasına eklenen tüm dikenli duvarların (engeller_0, engeller_2, engeller_2 (1), engeller_4 vb.)
        /// mevcut (X, Y) konumlarını koruyarak:
        /// - Physics Layer'larını "Layer 1" yapar (karakterle çarpışabilmeleri için),
        /// - Eksik BoxCollider2D bileşenlerini ekler ve Y=180 rotasyonunu 2D uyumlu flipX'e çevirir,
        /// - Sorting Layer'larını "Layer 2" ve Order in Layer değerlerini koruyarak uçurum duvarlarının altında kalmalarını önler,
        /// - Muhafızın (muhafız - normal_0) konumunu sabitler.
        /// </summary>
        public static void SyncSpikyWallsPlacement()
        {
            bool modified = false;
            GameObject dirtyRef = null;

            int layer1Physics = LayerMask.NameToLayer("Layer 1");

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

                // 1. Mevcut (X, Y) konumunu koru, yalnızca Z != 0 ise Z = 0 yap
                Vector3 pos = wallObj.transform.position;
                if (Mathf.Abs(pos.z) > 0.001f)
                {
                    wallObj.transform.position = new Vector3(pos.x, pos.y, 0f);
                    modified = true;
                }

                // 2. Y ekseninde 180 derece döndürülmüşse (2D BoxCollider2D hatasını önlemek için) rotasyonu sıfırlayıp flipX aç
                Vector3 euler = wallObj.transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(euler.y, 180f)) < 15f)
                {
                    wallObj.transform.rotation = Quaternion.Euler(0f, 0f, euler.z);
                    sr.flipX = !sr.flipX;
                    modified = true;
                }

                // 3. Physics Layer: Doğu terasındaki karakter "Layer 1"de olduğu için duvarları da "Layer 1" yap
                if (layer1Physics != -1 && wallObj.layer != layer1Physics)
                {
                    wallObj.layer = layer1Physics;
                    modified = true;
                }

                // 4. Sorting Layer: Üst katman uçurum karolarının (Layer 2) altında kesilmemesi için "Layer 2" yap, mevcut Order'ı koru
                if (sr.sortingLayerName != "Layer 2")
                {
                    sr.sortingLayerName = "Layer 2";
                    modified = true;
                }
                if (sr.sortingOrder <= 0)
                {
                    sr.sortingOrder = 3;
                    modified = true;
                }

                // 5. Collider: Eksikse BoxCollider2D ekle ve katı engel (isTrigger = false) olduğundan emin ol
                BoxCollider2D boxCol = wallObj.GetComponent<BoxCollider2D>();
                if (boxCol == null)
                {
                    boxCol = wallObj.AddComponent<BoxCollider2D>();
                    if (sr.sprite != null)
                    {
                        boxCol.size = sr.sprite.bounds.size;
                        boxCol.offset = Vector2.zero;
                    }
                    modified = true;
                }
                if (boxCol.isTrigger)
                {
                    boxCol.isTrigger = false;
                    modified = true;
                }
            }

            GameObject guardObj = GameObject.Find("muhafız - normal_0");
            if (guardObj != null)
            {
                Vector3 targetGuardPos = new Vector3(5.32f, -6.48f, 5.320569f);
                if (Vector3.Distance(guardObj.transform.position, targetGuardPos) > 0.0001f)
                {
                    guardObj.transform.position = targetGuardPos;
                    dirtyRef = guardObj;
                    modified = true;
                }
            }

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

        /// <summary>
        /// Sağ taraftaki doğu merdiveni (PF Struct - Stairs E 02) yeni konumuna (y ≈ 2.07) taşındığında,
        /// merdivenin X konumunu 12'ye hizalar, bulunduğu (11, 1..2) noktasını merdiven geçiş bölgesi yapar
        /// ve eski (11, 4..5) boşluğunu duvarla kapatır.
        /// </summary>
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

            // 1. Layer 2 - Wall: Eski merdiven boşluğunu (11, 4..5) kapat, yeni merdiven bölgesini (11, 1..2) aç
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

                // (10, 1) karesinin görselini koruyup merdiven sol girişinde takılmayı önlemek için collider'ını kapat
                if (layer2Wall.GetColliderType(new Vector3Int(10, 1, 0)) != Tile.ColliderType.None)
                {
                    layer2Wall.SetColliderType(new Vector3Int(10, 1, 0), Tile.ColliderType.None);
                    modified = true;
                }

                RefreshTilemapCollider(layer2Wall);
            }

            // 2. Layer 2 - Grass: Yeni merdivenin üstünü (11, 1..2) temizle ki karakter Layer 1'de görünmez olmasın; eski yeri (11, 4..5) doldur
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

            // 3. Layer 1 - Grass: Yeni merdivenin altına (11, 1..2) zemin karolarını koy
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

            // 4. Layer 1 - Wall: Merdiven duvar kesiti karosunu (11, 4)'ten (11, 1)'e taşı ve collider ayarlarını güncelle
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

            // 5. Layer 1 - Wall Shadow: Gölge boşluğunu (12, 4)'ten (12, 1)'e taşı
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
