namespace telegram_bot_aca.Services;

public enum JobCancelOutcome
{
    NotFound=1,
    AlreadyFinalized=2,
    CancelledQueued=3,
    CancellationRequested=4
}