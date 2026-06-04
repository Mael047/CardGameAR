using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Música")]
    [SerializeField] private EventReference bgMusic;

    [Header("UI")]
    [SerializeField] private EventReference turnChangeSFX;
    [SerializeField] private EventReference buttonClickSFX;
    [SerializeField] private EventReference fightSFX;
    [SerializeField] private EventReference gameOverSFX;

    private EventInstance musicInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (!bgMusic.IsNull)
        {
            musicInstance = RuntimeManager.CreateInstance(bgMusic);
            musicInstance.start();
        }
    }

    private void OnDestroy()
    {
        musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        musicInstance.release();
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

    public void PlaySFX(EventReference eventRef)
    {
        if (!eventRef.IsNull)
            RuntimeManager.PlayOneShot(eventRef);
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
            PlaySFX(card.Data.spellSFX); // Este campo en CardData también debe ser EventReference
    }

    private void HandleCreatureAttacked(int playerIndex, int laneIndex, int damage)
    {
        PlayerState player = GameManager.Instance.Players[playerIndex];
        CardInstance attacker = player.CreatureLanes[laneIndex];
        if (attacker != null)
            PlaySFX(attacker.Data.attackSFX); // EventReference en CardData
    }

    private void HandleFloopActivated(int playerIndex, int laneIndex)
    {
        PlayerState player = GameManager.Instance.Players[playerIndex];
        CardInstance creature = player.CreatureLanes[laneIndex];
        if (creature != null)
            PlaySFX(creature.Data.floopSFX); // EventReference en CardData
    }

    private void HandleDamageTaken(int playerIndex, int laneIndex, CardInstance card)
    {
        PlaySFX(card.Data.damageSFX); // EventReference en CardData
    }

    private void HandleGameOver(int winnerIndex)
    {
        PlaySFX(gameOverSFX);
    }
}