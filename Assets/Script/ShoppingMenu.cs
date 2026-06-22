using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 第一市場的買賣選單。
/// 卡片 Prefab 結構：Content（TextMeshProUGUI，名稱／單價／家中剩餘三行文字都在這裡換行顯示）；
/// func Col 底下放 AmountText（TextMeshProUGUI）、AddButton、SubButton（Button）。
/// </summary>
public class ShoppingMenu : MonoBehaviour
{
    public static ShoppingMenu Instance { get; private set; }

    [Header("Window")]
    [SerializeField] private GameObject menuWindow; // 商店視窗本體，用來開關介面

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Mode Toggle")]
    [SerializeField] private GameObject buyBlock;
    [SerializeField] private GameObject sellBlock;

    [Header("Scroll View")]
    [SerializeField] private Transform contentParent;  // Viewport/Content
    [SerializeField] private GameObject shopCardPrefab;

    [Header("Result Block")]
    [SerializeField] private TextMeshProUGUI listText;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Buttons")]
    [SerializeField] private Button checkoutButton;
    [SerializeField] private GenericEventTrigger exitButton;

    [Header("外觀設定")]
    [SerializeField] private float dimAlpha = 0.5f; // 未選取模式時的透明度

    public class ShopItem
    {
        public string itemKey;        // ResourceManager 對應的欄位名
        public string displayName;    // 顯示名稱
        public int unitPrice;         // 單價
        public int availableAmount;   // 今日可購買數量（買模式）或玩家持有數量（賣模式）
        public int selectedAmount;    // 玩家選擇的數量，初始 0
    }

    private enum ShopMode { Buy, Sell }
    private ShopMode currentMode = ShopMode.Buy;
    private List<ShopItem> currentItems = new();

    // 買模式的固定貨物池：key、顯示名稱、單價、出現機率（1 = 每次必出現）
    private readonly (string key, string name, int price, float chance)[] buyStockPool = new[]
    {
        ("tofu",      "豆腐", 1,  1f),
        ("driedFish", "魚乾", 3,  1f),
        ("rice",      "米",   5,  1f),
        ("riceBran",  "米糠", 1,  1f),
        ("sweetPotatoLeaves", "地瓜葉", 1, 1f),
        ("firewood",  "木柴", 2,  1f),
        ("salt",      "鹽",   2,  0.6f),
        ("oil",       "油",   4,  0.6f),
        ("cloth",     "布料", 8,  0.3f),
        ("medicine",  "藥材", 10, 0.3f),
    };

    // 賣模式的固定貨物池：key、顯示名稱、單價
    private readonly (string key, string name, int price)[] sellGoodsPool = new[]
    {
        ("eggs",       "雞蛋", 1),
        ("pickledVeg", "醃菜", 2),
    };

    // 每買一份實際得到的數量：米油鹽米糠地瓜葉是 3 份，木柴是 5 份，沒列在這裡的物品買一份就是一個
    private static readonly Dictionary<string, int> BulkPurchaseMultipliers = new()
    {
        { "rice", 3 }, { "oil", 3 }, { "salt", 3 }, { "riceBran", 3 }, { "sweetPotatoLeaves", 3 },
        { "firewood", 5 },
    };

    // 今天決定要上架的種類（只記種類，不含執行期的 selectedAmount）
    private List<(string key, string name, int price)> todaysMarketRoll;
    private bool marketStockGenerated = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (checkoutButton != null) checkoutButton.onClick.AddListener(OnCheckout);
        if (exitButton != null) exitButton.OnClick.AddListener(HandleExit);

        GenericEventTrigger buyTrigger = buyBlock != null ? buyBlock.GetComponent<GenericEventTrigger>() : null;
        if (buyTrigger != null) buyTrigger.OnClick.AddListener(OnBuyBlockClick);

