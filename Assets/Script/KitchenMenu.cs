using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 煮飯前的食材選擇選單，掛在 Menu Window 上。
/// 負責顯示基底食材庫存、加菜選擇互動，以及結算結果文字。
/// </summary>
public class KitchenMenu : MonoBehaviour
{
    [Header("食材區塊容器")]
    [SerializeField] private GameObject ingredientBlockCols; // Ingredient Block 下的 Col(0)
    [SerializeField] private GameObject addBlockCols;        // Add Block 下的 Col(0)

    [Header("文字顯示")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI effectText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("按鈕與視窗")]
    [SerializeField] private Button cookButton;
    [SerializeField] private GameObject menuWindow; // Menu Window 本體

    [Header("外觀設定")]
    [SerializeField] private float dimAlpha = 0.5f; // 不可選/未選取時的透明度

    // 基底食材區塊設定：顯示名稱、ResourceManager key（null 表示無限）、缺少時扣除的好感度
    private readonly Dictionary<string, (string displayName, string resourceKey, int penalty)> ingredientDefs = new()
    {
        { "Potato Block", ("地瓜", null, 0) },
        { "Rice Block",   ("米",   "rice", 3) },
        { "Oil Block",    ("油",   "oil",  3) },
        { "Salt Block",   ("鹽",   "salt", 5) },
    };

    // 加菜區塊設定：顯示名稱、ResourceManager key、好感度加成
    private Dictionary<string, (string displayName, string resourceKey, int favorability)> addItems = new()
    {
        { "Tofu Block",  ("豆腐", "tofu",      5) },
        { "Fish Block",  ("魚乾", "driedFish", 8) },
        { "Egg Block",   ("雞蛋", "eggs",      3) },
    };

    private class IngredientSlot
    {
        public string displayName;
        public string resourceKey; // null 表示無限（地瓜）
        public int penaltyWhenZero;
        public TextMeshProUGUI contentText;
        public Image blockImage;
        public Color originalColor;
    }

    private class AddItemSlot
    {
        public string displayName;
        public string resourceKey;
        public int favorability;
        public TextMeshProUGUI contentText;
        public Image blockImage;
        public Color originalColor;
    }

    private readonly List<IngredientSlot> ingredientSlots = new();
    private readonly List<AddItemSlot> addItemSlots = new();
    private AddItemSlot selectedAddItem;

    private void Awake()
    {
        if (cookButton != null)
        {
            cookButton.onClick.AddListener(OnClickCook);
        }

        BuildIngredientSlots();
        BuildAddItemSlots();
    }

    private void Start()
    {
        RefreshIngredientDisplay();
    }

    private void Update()
    {
        RefreshIngredientDisplay();
    }

    private void BuildIngredientSlots()
    {
        ingredientSlots.Clear();
        if (ingredientBlockCols == null) return;

        foreach (Transform child in ingredientBlockCols.transform)
        {
            if (!ingredientDefs.TryGetValue(child.name, out var def))
            {
                Debug.LogWarning($"【KitchenMenu】未知的食材區塊名稱：{child.name}");
                continue;
            }

            Image blockImage = child.GetComponent<Image>();
            TextMeshProUGUI contentText = child.Find("Content")?.GetComponent<TextMeshProUGUI>();

            ingredientSlots.Add(new IngredientSlot
            {
                displayName = def.displayName,
                resourceKey = def.resourceKey,
                penaltyWhenZero = def.penalty,
                contentText = contentText,
                blockImage = blockImage,
                originalColor = blockImage != null ? blockImage.color : Color.white
            });
        }
    }

    private void BuildAddItemSlots()
    {
        addItemSlots.Clear();
        if (addBlockCols == null) return;

        foreach (Transform child in addBlockCols.transform)
        {
            if (!addItems.TryGetValue(child.name, out var def))
            {
                Debug.LogWarning($"【KitchenMenu】未知的加菜區塊名稱：{child.name}");
                continue;
            }

            Image blockImage = child.GetComponent<Image>();
            TextMeshProUGUI contentText = child.Find("Content")?.GetComponent<TextMeshProUGUI>();
            GenericEventTrigger trigger = child.GetComponent<GenericEventTrigger>();

            AddItemSlot slot = new AddItemSlot
            {
                displayName = def.displayName,
                resourceKey = def.resourceKey,
                favorability = def.favorability,
                contentText = contentText,
                blockImage = blockImage,
                originalColor = blockImage != null ? blockImage.color : Color.white
            };

            if (trigger != null)
            {
                trigger.OnClick.AddListener(() => OnAddItemClicked(slot));
            }
            else
            {
                Debug.LogWarning($"【KitchenMenu】{child.name} 缺少 GenericEventTrigger 元件，無法點擊。");
            }

            addItemSlots.Add(slot);
        }
    }

