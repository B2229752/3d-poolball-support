using UnityEngine;

public class BallPhysics : MonoBehaviour
{
    // 在 Unity 面板裡設定這顆球是 Solid(全色), Stripe(花色) 還是 EightBall(8號)
    
    private Rigidbody rb;
    public float stopVelocityThreshold = 0.05f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // 🌟 解除 Unity 預設的旋轉限速 (預設只有 7，改成 100 以上才夠真實)
        rb.maxAngularVelocity = 150f; 
    }

    // 場控 (GameManager) 會來問每顆球停了沒
    public bool IsStopped()
    {
        return rb.velocity.magnitude < stopVelocityThreshold && rb.angularVelocity.magnitude < stopVelocityThreshold;
    }
}