using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using telegram_bot_aca.Bot;
using telegram_bot_aca.Bot.Commands;
using telegram_bot_aca.Data;
using telegram_bot_aca.Services;
using Telegram.Bot;

namespace telegram_bot_aca;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = "Data Source=telegrambotaca.db";
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        
        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddOptions<TelegramBotOptions>()
            .Bind(builder.Configuration.GetSection(TelegramBotOptions.SectionName));

        builder.Services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<TelegramBotOptions>>();
            var botToken = options.Value.Token;
            return new TelegramBotClient(botToken);
        });

        builder.Services.AddScoped<IUserService, UserService>();

        builder.Services.AddScoped<ITelegramCommandFactory, TelegramCommandFactory>();
        builder.Services.AddScoped<ITelegramCommand, StartCommand>();
        builder.Services.AddScoped<ITelegramCommand, RegisterCommand>();
        builder.Services.AddScoped<ITelegramCommand, MediaUploadCommand>();
        builder.Services.AddScoped<ITelegramCommand, PendingConversionCommand>();

        builder.Services.AddSingleton<IConversionSessionStore, InMemoryConversionSessionStore>();
        
        builder.Services.AddSingleton<ITelegramUpdateHandler, TelegramUpdateHandler>();

        builder.Services.AddSingleton<IJobQueue, JobQueue>();

        builder.Services.AddScoped<IJobProcessor, MediaConversionJobProcessor>();
        builder.Services.AddScoped<IJobSubmissionService, JobSubmissionService>();

        builder.Services.AddSingleton<INotificationService, TelegramNotificationService>();
        builder.Services.AddHostedService<TelegramBotInitializer>();
        builder.Services.AddHostedService<TelegramPollingService>();
        builder.Services.AddHostedService<JobWorkerHosterService>();
        
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}