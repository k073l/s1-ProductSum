using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

#if MONO
using HarmonyLib;
using TMPro;
using ScheduleOne;
using ScheduleOne.Economy;
using ScheduleOne.Quests;
using ScheduleOne.Product;
using ScheduleOne.PlayerScripts;
using System.Reflection;
#else
using Il2CppTMPro;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.PlayerScripts;
#endif

[assembly:
    MelonInfo(typeof(ProductSum.ProductSum), ProductSum.BuildInfo.Name, ProductSum.BuildInfo.Version,
        ProductSum.BuildInfo.Author, ProductSum.BuildInfo.DownloadLink)]
[assembly: MelonColor(1, 255, 215, 0)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ProductSum
{
    public static class BuildInfo
    {
        public const string Name = "ProductSum";
        public const string Description = "sums your products duh";
        public const string Author = "k073l";
        public const string Company = null;
        public const string Version = "1.1";
        public const string DownloadLink = null;
    }

    public class ProductSum : MelonMod
    {
        private static MelonLogger.Instance MelonLogger { get; set; }
        private static MelonPreferences_Category Category;
        private static MelonPreferences_Entry<bool> AlwaysOn;

        private static MelonPreferences_Entry<string> Keybind;

        private static MelonPreferences_Entry<int> Timeout;

        private static MelonPreferences_Entry<bool> SplitByTimeSlot;
        private GameObject uiContainer;
        private TextMeshProUGUI tmpText;
        private static bool hasPlayerSpawned = false;
        private bool uiCreated = false;
        private bool timerActive = false;
        private float timerStartTime = 0f;

#if MONO
        private static MethodInfo shouldShowJournalEntryMethod;
#endif

        public override void OnInitializeMelon()
        {
            MelonLogger = LoggerInstance;
            MelonLogger.Msg("ProductSum initialized!");

            Category = MelonPreferences.CreateCategory("ProductSum", "ProductSum Settings");
            AlwaysOn = Category.CreateEntry("AlwaysOn", true,
                "When enabled, the product summary is always visible (when available)");
            SplitByTimeSlot = Category.CreateEntry("SplitByTimeSlot", true,
                "When enabled, the product summary is split by time slot");
            Keybind = Category.CreateEntry("Keybind", "P",
                "Key to temporarily show the product summary (when not in Always On mode)");
            Timeout = Category.CreateEntry("Timeout", 5,
                "How many seconds the product summary remains visible after pressing the keybind");

#if MONO
            shouldShowJournalEntryMethod = AccessTools.Method(typeof(Contract), "ShouldShowJournalEntry");
            if (shouldShowJournalEntryMethod == null)
                MelonLogger.Error("Failed to find method: Contract.ShouldShowJournalEntry()");
#endif
        }

        private IEnumerator WaitForPlayer()
        {
            while (Player.Local == null || Player.Local.gameObject == null)
                yield return null;

            if (!hasPlayerSpawned)
            {
                hasPlayerSpawned = true;
                MelonCoroutines.Start(CreateProductUI());
            }
            else if (uiCreated && uiContainer != null)
            {
                uiContainer.SetActive(true);
                // update UI after going back to main menu
                UpdateProductUI();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                MelonLogger.Msg("Main scene loaded, waiting for player...");
                MelonCoroutines.Start(WaitForPlayer());
            }
            else
            {
                hasPlayerSpawned = false;
                uiCreated = false;

                if (uiContainer != null)
                    uiContainer.SetActive(false);
                    GameObject.Destroy(uiContainer);
                uiContainer = null;
            }
        }

        private IEnumerator CreateProductUI()
        {
            yield return new WaitForSeconds(1f);

            if (Player.Local != null && Player.Local.gameObject != null && !uiCreated)
            {
                try
                {
                    GameObject hudParent = GameObject.Find("UI/HUD/");
                    if (hudParent == null)
                    {
                        MelonLogger.Error("Could not find HUD parent!");
                        yield break;
                    }

                    uiContainer = new GameObject("ProductSumContainer");
                    uiContainer.transform.SetParent(hudParent.transform, false);

                    RectTransform rectTransform = uiContainer.AddComponent<RectTransform>();
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(0, 1);
                    rectTransform.pivot = new Vector2(0, 1);
                    rectTransform.anchoredPosition = new Vector2(10, -40);
                    rectTransform.sizeDelta = new Vector2(200, 100);

                    GameObject bgGO = new GameObject("Background");
                    bgGO.transform.SetParent(uiContainer.transform, false);

                    Image bg = bgGO.AddComponent<Image>();
                    bg.color = new Color(0, 0, 0, 0.5f);

                    RectTransform bgRect = bg.GetComponent<RectTransform>();
                    bgRect.anchorMin = Vector2.zero;
                    bgRect.anchorMax = Vector2.one;
                    bgRect.offsetMin = Vector2.zero;
                    bgRect.offsetMax = Vector2.zero;

                    GameObject textChildGO = new GameObject("Text");
                    textChildGO.transform.SetParent(uiContainer.transform, false);

                    tmpText = textChildGO.AddComponent<TextMeshProUGUI>();
                    tmpText.alignment = TextAlignmentOptions.TopLeft;
                    tmpText.fontSize = 15;
                    tmpText.color = Color.white;
                    tmpText.text = "";

                    TextMeshProUGUI existingText = hudParent.GetComponentInChildren<TextMeshProUGUI>();
                    if (existingText != null && existingText.font != null)
                        tmpText.font = existingText.font;

                    RectTransform textRect = tmpText.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = new Vector2(5, 5);
                    textRect.offsetMax = new Vector2(-5, -5);

                    uiContainer.SetActive(false);
                    uiCreated = true;
                    MelonLogger.Msg("ProductSum UI created successfully!");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"Error creating UI: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        public override void OnUpdate()
        {
            if (!uiCreated || tmpText == null) return;

            if (!AlwaysOn.Value && Input.GetKeyDown(ParseKeybind(Keybind.Value)))
            {
                MelonLogger.Msg("Timer started");
                timerActive = true;
                timerStartTime = Time.time;
                UpdateProductUI();
            }

            if (timerActive && Time.time - timerStartTime >= Timeout.Value)
            {
                timerActive = false;
                if (!AlwaysOn.Value)
                    uiContainer.SetActive(false);
            }

            if (AlwaysOn.Value || timerActive || uiContainer.activeSelf)
            {
                UpdateProductUI();
            }
        }


        private void UpdateProductUI()
        {
            try
            {
                var productMap = new Dictionary<EDealWindow, Dictionary<string, int>>();
                bool hasValidContracts = false;
                int validContractCount = 0;

                var contracts = Contract.Contracts;
                if (contracts != null && contracts.Count > 0)
                {
                    foreach (Contract contract in contracts)
                    {
                        bool shouldShow = false;

#if MONO
                        if (shouldShowJournalEntryMethod != null)
                        {
                            try
                            {
                                shouldShow = (bool)shouldShowJournalEntryMethod.Invoke(contract, null);
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Error($"Reflection error: {ex.Message}");
                                continue;
                            }
                        }
#else
                        try
                        {
                            shouldShow = contract.ShouldShowJournalEntry();
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Error calling ShouldShowJournalEntry: {ex.Message}");
                            continue;
                        }
#endif

                        if (!shouldShow)
                            continue;

                        // count valid contracts
                        validContractCount++;
                        hasValidContracts = true;

                        ProductList productList = contract.ProductList;
                        QuestWindowConfig questWindowConfig = contract.DeliveryWindow;

                        if (productList?.entries != null)
                        {
                            foreach (var entry in productList.entries)
                            {
                                var item = Registry.GetItem(entry.ProductID);
                                if (item == null) continue;

                                string name = item.Name;
                                int quantity = entry.Quantity;

                                EDealWindow timeSlot = DealWindowInfo.GetWindow(questWindowConfig.WindowStartTime);
                                if (!productMap.ContainsKey(timeSlot))
                                    productMap[timeSlot] = new Dictionary<string, int>();

                                var slotDict = productMap[timeSlot];
                                if (slotDict.ContainsKey(name))
                                    slotDict[name] += quantity;
                                else
                                    slotDict[name] = quantity;
                            }
                        }
                    }

                    // at least one valid contract
                    if (hasValidContracts && validContractCount > 1)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("Summary:");
                        sb.AppendLine("---------------");

                        if (SplitByTimeSlot.Value)
                        {
                            foreach (var kvp in productMap)
                            {
                                string timeSlotLabel = Enum.GetName(typeof(EDealWindow), kvp.Key);
                                sb.AppendLine($"<u>{timeSlotLabel}</u>");
                                foreach (var item in kvp.Value)
                                    sb.AppendLine($"<b>{item.Value}</b>x <i>{item.Key}</i>");
                                sb.AppendLine();
                            }
                        }
                        else
                        {
                            var merged = new Dictionary<string, int>();
                            foreach (var slot in productMap.Values)
                            {
                                foreach (var item in slot)
                                {
                                    if (merged.ContainsKey(item.Key))
                                        merged[item.Key] += item.Value;
                                    else
                                        merged[item.Key] = item.Value;
                                }
                            }
                            foreach (var item in merged)
                                sb.AppendLine($"<b>{item.Value}</b>x <i>{item.Key}</i>");
                        }

                        tmpText.text = sb.ToString();

                        // resize UI
                        Canvas.ForceUpdateCanvases();
                        float preferredHeight = tmpText.preferredHeight + 20f;
                        var rectTransform = uiContainer.GetComponent<RectTransform>();
                        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, preferredHeight);

                        // toggle UI visibility
                        bool shouldShow = AlwaysOn.Value || timerActive;
                        if (shouldShow && !uiContainer.activeSelf)
                            uiContainer.SetActive(true);
                        else if (!shouldShow && uiContainer.activeSelf)
                            uiContainer.SetActive(false);
                    }
                    else if (uiContainer.activeSelf)
                    {
                        uiContainer.SetActive(false);
                        timerActive = false;
                    }
                }
                else if (uiContainer.activeSelf && !AlwaysOn.Value)
                {
                    uiContainer.SetActive(false);
                    timerActive = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error updating product list: {ex.Message}");
            }
        }


        private KeyCode ParseKeybind(string keybind)
        {
            if (System.Enum.TryParse<KeyCode>(keybind, out KeyCode result))
                return result;
            return KeyCode.P; // default to p
        }
    }
}