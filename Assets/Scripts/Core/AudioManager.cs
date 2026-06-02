using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Fuentes")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Música")]
    [SerializeField] private AudioClip bgMusic;

    [Header("UI")]
    [SerializeField] private AudioClip turnChangeSFX;
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioClip fightSFX;
    [SerializeField] private AudioClip gameOverSFX;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (musicSource != null && bgMusic != null)
        {
            musicSource.clip = bgMusic;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    private void OnEnable()
    {
        GameEvents.OnGameStateChanged += HandleStateChanged;
        GameEvents.OnTurnChanged += HandleTurnChanged;
        GameEvents.OnCardPlayed += HandleCardPlayed;
        GameEvents.OnCreatureAttacked += HandleCreatureAttacked;
        GameEvents.OnFloopActivated += HandleFloopActivated;
        GameEvents.OnDamageTaken += HandleDamageTaken;
        GameEvents.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStateChanged -= HandleStateChanged;
        GameEvents.OnTurnChanged -= HandleTurnChanged;
        GameEvents.OnCardPlayed -= HandleCardPlayed;
        GameEvents.OnCreatureAttacked -= HandleCreatureAttacked;
        GameEvents.OnFloopActivated -= HandleFloopActivated;
        GameEvents.OnDamageTaken -= HandleDamageTaken;
        GameEvents.OnGameOver -= HandleGameOver;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSFX);
    }

    private void HandleStateChanged(GameState newState)
    {
        if (newState == GameState.Fight)
            PlaySFX(fightSFX);
    }

    private void HandleTurnChanged(int activePlayerIndex)
    {
        PlaySFX(turnChangeSFX);
    }

    private void HandleCardPlayed(int playerIndex, int laneIndex, CardInstance card)
    {
        if (card.Data.cardType == CardType.Spell)
            PlaySFX(card.Data.spellSFX);
    }

    private void HandleCreatureAttacked(int playerIndex, int laneIndex, int damage)
    {
        PlayerState player = GameManager.Instance.Players[playerIndex];
        CardInstance attacker = player.CreatureLanes[laneIndex];
        if (attacker != null)
            PlaySFX(attacker.Data.attackSFX);
    }

    private void HandleFloopActivated(int playerIndex, int laneIndex)
    {
        PlayerState player = GameManager.Instance.Players[playerIndex];
        CardInstance creature = player.CreatureLanes[laneIndex];
        if (creature != null)
            PlaySFX(creature.Data.floopSFX);
    }

    private void HandleDamageTaken(int playerIndex, int laneIndex, CardInstance card)
    {
        PlaySFX(card.Data.damageSFX);
    }

    private void HandleGameOver(int winnerIndex)
    {
        PlaySFX(gameOverSFX);
    }
}
