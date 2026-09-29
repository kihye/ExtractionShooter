using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameUiPresentationFixer
{
    public const string PretendardFontPath = "Assets/_Resources/Fonts/Pretendard-Medium SDF.asset";

    private const string BaseScenePath = "Assets/_Game/Scenes/BaseScene.unity";
    private const string PrototypeScenePath = "Assets/_Game/Scenes/PrototypeScene.unity";
    private const string ShopCatalogPath = "Assets/_Game/Data/Shop/BaseShopCatalog.asset";
    private const string SessionStatePath = "Assets/_Game/Data/Runtime/GameSessionState.asset";

    private static readonly Color WindowColor = new Color(0.05f, 0.06f, 0.07f, 0.94f);
    private static readonly Color PanelColor = new Color(0.085f, 0.095f, 0.11f, 0.96f);
    private static readonly Color HeaderColor = new Color(0.13f, 0.16f, 0.19f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.23f, 0.28f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.68f, 0.74f, 0.80f, 1f);

    [MenuItem("Tools/Extraction Shooter/Fix Shop UI And Game Fonts")]
    public static void FixShopUiAndGameFonts()
    {
        TMP_FontAsset font = LoadPretendardFont();
        if (font == null)
        {
            Debug.LogError($"Missing TMP font asset at {PretendardFontPath}.");
            return;
        }

        int sceneTextCount = 0;
        sceneTextCount += FixScene(PrototypeScenePath, font, false);
        sceneTextCount += FixScene(BaseScenePath, font, true);
        int prefabTextCount = FixPrefabFonts(font);
        bool settingsChanged = FixTmpSettings(font);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Shop UI and font presentation fix completed. Scene TMP texts: {sceneTextCount}, prefab TMP texts: {prefabTextCount}, TMP Settings changed: {settingsChanged}.");
    }

    [MenuItem("Tools/Extraction Shooter/Fix Shop UI Layout")]
    public static void FixShopUiLayoutOnly()
    {
        TMP_FontAsset font = LoadPretendardFont();
        if (font == null)
        {
            Debug.LogError($"Missing TMP font asset at {PretendardFontPath}.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
        FixBaseShopScene(scene, font);
        ApplyFontToScene(scene, font);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Shop UI layout fix completed.");
    }

    public static TMP_FontAsset LoadPretendardFont()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardFontPath);
    }

    public static bool ApplyFontToText(TMP_Text text, TMP_FontAsset font)
    {
        if (text == null || font == null)
        {
            return false;
        }

        bool changed = false;
        if (text.font != font)
        {
            text.font = font;
            changed = true;
        }

        if (font.material != null && text.fontSharedMaterial != font.material)
        {
            text.fontSharedMaterial = font.material;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(text);
        }

        return changed;
    }

    private static int FixScene(string scenePath, TMP_FontAsset font, bool fixBaseShop)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int changedTextCount = ApplyFontToScene(scene, font);

        if (fixBaseShop)
        {
            FixBaseShopScene(scene, font);
            changedTextCount = ApplyFontToScene(scene, font);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return changedTextCount;
    }

    private static int ApplyFontToScene(Scene scene, TMP_FontAsset font)
    {
        int changedCount = 0;
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || text.gameObject.scene != scene)
            {
                continue;
            }

            if (ApplyFontToText(text, font))
            {
                changedCount++;
            }
        }

        return changedCount;
    }

    private static int FixPrefabFonts(TMP_FontAsset font)
    {
        int changedTextCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool prefabChanged = false;

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (ApplyFontToText(text, font))
                {
                    changedTextCount++;
                    prefabChanged = true;
                }
            }

            if (prefabChanged)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        return changedTextCount;
    }

    private static bool FixTmpSettings(TMP_FontAsset font)
    {
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
        if (settings == null)
        {
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(settings);
        SerializedProperty defaultFontAsset = serializedObject.FindProperty("m_defaultFontAsset");
        if (defaultFontAsset == null || defaultFontAsset.objectReferenceValue == font)
        {
            return false;
        }

        defaultFontAsset.objectReferenceValue = font;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return true;
    }

    private static void FixBaseShopScene(Scene scene, TMP_FontAsset font)
    {
        DisableLegacyBaseEconomyUi(scene);

        ShopGuiController shopGui = FindSceneComponent<ShopGuiController>(scene);
        if (shopGui == null)
        {
            Debug.LogError("BaseScene is missing ShopGuiController.");
            return;
        }

        ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopCatalogPath);
        GameSessionState sessionState = AssetDatabase.LoadAssetAtPath<GameSessionState>(SessionStatePath);
        BuildShopWindow(shopGui, catalog, sessionState, font);
    }

    private static void DisableLegacyBaseEconomyUi(Scene scene)
    {
        foreach (BaseEconomyUI economyUi in Resources.FindObjectsOfTypeAll<BaseEconomyUI>())
        {
            if (economyUi == null || economyUi.gameObject.scene != scene)
            {
                continue;
            }

            economyUi.gameObject.SetActive(false);
            EditorUtility.SetDirty(economyUi.gameObject);
            EditorUtility.SetDirty(economyUi);
        }
    }

    private static void BuildShopWindow(ShopGuiController shopGui, ShopCatalog catalog, GameSessionState sessionState, TMP_FontAsset font)
    {
        RectTransform root = EnsureRect(shopGui.gameObject);
        SetStretch(root);
        shopGui.gameObject.SetActive(true);
        SetUiLayerRecursive(shopGui.gameObject);

        RectTransform panel = EnsureChildRect(root, "ShopWindow");
        panel.SetAsLastSibling();
        SetAnchored(panel, Vector2.zero, new Vector2(1040f, 670f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        DisableLegacyShopChild(panel, "Title");
        DisableLegacyShopChild(panel, "ShopListText");
        DisableLegacyShopChild(panel, "PlayerInventoryText");

        TMP_Text titleText = EnsureText(panel, "ShopTitleText", "Base Trader", 30f, TextAlignmentOptions.MidlineLeft, TextColor, font);
        SetAnchored(titleText.rectTransform, new Vector2(28f, -30f), new Vector2(540f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

        TMP_Text creditsText = EnsureText(panel, "CreditsText", "Credits: 0", 21f, TextAlignmentOptions.MidlineRight, TextColor, font);
        SetAnchored(creditsText.rectTransform, new Vector2(-156f, -30f), new Vector2(330f, 42f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));

        Button closeButton = EnsureButton(panel, "CloseButton", "X", new Vector2(-42f, -30f), new Vector2(44f, 34f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), font);

        Button buyTabButton = EnsureButton(panel, "BuyTabButton", "구매", new Vector2(90f, -91f), new Vector2(124f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), font);
        Button sellTabButton = EnsureButton(panel, "SellTabButton", "판매", new Vector2(222f, -91f), new Vector2(124f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), font);

        ListViewParts buyList = EnsureList(panel, "BuyList", "구매 가능한 품목이 없습니다.", new Vector2(28f, -142f), new Vector2(500f, 398f), font);
        ListViewParts sellList = EnsureList(panel, "SellList", "판매 가능한 아이템이 없습니다.", new Vector2(28f, -142f), new Vector2(500f, 398f), font);

        List<ShopTradeRowView> buyRows = EnsureRowPool(buyList.Content, "BuyRow", 12, font);
        List<ShopTradeRowView> sellRows = EnsureRowPool(sellList.Content, "SellRow", 12, font);

        RectTransform detailPanel = EnsureChildRect(panel, "TradeDetailPanel");
        SetAnchored(detailPanel, new Vector2(-28f, -142f), new Vector2(485f, 398f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        EnsureImage(detailPanel.gameObject, PanelColor);

        TMP_Text detailHeader = EnsureText(detailPanel, "DetailHeaderText", "거래 상세", 18f, TextAlignmentOptions.Left, MutedTextColor, font);
        SetAnchored(detailHeader.rectTransform, new Vector2(22f, -18f), new Vector2(420f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        TMP_Text selectedItemText = EnsureText(detailPanel, "SelectedItemText", "선택된 품목 없음", 25f, TextAlignmentOptions.Left, TextColor, font);
        SetAnchored(selectedItemText.rectTransform, new Vector2(22f, -58f), new Vector2(430f, 78f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        selectedItemText.alignment = TextAlignmentOptions.TopLeft;
        selectedItemText.textWrappingMode = TextWrappingModes.Normal;
        selectedItemText.overflowMode = TextOverflowModes.Truncate;

        TMP_Text quantityLabel = EnsureText(detailPanel, "QuantityLabel", "수량", 17f, TextAlignmentOptions.MidlineLeft, MutedTextColor, font);
        SetAnchored(quantityLabel.rectTransform, new Vector2(22f, -162f), new Vector2(160f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

        Button decreaseButton = EnsureButton(detailPanel, "DecreaseQuantityButton", "-", new Vector2(52f, -214f), new Vector2(52f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), font);
        TMP_Text quantityText = EnsureText(detailPanel, "QuantityText", "1", 24f, TextAlignmentOptions.Center, TextColor, font);
        SetAnchored(quantityText.rectTransform, new Vector2(140f, -214f), new Vector2(112f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
        Button increaseButton = EnsureButton(detailPanel, "IncreaseQuantityButton", "+", new Vector2(228f, -214f), new Vector2(52f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), font);

        TMP_Text totalPriceText = EnsureText(detailPanel, "TotalPriceText", "합계: 0 Credits", 19f, TextAlignmentOptions.MidlineLeft, TextColor, font);
        SetAnchored(totalPriceText.rectTransform, new Vector2(22f, -282f), new Vector2(430f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

        Button confirmButton = EnsureButton(detailPanel, "ConfirmTradeButton", "구매", new Vector2(102f, -346f), new Vector2(160f, 46f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), font);
        TMP_Text confirmButtonText = confirmButton.GetComponentInChildren<TMP_Text>(true);

        TMP_Text resultText = EnsureText(panel, "TradeResultText", "", 18f, TextAlignmentOptions.TopLeft, MutedTextColor, font);
        SetAnchored(resultText.rectTransform, new Vector2(28f, -572f), new Vector2(984f, 58f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        resultText.textWrappingMode = TextWrappingModes.Normal;
        resultText.overflowMode = TextOverflowModes.Truncate;

        buyList.Root.gameObject.SetActive(true);
        sellList.Root.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(shopGui);
        SetProperty(serializedObject, "playerInventory", FindSceneComponent<PlayerInventory>(shopGui.gameObject.scene));
        SetProperty(serializedObject, "uiModeController", FindSceneComponent<GameplayUiModeController>(shopGui.gameObject.scene));
        SetProperty(serializedObject, "sessionState", sessionState);
        SetProperty(serializedObject, "shopCatalog", catalog);
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "titleText", titleText);
        SetProperty(serializedObject, "creditsText", creditsText);
        SetProperty(serializedObject, "selectedItemText", selectedItemText);
        SetProperty(serializedObject, "quantityText", quantityText);
        SetProperty(serializedObject, "totalPriceText", totalPriceText);
        SetProperty(serializedObject, "resultText", resultText);
        SetProperty(serializedObject, "buyTabButton", buyTabButton);
        SetProperty(serializedObject, "sellTabButton", sellTabButton);
        SetProperty(serializedObject, "decreaseQuantityButton", decreaseButton);
        SetProperty(serializedObject, "increaseQuantityButton", increaseButton);
        SetProperty(serializedObject, "confirmButton", confirmButton);
        SetProperty(serializedObject, "confirmButtonText", confirmButtonText);
        SetProperty(serializedObject, "closeButton", closeButton);
        SetProperty(serializedObject, "buyListContainer", buyList.Root);
        SetProperty(serializedObject, "sellListContainer", sellList.Root);
        SetProperty(serializedObject, "buyEmptyText", buyList.EmptyText);
        SetProperty(serializedObject, "sellEmptyText", sellList.EmptyText);
        SetProperty(serializedObject, "shopListText", panel.Find("ShopListText")?.GetComponent<TMP_Text>());
        SetProperty(serializedObject, "playerInventoryText", panel.Find("PlayerInventoryText")?.GetComponent<TMP_Text>());
        SetList(serializedObject.FindProperty("buyRowPool"), buyRows);
        SetList(serializedObject.FindProperty("sellRowPool"), sellRows);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shopGui);
    }

    private static ListViewParts EnsureList(RectTransform parent, string name, string emptyMessage, Vector2 anchoredPosition, Vector2 size, TMP_FontAsset font)
    {
        RectTransform listRoot = EnsureChildRect(parent, name);
        SetAnchored(listRoot, anchoredPosition, size, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(listRoot.gameObject, PanelColor);

        ScrollRect scrollRect = EnsureComponent<ScrollRect>(listRoot.gameObject);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        RectTransform header = EnsureChildRect(listRoot, "ListHeader");
        SetAnchored(header, new Vector2(0f, 0f), new Vector2(0f, 38f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        EnsureImage(header.gameObject, HeaderColor);

        TMP_Text headerText = EnsureText(header, "HeaderText", name == "BuyList" ? "품목" : "소지품", 16f, TextAlignmentOptions.MidlineLeft, MutedTextColor, font);
        SetStretch(headerText.rectTransform, new Vector2(14f, 6f), new Vector2(-14f, -6f));

        RectTransform viewport = EnsureChildRect(listRoot, "Viewport");
        SetStretch(viewport, new Vector2(8f, 8f), new Vector2(-8f, -46f));
        EnsureImage(viewport.gameObject, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
        EnsureComponent<Mask>(viewport.gameObject).showMaskGraphic = false;

        RectTransform content = EnsureChildRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(content.gameObject);
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text emptyText = EnsureText(viewport, "EmptyText", emptyMessage, 17f, TextAlignmentOptions.Center, MutedTextColor, font);
        SetStretch(emptyText.rectTransform, new Vector2(18f, 18f), new Vector2(-18f, -18f));
        emptyText.textWrappingMode = TextWrappingModes.Normal;
        emptyText.overflowMode = TextOverflowModes.Truncate;
        emptyText.gameObject.SetActive(false);
        emptyText.transform.SetAsLastSibling();

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        return new ListViewParts(listRoot, content, emptyText);
    }

    private static List<ShopTradeRowView> EnsureRowPool(RectTransform parent, string prefix, int count, TMP_FontAsset font)
    {
        List<ShopTradeRowView> rows = new List<ShopTradeRowView>(count);
        for (int i = 0; i < count; i++)
        {
            RectTransform rowRect = EnsureChildRect(parent, $"{prefix}_{i:00}");
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 62f);

            LayoutElement layoutElement = EnsureComponent<LayoutElement>(rowRect.gameObject);
            layoutElement.preferredHeight = 62f;
            layoutElement.minHeight = 62f;

            Image background = EnsureImage(rowRect.gameObject, ButtonColor);
            Button button = EnsureComponent<Button>(rowRect.gameObject);
            button.targetGraphic = background;

            RectTransform icon = EnsureChildRect(rowRect, "Icon");
            SetAnchored(icon, new Vector2(31f, 0f), new Vector2(38f, 38f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
            EnsureImage(icon.gameObject, new Color(0.35f, 0.72f, 0.82f, 0.92f));

            TMP_Text nameText = EnsureText(rowRect, "NameText", "", 16f, TextAlignmentOptions.Left, TextColor, font);
            SetAnchored(nameText.rectTransform, new Vector2(62f, -8f), new Vector2(240f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text detailText = EnsureText(rowRect, "DetailText", "", 13f, TextAlignmentOptions.MidlineRight, MutedTextColor, font);
            SetAnchored(detailText.rectTransform, new Vector2(-232f, 0f), new Vector2(72f, 36f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            detailText.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text priceText = EnsureText(rowRect, "PriceText", "", 15f, TextAlignmentOptions.MidlineRight, TextColor, font);
            SetAnchored(priceText.rectTransform, new Vector2(-18f, 0f), new Vector2(150f, 36f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            priceText.overflowMode = TextOverflowModes.Ellipsis;

            ShopTradeRowView row = EnsureComponent<ShopTradeRowView>(rowRect.gameObject);
            SerializedObject serializedObject = new SerializedObject(row);
            SetProperty(serializedObject, "button", button);
            SetProperty(serializedObject, "iconImage", icon.GetComponent<Image>());
            SetProperty(serializedObject, "nameText", nameText);
            SetProperty(serializedObject, "detailText", detailText);
            SetProperty(serializedObject, "priceText", priceText);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            row.gameObject.SetActive(false);
            rows.Add(row);
        }

        return rows;
    }

    private static void DisableLegacyShopChild(RectTransform panel, string childName)
    {
        Transform child = panel.Find(childName);
        if (child == null)
        {
            return;
        }

        child.gameObject.SetActive(false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.text = "";
            EditorUtility.SetDirty(text);
        }

        EditorUtility.SetDirty(child.gameObject);
    }

    private static RectTransform EnsureRect(GameObject gameObject)
    {
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        return rectTransform != null ? rectTransform : gameObject.AddComponent<RectTransform>();
    }

    private static RectTransform EnsureChildRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        RectTransform rectTransform;
        if (existing == null)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            rectTransform = child.GetComponent<RectTransform>();
        }
        else
        {
            rectTransform = existing.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = existing.gameObject.AddComponent<RectTransform>();
            }
        }

        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        SetUiLayerRecursive(rectTransform.gameObject);
        return rectTransform;
    }

    private static TMP_Text EnsureText(RectTransform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
    {
        RectTransform rect = EnsureChildRect(parent, name);
        TextMeshProUGUI textComponent = EnsureComponent<TextMeshProUGUI>(rect.gameObject);
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.raycastTarget = false;
        ApplyFontToText(textComponent, font);
        return textComponent;
    }

    private static Button EnsureButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, TMP_FontAsset font)
    {
        RectTransform rect = EnsureChildRect(parent, name);
        SetAnchored(rect, anchoredPosition, size, anchorMin, anchorMax, pivot);
        Image image = EnsureImage(rect.gameObject, ButtonColor);
        Button button = EnsureComponent<Button>(rect.gameObject);
        button.targetGraphic = image;

        TMP_Text labelText = EnsureText(rect, "Label", label, 17f, TextAlignmentOptions.Center, TextColor, font);
        SetStretch(labelText.rectTransform, new Vector2(8f, 5f), new Vector2(-8f, -5f));
        return button;
    }

    private static Image EnsureImage(GameObject gameObject, Color color)
    {
        Image image = EnsureComponent<Image>(gameObject);
        image.color = color;
        image.raycastTarget = true;
        EditorUtility.SetDirty(image);
        return image;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        EditorUtility.SetDirty(rect);
    }

    private static void SetStretch(RectTransform rect)
    {
        SetStretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        EditorUtility.SetDirty(rect);
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component != null && component.gameObject.scene == scene)
            {
                return component;
            }
        }

        return null;
    }

    private static void SetProperty(SerializedObject serializedObject, string fieldName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"{serializedObject.targetObject.name} is missing serialized field '{fieldName}'.");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static void SetList<T>(SerializedProperty property, IReadOnlyList<T> values) where T : Object
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetUiLayerRecursive(GameObject gameObject)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            gameObject.layer = uiLayer;
        }

        foreach (Transform child in gameObject.transform)
        {
            SetUiLayerRecursive(child.gameObject);
        }
    }

    private readonly struct ListViewParts
    {
        public ListViewParts(RectTransform root, RectTransform content, TMP_Text emptyText)
        {
            Root = root;
            Content = content;
            EmptyText = emptyText;
        }

        public RectTransform Root { get; }
        public RectTransform Content { get; }
        public TMP_Text EmptyText { get; }
    }
}
