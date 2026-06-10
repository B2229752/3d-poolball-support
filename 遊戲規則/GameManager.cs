using UnityEngine;
using System.Collections; 

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState { Aiming, BallsRolling, Foul, GameOver }
    public GameState currentState = GameState.Aiming;

    [Header("遊戲狀態")]
    public bool isPlayer1Turn = true;
    public bool isTableOpen = true;

    [Header("玩家花色")]
    public string player1Group = "還沒決定";
    public string player2Group = "還沒決定";

    [Header("記分板")]
    public int player1Score = 0;
    public int player2Score = 0;

    [Header("場景綁定")]
    public GameObject cueStick; 

    private bool validBallPocketedThisTurn = false;
    private bool isFoulThisTurn = false;

    void Awake() { if (Instance == null) Instance = this; }

    public void NotifyShotTaken()
    {
        if (currentState != GameState.Aiming) return;
        
        currentState = GameState.BallsRolling;
        validBallPocketedThisTurn = false;
        isFoulThisTurn = false;
        
        if (cueStick != null) cueStick.SetActive(false); 
        
        Debug.Log("🎱 擊球！等待所有球停下...");
        StartCoroutine(CheckBallsStoppedCoroutine());
    }

    IEnumerator CheckBallsStoppedCoroutine()
    {
        yield return new WaitForSeconds(0.5f); 

        Rigidbody[] allBalls = FindObjectsOfType<Rigidbody>();
        bool allStopped = false;
        
        float maxWaitTime = 15f; 
        float timer = 0f;

        while (!allStopped && timer < maxWaitTime)
        {
            allStopped = true;
            
            foreach (Rigidbody rb in allBalls)
            {
                if (rb != null && rb.gameObject.activeInHierarchy && rb.name.Contains("Billiard"))
                {
                    // 🌟 新增規則：如果有球飛出宇宙 (掉下桌子高度 < -1)，直接視同進洞/洗袋處理！
                    if (rb.transform.position.y < -1f)
                    {
                        Debug.Log($"⚠️ {rb.name} 飛出桌外了！");
                        int ballNum = -1;
                        string[] parts = rb.name.Split('_');
                        if (parts.Length > 1) int.TryParse(parts[1], out ballNum);
                        
                        OnBallPocketed(rb.gameObject, ballNum);
                        continue;
                    }

                    float speed = rb.velocity.magnitude;
                    float spin = rb.angularVelocity.magnitude;

                    if (speed > 0.1f || spin > 0.1f)
                    {
                        allStopped = false;
                        break; 
                    }
                    else if (speed > 0f || spin > 0f)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
            
            yield return new WaitForSeconds(0.2f); 
            timer += 0.2f;
        }

        Debug.Log("✅ 所有球已確認停止，開始結算！");
        EndTurn(); 
    }

    public void OnBallPocketed(GameObject ballObj, int ballNumber)
    {
        ballObj.SetActive(false);
        Rigidbody rb = ballObj.GetComponent<Rigidbody>();
        if (rb != null) {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"進洞了！球號: {ballNumber}");

        // 👑 規則 1：母球洗袋 (0號球) -> 判定犯規，交給 FreeBallPlacement 處理
        if (ballNumber == 0) {
            isFoulThisTurn = true;
            Debug.Log("⚠️ 犯規：母球洗袋！對手獲得自由球！");
            return; 
        }

        // 👑 規則 2：8 號球結算
        if (ballNumber == 8) {
            Handle8Ball();
            return;
        }

        string pocketedGroup = (ballNumber >= 1 && ballNumber <= 7) ? "Solids (全色)" : "Stripes (花色)";

        if (isTableOpen) {
            AssignGroups(pocketedGroup);
        }

        string currentTurnGroup = isPlayer1Turn ? player1Group : player2Group;
        if (pocketedGroup == currentTurnGroup) {
            validBallPocketedThisTurn = true;
            if (isPlayer1Turn) player1Score++; else player2Score++;
        } else {
            if (isPlayer1Turn) player2Score++; else player1Score++;
        }
    }

    private void EndTurn()
    {
        if (currentState == GameState.GameOver) return;

        // 👑 規則結算：如果是犯規，狀態切換為 Foul，啟動自由球機制
        if (isFoulThisTurn) {
            SwitchTurn();
            currentState = GameState.Foul; 
            Debug.Log("⚠️ 犯規結算：請使用滑鼠擺放自由球！");
        } 
        else if (!validBallPocketedThisTurn) {
            SwitchTurn();
            ResetCueStick(); 
        } 
        else {
            Debug.Log("🔥 好球！繼續打！");
            ResetCueStick(); 
        }
    }

    private void ResetCueStick()
    {
        currentState = GameState.Aiming;
        if (cueStick != null) {
            cueStick.SetActive(true); 
        }
    }

    private void SwitchTurn()
    {
        isPlayer1Turn = !isPlayer1Turn;
        string pName = isPlayer1Turn ? "Player 1" : "Player 2";
        Debug.Log($"🔄 換人！現在輪到 {pName}");
    }

    private void AssignGroups(string firstGroup)
    {
        string secondGroup = firstGroup == "Solids (全色)" ? "Stripes (花色)" : "Solids (全色)";
        if (isPlayer1Turn) {
            player1Group = firstGroup; player2Group = secondGroup;
        } else {
            player2Group = firstGroup; player1Group = secondGroup;
        }
        isTableOpen = false;
    }

    private void Handle8Ball() {
        int currentScore = isPlayer1Turn ? player1Score : player2Score;
        if (currentScore >= 7 && !isFoulThisTurn) {
            Debug.Log("🎉 合法打進 8 號球，獲勝！");
        } else {
            Debug.Log("💀 犯規敗！對手獲勝！");
        }
        currentState = GameState.GameOver;
    }
}