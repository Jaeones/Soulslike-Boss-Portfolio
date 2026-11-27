using System;
using UnityEngine;

public sealed class UIBindingMediator
{
    private readonly Action<PlayerController> _bindPlayer;
    private readonly Action _unbindPlayer;
    private readonly Func<PlayerController> _findPlayer;

    public UIBindingMediator(
        Action<PlayerController> bindPlayer,
        Action unbindPlayer,
        Func<PlayerController> findPlayer = null)
    {
        _bindPlayer = bindPlayer;
        _unbindPlayer = unbindPlayer;
        _findPlayer = findPlayer ?? (() => UnityEngine.Object.FindObjectOfType<PlayerController>());
    }

    public void Rebind(PlayerController preferredPlayer)
    {
        PlayerController resolvedPlayer = preferredPlayer != null ? preferredPlayer : _findPlayer();
        _bindPlayer?.Invoke(resolvedPlayer);
    }

    public void Clear()
    {
        _unbindPlayer?.Invoke();
    }
}
