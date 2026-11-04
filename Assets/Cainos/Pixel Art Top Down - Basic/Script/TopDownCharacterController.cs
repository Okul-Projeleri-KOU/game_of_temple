using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class TopDownCharacterController : MonoBehaviour
    {
        public float speed;

        [Header("Karakter Görünümü")]
        public bool useNewCharacter = true;
        public float walkAnimFps = 9f;
        public float idleAnimFps = 3f;

        [Header("Yön Kareleri")]
        [SerializeField] private Sprite[] southIdleFrames;
        [SerializeField] private Sprite[] southWalkFrames;
        [SerializeField] private Sprite[] northIdleFrames;
        [SerializeField] private Sprite[] northWalkFrames;
        [SerializeField] private Sprite[] eastIdleFrames;
        [SerializeField] private Sprite[] eastWalkFrames;
        [SerializeField] private Sprite[] westIdleFrames;
        [SerializeField] private Sprite[] westWalkFrames;

        [Header("Yedek Karakter")]
        [SerializeField] private Sprite backupOriginalFrontSprite;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private int currentDirection = 0;
        private float animTimer = 0f;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitEditorCharacterSync()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying)
                {
                    EnsureKarakterTextureImportSettings();
                    TopDownCharacterController[] controllers = Object.FindObjectsByType<TopDownCharacterController>(FindObjectsSortMode.None);
                    foreach (TopDownCharacterController ctrl in controllers)
                    {
                        if (ctrl != null)
                        {
                            ctrl.EnsureCharacterSpritesLoaded();
                            ctrl.ApplyCharacterSkinMode(false, false);
                        }
                    }
                }
            };
        }

        private static void EnsureKarakterTextureImportSettings()
        {
            const string texPath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/karakter.png";
            UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(texPath) as UnityEditor.TextureImporter;
            if (importer == null)
                return;

            Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(texPath);
            bool needsReimport = Mathf.Abs(importer.spritePixelsPerUnit - 192f) > 1f;

            if (!needsReimport && subAssets != null)
            {
                foreach (Object obj in subAssets)
                {
                    if (obj is Sprite sp && sp.name == "karakter_1")
                    {
                        if (sp.pivot.y > 15f)
                        {
                            needsReimport = true;
                        }
                        break;
                    }
                }
            }

            if (needsReimport)
            {
                importer.spritePixelsPerUnit = 192f;
                UnityEditor.AssetDatabase.ImportAsset(texPath, UnityEditor.ImportAssetOptions.ForceUpdate);
            }
        }

        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && !Application.isPlaying)
                {
                    EnsureCharacterSpritesLoaded();
                    ApplyCharacterSkinMode(false, true);
                }
            };
        }
#endif

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureCharacterSpritesLoaded();
            ApplyCharacterSkinMode(false, false);
        }

        private void Start()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            EnsureCharacterSpritesLoaded();
            ApplyCharacterSkinMode(false, false);
        }

        private void Update()
        {
            if (DialogueManager.Instance != null &&
                (DialogueManager.Instance.IsGuardChatOpen || DialogueManager.Instance.IsVictoryScreenOpen || DialogueManager.Instance.IsStartScreenOpen || DialogueManager.Instance.IsPauseMenuOpen))
            {
                if (animator != null && animator.enabled)
                {
                    animator.SetBool("IsMoving", false);
                }
                Rigidbody2D rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                if (useNewCharacter)
                {
                    UpdateNewCharacterSprite(false);
                }
                return;
            }

            Vector2 dir = Vector2.zero;
            if (Input.GetKey(KeyCode.A))
            {
                dir.x = -1;
                currentDirection = 3;
            }
            else if (Input.GetKey(KeyCode.D))
            {
                dir.x = 1;
                currentDirection = 2;
            }

            if (Input.GetKey(KeyCode.W))
            {
                dir.y = 1;
                currentDirection = 1;
            }
            else if (Input.GetKey(KeyCode.S))
            {
                dir.y = -1;
                currentDirection = 0;
            }

            dir.Normalize();
            bool isMoving = dir.magnitude > 0.01f;

            if (useNewCharacter)
            {
                if (animator != null && animator.enabled)
                {
                    animator.enabled = false;
                }
                UpdateNewCharacterSprite(isMoving);
            }
            else
            {
                if (animator != null)
                {
                    if (!animator.enabled)
                        animator.enabled = true;
                    animator.SetInteger("Direction", currentDirection);
                    animator.SetBool("IsMoving", isMoving);
                }
            }

            GetComponent<Rigidbody2D>().linearVelocity = speed * dir;
        }

        private void UpdateNewCharacterSprite(bool isMoving)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return;

            if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
            {
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
            }
            if (spriteRenderer.flipX)
            {
                spriteRenderer.flipX = false;
            }

            Sprite[] activeFrames = GetDirectionalFrames(currentDirection, isMoving);
            if (activeFrames == null || activeFrames.Length == 0)
                return;

            float fps = isMoving ? walkAnimFps : idleAnimFps;
            animTimer += Time.deltaTime * Mathf.Max(1f, fps);
            int frameIndex = Mathf.FloorToInt(animTimer) % activeFrames.Length;

            Sprite nextSprite = activeFrames[frameIndex];
            if (nextSprite != null && spriteRenderer.sprite != nextSprite)
            {
                spriteRenderer.sprite = nextSprite;
            }
        }

        private Sprite[] GetDirectionalFrames(int dir, bool isMoving)
        {
            switch (dir)
            {
                case 1: // Kuzey (W - Arkadan)
                    return isMoving && HasValidSprites(northWalkFrames) ? northWalkFrames : northIdleFrames;
                case 2: // Doğu (D - Sağdan)
                    return isMoving && HasValidSprites(eastWalkFrames) ? eastWalkFrames : eastIdleFrames;
                case 3: // Batı (A - Soldan)
                    return isMoving && HasValidSprites(westWalkFrames) ? westWalkFrames : westIdleFrames;
                default: // Güney (S - Önden)
                    return isMoving && HasValidSprites(southWalkFrames) ? southWalkFrames : southIdleFrames;
            }
        }

        private static bool HasValidSprites(Sprite[] arr)
        {
            return arr != null && arr.Length > 0 && arr[0] != null;
        }

        public void ApplyCharacterSkinMode(bool isMoving, bool markDirty)
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return;

            if (useNewCharacter)
            {
                if (animator != null && animator.enabled)
                {
                    animator.enabled = false;
                }
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.flipX = false;

                if (HasValidSprites(southIdleFrames))
                {
                    spriteRenderer.sprite = southIdleFrames[0];
                }
            }
            else
            {
                if (animator != null && !animator.enabled)
                {
                    animator.enabled = true;
                }
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                spriteRenderer.size = new Vector2(0.65625f, 1.4f);

                if (backupOriginalFrontSprite != null)
                {
                    spriteRenderer.sprite = backupOriginalFrontSprite;
                }
            }

