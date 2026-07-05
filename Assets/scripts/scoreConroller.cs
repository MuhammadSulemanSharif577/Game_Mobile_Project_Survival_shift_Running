using UnityEngine;

public class scoreController : MonoBehaviour
{

   public static int CoinCount;
   [SerializeField] GameObject textBox;
   [SerializeField]  int internalCoinCount;
   
    void Update()
    {
        internalCoinCount = CoinCount;
        if (textBox != null)
        {
            var textMesh = textBox.GetComponent<TMPro.TMP_Text>();
            if (textMesh != null)
            {
                textMesh.text = CoinCount.ToString();
            }
        }
    }
}
