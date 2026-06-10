using UnityEngine;
using System.Collections;
using System.Collections.Generic; 

public class GameManager : MonoBehaviour
{
    // 🌟 單例模式 (Singleton)：確保整個遊戲只有一個 GameManager，讓其他腳本可以隨時透過 GameManager.Instance 呼叫它
    public static GameManager Instance;

    // 🌟 狀態機設計：規範遊戲目前處於什麼階段，避免玩家在球還沒停的時候亂出桿
    public enum GameState { Aiming, BallsRolling, Foul, GameOver }
    public GameState currentState = GameState.Aiming;

    [Header("遊戲狀態")]
    public bool isPlayer1Turn = true; // 紀錄球權歸屬
    public bool isTableOpen = true;   // 開放球局 (還沒決定誰打花色/全色)
    public bool isBreakShot = true;   // 是否為開球局 (第一竿)

    [Header("玩家花色群組")]
    public string player1Group = "還沒決定";
    public string player2Group = "還沒決定";

    [Header("記分板")]
    public int player1Score = 0; // 當分數達到 7 時，代表可以打黑 8 了
    public int player2Score = 0;

    [Header("場景綁定")]
    public GameObject cueStick; 
    public Transform footSpot; // 腳點 (開球時打進 8 號球，8 號球要退回這個點)

    // === 本回合狀態追蹤 (每次出桿前都會清空) ===
    private bool validBallPocketedThisTurn = false; // 這回合有沒有合法進球
    private bool isFoulThisTurn = false;            // 這回合有沒有犯規
    private int firstHitBallNumber = -1;            // 記錄母球第一顆撞到的是幾號球
    private bool hasHitAnythingThisTurn = false;    // 母球有沒有撞到東西 (空桿判定)
    private bool hasHitCushionAfterContact = false; // 撞到目標球後，有沒有球碰顆星 (進階規則，目前預留)
    
    // 🌟 運用 HashSet 的「元素唯一性」特性，紀錄這回合有哪幾顆「不同」的球碰到了顆星(桌邊)
    private HashSet<int> cushionHitBallsThisTurn = new HashSet<int>();

    // 🌟 開放局進球暫存：紀錄這回合第一顆掉進去的球的花色，等確定沒犯規才正式鎖定球權
    private string potentialGroupThisTurn = "";

    void Awake() { if (Instance == null) Instance = this; }

    // === 輔助函數：快速取得當前玩家的資訊 ===
    public string GetCurrentPlayerName() { return isPlayer1Turn ? "Player 1" : "Player 2"; }
    public string GetCurrentPlayerGroup() { return isPlayer1Turn ? player1Group : player2Group; }
    public int GetCurrentPlayerScore() { return isPlayer1Turn ? player1Score : player2Score; }

    // ==========================================
    // 1. 玩家出桿瞬間的通知接口 (由 CueStickTrigger 呼叫)
    // ==========================================
    public void NotifyShotTaken()
    {
        if (currentState != GameState.Aiming) return;
        
        // 切換狀態為滾動中，鎖死玩家操作
        currentState = GameState.BallsRolling;
        
        // 重置所有「本回合」的判斷變數
        validBallPocketedThisTurn = false;
        isFoulThisTurn = false;
        firstHitBallNumber = -1; 
        hasHitAnythingThisTurn = false;
        hasHitCushionAfterContact = false; 
        cushionHitBallsThisTurn.Clear();
        potentialGroupThisTurn = "";
        
        // 隱藏球桿，避免阻礙視線或引發物理 Bug
        if (cueStick != null) cueStick.SetActive(false); 
        
        Debug.Log($"🎱 【出桿通知】{GetCurrentPlayerName()} 已出桿！等待所有撞球停下...");
        
        // 啟動協程，開始監視球體狀態
        StartCoroutine(CheckBallsStoppedCoroutine());
    }

    // ==========================================
    // 2. 物理碰撞通知接口 (由 CueBallPhysics 呼叫)
    // ==========================================
    public void NotifyFirstHitBall(int ballNumber)
    {
        // 只紀錄「第一顆」撞到的球
        if (!hasHitAnythingThisTurn)
        {
            firstHitBallNumber = ballNumber;
            hasHitAnythingThisTurn = true;
        }
    }