    private void RefreshIngredientDisplay()
    {
        foreach (var slot in ingredientSlots)
        {
            bool isInfinite = slot.resourceKey == null;
            int amount = isInfinite ? 0 : GetStock(slot.resourceKey);

            if (slot.contentText != null)
            {
                slot.contentText.text = isInfinite
                    ? $"{slot.displayName} (∞)"
                    : $"{slot.displayName} ({amount})";
            }

            bool isEmpty = !isInfinite && amount <= 0;
            SetBlockDim(slot.blockImage, slot.originalColor, isEmpty);
        }

        RefreshAddItemDisplay();
        UpdateResult();
    }

    private void RefreshAddItemDisplay()
    {
        foreach (var slot in addItemSlots)
        {
            int amount = GetStock(slot.resourceKey);

            if (slot.contentText != null)
            {
                slot.contentText.text = $"{slot.displayName} ({amount})\n好感度+{slot.favorability}";
            }

            if (amount <= 0 && selectedAddItem == slot)
            {
                selectedAddItem = null; // 庫存歸零時強制取消選取
            }

            bool isBright = amount > 0 && selectedAddItem == slot;
            SetBlockDim(slot.blockImage, slot.originalColor, !isBright);
        }
    }

    private void OnAddItemClicked(AddItemSlot slot)
    {
        if (GetStock(slot.resourceKey) <= 0) return; // 數量為 0，不可點擊

        selectedAddItem = (selectedAddItem == slot) ? null : slot;

        RefreshAddItemDisplay();
        UpdateResult();
    }

    private void UpdateResult()
    {
        if (resultText != null)
        {
            resultText.text = selectedAddItem != null
                ? $"今天的飯：地瓜簽粥 + {selectedAddItem.displayName}"
                : "今天的飯：地瓜簽粥";
        }

        var (effect, missing) = CalculateMealEffect();

        if (effectText != null)
        {
            string sign = effect >= 0 ? "+" : "";
            effectText.text = $"預計效果：全家好感度 {sign}{effect}";
        }

        if (messageText != null)
        {
            if (missing.Count == 0)
            {
                messageText.text = "警告：無";
                messageText.color = Color.white;
            }
            else
            {
                messageText.text = $"警告：{string.Join("、", missing)}";
                messageText.color = Color.red;
            }
        }
    }

    /// <summary>
    /// 計算這份餐點的好感度效果（加菜加成 + 缺少基底食材的扣分），並列出缺少項目。
    /// </summary>
    private (int effect, List<string> missing) CalculateMealEffect()
    {
        int effect = selectedAddItem != null ? selectedAddItem.favorability : 0;
        List<string> missing = new List<string>();

        foreach (var slot in ingredientSlots)
        {
            if (slot.resourceKey == null) continue; // 地瓜永遠不缺

            if (GetStock(slot.resourceKey) <= 0)
            {
                effect -= slot.penaltyWhenZero;
                missing.Add($"缺{slot.displayName}（好感度 -{slot.penaltyWhenZero}）");
            }
        }

        return (effect, missing);
    }

    private void OnClickCook()
    {
        var (effect, _) = CalculateMealEffect();
        string addItemKey = selectedAddItem != null ? selectedAddItem.resourceKey : null;

        if (EventManager.Instance != null)
        {
            EventManager.Instance.RecordKitchenMealChoice(addItemKey, effect);
        }

        if (ResourceManager.Instance != null)
        {
            ConsumeIfAvailable("rice");
            ConsumeIfAvailable("oil");
            ConsumeIfAvailable("salt");

            if (selectedAddItem != null)
            {
                ResourceManager.Instance.ConsumeIngredient(selectedAddItem.resourceKey, 1);
            }
        }

        if (menuWindow != null) menuWindow.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ConfirmKitchenIngredients();
        }
    }

    private void ConsumeIfAvailable(string key)
    {
        if (ResourceManager.Instance.GetIngredientCount(key) > 0)
        {
            ResourceManager.Instance.ConsumeIngredient(key, 1);
        }
    }

    private int GetStock(string key)
    {
        return ResourceManager.Instance != null ? ResourceManager.Instance.GetIngredientCount(key) : 0;
    }

    private void SetBlockDim(Image img, Color originalColor, bool dim)
    {
        if (img == null) return;
        Color c = originalColor;
        c.a = dim ? dimAlpha : originalColor.a;
        img.color = c;
    }

    /// <summary>
    /// 開啟食材選單視窗
    /// </summary>
    public void OpenMenu()
    {
        if (menuWindow != null)
        {
            menuWindow.SetActive(true);
        }
        RefreshIngredientDisplay();
    }

    /// <summary>
    /// 退出按鈕點擊事件，僅隱藏介面，不重置食材選擇
    /// </summary>
    public void HandleExit()
    {
        if (menuWindow != null)
        {
            menuWindow.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CloseKitchenGame();
        }
    }
}
