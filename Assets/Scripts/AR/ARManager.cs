using UnityEngine;

public class ARManager : MonoBehaviour
{
    public static ARManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnCard(string qrID, Transform parent)
    {
        CardData data = FindCardByQR(qrID);

        if (data == null)
        {
            Debug.LogWarning("No se encontró carta con QR: " + qrID);
            return;
        }

        if (data.creaturePrefab == null)
        {
            Debug.LogWarning("La carta no tiene prefab asignado");
            return;
        }

        GameObject obj = Instantiate(data.creaturePrefab, parent);
        obj.transform.localPosition = new Vector3(0, 0.1f, 0);
        obj.transform.localRotation = Quaternion.identity;

        Debug.Log("Spawn de: " + data.cardName);
    }

    private CardData FindCardByQR(string qrID)
    {
        Debug.Log("Buscando QR: " + qrID);

        foreach (var player in GameManager.Instance.Players)
        {
            foreach (var card in player.DeckData.cards)
            {
                Debug.Log("Comparando con: " + card.qrID);

                if (card.qrID == qrID)
                {
                    Debug.Log("MATCH ENCONTRADO ✅");
                    return card;
                }
            }
        }

        Debug.LogWarning("NO SE ENCONTRO MATCH ❌");
        return null;
    }
}