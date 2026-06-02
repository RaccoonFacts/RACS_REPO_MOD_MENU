using BepInEx;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;

namespace REPOMENU
{
    [BepInPlugin("com.raccoonfacts.repomenu", "Raccoon Mod Menu", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;

        private void Awake()
        {
            harmony = new Harmony("com.raccoonfacts.repomenu");
            harmony.PatchAll();
            gameObject.AddComponent<ModMenuBehaviour>();
            Logger.LogInfo("Raccoon Mod Menu loaded!");
        }
    }

    public class ModMenuBehaviour : MonoBehaviour
    {
        // ── Toggles ──────────────────────────────────────────────────────────
        public static bool godMode = false;
        public static bool speedHack = false;
        public static bool noclip = false;

        // ── UI state ─────────────────────────────────────────────────────────
        private bool menuOpen = false;
        private bool showCatalog = false;
        private bool showLevelItems = false;
        private bool showCrates = false;
        private bool showMorph = false;

        private string catalogSearch = "";
        private string levelItemSearch = "";
        private string morphSearch = "";

        private Vector2 catalogScroll;
        private Vector2 levelScroll;
        private Vector2 morphScroll;

        // ── Noclip ───────────────────────────────────────────────────────────
        private CharacterController _cc;
        private Rigidbody _playerRb;
        private bool _noclipWasActive = false;

        // ── Morph ────────────────────────────────────────────────────────────
        private GameObject _morphObj = null;
        private bool _isMorphed = false;
        private List<Renderer> _playerRends = new List<Renderer>();

        // ─────────────────────────────────────────────────────────────────────
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) menuOpen = !menuOpen;
            HandleSpeedHack();
            HandleNoclip();
            HandleMorphFollow();
        }

        private void HandleSpeedHack()
        {
            if (!speedHack || PlayerController.instance == null) return;
            PlayerController.instance.MoveSpeed = 5f;
            PlayerController.instance.SprintSpeed = 10f;
            PlayerController.instance.EnergyCurrent = PlayerController.instance.EnergyStart;
        }

        private void HandleNoclip()
        {
            if (PlayerController.instance == null) return;
            if (_cc == null) _cc = PlayerController.instance.GetComponent<CharacterController>();

            if (noclip)
            {
                if (_cc == null) _cc = PlayerController.instance.GetComponent<CharacterController>();
                if (_playerRb == null) _playerRb = PlayerController.instance.GetComponent<Rigidbody>();

                if (_cc != null) _cc.enabled = false;
                if (_playerRb != null)
                {
                    _playerRb.useGravity = false;
                    _playerRb.velocity = Vector3.zero;
                    _playerRb.angularVelocity = Vector3.zero;
                }

                float spd = 8f * (Input.GetKey(KeyCode.LeftShift) ? 3f : 1f);
                Vector3 move = Vector3.zero;
                Transform cam = Camera.main != null ? Camera.main.transform : PlayerController.instance.transform;
                if (Input.GetKey(KeyCode.W)) move += cam.forward;
                if (Input.GetKey(KeyCode.S)) move -= cam.forward;
                if (Input.GetKey(KeyCode.A)) move -= cam.right;
                if (Input.GetKey(KeyCode.D)) move += cam.right;
                if (Input.GetKey(KeyCode.E)) move += Vector3.up;
                if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
                PlayerController.instance.transform.position += move * spd * Time.deltaTime;
                _noclipWasActive = true;
            }
            else if (_noclipWasActive)
            {
                if (_cc != null) _cc.enabled = true;
                if (_playerRb != null) _playerRb.useGravity = true;
                _noclipWasActive = false;
            }
        }

        private void HandleMorphFollow()
        {
            if (!_isMorphed || _morphObj == null || PlayerController.instance == null) return;
            _morphObj.transform.position = PlayerController.instance.transform.position;
            _morphObj.transform.rotation = PlayerController.instance.transform.rotation;
        }