#if UNITY_EDITOR
            if (markDirty && !Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(spriteRenderer);
            }
#endif
        }

        public void EnsureCharacterSpritesLoaded()
        {
            Dictionary<string, Sprite> spriteMap = new Dictionary<string, Sprite>();

#if UNITY_EDITOR
            Object[] karakterAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Cainos/Pixel Art Top Down - Basic/Texture/karakter.png");
            if (karakterAssets != null)
            {
                foreach (Object obj in karakterAssets)
                {
                    if (obj is Sprite sp)
                    {
                        spriteMap[sp.name] = sp;
                    }
                }
            }

            if (backupOriginalFrontSprite == null)
            {
                Object[] oldPlayerAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Player.png");
                if (oldPlayerAssets != null)
                {
                    foreach (Object obj in oldPlayerAssets)
                    {
                        if (obj is Sprite sp && sp.name == "TX Player F")
                        {
                            backupOriginalFrontSprite = sp;
                            break;
                        }
                    }
                }
            }
#endif
            if (spriteMap.Count == 0 || backupOriginalFrontSprite == null)
            {
                Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (Sprite sp in allSprites)
                {
                    if (sp == null)
                        continue;
                    if (sp.name.StartsWith("karakter_"))
                        spriteMap[sp.name] = sp;
                    else if (backupOriginalFrontSprite == null && sp.name == "TX Player F")
                        backupOriginalFrontSprite = sp;
                }
            }

            if ( spriteMap.Count == 0)
                return;

            southIdleFrames = PickSprites(spriteMap, "karakter_0", "karakter_1", "karakter_2", "karakter_3");
            southWalkFrames = PickSprites(spriteMap, "karakter_5", "karakter_6", "karakter_7", "karakter_6");

            northIdleFrames = PickSprites(spriteMap, "karakter_11", "karakter_12", "karakter_13", "karakter_14");
            northWalkFrames = PickSprites(spriteMap, "karakter_16", "karakter_17", "karakter_18", "karakter_19", "karakter_21", "karakter_22");

            eastIdleFrames = PickSprites(spriteMap, "karakter_23", "karakter_24", "karakter_25", "karakter_26", "karakter_27");
            eastWalkFrames = PickSprites(spriteMap, "karakter_28", "karakter_29", "karakter_30", "karakter_31", "karakter_32", "karakter_33", "karakter_34");

            westIdleFrames = PickSprites(spriteMap, "karakter_35", "karakter_36", "karakter_37", "karakter_38");
            westWalkFrames = PickSprites(spriteMap, "karakter_40", "karakter_41", "karakter_42", "karakter_44");
        }

        private static Sprite[] PickSprites(Dictionary<string, Sprite> map, params string[] names)
        {
            List<Sprite> result = new List<Sprite>(names.Length);
            foreach (string n in names)
            {
                if (map.TryGetValue(n, out Sprite s) && s != null)
                {
                    result.Add(s);
                }
            }
            return result.ToArray();
        }

        [ContextMenu("Yeni Karaktere Geç (karakter.png)")]
        public void SwitchToNewCharacter()
        {
            useNewCharacter = true;
            EnsureCharacterSpritesLoaded();
            ApplyCharacterSkinMode(false, true);
        }

        [ContextMenu("Yedek Eski Karaktere Dön (TX Player.png)")]
        public void SwitchToBackupCharacter()
        {
            useNewCharacter = false;
            EnsureCharacterSpritesLoaded();
            ApplyCharacterSkinMode(false, true);
        }
    }
}
