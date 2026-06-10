using UnityEngine;

public class CueStickTrigger : MonoBehaviour
{
    [Header("打擊參數")]
    public float powerMultiplier = 5f;  // 力道倍率
    public float maxPower = 6f;         // 最大力道限制

    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    private Collider tipCollider;

    void Start()
    {
        lastPosition = transform.position;
        tipCollider = GetComponent<Collider>();
    }

    // 🌟 關鍵修復：球桿重新啟用時，給它 0.2 秒的冷卻時間！
    void OnEnable()
    {
        lastPosition = transform.position;
        currentVelocity = Vector3.zero;
        
        // 先強制關閉感應器，避免瞬移產生光速衝擊波
        if (tipCollider != null) tipCollider.enabled = false;
        
        // 0.2 秒後再呼叫底下的 EnableCollider 重新開啟感應
        Invoke("EnableCollider", 0.2f);
    }

    void FixedUpdate()
    {
        currentVelocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
        lastPosition = transform.position;
    }

    // 🌟 關鍵修復：從 OnTriggerEnter 改成 OnTriggerStay
    void OnTriggerStay(Collider other)
    {
        CueBallPhysics cueBall = other.GetComponent<CueBallPhysics>();

        if (cueBall != null && cueBall.IsStopped())
        {
            Vector3 hitDirection = transform.forward; 
            float thrustSpeed = Vector3.Dot(currentVelocity, hitDirection);

            // 只要球桿在球體內，且戳刺速度大於 0.05，就立刻判定為有效擊球！
            if (thrustSpeed > 0.05f) 
            {
                float finalPower = Mathf.Clamp(thrustSpeed * powerMultiplier, 0, maxPower);
                
                // 1. 把球打出去！
                cueBall.Strike(hitDirection, finalPower);
                Debug.Log($"實體球桿擊中！戳刺速度: {thrustSpeed}, 最終力道: {finalPower}");

                // 2. 通知裁判「我出桿了」
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.NotifyShotTaken();
                }

                // 防呆機制：打中後瞬間關閉感應器 0.5 秒 (避免一桿產生連續撞擊)
                tipCollider.enabled = false;
                Invoke("EnableCollider", 0.5f);
            }
        }
    }

    void EnableCollider()
    {
        tipCollider.enabled = true;
    }
}