    public void NotifyCushionHit(int ballNumber)
    {
        // 只要碰到顆星，就把球號塞進 HashSet (同一顆球碰三次牆也只會記 1 次)
        cushionHitBallsThisTurn.Add(ballNumber);
        if (hasHitAnythingThisTurn) 
        {
            hasHitCushionAfterContact = true;
        }
    }

    // ==========================================
    // 3. 核心監視器：等待物理引擎運算完畢
    // ==========================================
    IEnumerator CheckBallsStoppedCoroutine()
    {
        yield return new WaitForSeconds(0.5f); // 先等 0.5 秒，確保母球已經確實離開桿頭並產生碰撞

        Rigidbody[] allBalls = FindObjectsOfType<Rigidbody>();
        bool allStopped = false;
        float maxWaitTime = 15f; // 防呆機制：最長只等 15 秒，避免遇到球卡死無限迴圈的問題
        float timer = 0f;

        // 當「不是所有球都停下」且「還沒超時」
        while (!allStopped && timer < maxWaitTime)
        {
            allStopped = true; // 先假設大家都停了
            foreach (Rigidbody rb in allBalls)
            {
                if (rb != null && rb.gameObject.activeInHierarchy && (rb.name.Contains("Billiard") || rb.name.Contains("CueBall")))
                {
                    // --- 物理例外檢查：球飛出檯面 ---
                    if (rb.transform.position.y < -1f)
                    {
                        int ballNum = -1;
                        if (rb.name.Contains("CueBall")) ballNum = 0;
                        else 
                        {
                            string[] parts = rb.name.Split('_');
                            if (parts.Length > 1) int.TryParse(parts[1], out ballNum);
                        }
                        
                        if (ballNum != 0 && ballNum != -1) 
                        {
                            Debug.Log($"<color=red><b>⚠️ 犯規 (規則3)：{rb.name} 飛出檯面！</b></color>");
                        }
                        
                        // 把飛出去的球當作「進洞(洗袋)」丟給進球邏輯統一處理
                        OnBallPocketed(rb.gameObject, ballNum);
                        continue;
                    }

                    // --- 檢查物理速度 ---
                    // 只要有一顆球的速度 > 0.1，就打破「大家都停了」的假設
                    if (rb.velocity.magnitude > 0.1f || rb.angularVelocity.magnitude > 0.1f)
                    {
                        allStopped = false;
                        break; 
                    }
                    // 優化：如果速度已經極小(微弱滑行)，直接強行歸零，節省 PhysX 的運算資源
                    else if (rb.velocity.magnitude > 0f || rb.angularVelocity.magnitude > 0f)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
            yield return new WaitForSeconds(0.2f); // 每 0.2 秒掃描一次
            timer += 0.2f;
        }

        EndTurn(); // 迴圈結束，代表球全停了，開始進行規則結算
    }

    // ==========================================
    // 4. 進球處理邏輯 (由 PocketTrigger 呼叫)
    // ==========================================
    public void OnBallPocketed(GameObject ballObj, int ballNumber)
    {
        // 隱藏球體並消除殘留的物理動能
        ballObj.SetActive(false);
        Rigidbody rb = ballObj.GetComponent<Rigidbody>();
        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        // --- 犯規判定：母球洗袋 ---
        if (ballNumber == 0) {
            isFoulThisTurn = true;
            Debug.Log("<color=red><b>🚨 【犯規通知】白球進洞犯規 (母球洗袋)！對手將獲得自由球 (Free Ball)。</b></color>");
            return; 
        }

        string ballType = (ballNumber >= 1 && ballNumber <= 7) ? "全色球 (Solids)" : 
                          (ballNumber >= 9 && ballNumber <= 15) ? "花色球 (Stripes)" : "黑球 (8號)";
        Debug.Log($"🎯 【進球通知】打進了 {ballNumber} 號球！ ({ballType})");

        // --- 黑 8 處理 ---
        if (ballNumber == 8) {
            if (isBreakShot) {
                // 特殊規則：開球打進 8 號球不算贏，要把球拿回來放在腳點繼續打
                Debug.Log("<color=orange><b>⚠️ 【開球規則】打進 8 號球！球將退回腳點，開球者保留球權繼續擊球。</b></color>");
                StartCoroutine(Respawn8Ball(ballObj)); 
                return;
            } else {
                Handle8BallPocketed(); // 轉交給遊戲結束邏輯判定
                return;
            }
        }

        string ballGroup = (ballNumber >= 1 && ballNumber <= 7) ? "Solids (全色)" : "Stripes (花色)";

        // --- 開放局處理 ---
        if (isTableOpen) {
            // 先不直接判定花色歸屬，而是先把這回合進的第一顆球記錄下來
            if (string.IsNullOrEmpty(potentialGroupThisTurn)) {
                potentialGroupThisTurn = ballGroup;
            }
            validBallPocketedThisTurn = true;
            if (isPlayer1Turn) player1Score++; else player2Score++;
            return; // 結束，等 EndTurn 才真正確立花色
        }

        // --- 鎖定局處理 ---
        string currentTurnGroup = GetCurrentPlayerGroup();
        if (ballGroup == currentTurnGroup) {
            // 合法進了自己的球
            validBallPocketedThisTurn = true;
            if (isPlayer1Turn) player1Score++; else player2Score++;
            Debug.Log($"✨ 好球！打進了自己的 {ballGroup} 球！");
        } else {
            // 打進了對手的球
            isFoulThisTurn = true;
            if (isPlayer1Turn) player2Score++; else player1Score++;
            Debug.Log("<color=orange><b>⚠️ 【犯規通知】打進了對手的球！本回合進球不歸入我方計分。</b></color>");
        }
    }

    IEnumerator Respawn8Ball(GameObject eightBall)
    {
        yield return new WaitForSeconds(1f); 
        Vector3 spawnPos = footSpot != null ? footSpot.position : new Vector3(0, 0, 0.5f);
        eightBall.transform.position = spawnPos;
        eightBall.SetActive(true);
        Rigidbody rb = eightBall.GetComponent<Rigidbody>();
        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        validBallPocketedThisTurn = true; 
    }

    // ==========================================
    // 5. 回合結束與犯規總結算 (大腦核心)
    // ==========================================
    private void EndTurn()
    {
        if (currentState == GameState.GameOver) return;

        // --- 開球局專屬規則 ---
        if (isBreakShot) 
        {
            int childCushionHits = cushionHitBallsThisTurn.Count;
            // 開球時如果母球有碰顆星，不算在「子球碰顆星」的額度內
            if (cushionHitBallsThisTurn.Contains(0)) childCushionHits--; 

            // 規則：開球若沒進球，且沒有 4 顆子球碰顆星，視為非法開球犯規
            if (!validBallPocketedThisTurn && childCushionHits < 4 && !isFoulThisTurn) {
                isFoulThisTurn = true;
                Debug.Log($"<color=red><b>⚠️ 犯規 (開球規則)：非法開球！沒有子球進袋，且僅有 {childCushionHits} 顆子球碰觸顆星！</b></color>");
            }
            isBreakShot = false; // 第一桿結束，解除開球狀態
        }
        // --- 一般局通用規則 ---
        else 
        {
            // 規則 5：空桿 (沒撞到任何球)
            if (!hasHitAnythingThisTurn && !isFoulThisTurn) {
                isFoulThisTurn = true;
                Debug.Log("<color=red><b>⚠️ 犯規 (規則5)：空桿！母球未撞擊任何子球。</b></color>");
            }

            // 規則 1 延伸：開放局時，母球不能拿 8 號當第一顆球撞
            if (isTableOpen && hasHitAnythingThisTurn && firstHitBallNumber == 8 && !isFoulThisTurn) {
                isFoulThisTurn = true;
                Debug.Log("<color=red><b>⚠️ 犯規 (規則1)：開放局時，母球最先撞擊到 8 號球！</b></color>");
            }

            // 規則 1 主體：撞錯花色
            if (!isTableOpen && hasHitAnythingThisTurn && !isFoulThisTurn) {
                string currentGroup = GetCurrentPlayerGroup();
                string firstHitGroup = (firstHitBallNumber >= 1 && firstHitBallNumber <= 7) ? "Solids (全色)" : "Stripes (花色)";

                if (firstHitBallNumber == 8 && GetCurrentPlayerScore() < 7) {
                    isFoulThisTurn = true;
                    Debug.Log("<color=red><b>⚠️ 犯規 (規則1)：子球未清光前先撞到 8 號球！</b></color>");
                }
                else if (firstHitBallNumber != 8 && firstHitGroup != currentGroup) {
                    isFoulThisTurn = true;
                    Debug.Log($"<color=red><b>⚠️ 犯規 (規則1)：撞錯花色！目標應為 {currentGroup}，但先撞擊了 {firstHitGroup}。</b></color>");
                }
            }
        }

        // --- 最終審判 ---
        // 防呆：如果在進攻黑8時不小心犯規了(例如母球跟著洗袋)，直接判定敗局
        if (GetCurrentPlayerScore() >= 7 && isFoulThisTurn) {
            string loser = GetCurrentPlayerName();
            string winner = isPlayer1Turn ? "Player 2" : "Player 1";
            Debug.Log($"<color=red><b>💀 敗局判定：{loser} 在進攻 8 號球時犯規，直接判定落敗！\n🏆 恭喜 {winner} 獲勝！</b></color>");
            currentState = GameState.GameOver;
            return;
        }

        // 裁決 1：本回合有犯規 -> 啟動自由球機制
        if (isFoulThisTurn) {
            SwitchTurn();
            currentState = GameState.Foul; 
            Debug.Log($"<color=red><b>❌ 【裁判判決】本回合確定犯規！強制換人。\n👉 進入【自由球模式】！請現正進攻的 {GetCurrentPlayerName()} 移動滑鼠至綠色桌面，並「點擊左鍵」擺放母球！</b></color>");
        } 
        // 裁決 2：合法且有進球 -> 繼續打
        else if (validBallPocketedThisTurn) {
            
            // 🌟 嚴謹的球權確立：確定沒犯規，且有進球，如果是開放局才正式分配花色！
            if (isTableOpen && !string.IsNullOrEmpty(potentialGroupThisTurn)) {
                AssignGroups(potentialGroupThisTurn);
            }

            Debug.Log($"🔥 【好球】{GetCurrentPlayerName()} 合法進球！保留球權，請繼續進攻。");
            ResetCueStick(); 
        } 
        // 裁決 3：沒犯規也沒進球 -> 單純換人
        else {
            SwitchTurn();
            Debug.Log($"🔄 【回合交換】本回合無人進球。平穩換人！現在輪到：{GetCurrentPlayerName()} 進攻。");
            ResetCueStick(); 
        }
    }

    private void ResetCueStick()
    {
        currentState = GameState.Aiming;
        if (cueStick != null) cueStick.SetActive(true); 
    }

    private void SwitchTurn()
    {
        string oldPlayer = GetCurrentPlayerName();
        isPlayer1Turn = !isPlayer1Turn;
        string newPlayer = GetCurrentPlayerName();
        
        // 利用 Unity Rich Text 印出醒目的換人分隔線
        Debug.Log($"<color=cyan><b>===========================================</b></color>\n" +
                  $"🔄 <b>【球權切換】</b> 結束 {oldPlayer} 的回合 ➡️ <b>現在輪到：{newPlayer}</b>\n" +
                  $"<color=cyan><b>===========================================</b></color>");
    }

    private void AssignGroups(string firstPocketedGroup)
    {
        string alternativeGroup = (firstPocketedGroup == "Solids (全色)") ? "Stripes (花色)" : "Solids (全色)";
        if (isPlayer1Turn) {
            player1Group = firstPocketedGroup; player2Group = alternativeGroup;
        } else {
            player2Group = firstPocketedGroup; player1Group = alternativeGroup;
        }
        isTableOpen = false;
        Debug.Log($"📢 局勢鎖定！【P1】:{player1Group} / 【P2】:{player2Group}");
    }

    private void Handle8BallPocketed()
    {
        currentState = GameState.GameOver;
        string currentPlayer = GetCurrentPlayerName();
        string opponent = isPlayer1Turn ? "Player 2" : "Player 1";

        // 敗局：子球沒清完就打進黑8
        if (GetCurrentPlayerScore() < 7) {
            Debug.Log($"<color=red><b>💀 【嚴重犯規】{currentPlayer} 尚未清光子球，就提前打進黑 8 球！直接判定落敗！\n🏆 恭喜 {opponent} 獲勝！</b></color>");
        } 
        // 敗局：打進黑8的同時犯規(如洗袋)
        else if (isFoulThisTurn) {
            Debug.Log($"<color=red><b>💀 【敗局判定】{currentPlayer} 在進攻 8 號球時伴隨其他犯規，直接判定落敗！\n🏆 恭喜 {opponent} 獲勝！</b></color>");
        } 
        // 勝局：完美絕殺
        else {
            Debug.Log($"<color=green><b>🎉🎉 【完美絕殺】{currentPlayer} 合法打進黑 8 球，贏得比賽！</b></color>");
        }
    }
}
