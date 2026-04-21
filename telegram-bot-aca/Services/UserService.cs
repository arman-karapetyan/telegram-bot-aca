using Microsoft.EntityFrameworkCore;
using telegram_bot_aca.Data;
using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Services;

public interface IUserService
{
    Task<AppUser> RegisterTelegramUserAsync(long chatId, string username, CancellationToken cancellationToken);
    Task<AppUser?> GetByTelegramChatIdAsync(long chatId, CancellationToken cancellationToken);
}

public class UserService:IUserService
{
    private readonly AppDbContext _dbContext;

    public UserService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<AppUser> RegisterTelegramUserAsync(long chatId, string username, CancellationToken cancellationToken)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            throw new InvalidOperationException("Username cannot be empty.");
        }

        var existingChatUser =
            await _dbContext.Users.FirstOrDefaultAsync(x => x.TelegramChatId == chatId, cancellationToken);

        if (existingChatUser is not null)
        {
            throw new InvalidOperationException("User with this chat id already exists.");
        }

        var existingName =
            await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);
        if (existingName is not null)
        {
            throw new InvalidOperationException("User with this username already exists, Please choose another one.");
        }
        
        var user = new AppUser
        {
            TelegramChatId = chatId,
            Username = normalizedUsername
        };
        
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task<AppUser?> GetByTelegramChatIdAsync(long chatId, CancellationToken cancellationToken)
    {
        return _dbContext.Users.FirstOrDefaultAsync(x => x.TelegramChatId == chatId, cancellationToken);
    }
}