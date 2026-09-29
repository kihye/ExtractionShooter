using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ShopSceneSetupTool
{
    private const string BaseScenePath = "Assets/_Game/Scenes/BaseScene.unity";
    private const string ShopCatalogPath = "Assets/_Game/Data/Shop/BaseShopCatalog.asset";
    private const string SessionStatePath = "Assets/_Game/Data/Runtime/GameSessionState.asset";
    private const string LightRifleAmmoPath = "Assets/_Game/Items/LightRifleAmmo.asset";
    private const string MedicalSupplyPath = "Assets/_Game/Items/MedicalSupply.asset";
    private const string ScrapPath = "Assets/_Game/Items/Scrap.asset";
    private const string StrangeArtifactPath = "Assets/_Game/Items/StrangeArtifact.asset";

    private static readonly Color WindowColor = new Color(0.05f, 0.06f, 0.07f, 0.92f);
    private static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.12f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.26f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.68f, 0.72f, 0.78f, 1f);

    [MenuItem("Tools/Extraction Shooter/Setup Base Shop Trading")]
    public static void SetupBaseShopTradingAndSave()
    {
        EnsureDataFolder();
        ShopCatalog catalog = EnsureShopCatalog();
        GameSessionState sessionState = AssetDatabase.LoadAssetAtPath<GameSessionState>(SessionStatePath);
        EnsureInitialCredits(sessionState, 250);

        Scene scene = EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
        ShopGuiController shopGui = Object.FindFirstObjectByType<ShopGuiController>();
        if (shopGui == null)
        {
            Debug.LogError("BaseScene is missing ShopGuiController.");
            return;
        }

        BuildShopWindow(shopGui, catalog, sessionState);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        GameUiPresentationFixer.FixShopUiAndGameFonts();
        Debug.Log("Base Shop trading setup completed.");
    }

    private static void EnsureDataFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Data"))
        {
            AssetDatabase.CreateFolder("Assets/_Game", "Data");
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Shop"))
        {
            AssetDatabase.CreateFolder("Assets/_Game/Data", "Shop");
        }
    }

    private static ShopCatalog EnsureShopCatalog()
    {
        ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ShopCatalog>();
            AssetDatabase.CreateAsset(catalog, ShopCatalogPath);
        }

        SerializedObject serializedObject = new SerializedObject(catalog);
        serializedObject.FindProperty("shopName").stringValue = "Base Trader";
        serializedObject.FindProperty("currencyDisplayName").stringValue = "Credits";

        SerializedProperty buyEntries = serializedObject.FindProperty("buyEntries");
        buyEntries.arraySize = 2;
        SetBuyEntry(buyEntries.GetArrayElementAtIndex(0), AssetDatabase.LoadAssetAtPath<ItemDefinition>(LightRifleAmmoPath), 2);
        SetBuyEntry(buyEntries.GetArrayElementAtIndex(1), AssetDatabase.LoadAssetAtPath<ItemDefinition>(MedicalSupplyPath), 35);

        SerializedProperty sellEntries = serializedObject.FindProperty("sellEntries");
        sellEntries.arraySize = 2;
        SetSellEntry(sellEntries.GetArrayElementAtIndex(0), AssetDatabase.LoadAssetAtPath<ItemDefinition>(ScrapPath), 8);
        SetSellEntry(sellEntries.GetArrayElementAtIndex(1), AssetDatabase.LoadAssetAtPath<ItemDefinition>(StrangeArtifactPath), 55);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void SetBuyEntry(SerializedProperty property, ItemDefinition item, int unitPrice)
    {
        property.FindPropertyRelative("item").objectReferenceValue = item;
        property.FindPropertyRelative("unitPrice").intValue = unitPrice;
    }

    private static void SetSellEntry(SerializedProperty property, ItemDefinition item, int unitPrice)
    {
        property.FindPropertyRelative("item").objectReferenceValue = item;
        property.FindPropertyRelative("unitPrice").intValue = unitPrice;
    }

    private static void EnsureInitialCredits(GameSessionState sessionState, int amount)
    {
        if (sessionState == null)
        {
            Debug.LogError("GameSessionState asset is missing.");
            return;
        }

        SerializedObject serializedObject = new SerializedObject(sessionState);
        SerializedProperty initialCredits = serializedObject.FindProperty("initialCredits");
        if (initialCredits != null && initialCredits.intValue == 0)
        {
            initialCredits.intValue = amount;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sessionState);
        }
    }

    private static void BuildShopWindow(ShopGuiController shopGui, ShopCatalog catalog, GameSessionState sessionState)
    {
        RectTransform root = EnsureRect(shopGui.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "ShopWindow");
        SetAnchored(panel, Vector2.zero, new Vector2(900f, 620f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        DisableLegacyPlaceholder(panel, "ShopListText");
        DisableLegacyPlaceholder(panel, "PlayerInventoryText");

        TMP_Text titleText = EnsureText(panel, "ShopTitleText", "Base Trader", 30f, TextAlignmentOptions.Left, TextColor);
        SetAnchored(titleText.rectTransform, new Vector2(28f, -24f), new Vector2(420f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        TMP_Text creditsText = EnsureText(panel, "CreditsText", "Credits: 0", 20f, TextAlignmentOptions.Right, TextColor);
        SetAnchored(creditsText.rectTransform, new Vector2(-158f, -30f), new Vector2(260f, 34f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));

        Button closeButton = EnsureButton(panel, "CloseButton", "X", new Vector2(-42f, -30f), new Vector2(44f, 34f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));

        Button buyTabButton = EnsureButton(panel, "BuyTabButton", "BUY", new Vector2(90f, -86f), new Vector2(124f, 38f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
        Button sellTabButton = EnsureButton(panel, "SellTabButton", "SELL", new Vector2(222f, -86f), new Vector2(124f, 38f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));

        RectTransform buyContent = EnsureList(panel, "BuyList", new Vector2(28f, -128f), new Vector2(395f, 325f));
        RectTransform sellContent = EnsureList(panel, "SellList", new Vector2(28f, -128f), new Vector2(395f, 325f));

        List<ShopTradeRowView> buyRows = EnsureRowPool(buyContent, "BuyRow", 12);
        List<ShopTradeRowView> sellRows = EnsureRowPool(sellContent, "SellRow", 12);

        RectTransform detailPanel = EnsureChildRect(panel, "TradeDetailPanel");
        SetAnchored(detailPanel, new Vector2(-238f, -284f), new Vector2(390f, 325f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        EnsureImage(detailPanel.gameObject, PanelColor);

        TMP_Text selectedItemText = EnsureText(detailPanel, "SelectedItemText", "선택된 품목 없음", 22f, TextAlignmentOptions.Left, TextColor);
        SetAnchored(selectedItemText.rectTransform, new Vector2(18f, -22f), new Vector2(340f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        TMP_Text quantityLabel = EnsureText(detailPanel, "QuantityLabel", "Quantity", 16f, TextAlignmentOptions.Left, MutedTextColor);
        SetAnchored(quantityLabel.rectTransform, new Vector2(18f, -84f), new Vector2(150f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Button decreaseButton = EnsureButton(detailPanel, "DecreaseQuantityButton", "-", new Vector2(55f, -136f), new Vector2(52f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
        TMP_Text quantityText = EnsureText(detailPanel, "QuantityText", "1", 24f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(quantityText.rectTransform, new Vector2(116f, -136f), new Vector2(76f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        Button increaseButton = EnsureButton(detailPanel, "IncreaseQuantityButton", "+", new Vector2(197f, -136f), new Vector2(52f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));

        TMP_Text totalPriceText = EnsureText(detailPanel, "TotalPriceText", "합계: 0 Credits", 18f, TextAlignmentOptions.Left, TextColor);
        SetAnchored(totalPriceText.rectTransform, new Vector2(18f, -192f), new Vector2(340f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Button confirmButton = EnsureButton(detailPanel, "ConfirmTradeButton", "구매", new Vector2(88f, -254f), new Vector2(140f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
        TMP_Text confirmButtonText = confirmButton.GetComponentInChildren<TMP_Text>(true);

        TMP_Text resultText = EnsureText(panel, "TradeResultText", "", 17f, TextAlignmentOptions.Left, MutedTextColor);
        SetAnchored(resultText.rectTransform, new Vector2(28f, -494f), new Vector2(820f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(shopGui);
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
        SetProperty(serializedObject, "buyListContainer", buyContent);
        SetProperty(serializedObject, "sellListContainer", sellContent);
        SetList(serializedObject.FindProperty("buyRowPool"), buyRows);
        SetList(serializedObject.FindProperty("sellRowPool"), sellRows);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shopGui);
    }

    private static RectTransform EnsureList(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform listRoot = EnsureChildRect(parent, name);
        SetAnchored(listRoot, anchoredPosition, size, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(listRoot.gameObject, PanelColor);

        ScrollRect scrollRect = EnsureComponent<ScrollRect>(listRoot.gameObject);
        RectTransform viewport = EnsureChildRect(listRoot, "Viewport");
        SetStretch(viewport, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        EnsureImage(viewport.gameObject, new Color(0f, 0f, 0f, 0f));
        EnsureComponent<Mask>(viewport.gameObject).showMaskGraphic = false;

        RectTransform content = EnsureChildRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 520f);

        VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(content.gameObject);
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(content.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    private static List<ShopTradeRowView> EnsureRowPool(RectTransform parent, string prefix, int count)
    {
        List<ShopTradeRowView> rows = new List<ShopTradeRowView>(count);
        for (int i = 0; i < count; i++)
        {
            RectTransform rowRect = EnsureChildRect(parent, $"{prefix}_{i:00}");
            rowRect.sizeDelta = new Vector2(0f, 58f);
            LayoutElement layoutElement = EnsureComponent<LayoutElement>(rowRect.gameObject);
            layoutElement.preferredHeight = 58f;

            Image background = EnsureImage(rowRect.gameObject, ButtonColor);
            Button button = EnsureComponent<Button>(rowRect.gameObject);
            button.targetGraphic = background;

            RectTransform icon = EnsureChildRect(rowRect, "Icon");
            SetAnchored(icon, new Vector2(30f, 0f), new Vector2(36f, 36f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
            EnsureImage(icon.gameObject, new Color(0.35f, 0.72f, 0.82f, 0.92f));

            TMP_Text nameText = EnsureText(rowRect, "NameText", "", 16f, TextAlignmentOptions.Left, TextColor);
            SetAnchored(nameText.rectTransform, new Vector2(58f, -8f), new Vector2(180f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            TMP_Text detailText = EnsureText(rowRect, "DetailText", "", 13f, TextAlignmentOptions.Left, MutedTextColor);
            SetAnchored(detailText.rectTransform, new Vector2(58f, -34f), new Vector2(140f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            TMP_Text priceText = EnsureText(rowRect, "PriceText", "", 15f, TextAlignmentOptions.Right, TextColor);
            SetAnchored(priceText.rectTransform, new Vector2(-70f, 0f), new Vector2(120f, 36f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));

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

    private static void DisableLegacyPlaceholder(RectTransform panel, string childName)
    {
        Transform child = panel.Find(childName);
        if (child != null)
        {
            child.gameObject.SetActive(false);
        }
    }

    private static RectTransform EnsureRect(GameObject gameObject)
    {
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        return rectTransform != null ? rectTransform : gameObject.AddComponent<RectTransform>();
    }

    private static RectTransform EnsureChildRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            RectTransform existingRect = existing.GetComponent<RectTransform>();
            return existingRect != null ? existingRect : existing.gameObject.AddComponent<RectTransform>();
        }

        GameObject child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static TMP_Text EnsureText(RectTransform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = EnsureChildRect(parent, name);
        TMP_Text textComponent = EnsureComponent<TextMeshProUGUI>(rect.gameObject);
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private static Button EnsureButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        RectTransform rect = EnsureChildRect(parent, name);
        SetAnchored(rect, anchoredPosition, size, anchorMin, anchorMax, pivot);
        Image image = EnsureImage(rect.gameObject, ButtonColor);
        Button button = EnsureComponent<Button>(rect.gameObject);
        button.targetGraphic = image;

        TMP_Text labelText = EnsureText(rect, "Label", label, 18f, TextAlignmentOptions.Center, TextColor);
        SetStretch(labelText.rectTransform, new Vector2(6f, 4f), new Vector2(-6f, -4f));
        return button;
    }

    private static Image EnsureImage(GameObject gameObject, Color color)
    {
        Image image = EnsureComponent<Image>(gameObject);
        image.color = color;
        image.raycastTarget = true;
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
}
