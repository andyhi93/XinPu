using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 遊戲總管理器：負責管理遊戲模式切換、全域狀態與對話啟動。
/// </summary>
public enum GameState { Dialogue, FreeRoam, Minigame }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Yarn Spinner 設定")]
    public DialogueRunner dialogueRunner;
    [Tooltip("一開始啟動遊戲時的對話節點名稱")]
    public string startingNode = "Start";
    [Tooltip("是否在遊戲啟動時自動執行 startingNode")]
    public bool autoStartDialogue = true;

    [Header("目前狀態")]
    public GameState currentState = GameState.FreeRoam;

    [Header("小遊戲引用")]
    public PersimmonMiniGame persimmonGame; // 在 Inspector 中拖入
    public KitchenMiniGame kitchenGame;     // 在 Inspector 中拖入
    public KitchenMenu kitchenMenu;         // 在 Inspector 中拖入
    public PigFoodMiniGame pigFoodGame;     // 在 Inspector 中拖入
    public ShoppingMenu shoppingMenu;       // 在 Inspector 中拖入

    [Header("對話 UI")]
    [Tooltip("拖入 'Dialogue System (1) Variant' 底下的 'Canvas' 子物件。\n" +
        "原因：Yarn Spinner 的 LinePresenter 只會淡入淡出畫面的 Alpha，CanvasGroup 的 Blocks Raycasts 永遠是 true，\n" +
        "package 本身不會在沒有台詞時關閉它。這個底部對話框區域因此會一直擋住點擊，\n" +
        "包含畫面下方的小遊戲／選單按鈕（例如灶房選單的「開始煮飯」）。\n" +
        "這裡只在 GameState.Dialogue 時才開啟這個 Canvas，其餘狀態關閉，避免擋到其他 UI。")]
    [SerializeField] private GameObject dialogueCanvas;

    [Header("早餐狀態")]
    [SerializeField] private LocationManager.Location diningHallLocation = LocationManager.Location.廳堂;

    // 是否已經在 KitchenMenu 選好這一餐的食材，選好之前進灶房都先開選單
    private bool kitchenIngredientsSelected = false;

    // 商店是否是從進鎮的 Scene_TownMenu 開啟的，關閉後要跳回 Scene_TownMenu 而不是直接回三合院
    private bool shopOpenedFromTown = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 確保 LunchDeliveryManager 存在
            if (GetComponent<LunchDeliveryManager>() == null)
            {
                gameObject.AddComponent<LunchDeliveryManager>();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        // 延遲訂閱以確保 LocationManager.Instance 已就緒，或在 Start 處理
    }

    private void Start()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        }

        // 手動註冊指令，避免 Yarn Spinner 找不到實例目標 (needs a target)
        if (dialogueRunner != null)
        {
            dialogueRunner.AddCommandHandler("open_kitchen", OpenKitchenGame);
            dialogueRunner.AddCommandHandler("open_pig_food", OpenPigFoodGame);
            dialogueRunner.AddCommandHandler("open_shop", OpenShoppingMenu);

            dialogueRunner.AddFunction("is_pig_food_done", () => pigFoodGame != null && pigFoodGame.IsPigFoodDone());
            dialogueRunner.AddFunction("is_pig_food_cooking", () => pigFoodGame != null && pigFoodGame.IsPigFoodCooking());
        }

        // 初始自動啟動
        if (autoStartDialogue && dialogueRunner != null && !string.IsNullOrEmpty(startingNode))
        {
            StartDialogue(startingNode);
        }

        // 訂閱地點切換事件
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged += HandleLocationChanged;
        }
    }

    private bool pendingFreeRoamRecovery = false; // 見 OnDialogueComplete() 的說明：不能在事件處理常式裡直接重啟對話

    private void Update()
    {
        UpdateBreakfastEventDot();

        if (pendingFreeRoamRecovery)
        {
            pendingFreeRoamRecovery = false;

            // 延到這裡才真正觸發，並重新確認狀態還是「卡在 Dialogue、但其實沒有對話在跑」，
            // 避免覆蓋掉這一幀內可能由別的系統（例如隨機事件）合法啟動的新對話。
            if (currentState == GameState.Dialogue && dialogueRunner != null && !dialogueRunner.IsDialogueRunning)
            {
                ForceStartDialogue("Scene_FreeRoam");
            }
        }
    }

    /// <summary>
    /// 早飯送出之前，廳堂熱點顯示提示圓點；過了 8:30 由黃轉紅。
    /// 實際的顯示/變色邏輯交給 LocationManager 轉發給對應的 LocationHotspot 處理。
    /// </summary>
    private void UpdateBreakfastEventDot()
    {
        if (LocationManager.Instance == null) return;

        bool shouldShow = (KitchenMiniGame.IsFoodDone() || KitchenMiniGame.IsFoodServed()) && !EventManager.HasFlag("breakfast_delivered");
        bool isCritical = TimeManager.IsTimeAfter(8, 30);
        LocationManager.Instance.SetEventDot(diningHallLocation, shouldShow, isCritical);
    }

    private void OnDestroy()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged -= HandleLocationChanged;
        }
    }

    /// <summary>
    /// 處理地點切換：自動開啟對應地點的小遊戲
    /// </summary>
    private void HandleLocationChanged(LocationManager.Location newLocation)
    {
        if (newLocation == LocationManager.Location.禾埕)
        {
            OpenPersimmonGame();
        }
        else if (newLocation == LocationManager.Location.灶房)
        {
            Debug.Log("【GameManager】進入灶房，觸發菜單對話。");
            StartDialogue("Scene_KitchenMenu");
        }
        else if (newLocation == LocationManager.Location.廳堂)
        {
            Debug.Log("【GameManager】進入廳堂，觸發對話。");
            StartDialogue("Scene_Hall");
        }
        else if (newLocation == LocationManager.Location.豬欄)
        {
            Debug.Log("【GameManager】進入豬欄，觸發對話。");
            StartDialogue("Scene_PigPen");
        }
        else if (newLocation == LocationManager.Location.外出)
        {
            Debug.Log("【GameManager】進入外出地點，觸發對話。");
            StartDialogue("Scene_Outside_Menu");
        }
    }

    public void OpenPersimmonGame()
    {
        if (persimmonGame != null)
        {
            Debug.Log("【GameManager】進入禾埕，自動開啟柿子小遊戲。");
            SetGameState(GameState.Minigame);
            persimmonGame.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 關閉小遊戲並自動切換回三合院
    /// </summary>
    public void ClosePersimmonGame()
    {
        Debug.Log("【GameManager】柿子小遊戲關閉，返回三合院。");
        SetGameState(GameState.FreeRoam);

        if (LocationManager.Instance != null)
        {
            // 自動切換回中心點，避免待在禾埕卻沒在玩遊戲
            LocationManager.Instance.GoToLocation(LocationManager.Location.三合院);
        }
    }

    /// <summary>
    /// 供 Yarn 腳本檢查：這一輪煮飯是不是已經選過食材、正在進行中（不管有沒有把小遊戲視窗關起來）。
    /// 用來判斷「煮飯」選項要直接恢復小遊戲，還是要先問柴薪、走食材選單。
    /// </summary>
    [YarnFunction("is_kitchen_session_active")]
    public static bool IsKitchenSessionActive()
    {
        return Instance != null && Instance.kitchenIngredientsSelected;
    }

    public void OpenKitchenGame()
    {
        if (!kitchenIngredientsSelected)
        {
            if (kitchenMenu == null) return;

            Debug.Log("【GameManager】進入灶房，尚未選擇食材，開啟煮飯選單。");
            SetGameState(GameState.Minigame);
            kitchenMenu.OpenMenu();
        }
        else
        {
            if (kitchenGame == null) return;

            Debug.Log("【GameManager】進入灶房，食材已選好，開啟廚房小遊戲。");
            SetGameState(GameState.Minigame);
            kitchenGame.OpenGame();
        }
    }

    /// <summary>
    /// 由 KitchenMenu 的煮飯按鈕呼叫：食材已選好，切換到廚房煮飯小遊戲。
    /// </summary>
    public void ConfirmKitchenIngredients()
    {
        kitchenIngredientsSelected = true;

        if (kitchenGame != null)
        {
            kitchenGame.OpenGame();
        }
    }

    /// <summary>
    /// 由 KitchenMiniGame 呼叫：這一輪飯已經盛出且火也熄了（完整跑完一次），
    /// 把「食材已選好」的狀態清掉，下一輪（例如晚飯）才會重新詢問柴薪、重新走食材選單。
    /// </summary>
    public void ResetKitchenIngredientsSelected()
    {
        kitchenIngredientsSelected = false;
    }

    public void CloseKitchenGame()
    {
        Debug.Log("【GameManager】廚房小遊戲關閉，返回三合院。");
        SetGameState(GameState.FreeRoam);

        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.GoToLocation(LocationManager.Location.三合院);
        }
    }

    public void ClosePigFoodGame()
    {
        Debug.Log("【GameManager】豬菜小遊戲關閉，返回三合院。");
        SetGameState(GameState.FreeRoam);

        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.GoToLocation(LocationManager.Location.三合院);
        }
    }

    public void OpenPigFoodGame()
    {
        if (pigFoodGame != null)
        {
            Debug.Log("【GameManager】開啟煮豬菜小遊戲。");
            SetGameState(GameState.Minigame);
            pigFoodGame.OpenPigFoodWindow();
        }
    }

    public void OpenShoppingMenu()
    {
        if (shoppingMenu != null)
        {
            Debug.Log("【GameManager】進入第一市場，開啟商店。");
            SetGameState(GameState.Minigame);
            shoppingMenu.OpenMenu();
        }
    }

    /// <summary>
    /// 供 Yarn 指令使用：從 Scene_TownMenu 開啟市場 UI，跟 open_shop 的差別只在於
    /// 關閉後要回到 Scene_TownMenu，而不是直接跳回三合院。
    /// </summary>
    [YarnCommand("open_market_ui")]
    public static void OpenMarketFromTown()
    {
        if (Instance == null) return;
        Instance.shopOpenedFromTown = true;
        Instance.OpenShoppingMenu();
    }

    public void CloseShoppingMenu()
    {
        SetGameState(GameState.FreeRoam);

        if (shopOpenedFromTown)
        {
            Debug.Log("【GameManager】商店關閉，返回鎮上選單。");
            shopOpenedFromTown = false;
            StartDialogue("Scene_TownMenu");
        }
        else
        {
            Debug.Log("【GameManager】商店關閉，返回三合院。");
            if (LocationManager.Instance != null)
            {
                LocationManager.Instance.GoToLocation(LocationManager.Location.三合院);
            }
        }
    }

    /// <summary>
    /// 全域統一啟動對話的入口
    /// </summary>
    /// <param name="nodeName">Yarn 節點名稱</param>
    public void StartDialogue(string nodeName)
    {
        if (dialogueRunner == null)
        {
            Debug.LogError("【GameManager】找不到 DialogueRunner，無法啟動對話。");
            return;
        }

        if (dialogueRunner.IsDialogueRunning)
        {
            Debug.LogWarning($"【GameManager】對話系統忙碌中，忽略啟動要求：{nodeName}");
            return;
        }

        Debug.Log($"【GameManager】啟動劇情節點：{nodeName}");
        SetGameState(GameState.Dialogue);
        dialogueRunner.StartDialogue(nodeName);

        // 監聽結束事件
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
    }

    /// <summary>
    /// 強制啟動對話（中斷當前狀態）
    /// </summary>
    public void ForceStartDialogue(string nodeName)
    {
        if (dialogueRunner == null) return;

        if (dialogueRunner.IsDialogueRunning)
        {
            dialogueRunner.Stop();
        }

        Debug.Log($"【GameManager】強制啟動劇情：{nodeName}");
        SetGameState(GameState.Dialogue);
        dialogueRunner.StartDialogue(nodeName);
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
    }

    private void OnDialogueComplete()
    {
        dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);

        // 對話中如果有指令（例如 open_kitchen）已經把狀態切到別的模式（如 Minigame），
        // 這裡就不該再蓋回 FreeRoam，只有「還停留在 Dialogue」時才需要自動恢復。
        //
        // 防呆：如果劇情節點忘記寫 <<jump Scene_FreeRoam>> 就結束了，這裡自動補跑一次，
        // 避免畫面卡在半路（背景／角色顯示／傳送位置停留在最後一個場景）。
        //
        // 注意：不能在這個事件處理常式裡直接呼叫 ForceStartDialogue／StartDialogue——
        // 這是對 DialogueRunner 的重入呼叫（它自己的 onDialogueComplete 還沒處理完，
        // 內部還在收尾），實測會讓新對話卡死、畫面完全沒反應。改成設一個旗標，
        // 延到下一次 Update() 才真正觸發，讓 DialogueRunner 有機會先把這一輪收尾完。
        if (currentState == GameState.Dialogue)
        {
            pendingFreeRoamRecovery = true;
        }
    }

    /// <summary>
    /// 切換遊戲模式，並處理模式切換的連帶影響
    /// </summary>
    public void SetGameState(GameState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case GameState.Dialogue:
                // 對話中暫停遊戲時間
                if (TimeManager.Instance != null) TimeManager.Instance.isGamePaused = true;
                break;
            case GameState.FreeRoam:
                // 自由行動恢復時間
                if (TimeManager.Instance != null) TimeManager.Instance.isGamePaused = false;
                break;
            case GameState.Minigame:
                // 進入小遊戲「不」暫停時間（讓廚房等邏輯持續運作）
                if (TimeManager.Instance != null) TimeManager.Instance.isGamePaused = false;
                break;
        }

        // 只有真的在對話時才需要對話框 Canvas 接收點擊；其餘狀態關閉，
        // 避免它底部那塊一直存在的隱形 Blocks Raycasts 區域擋住小遊戲/選單的按鈕。
        if (dialogueCanvas != null)
        {
            dialogueCanvas.SetActive(newState == GameState.Dialogue);
        }

        Debug.Log($"【GameManager】模式切換至：{newState}");
    }

    /// <summary>
    /// 供 Yarn 指令使用的模式切換：<<game_mode "Minigame">>
    /// </summary>
    [YarnCommand("game_mode")]
    public static void SetModeCommand(string modeName)
    {
        if (Instance == null) return;
        if (System.Enum.TryParse(modeName, true, out GameState newState))
        {
            Instance.SetGameState(newState);
        }
    }

}
