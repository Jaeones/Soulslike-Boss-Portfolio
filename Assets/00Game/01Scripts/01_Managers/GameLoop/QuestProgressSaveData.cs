using System;
using System.Collections.Generic;

[Serializable]
public sealed class QuestProgressSaveData
{
    public string questStep = nameof(QuestProgressStep.Arrival);
    public string currentCheckpointId = string.Empty;
    public string currentCheckpointSceneName = string.Empty;
    public List<string> activatedBonfireIds = new();
    public bool goalKnown;
    public bool sealAccepted;
    public bool bossFightStarted;
    public bool bossIntroSeen;
    public bool bossCleared;
    public bool stoneGolemCleared;
    public bool endingClaimed;
    public bool rearGateOpened;
    public bool bellPepperInstalled;
    public string injuredKnightQuestState = nameof(InjuredKnightQuestState.None);
    public bool injuredKnightQuestItemCollected;

    public QuestProgressSaveData Clone()
    {
        return new QuestProgressSaveData
        {
            questStep = questStep,
            currentCheckpointId = currentCheckpointId,
            currentCheckpointSceneName = currentCheckpointSceneName,
            activatedBonfireIds = activatedBonfireIds != null ? new List<string>(activatedBonfireIds) : new List<string>(),
            goalKnown = goalKnown,
            sealAccepted = sealAccepted,
            bossFightStarted = bossFightStarted,
            bossIntroSeen = bossIntroSeen,
            bossCleared = bossCleared,
            stoneGolemCleared = stoneGolemCleared,
            endingClaimed = endingClaimed,
            rearGateOpened = rearGateOpened,
            bellPepperInstalled = bellPepperInstalled,
            injuredKnightQuestState = injuredKnightQuestState,
            injuredKnightQuestItemCollected = injuredKnightQuestItemCollected
        };
    }

    public static QuestProgressSaveData CreateDefault()
    {
        return new QuestProgressSaveData();
    }

    public QuestProgressStep GetStepOrDefault()
    {
        return Enum.TryParse(questStep, out QuestProgressStep parsed)
            ? parsed
            : QuestProgressStep.Arrival;
    }

    public void SetStep(QuestProgressStep step)
    {
        questStep = step.ToString();
    }

    public InjuredKnightQuestState GetInjuredKnightQuestStateOrDefault()
    {
        return Enum.TryParse(injuredKnightQuestState, out InjuredKnightQuestState parsed)
            ? parsed
            : InjuredKnightQuestState.None;
    }

    public void SetInjuredKnightQuestState(InjuredKnightQuestState state)
    {
        injuredKnightQuestState = state.ToString();
    }
}
