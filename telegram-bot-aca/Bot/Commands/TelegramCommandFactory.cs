namespace telegram_bot_aca.Bot.Commands;

public class TelegramCommandFactory:ITelegramCommandFactory
{
    private readonly IEnumerable<ITelegramCommand> _commands;

    public TelegramCommandFactory(IEnumerable<ITelegramCommand> commands)
    {
        _commands = commands;
    }
    
    public ITelegramCommand Resolve(TelegramCommandContext context)
    {
        return _commands.FirstOrDefault(x => x.CanHandle(context)) ??
               throw new InvalidOperationException("No command found for this message");
    }
}