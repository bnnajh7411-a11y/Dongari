using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PlayerHealth))]
public class MountainFallDamageController : MonoBehaviour
{
    private const string TargetSceneName = "Mountain";
    private const float GroundNormalThreshold = 0.5f;

    [SerializeField, Min(0f)] private float minimumFallDistance = 6f;
    [SerializeField, Min(1)] private int minimumFallDamage = 1;
    [SerializeField, Min(1)] private int maximumFallDamage = 3;
    [SerializeField, Min(0.01f)] private float additionalDistancePerDamage = 3f;

    private readonly HashSet<Collider2D> groundContacts = new HashSet<Collider2D>();

    private Collider2D playerCollider;
    private PlayerHealth playerHealth;
    private bool isGrounded;
    private bool hasFallStart;
    private float fallStartFeetY;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != TargetSceneName)
        {
            enabled = false;
            return;
        }

        playerCollider = GetComponent<Collider2D>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollisionContact(collision, true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleCollisionContact(collision, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        HandleCollisionContact(collision, false);
    }

    private void HandleCollisionContact(Collision2D collision, bool isContacting)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        bool hasGroundContact = false;

        if (isContacting)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                if (contact.normal.y > GroundNormalThreshold)
                {
                    hasGroundContact = true;
                    break;
                }
            }
        }

        SetTrackedContact(groundContacts, collision.collider, hasGroundContact);
        RefreshGroundedState();
    }

    private void RefreshGroundedState()
    {
        bool groundedNow = groundContacts.Count > 0;
        if (groundedNow == isGrounded)
        {
            return;
        }

        if (!groundedNow)
        {
            isGrounded = false;
            hasFallStart = true;
            fallStartFeetY = GetFeetY();
            return;
        }

        if (hasFallStart)
        {
            ResolveFallDamage();
        }

        isGrounded = true;
        hasFallStart = false;
    }

    private void ResolveFallDamage()
    {
        float landedFeetY = GetFeetY();
        float fallDistance = fallStartFeetY - landedFeetY;

        if (fallDistance < minimumFallDistance)
        {
            return;
        }

        float extraFallDistance = fallDistance - minimumFallDistance;
        int extraDamage = Mathf.FloorToInt(extraFallDistance / additionalDistancePerDamage);
        int fallDamage = Mathf.Clamp(minimumFallDamage + Mathf.Max(0, extraDamage), minimumFallDamage, maximumFallDamage);

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            return;
        }

        playerHealth.TakeDamage(fallDamage, true);
    }

    private float GetFeetY()
    {
        if (playerCollider == null)
        {
            playerCollider = GetComponent<Collider2D>();
        }

        return playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
    }

    private static void SetTrackedContact(HashSet<Collider2D> contacts, Collider2D other, bool isContacting)
    {
        if (other == null)
        {
            return;
        }

        if (isContacting)
        {
            contacts.Add(other);
            return;
        }

        contacts.Remove(other);
    }
}
