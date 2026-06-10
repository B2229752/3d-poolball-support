using UnityEngine;

public class DesktopCueController : MonoBehaviour
{
    [Header("核心物件")]
    public Transform cueBall; // 母球
    public Transform cueTip;  // 桿頭

    [Header("操控靈敏度")]
    public float aimSensitivity = 3f;
    public float strokeSensitivity = 0.02f;
    public float freeMoveSpeed = 1.5f;

    [Header("距離限制")]
    public float minDistance = 0.02f;
    public float maxDistance = 0.5f;

    public float currentAngle = 0f;
    public float currentDistance = 0.2f;

    [Header("狀態切換")]
    public bool isLockedOnBall = true;

    // 🌟 新增：記錄現在是「瞄準」還是「蓄力打擊」模式
    public enum ControlMode { Aiming, Powering }
    public ControlMode currentMode = ControlMode.Aiming;

    void Start()
    {
        UpdateCuePosition();
    }

    void OnEnable()
    {
        isLockedOnBall = true;
        currentMode = ControlMode.Aiming; // 每次重新出現，預設回到瞄準模式
        currentDistance = 0.2f; // 重置抽桿距離
        UpdateCuePosition();
    }

    void Update()
    {
        if (cueBall == null || cueTip == null) return;

        // --- 滑鼠右鍵：切換自由飛行 / 鎖定母球 ---
        if (Input.GetMouseButtonDown(1))
        {
            isLockedOnBall = !isLockedOnBall;
            Debug.Log(isLockedOnBall ? "🔒 鎖定母球模式" : "🕊️ 自由移動模式");
        }

        if (isLockedOnBall)
        {
            // 🌟 左鍵單擊切換模式：瞄準 <-> 蓄力
            if (Input.GetMouseButtonDown(0))
            {
                if (currentMode == ControlMode.Aiming)
                {
                    currentMode = ControlMode.Powering;
                    Debug.Log("🔥 切換至：蓄力打擊模式 (上下移動滑鼠抽桿)");
                }
                else
                {
                    currentMode = ControlMode.Aiming;
                    Debug.Log("🎯 切換至：瞄準模式 (左右移動滑鼠瞄準)");
                }
            }

            // 根據當下模式執行不同操作
            if (currentMode == ControlMode.Powering)
            {
                // 蓄力模式：只允許前後抽桿 (用 Mouse Y)
                float mouseY = Input.GetAxis("Mouse Y");
                currentDistance -= mouseY * strokeSensitivity;
                currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            }
            else
            {
                // 瞄準模式：只允許左右旋轉 (用 Mouse X)
                float mouseX = Input.GetAxis("Mouse X");
                currentAngle += mouseX * aimSensitivity;
            }

            UpdateCuePosition();
        }
        else
        {
            // --- 自由移動模式 ---
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            float moveY = 0f;
            if (Input.GetKey(KeyCode.E)) moveY = 1f;
            if (Input.GetKey(KeyCode.Q)) moveY = -1f;

            Vector3 moveDir = new Vector3(moveX, moveY, moveZ);
            transform.Translate(moveDir * freeMoveSpeed * Time.deltaTime, Space.World);

            float mouseX = Input.GetAxis("Mouse X");
            transform.Rotate(Vector3.up, mouseX * aimSensitivity, Space.World);
        }
    }

    private void UpdateCuePosition()
    {
        if (cueBall != null && cueTip != null)
        {
            Quaternion rotation = Quaternion.Euler(5f, currentAngle, 0f);
            transform.rotation = rotation;

            Vector3 direction = rotation * Vector3.forward;
            Vector3 targetTipPosition = cueBall.position - direction * currentDistance;
            
            Vector3 tipOffset = transform.position - cueTip.position;
            transform.position = targetTipPosition + tipOffset;
        }
    }
}