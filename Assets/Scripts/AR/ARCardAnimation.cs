using UnityEngine;

public class ARCardAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Controller custom con solo los 4 estados")]
    [Tooltip("Asigna aquí el AnimatorController template (Idle, Attack, Damage, Death). Se reemplaza en Setup().")]
    public RuntimeAnimatorController templateController;

    [Header("Clips para timing automático")]
    public AnimationClip clipAttack;
    public AnimationClip clipDamage;
    public AnimationClip clipDeath;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void Setup()
    {
        if (animator != null && templateController != null)
        {
            animator.runtimeAnimatorController = templateController;
            Invoke(nameof(PlayIdle), 0.05f);
        }
    }

    public float GetAttackLength() => clipAttack != null ? clipAttack.length : 0.6f;
    public float GetDamageLength() => clipDamage != null ? clipDamage.length : 0.4f;
    public float GetDeathLength()  => clipDeath != null ? clipDeath.length : 1.2f;

    public void PlayIdle()
    {
        if (animator != null)
            animator.Play("Idle", 0, 0f);
    }

    public void PlayAttack()
    {
        if (animator != null)
            animator.Play("Attack", 0, 0f);
    }

    public void PlayDamage()
    {
        if (animator != null)
            animator.Play("Damage", 0, 0f);
    }

    public void PlayDeath()
    {
        if (animator != null)
            animator.Play("Death", 0, 0f);
    }
}
