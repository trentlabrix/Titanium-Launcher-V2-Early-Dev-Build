#pragma once
#include "WorldSetup.h"
namespace MatchFlow
{
inline Titanium::Ref<UWorld> ActiveWorld;
inline bool WarmupActivated = false, WaitingLogged = false;
inline float NextPawnRetry = 0.f;
inline bool Tick(UNetDriver *Driver, AFortGameModeAthena *GM, AFortGameStateAthena *GS)
{
    auto W = Driver ? Driver->World : nullptr;
    if (!W || !GM || !GS)
        return false;
    if (ActiveWorld.Get() != W)
    {
        ActiveWorld.Set(W);
        WarmupActivated = WaitingLogged = false;
        NextPawnRetry = 0.f;
    }
    float Now = Titanium::Now();
    bool Warmup = GS->GamePhase == EAthenaGamePhase::Warmup;
    bool Retry = Warmup && !WarmupActivated && Now >= NextPawnRetry;
    if (Retry)
        NextPawnRetry = Now + 1.f;
    bool PossessedHuman = false;
    for (auto Connection : Driver->ClientConnections)
    {
        auto PC = Connection ? Cast<AFortPlayerControllerAthena>(Connection->PlayerController) : nullptr;
        if (!PC || !PC->PlayerState || PC->PlayerState->bOnlySpectator || !PC->bReadyToStartMatch ||
            !PC->bHasClientFinishedLoading || !PC->bHasServerFinishedLoading)
            continue;
        if (Retry && !PC->Pawn && PC->PlayerState && !PC->PlayerState->bOnlySpectator)
            GM->RestartPlayer(PC); // Native restart calls SpawnDefaultPawnFor and owns possession.
        auto Pawn = Cast<AFortPlayerPawnAthena>(PC->Pawn);
        if (Pawn && !Pawn->IsDead())
        {
            // Native pawn creation can occur after the ready RPC has returned.
            WorldSetup::Request(W);
            if (PC->AcknowledgedPawn == PC->Pawn)
                PossessedHuman = true;
        }
    }
    if (Warmup && !WarmupActivated)
    {
        float Duration = std::max(30.f, GM->WarmupCountdownDuration);
        // This is the gameplay warmup clock, not a network timeout or a fabricated
        // loading flag. Give the real client its warmup after possession is acked.
        GS->WarmupCountdownStartTime = Now;
        GS->WarmupCountdownEndTime = Now + Duration;
        if (PossessedHuman)
        {
            WarmupActivated = true;
            Log("Client possession acknowledged; native warmup clock and bot population started.");
        }
        else if (!WaitingLogged)
        {
            WaitingLogged = true;
            Log("Warmup waiting for native pawn creation and real client possession acknowledgement.");
        }
    }
    return PossessedHuman;
}
} // namespace MatchFlow
