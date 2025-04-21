using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

#if MONO
using HarmonyLib;
using TMPro;
using ScheduleOne;
using ScheduleOne.Quests;
using ScheduleOne.Product;
using ScheduleOne.PlayerScripts;
using System.Reflection;
#else
using Il2CppTMPro;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.PlayerScripts;
#endif

[assembly: MelonInfo(typeof(ProductSum.ProductSum), ProductSum.BuildInfo.Name, ProductSum.BuildInfo.Version, ProductSum.BuildInfo.Author, ProductSum.BuildInfo.DownloadLink)]
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
        public const string Version = "1.0";
        public const string DownloadLink = null;
    }

    public class ProductSum : MelonMod
    {
        private static MelonLogger.Instance MelonLogger { get; set; }
        private GameObject uiContainer;
        private TextMeshProUGUI tmpText;
        private static bool hasPlayerSpawned = false;
        private bool uiCreated = false;

#if MONO
        private static MethodInfo shouldShowJournalEntryMethod;
#endif

        public override void OnInitializeMelon()
        {
            MelonLogger = LoggerInstance;
            MelonLogger.Msg("ProductSum initialized!");

#if MONO
            shouldShowJournalEntryMethod = AccessTools.Method(typeof(Contract), "ShouldShowJournalEntry");
            if (shouldShowJournalEntryMethod == null)
                MelonLogger.Error("Failed to find method: Contract.ShouldShowJournalEntry()");
#endif
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

                if (uiContainer != null)
                    uiContainer.SetActive(false);
            }
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

            try
            {
                Dictionary<string, int> productMap = new Dictionary<string, int>();
                bool hasValidContracts = false;

                var contracts = Contract.Contracts;
                if (contracts != null && contracts.Count > 0)
                {
                    foreach (Contract contract in contracts)
                    {
                        bool shouldShow = false;

#if MONO
                        // In Mono, use reflection
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
                        // On IL2CPP, directly call the method
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

                        hasValidContracts = true;
                        ProductList productList = contract.ProductList;

                        if (productList != null && productList.entries != null)
                        {
                            foreach (ProductList.Entry entry in productList.entries)
                            {
                                var item = Registry.GetItem(entry.ProductID);
                                if (item != null)
                                {
                                    string name = item.Name;
                                    int quantity = entry.Quantity;

                                    if (productMap.ContainsKey(name))
                                        productMap[name] += quantity;
                                    else
                                        productMap[name] = quantity;
                                }
                            }
                        }
                    }

                    if (hasValidContracts && productMap.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("Summary:");
                        sb.AppendLine("---------------");

                        foreach (var kvp in productMap)
                        {
                            sb.AppendLine($"<b>{kvp.Value}</b>x <i>{kvp.Key}</i>");
                        }

                        tmpText.text = sb.ToString();

                        // resize ui
                        Canvas.ForceUpdateCanvases();
                        float preferredHeight = tmpText.preferredHeight + 20f;
                        var rectTransform = uiContainer.GetComponent<RectTransform>();
                        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, preferredHeight);

                        if (!uiContainer.activeSelf)
                            uiContainer.SetActive(true);
                    }
                    else if (uiContainer.activeSelf)
                    {
                        uiContainer.SetActive(false);
                    }
                }
                else if (uiContainer.activeSelf)
                {
                    uiContainer.SetActive(false);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error updating product list: {ex.Message}");
            }
        }
    }
}