        GenericEventTrigger sellTrigger = sellBlock != null ? sellBlock.GetComponent<GenericEventTrigger>() : null;
        if (sellTrigger != null) sellTrigger.OnClick.AddListener(OnSellBlockClick);
    }

    private void Start()
    {
        // 跟 UIManager 的金錢顯示用同一個來源：訂閱 OnStatChanged，金錢有變動就即時更新 TitleText
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnStatChanged += HandleStatChanged;
        }
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnStatChanged -= HandleStatChanged;
        }
    }

    private void HandleStatChanged(string statName, object value)
    {
        if (statName.ToLower() == "money")
        {
            UpdateTitleText();
        }
    }

    private void OnEnable()
    {
        RefreshShopOpenState();
    }

    /// <summary>
    /// 重新整理整個商店畫面：重置成買模式、重新生成卡片、更新標題與清單文字。
    /// OnEnable() 跟 OpenMenu() 都會呼叫，避免 menuWindow 跟腳本所在物件不是同一個時，
    /// OnEnable() 沒有跟著每次開店重新觸發，導致 TitleText 停留在舊的金錢數字。
    /// </summary>
    private void RefreshShopOpenState()
    {
        currentMode = ShopMode.Buy;
        currentItems = BuildBuyItems();

        SetBlockHighlight(buyBlock, true);
        SetBlockHighlight(sellBlock, false);

        BuildCards();
        UpdateTitleText();
        UpdateListAndResult();
    }

    /// <summary>
    /// 跨日時呼叫：讓市場明天重新隨機上架。
    /// </summary>
    public void ResetDailyStock()
    {
        marketStockGenerated = false;
    }

    private void EnsureMarketStockGenerated()
    {
        if (marketStockGenerated) return;

        todaysMarketRoll = new List<(string key, string name, int price)>();
        foreach (var entry in buyStockPool)
        {
            if (Random.value <= entry.chance)
            {
                todaysMarketRoll.Add((entry.key, entry.name, entry.price));
            }
        }

        marketStockGenerated = true;
    }

    private List<ShopItem> BuildBuyItems()
    {
        EnsureMarketStockGenerated();

        List<ShopItem> result = new List<ShopItem>();
        foreach (var entry in todaysMarketRoll)
        {
            result.Add(new ShopItem
            {
                itemKey = entry.key,
                displayName = entry.name,
                unitPrice = entry.price,
                availableAmount = 99, // 市場買不完
                selectedAmount = 0
            });
        }
        return result;
    }

    private List<ShopItem> BuildSellItems()
    {
        // 不管家裡有沒有貨都顯示，讓玩家知道哪些東西可以賣；availableAmount=0 的卡片在 BindCard() 裡會變暗、不能點
        List<ShopItem> result = new List<ShopItem>();
        foreach (var entry in sellGoodsPool)
        {
            result.Add(new ShopItem
            {
                itemKey = entry.key,
                displayName = entry.name,
                unitPrice = entry.price,
                availableAmount = GetStock(entry.key),
                selectedAmount = 0
            });
        }
        return result;
    }

    public void OnBuyBlockClick()
    {
        if (currentMode == ShopMode.Buy) return;
        currentMode = ShopMode.Buy;
        ClearAllSelections();
        RefreshModeUI();
    }

    public void OnSellBlockClick()
    {
        if (currentMode == ShopMode.Sell) return;
        currentMode = ShopMode.Sell;
        ClearAllSelections();
        RefreshModeUI();
    }

    private void ClearAllSelections()
    {
        foreach (var item in currentItems)
        {
            item.selectedAmount = 0;
        }
    }

    private void RefreshModeUI()
    {
        currentItems = currentMode == ShopMode.Buy ? BuildBuyItems() : BuildSellItems();

        SetBlockHighlight(buyBlock, currentMode == ShopMode.Buy);
        SetBlockHighlight(sellBlock, currentMode == ShopMode.Sell);

        BuildCards();
        UpdateListAndResult();
    }

    private void SetBlockHighlight(GameObject block, bool active)
    {
        if (block == null) return;
        Image img = block.GetComponent<Image>();
        if (img == null) return;

        Color c = img.color;
        c.a = active ? 1f : dimAlpha;
        img.color = c;
    }

    private void BuildCards()
    {
        if (contentParent != null)
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(contentParent.GetChild(i).gameObject);
            }
        }

        if (contentParent == null || shopCardPrefab == null) return;

        foreach (var item in currentItems)
        {
            GameObject cardObj = Instantiate(shopCardPrefab, contentParent);
            BindCard(cardObj, item);
        }
    }

    private void BindCard(GameObject cardObj, ShopItem item)
    {
        TextMeshProUGUI contentText = cardObj.transform.Find("Content")?.GetComponent<TextMeshProUGUI>();
        Transform funcCol = cardObj.transform.Find("func Col");
        TextMeshProUGUI amountText = funcCol?.Find("AmountText")?.GetComponent<TextMeshProUGUI>();
        Button addButton = funcCol?.Find("AddButton")?.GetComponent<Button>();
        Button subButton = funcCol?.Find("SubButton")?.GetComponent<Button>();

        if (contentText != null)
        {
            contentText.text = $"{item.displayName}\n{item.unitPrice}元/份\n持有：{GetStock(item.itemKey)}份";
        }

        if (amountText != null) amountText.text = item.selectedAmount.ToString();

        // 賣模式下家裡沒有貨（availableAmount <= 0）的卡片：變暗、Add/Sub 都不能點，但還是讓玩家看得到這個東西可以賣
        bool sellable = item.availableAmount > 0;

        if (addButton != null)
        {
            addButton.interactable = sellable;
            addButton.onClick.AddListener(() =>
            {
                if (item.selectedAmount >= item.availableAmount) return;
                item.selectedAmount++;
                if (amountText != null) amountText.text = item.selectedAmount.ToString();
                UpdateListAndResult();
            });
        }

        if (subButton != null)
        {
            subButton.interactable = sellable;
            subButton.onClick.AddListener(() =>
            {
                if (item.selectedAmount <= 0) return;
                item.selectedAmount--;
                if (amountText != null) amountText.text = item.selectedAmount.ToString();
                UpdateListAndResult();
            });
        }

        CanvasGroup cardGroup = cardObj.GetComponent<CanvasGroup>();
        if (cardGroup != null)
        {
            cardGroup.alpha = sellable ? 1f : dimAlpha;
        }
    }

    private void UpdateListAndResult()
    {
        if (listText != null)
        {
            string title = currentMode == ShopMode.Buy ? "購物清單：" : "出售清單：";
            List<string> lines = new List<string>();
            foreach (var item in currentItems)
            {
                if (item.selectedAmount > 0)
                {
                    lines.Add($"{item.displayName} x{item.selectedAmount}");
                }
            }

            listText.text = lines.Count == 0
                ? $"{title}\n（尚未選擇）"
                : $"{title}\n{string.Join("、", lines)}";
        }

        int total = CalculateTotal();

        if (currentMode == ShopMode.Buy)
        {
            int money = (int)ResourceManager.GetStat("money");
            bool canAfford = money >= total;

            if (resultText != null)
            {
                resultText.text = canAfford ? $"合計：{total}元" : $"合計：{total}元(不夠錢)";
                resultText.color = canAfford ? Color.black : Color.red;
            }

            if (checkoutButton != null) checkoutButton.interactable = canAfford;
        }
        else
        {
            if (resultText != null)
            {
                resultText.text = $"預計收入：{total}元";
                resultText.color = Color.black;
            }

            if (checkoutButton != null) checkoutButton.interactable = true;
        }
    }

    private int CalculateTotal()
    {
        int total = 0;
        foreach (var item in currentItems)
        {
            total += item.unitPrice * item.selectedAmount;
        }
        return total;
    }

    public void OnCheckout()
    {
        int total = CalculateTotal();

        if (currentMode == ShopMode.Buy)
        {
            if ((int)ResourceManager.GetStat("money") < total) return; // 防呆，理論上錢不夠時按鈕已經不能按

            foreach (var item in currentItems)
            {
                if (item.selectedAmount <= 0) continue;
                int multiplier = BulkPurchaseMultipliers.TryGetValue(item.itemKey, out int m) ? m : 1;
                ResourceManager.AdjustItem(item.itemKey, item.selectedAmount * multiplier);
            }

            ResourceManager.AdjustStat("money", -total);
        }
        else
        {
            foreach (var item in currentItems)
            {
                if (item.selectedAmount <= 0) continue;
                ResourceManager.AdjustItem(item.itemKey, -item.selectedAmount);
            }

            ResourceManager.AdjustStat("money", total);
        }

        currentItems = currentMode == ShopMode.Buy ? BuildBuyItems() : BuildSellItems();
        BuildCards();
        UpdateTitleText();
        UpdateListAndResult();
    }

    private void UpdateTitleText()
    {
        if (titleText == null) return;
        int money = (int)ResourceManager.GetStat("money");
        titleText.text = $"第一市場　　身上：{money}元";
    }

    private int GetStock(string key)
    {
        return ResourceManager.Instance != null ? ResourceManager.Instance.GetIngredientCount(key) : 0;
    }

    /// <summary>
    /// 開啟商店視窗
    /// </summary>
    public void OpenMenu()
    {
        if (menuWindow != null)
        {
            menuWindow.SetActive(true);
        }

        RefreshShopOpenState();
    }

    /// <summary>
    /// 退出按鈕點擊事件，直接關閉 Menu Window，不重置任何 ResourceManager 數值
    /// </summary>
    public void HandleExit()
    {
        if (menuWindow != null)
        {
            menuWindow.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CloseShoppingMenu();
        }
    }
}
