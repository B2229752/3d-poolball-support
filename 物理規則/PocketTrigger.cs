using UnityEngine;

public class PocketTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // 確保掉進來的是撞球
        if (other.name.Contains("Billiard") || other.name.Contains("CueBall"))
        {
            int ballNumber = -1;

            // 自動從球的名字萃取出號碼 (針對你的命名格式：Billiard_03_Solid)
            string[] parts = other.name.Split('_');
            if (parts.Length > 1)
            {
                int.TryParse(parts[1], out ballNumber);
            }

            // 如果有成功抓到號碼，把「球的物件」跟「號碼」一起交給裁判
            if (ballNumber != -1)
            {
                GameManager.Instance.OnBallPocketed(other.gameObject, ballNumber);
            }
        }
    }
}