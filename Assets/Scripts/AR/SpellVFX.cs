using UnityEngine;

public class SpellVFX : MonoBehaviour
{
    public static SpellVFX Instance { get; private set; }

    [Header("Prefab global de partículas (fallback)")]
    public ParticleSystem globalSpellEffect;

    [Header("Colores placeholder (fallback)")]
    public Color scienceBlastColor = new Color(1f, 0.3f, 0.2f);
    public Color candyPushColor = new Color(0.2f, 0.6f, 1f);
    public Color booToYouColor = new Color(0.7f, 0.3f, 0.9f);
    public Color smellColor = new Color(0.3f, 0.8f, 0.2f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void PlayAtLane(int playerIndex, int laneIndex, ParticleSystem cardEffect = null, Color? fallbackColor = null)
    {
        Transform anchor = GetLaneAnchor(playerIndex, laneIndex);
        if (anchor == null) return;

        Vector3 pos = anchor.position;

        // 1. Usar el prefab específico de la carta (CardData.spellEffect)
        if (cardEffect != null)
        {
            Instantiate(cardEffect, pos, Quaternion.identity);
            return;
        }

        // 2. Fallback: global prefab
        if (globalSpellEffect != null)
        {
            ParticleSystem ps = Instantiate(globalSpellEffect, pos, Quaternion.identity);
            if (fallbackColor.HasValue)
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = fallbackColor.Value;
            }
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(ps.gameObject, duration);
            return;
        }

        // 3. Placeholder esfera
        if (fallbackColor.HasValue)
            SpawnPlaceholder(pos, fallbackColor.Value);
    }

    private void SpawnPlaceholder(Vector3 position, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SpellVFX_Placeholder";
        go.transform.position = position;
        go.transform.localScale = Vector3.zero;

        Renderer r = go.GetComponent<Renderer>();
        r.material.color = color;

        PlaceholderAnim anim = go.AddComponent<PlaceholderAnim>();
        anim.Play(0.5f);
    }

    private Transform GetLaneAnchor(int playerIndex, int laneIndex)
    {
        ARBoardManager board = ARBoardManager.Instance;
        if (board == null) return null;
        return playerIndex == 0 ? board.player1Lanes[laneIndex] : board.player2Lanes[laneIndex];
    }

    private class PlaceholderAnim : MonoBehaviour
    {
        public void Play(float duration) => StartCoroutine(Animate(duration));

        private System.Collections.IEnumerator Animate(float duration)
        {
            float t = 0;
            Vector3 from = Vector3.zero;
            Vector3 to = Vector3.one * 1.5f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                transform.localScale = Vector3.Lerp(from, to, p * p);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
