using UnityEngine;

public class StateMachine
{
    public IState CurrentState { get; private set; }

    public void Initialize(IState startingState)
    {
        CurrentState = startingState;
        startingState.Enter();
    }

    public void ChangeState(IState newState)
    {
        if (CurrentState != null)
        {
            CurrentState.Exit();
        }

        CurrentState = newState;
        CurrentState.Enter();
    }
}