using UnityEngine;

public class FreeBallPlacement : MonoBehaviour
{
    [Header("核心物件")]
    public Transform cueBall;     // 母球
    public GameObject cueStick;   // 整根球桿

    private Rigidbody cueBallRb;

    void Start()
    {
        if (cueBall != null) cueBallRb = cueBall.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (GameManager.Instance.currentState != GameManager.GameState.Foul) 
            return;

        if (!cueBall.gameObject.activeSelf)
        {
            cueBall.gameObject.SetActive(true);
            cueStick.SetActive(false); 

            // 🌟 將球變成「不受重力影響的幽靈狀態」，避免移動時跟著滑鼠亂滾
            cueBallRb.isKinematic = true; 
        }

        Camera activeCam = null;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.enabled && cam.gameObject.activeInHierarchy)
            {
                activeCam = cam;
                break;
            }
        }

        if (activeCam == null) return;

        Ray ray = activeCam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.name == "Table_Bed")
            {
                // 抓取真實半徑，並額外加上 0.05 的安全高度，讓球有一點點「降落」的感覺，絕對不會卡進桌子
                float ballRadius = cueBall.GetComponent<SphereCollider>().radius * cueBall.localScale.y;
                cueBall.position = hit.point + new Vector3(0, ballRadius + 0.05f, 0);

                if (Input.GetMouseButtonDown(0))
                {
                    Debug.Log("✅ 自由球擺放完成！換人開球。");
                    
                    // 🌟 放下時，解除幽靈狀態，恢復正常物理重力！
                    cueBallRb.isKinematic = false; 
                    
                    cueStick.SetActive(true);
                    GameManager.Instance.currentState = GameManager.GameState.Aiming;
                }
            }
        }
    }
}