        // ── Spawn catalog item ────────────────────────────────────────────────
        private void SpawnCatalogItem(Item item)
        {
            if (PlayerController.instance == null) return;
            Vector3 pos = PlayerController.instance.transform.position
                        + PlayerController.instance.transform.forward * 2f;
            if (SemiFunc.IsMultiplayer())
                PhotonNetwork.InstantiateRoomObject(item.prefab.ResourcePath, pos, Quaternion.identity, 0, null);
            else
                UnityEngine.Object.Instantiate(item.prefab.Prefab, pos, Quaternion.identity);
        }

        // ── Spawn cosmetic crate ──────────────────────────────────────────────
        private void SpawnCrate(SemiFunc.Rarity rarity)
        {
            if (ValuableDirector.instance == null || PlayerController.instance == null) return;
            var setups = ValuableDirector.instance.cosmeticWorldObjectSetups;
            if (setups == null || (int)rarity >= setups.Count) return;

            var prefabRef = setups[(int)rarity].prefab;
            Vector3 pos = PlayerController.instance.transform.position
                        + PlayerController.instance.transform.forward * 2f;

            if (SemiFunc.IsMultiplayer())
                PhotonNetwork.InstantiateRoomObject(prefabRef.ResourcePath, pos, Quaternion.identity, 0, null);
            else
                UnityEngine.Object.Instantiate(prefabRef.Prefab, pos, Quaternion.identity);
        }

        // ── Teleport item to player ───────────────────────────────────────────
        private void TeleportValuableToMe(ValuableObject vo)
        {
            if (PlayerController.instance == null || vo == null) return;
            var rb = vo.GetComponent<Rigidbody>();
            if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            vo.transform.position = PlayerController.instance.transform.position
                                  + PlayerController.instance.transform.forward * 2f;
        }

        // ── Teleport player to item ───────────────────────────────────────────
        private void TeleportMeToValuable(ValuableObject vo)
        {
            if (PlayerController.instance == null || vo == null) return;
            PlayerController.instance.transform.position = vo.transform.position + Vector3.up * 1.5f;
        }

        // ── Morph ────────────────────────────────────────────────────────────
        private List<UnityEngine.Rendering.ShadowCastingMode> _playerRendShadows = new List<UnityEngine.Rendering.ShadowCastingMode>();
        private List<Light> _playerLights = new List<Light>();

