using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerInteractionModule
{
    private const float MaxInteractionDistance = 3.5f;
    private const float MaxInteractionHeightDelta = 3f;
    private const float MaxViewportOffsetSqr = 0.18f;

    private readonly PlayerController _player;
    private readonly KeyCode _interactionKey;
    private readonly List<IPlayerInteractable> _candidates = new List<IPlayerInteractable>();
    private readonly List<IPlayerInteractable> _registeredInteractables = new List<IPlayerInteractable>();
    private readonly List<IPlayerInteractable> _evaluationCandidates = new List<IPlayerInteractable>();
    private readonly HashSet<IPlayerInteractable> _evaluationSet = new HashSet<IPlayerInteractable>();
    private readonly RaycastHit[] _visibilityHits = new RaycastHit[16];

    private IPlayerInteractable _currentInteractable;
    private IPlayerInteractable _shownInteractable;
    private string _shownLabel;

    public PlayerInteractionModule(PlayerController player, KeyCode interactionKey)
    {
        _player = player;
        _interactionKey = interactionKey;
    }

    public KeyCode InteractionKey => _interactionKey;

    public void Tick(PlayerInputSnapshot inputSnapshot, IState currentState)
    {
        RefreshCurrentInteractable();
        RefreshPrompt(currentState);

        if (!inputSnapshot.InteractPressed || !CanProcessInteraction(currentState) || _currentInteractable == null)
        {
            return;
        }

        _currentInteractable.Interact(_player);
        RefreshCurrentInteractable();
        RefreshPrompt(currentState);
    }

    public void RegisterCandidate(IPlayerInteractable interactable)
    {
        if (interactable == null || !interactable.RequiresManualInteraction || _candidates.Contains(interactable))
        {
            return;
        }

        _candidates.Add(interactable);
        RefreshCurrentInteractable();
    }

    public void UnregisterCandidate(IPlayerInteractable interactable)
    {
        if (interactable == null)
        {
            return;
        }

        if (_shownInteractable == interactable)
        {
            HidePrompt(interactable);
            _shownInteractable = null;
            _shownLabel = null;
        }

        _candidates.Remove(interactable);
        if (_currentInteractable == interactable)
        {
            _currentInteractable = null;
        }
    }

    public void ClearCandidates()
    {
        if (_shownInteractable != null)
        {
            HidePrompt(_shownInteractable);
            _shownInteractable = null;
            _shownLabel = null;
        }

        _currentInteractable = null;
        _candidates.Clear();
    }

    private void RefreshCurrentInteractable()
    {
        IPlayerInteractable bestCandidate = null;
        int bestPriority = int.MinValue;
        float bestViewScore = float.MinValue;
        float bestSqrDistance = float.MaxValue;

        BuildEvaluationCandidates();

        for (int i = _evaluationCandidates.Count - 1; i >= 0; i--)
        {
            IPlayerInteractable candidate = _evaluationCandidates[i];
            if (!TryEvaluateCandidate(candidate, out int priority, out float sqrDistance, out float viewScore))
            {
                continue;
            }

            if (bestCandidate == null ||
                priority > bestPriority ||
                (priority == bestPriority && viewScore > bestViewScore) ||
                (priority == bestPriority && Mathf.Approximately(viewScore, bestViewScore) && sqrDistance < bestSqrDistance))
            {
                bestCandidate = candidate;
                bestPriority = priority;
                bestViewScore = viewScore;
                bestSqrDistance = sqrDistance;
            }
        }

        _currentInteractable = bestCandidate;
    }

    private void RefreshPrompt(IState currentState)
    {
        if (!CanProcessInteraction(currentState) || _currentInteractable == null)
        {
            if (_shownInteractable != null)
            {
                HidePrompt(_shownInteractable);
                _shownInteractable = null;
                _shownLabel = null;
            }

            return;
        }

        if (_shownInteractable != null && _shownInteractable != _currentInteractable)
        {
            HidePrompt(_shownInteractable);
            _shownInteractable = null;
            _shownLabel = null;
        }

        string currentLabel = _currentInteractable.GetInteractionLabel(_player);
        if (_shownInteractable == _currentInteractable && _shownLabel == currentLabel)
        {
            return;
        }

        ShowPrompt(_currentInteractable, currentLabel);
        _shownInteractable = _currentInteractable;
        _shownLabel = currentLabel;
    }

    private bool CanProcessInteraction(IState currentState)
    {
        if (_player == null || _player.IsDead || _player.IsBonfireInputLocked || currentState == null)
        {
            return false;
        }

        return currentState == _player.IdleState ||
               currentState == _player.MoveState;
    }

    private bool IsCandidateValid(IPlayerInteractable candidate)
    {
        if (candidate == null || candidate.PromptOwner == null || candidate.InteractionTransform == null)
        {
            return false;
        }

        return candidate.RequiresManualInteraction && candidate.CanInteract(_player);
    }

    private void BuildEvaluationCandidates()
    {
        _evaluationCandidates.Clear();
        _evaluationSet.Clear();

        PlayerInteractableRegistry.CopyTo(_registeredInteractables);
        AddCandidates(_registeredInteractables, unregisterInvalid: false);
        AddCandidates(_candidates, unregisterInvalid: true);
    }

    private void AddCandidates(List<IPlayerInteractable> source, bool unregisterInvalid)
    {
        for (int i = source.Count - 1; i >= 0; i--)
        {
            IPlayerInteractable candidate = source[i];
            if (candidate == null)
            {
                if (unregisterInvalid)
                {
                    source.RemoveAt(i);
                }

                continue;
            }

            if (_evaluationSet.Add(candidate))
            {
                _evaluationCandidates.Add(candidate);
            }
        }
    }

    private bool TryEvaluateCandidate(IPlayerInteractable candidate, out int priority, out float sqrDistance, out float viewScore)
    {
        priority = int.MinValue;
        sqrDistance = float.MaxValue;
        viewScore = float.MinValue;

        if (!IsCandidateValid(candidate))
        {
            if (_shownInteractable == candidate)
            {
                HidePrompt(candidate);
                _shownInteractable = null;
                _shownLabel = null;
            }

            _candidates.Remove(candidate);
            return false;
        }

        Vector3 focusPoint = GetFocusPoint(candidate);
        Vector3 playerPosition = _player.transform.position;
        Vector3 distanceVector = focusPoint - playerPosition;
        float heightDelta = Mathf.Abs(distanceVector.y);
        distanceVector.y = 0f;
        sqrDistance = distanceVector.sqrMagnitude;

        if (heightDelta > MaxInteractionHeightDelta || sqrDistance > MaxInteractionDistance * MaxInteractionDistance)
        {
            return false;
        }

        Camera viewCamera = ResolveViewCamera();
        if (viewCamera != null)
        {
            Vector3 viewportPoint = viewCamera.WorldToViewportPoint(focusPoint);
            if (viewportPoint.z <= 0f)
            {
                return false;
            }

            Vector2 viewportOffset = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
            float viewportOffsetSqr = viewportOffset.sqrMagnitude;
            if (viewportOffsetSqr > MaxViewportOffsetSqr)
            {
                return false;
            }

            if (!HasLineOfSight(candidate, viewCamera.transform.position, focusPoint))
            {
                return false;
            }

            viewScore = 1f - (viewportOffsetSqr / MaxViewportOffsetSqr);
        }
        else
        {
            Vector3 toTarget = focusPoint - playerPosition;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            viewScore = Vector3.Dot(_player.transform.forward, toTarget.normalized);
            if (viewScore <= 0.25f)
            {
                return false;
            }
        }

        priority = candidate.GetInteractionPriority(_player);
        return true;
    }

    private Camera ResolveViewCamera()
    {
        return Camera.main;
    }

    private Vector3 GetFocusPoint(IPlayerInteractable candidate)
    {
        Transform interactionTransform = candidate.InteractionTransform;
        if (interactionTransform == null)
        {
            return _player.transform.position;
        }

        // When an interactable provides an explicit interaction transform,
        // treat that point as the intended prompt location instead of
        // drifting to a child collider's bounds center.
        if (candidate.PromptOwner is Component explicitOwner &&
            explicitOwner.transform != interactionTransform)
        {
            return interactionTransform.position;
        }

        if (TryGetBoundsCenter(interactionTransform, out Vector3 boundsCenter))
        {
            return boundsCenter;
        }

        if (candidate.PromptOwner is Component ownerComponent &&
            ownerComponent.transform != interactionTransform &&
            TryGetBoundsCenter(ownerComponent.transform, out boundsCenter))
        {
            return boundsCenter;
        }

        return interactionTransform.position;
    }

    private bool HasLineOfSight(IPlayerInteractable candidate, Vector3 origin, Vector3 target)
    {
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction / distance,
            _visibilityHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        Collider nearestCollider = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _visibilityHits[i].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
            {
                continue;
            }

            float hitDistance = _visibilityHits[i].distance;
            if (hitDistance < nearestDistance)
            {
                nearestDistance = hitDistance;
                nearestCollider = hitCollider;
            }
        }

        return nearestCollider == null || BelongsToCandidate(candidate, nearestCollider.transform);
    }

    private bool IsPlayerCollider(Collider hitCollider)
    {
        Transform playerTransform = _player.transform;
        Transform hitTransform = hitCollider.transform;
        return hitTransform == playerTransform || hitTransform.IsChildOf(playerTransform);
    }

    private static bool BelongsToCandidate(IPlayerInteractable candidate, Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return false;
        }

        Transform interactionTransform = candidate.InteractionTransform;
        if (interactionTransform != null &&
            (hitTransform == interactionTransform || hitTransform.IsChildOf(interactionTransform) || interactionTransform.IsChildOf(hitTransform)))
        {
            return true;
        }

        if (candidate.PromptOwner is Component ownerComponent)
        {
            Transform ownerTransform = ownerComponent.transform;
            return hitTransform == ownerTransform || hitTransform.IsChildOf(ownerTransform) || ownerTransform.IsChildOf(hitTransform);
        }

        return false;
    }

    private static bool TryGetBoundsCenter(Transform root, out Vector3 boundsCenter)
    {
        Collider rootCollider = root.GetComponent<Collider>();
        if (rootCollider != null)
        {
            boundsCenter = rootCollider.bounds.center;
            return true;
        }

        Collider childCollider = root.GetComponentInChildren<Collider>();
        if (childCollider != null)
        {
            boundsCenter = childCollider.bounds.center;
            return true;
        }

        Renderer renderer = root.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            boundsCenter = renderer.bounds.center;
            return true;
        }

        boundsCenter = Vector3.zero;
        return false;
    }

    private static void HidePrompt(IPlayerInteractable interactable)
    {
        if (interactable?.PromptOwner == null)
        {
            return;
        }

        UIInteractionPrompt.Hide(interactable.PromptOwner);
    }

    private void ShowPrompt(IPlayerInteractable interactable, string label)
    {
        if (interactable?.PromptOwner == null)
        {
            return;
        }

        UIInteractionPrompt.Show(interactable.PromptOwner, _interactionKey, label);
    }
}
