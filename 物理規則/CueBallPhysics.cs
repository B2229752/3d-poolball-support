using UnityEngine;

// 強制要求掛這個腳本的物件一定要有 Rigidbody
[RequireComponent(typeof(Rigidbody))]
public class CueBallPhysics : MonoBehaviour
{
    private Rigidbody rb;
    
    [Header("物理判定參數")]
    public float stopVelocityThreshold = 0.05f; // 速度低於多少算「停下來」

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // 🌟 解除 Unity 預設的旋轉限速 (預設只有 7，改成 100 以上才夠真實)
        rb.maxAngularVelocity = 150f; 
    }

    // 1. 物理規則：接收球桿的撞擊力道
    public void Strike(Vector3 forceDirection, float hitPower)
    {
        // ForceMode.Impulse 代表「瞬間的衝擊力」，最適合用來模擬撞球擊球
        rb.AddForce(forceDirection * hitPower, ForceMode.Impulse);
    }

    // 2. 物理規則：判斷這顆球到底停了沒
    public bool IsStopped()
    {
        // 檢查直線速度和旋轉速度是不是都已經趨近於零
        bool isNotMoving = rb.velocity.magnitude < stopVelocityThreshold;
        bool isNotSpinning = rb.angularVelocity.magnitude < stopVelocityThreshold;
        
        return isNotMoving && isNotSpinning;
    }
}