        private void MorphIntoValuable(ValuableObject vo)
        {
            if (PlayerController.instance == null || vo == null) return;

            foreach (var l in _playerLights) if (l != null) l.enabled = true;
            _playerLights.Clear();

            if (FlashlightController.Instance != null)
            {
                Traverse t = Traverse.Create(FlashlightController.Instance);
                FlashlightController.Instance.enabled = true;
                t.Field("mesh").GetValue<MeshRenderer>()?.gameObject.SetActive(true);
                t.Field("meshShadows").GetValue<MeshRenderer>()?.gameObject.SetActive(true);
                t.Field("spotlight").GetValue<Light>()?.gameObject.SetActive(true);
                // Reset state to Hidden so it animates back in naturally
                t.Field("currentState").SetValue(0);
                t.Field("hideFlashlight").SetValue(false);
            }
            _playerRends.AddRange(PlayerController.instance.GetComponentsInChildren<Renderer>(true));
            foreach (var r in _playerRends)
            {
                _playerRendShadows.Add(r.shadowCastingMode);
                r.enabled = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Hide lights (flashlight etc)
            _playerLights.Clear();
            _playerLights.AddRange(PlayerController.instance.GetComponentsInChildren<Light>(true));
            foreach (var l in _playerLights) l.enabled = false;

            // Hide flashlight via its own controller
            if (FlashlightController.Instance != null)
            {
                Traverse t = Traverse.Create(FlashlightController.Instance);
                // Disable the whole component so Update() stops fighting us
                FlashlightController.Instance.enabled = false;
                // Manually kill all visual elements
                t.Field("mesh").GetValue<MeshRenderer>()?.gameObject.SetActive(false);
                t.Field("meshShadows").GetValue<MeshRenderer>()?.gameObject.SetActive(false);
                t.Field("spotlight").GetValue<Light>()?.gameObject.SetActive(false);
                var halo = t.Field("halo").GetValue<Behaviour>();
                if (halo != null) halo.enabled = false;
            }

            _morphObj = UnityEngine.Object.Instantiate(vo.gameObject,
                PlayerController.instance.transform.position, Quaternion.identity);

            foreach (var rb2 in _morphObj.GetComponentsInChildren<Rigidbody>())
            { rb2.isKinematic = true; rb2.useGravity = false; }
            foreach (var col in _morphObj.GetComponentsInChildren<Collider>())
                col.enabled = false;
            foreach (var mb in _morphObj.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;

            _isMorphed = true;
        }

        private void UnMorph()
        {
            if (_morphObj != null) { UnityEngine.Object.Destroy(_morphObj); _morphObj = null; }
            for (int i = 0; i < _playerRends.Count; i++)
            {
                if (_playerRends[i] == null) continue;
                _playerRends[i].enabled = true;
                if (i < _playerRendShadows.Count)
                    _playerRends[i].shadowCastingMode = _playerRendShadows[i];
            }
            _playerRends.Clear();
            _playerRendShadows.Clear();
            _isMorphed = false;
        }

        // ── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!menuOpen) return;

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

            float menuW = 290, menuH = 560;
            float menuX = (Screen.width / scale - menuW) / 2;
            float menuY = (Screen.height / scale - menuH) / 2;
            int btnW = 250, btnH = 45, pad = 10;

            GUI.Box(new Rect(menuX, menuY, menuW, menuH), "Raccoon Mod Menu");
            int row = 0;

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH),
                "God Mode: " + (godMode ? "ON" : "OFF")))
                godMode = !godMode;

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH),
                "Speed Hack: " + (speedHack ? "ON" : "OFF")))
            {
                speedHack = !speedHack;
                if (!speedHack && PlayerController.instance != null)
                {
                    PlayerController.instance.MoveSpeed = 0.5f;
                    PlayerController.instance.SprintSpeed = 1f;
                }
            }

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH),
                "Noclip: " + (noclip ? "ON" : "OFF")))
                noclip = !noclip;

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH), "Add $10,000"))
                if (StatsManager.instance != null)
                    StatsManager.instance.runStats["currency"] += 10000;

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH), "Full Heal"))
                if (PlayerAvatar.instance != null)
                    PlayerAvatar.instance.playerHealth.Heal(100, false);

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH),
                "Spawn from Catalog: " + (showCatalog ? "ON" : "OFF")))
                showCatalog = !showCatalog;

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH),
                "Cosmetic Crates: " + (showCrates ? "ON" : "OFF")))
                showCrates = !showCrates;

            if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH),
                "Level Items: " + (showLevelItems ? "ON" : "OFF")))
                showLevelItems = !showLevelItems;

            if (_isMorphed)
            {
                if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH), "Un-Morph"))
                    UnMorph();
            }
            else
            {
                if (GUI.Button(new Rect(menuX + 20, menuY + 50 + row++ * (btnH + pad), btnW, btnH),
                    "Morph Into Item: " + (showMorph ? "ON" : "OFF")))
                    showMorph = !showMorph;
            }

            float px = menuX + menuW + 10;
            if (showCatalog) { DrawCatalogPanel(px, menuY); px += 320; }
            if (showCrates) { DrawCratesPanel(px, menuY); px += 220; }
            if (showLevelItems) { DrawLevelItemsPanel(px, menuY); px += 370; }
            if (showMorph && !_isMorphed) DrawMorphPanel(px, menuY);
        }

        // ── Catalog panel ─────────────────────────────────────────────────────
        private void DrawCatalogPanel(float x, float y)
        {
            if (StatsManager.instance == null) return;
            GUI.Box(new Rect(x, y, 300, 420), "Spawn from Catalog");
            catalogSearch = GUI.TextField(new Rect(x + 10, y + 30, 280, 30), catalogSearch);

            int visible = 0;
            foreach (var kvp in StatsManager.instance.itemDictionary)
                if (MatchSearch(kvp.Key, catalogSearch)) visible++;

            catalogScroll = GUI.BeginScrollView(
                new Rect(x + 10, y + 70, 280, 340), catalogScroll,
                new Rect(0, 0, 260, visible * 35));
            int i = 0;
            foreach (var kvp in StatsManager.instance.itemDictionary)
            {
                if (!MatchSearch(kvp.Key, catalogSearch)) continue;
                if (GUI.Button(new Rect(0, i * 35, 260, 30), kvp.Key))
                    SpawnCatalogItem(kvp.Value);
                i++;
            }
            GUI.EndScrollView();
        }

        // ── Cosmetic crates panel ─────────────────────────────────────────────
        private void DrawCratesPanel(float x, float y)
        {
            if (ValuableDirector.instance == null) return;
            GUI.Box(new Rect(x, y, 200, 235), "Cosmetic Crates");

            string[] labels = {
                "Common (Green)",
                "Uncommon (Cyan)",
                "Rare (Purple)",
                "Ultra-Rare (Yellow)"
            };

            for (int i = 0; i < labels.Length; i++)
            {
                if (GUI.Button(new Rect(x + 10, y + 35 + i * 48, 180, 38), labels[i]))
                    SpawnCrate((SemiFunc.Rarity)i);
            }
        }

        // ── Level items panel ─────────────────────────────────────────────────
        private void DrawLevelItemsPanel(float x, float y)
        {
            if (ValuableDirector.instance == null) return;
            var list = ValuableDirector.instance.valuableList;

            GUI.Box(new Rect(x, y, 350, 420), "Level Items");
            levelItemSearch = GUI.TextField(new Rect(x + 10, y + 30, 330, 30), levelItemSearch);
            GUI.Label(new Rect(x + 10, y + 62, 330, 20), "[ Fetch ] = item to you   [ Go ] = you to item");

            int visible = 0;
            foreach (var vo in list)
                if (vo != null && MatchSearch(vo.gameObject.name, levelItemSearch)) visible++;

            levelScroll = GUI.BeginScrollView(
                new Rect(x + 10, y + 85, 330, 325), levelScroll,
                new Rect(0, 0, 310, visible * 38));
            int i = 0;
            foreach (var vo in list)
            {
                if (vo == null || !MatchSearch(vo.gameObject.name, levelItemSearch)) continue;
                float rowY = i * 38;
                float dollarVal = Traverse.Create(vo).Field("dollarValueCurrent").GetValue<float>();
                string label = vo.gameObject.name.Replace("(Clone)", "").Trim()
                             + "  $" + Mathf.RoundToInt(dollarVal);

                GUI.Label(new Rect(0, rowY, 170, 32), label);
                if (GUI.Button(new Rect(175, rowY, 60, 30), "Fetch")) TeleportValuableToMe(vo);
                if (GUI.Button(new Rect(242, rowY, 60, 30), "Go")) TeleportMeToValuable(vo);
                i++;
            }
            GUI.EndScrollView();
        }

        // ── Morph panel ───────────────────────────────────────────────────────
        private void DrawMorphPanel(float x, float y)
        {
            if (ValuableDirector.instance == null) return;
            var list = ValuableDirector.instance.valuableList;

            GUI.Box(new Rect(x, y, 300, 420), "Morph Picker");
            morphSearch = GUI.TextField(new Rect(x + 10, y + 30, 280, 30), morphSearch);

            int visible = 0;
            foreach (var vo in list)
                if (vo != null && MatchSearch(vo.gameObject.name, morphSearch)) visible++;

            morphScroll = GUI.BeginScrollView(
                new Rect(x + 10, y + 70, 280, 340), morphScroll,
                new Rect(0, 0, 260, visible * 35));
            int i = 0;
            foreach (var vo in list)
            {
                if (vo == null || !MatchSearch(vo.gameObject.name, morphSearch)) continue;
                string label = vo.gameObject.name.Replace("(Clone)", "").Trim();
                if (GUI.Button(new Rect(0, i * 35, 260, 30), label))
                    MorphIntoValuable(vo);
                i++;
            }
            GUI.EndScrollView();
        }

        private static bool MatchSearch(string name, string search) =>
            search == "" || name.ToLower().Contains(search.ToLower());
    }

    // ── Patches ───────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(PlayerHealth), "Hurt")]
    public class GodModePatch
    {
        static bool Prefix() => !ModMenuBehaviour.godMode;
    }

    [HarmonyPatch(typeof(ModdedCheck), "IsModded")]
    public class ModdedCheckPatch
    {
        static bool Prefix(ref bool __result) { __result = false; return false; }
    }
}