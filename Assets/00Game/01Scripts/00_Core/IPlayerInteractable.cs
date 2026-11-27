using UnityEngine;
using System.Collections.Generic;

public interface IPlayerInteractable
{
    bool RequiresManualInteraction { get; }
    Object PromptOwner { get; }
    Transform InteractionTransform { get; }

    bool CanInteract(PlayerController player);
    int GetInteractionPriority(PlayerController player);
    string GetInteractionLabel(PlayerController player);
    void Interact(PlayerController player);
}

public static class PlayerInteractableRegistry
{
    private static readonly HashSet<IPlayerInteractable> Interactables = new HashSet<IPlayerInteractable>();
    private static readonly List<IPlayerInteractable> StaleInteractables = new List<IPlayerInteractable>();

    public static void Register(IPlayerInteractable interactable)
    {
        if (interactable == null)
        {
            return;
        }

        Interactables.Add(interactable);
    }

    public static void Unregister(IPlayerInteractable interactable)
    {
        if (interactable == null)
        {
            return;
        }

        Interactables.Remove(interactable);
    }

    public static void CopyTo(List<IPlayerInteractable> destination)
    {
        destination.Clear();
        if (Interactables.Count == 0)
        {
            return;
        }

        foreach (IPlayerInteractable interactable in Interactables)
        {
            if (interactable == null || interactable.PromptOwner == null || interactable.InteractionTransform == null)
            {
                StaleInteractables.Add(interactable);
                continue;
            }

            destination.Add(interactable);
        }

        for (int i = 0; i < StaleInteractables.Count; i++)
        {
            Interactables.Remove(StaleInteractables[i]);
        }

        StaleInteractables.Clear();
    }
}
