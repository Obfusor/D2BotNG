using D2BotNG.Core.Protos;
using D2BotNG.Data;
using D2BotNG.Engine;
using Discord;
using Discord.WebSocket;
using Color = Discord.Color;
using MessageType = D2BotNG.Windows.MessageType;

namespace D2BotNG.Services;

/// <summary>
/// Discord bot service using slash commands with rich embeds.
/// Commands: list, status, start, stop, mule, startschedule, stopschedule, restart
/// </summary>
public class DiscordService : BackgroundService
{
    private readonly ILogger<DiscordService> _logger;
    private readonly SettingsRepository _settingsRepository;
    private readonly ProfileRepository _profileRepository;
    private readonly ProfileEngine _profileEngine;

    private DiscordSocketClient? _client;
    private ulong _guildId;
    private readonly HashSet<string> _authenticatedUsers = [];

    // Track current Discord settings to detect changes
    private bool _currentEnabled;
    private string? _currentToken;
    private string? _currentPassword;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private CancellationTokenSource? _clientCts;

    // Embed colors
    private static readonly Color ColorSuccess = new(87, 242, 135);  // Green
    private static readonly Color ColorError = new(237, 66, 69);     // Red
    private static readonly Color ColorInfo = new(88, 101, 242);     // Blurple

    // Discord limits: 6000 char total per message, 25 fields per embed, 1024 chars per field value.
    // We send one embed per message and use a safety margin so a chatty status can't blow the cap.
    private const int MaxFieldsPerEmbed = 25;
    private const int MaxEmbedContentChars = 5500;
    private const int MaxStatusFieldLength = 200;

    public DiscordService(
        ILogger<DiscordService> logger,
        SettingsRepository settingsRepository,
        ProfileRepository profileRepository,
        ProfileEngine profileEngine)
    {
        _logger = logger;
        _settingsRepository = settingsRepository;
        _profileRepository = profileRepository;
        _profileEngine = profileEngine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to settings changes
        _settingsRepository.SettingsChanged += OnSettingsChanged;

        try
        {
            // Initial connection attempt
            await ConnectIfEnabledAsync(stoppingToken);

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            _settingsRepository.SettingsChanged -= OnSettingsChanged;
            await DisconnectAsync();
        }
    }

    private async void OnSettingsChanged(object? sender, Settings settings)
    {
        try
        {
            await HandleSettingsChangeAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Discord settings change");
        }
    }

    private async Task HandleSettingsChangeAsync(Settings settings)
    {
        await _reconnectLock.WaitAsync();
        try
        {
            var discord = settings.Discord;
            var newEnabled = discord.Enabled && !string.IsNullOrEmpty(discord.Token);
            var tokenChanged = discord.Token != _currentToken;
            var enabledChanged = newEnabled != _currentEnabled;

            // Update guild ID
            if (ulong.TryParse(discord.ServerId, out var guildId))
            {
                _guildId = guildId;
            }

            // Clear authenticated users if password changed
            if (discord.Password != _currentPassword)
            {
                _currentPassword = discord.Password;
                if (_authenticatedUsers.Count > 0)
                {
                    _authenticatedUsers.Clear();
                    _logger.LogInformation("Discord password changed, cleared authenticated users");
                }
            }

            // Handle connection state changes
            if (enabledChanged || (newEnabled && tokenChanged))
            {
                if (newEnabled)
                {
                    _logger.LogInformation("Discord settings changed, reconnecting...");
                    await DisconnectAsync();
                    await ConnectAsync(discord.Token!, CancellationToken.None);
                }
                else
                {
                    _logger.LogInformation("Discord disabled, disconnecting...");
                    await DisconnectAsync();
                }
            }
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

    private async Task ConnectIfEnabledAsync(CancellationToken stoppingToken)
    {
        var settings = await _settingsRepository.GetAsync();
        var discord = settings.Discord;

        if (ulong.TryParse(discord.ServerId, out var guildId))
        {
            _guildId = guildId;
        }

        if (!discord.Enabled || string.IsNullOrEmpty(discord.Token))
        {
            _logger.LogInformation("Discord bot disabled or no token configured");
            return;
        }

        await ConnectAsync(discord.Token, stoppingToken);
    }

    private async Task ConnectAsync(string token, CancellationToken stoppingToken)
    {
        _clientCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Info,
            GatewayIntents = GatewayIntents.Guilds
        });

        _client.Log += Log;
        _client.Ready += OnReady;
        _client.SlashCommandExecuted += OnSlashCommandExecuted;

        try
        {
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _currentEnabled = true;
            _currentToken = token;
            _currentPassword = (await _settingsRepository.GetAsync()).Discord.Password;

            _logger.LogInformation("Discord bot started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord bot connection error");
            await DisconnectAsync();
        }
    }

    private async Task DisconnectAsync()
    {
        _currentEnabled = false;
        _currentToken = null;
        _authenticatedUsers.Clear();
        _guildId = 0;

        _clientCts?.Cancel();
        _clientCts?.Dispose();
        _clientCts = null;

        if (_client != null)
        {
            try
            {
                await _client.LogoutAsync();
                await _client.StopAsync();
            }
            catch
            {
                // Ignore disposal errors
            }

            try
            {
                await _client.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
            _client = null;
        }
    }

    private Task Log(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            // Remaining are too spammy
            _ => LogLevel.Debug
        };

        _logger.Log(level, msg.Exception, "{Source}: {Message}", msg.Source, msg.Message);
        return Task.CompletedTask;
    }

    private Task OnReady()
    {
        // Fire-and-forget to avoid blocking the gateway task
        _ = OnReadyAsync();
        return Task.CompletedTask;
    }

    private async Task OnReadyAsync()
    {
        try
        {
            if (_client == null || _guildId == 0) return;

            // Get the guild by ID
            var guild = _client.GetGuild(_guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found - bot may not have access", _guildId);
                return;
            }

            await RegisterSlashCommandsAsync(guild);

            // Send ready message to the first text channel we can access
            var textChannel = guild.TextChannels.FirstOrDefault(c =>
                guild.CurrentUser.GetPermissions(c).SendMessages);

            if (textChannel != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("D2BotNG Online")
                    .WithDescription("Bot is ready. Use `/help` to see available commands.")
                    .WithColor(ColorSuccess)
                    .WithCurrentTimestamp()
                    .Build();

                await textChannel.SendMessageAsync(embed: embed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Ready handler");
        }
    }

    private async Task RegisterSlashCommandsAsync(IGuild guild)
    {
        var commands = new List<SlashCommandBuilder>
        {
            new SlashCommandBuilder()
                .WithName("help")
                .WithDescription("Show available commands"),

            new SlashCommandBuilder()
                .WithName("list")
                .WithDescription("List all profiles"),

            new SlashCommandBuilder()
                .WithName("status")
                .WithDescription("Get profile status")
                .AddOption("profile", ApplicationCommandOptionType.String, "Profile name or 'all'", isRequired: true),

            new SlashCommandBuilder()
                .WithName("start")
                .WithDescription("Start profile(s)")
                .AddOption("profile", ApplicationCommandOptionType.String, "Profile name or 'all'", isRequired: true),

            new SlashCommandBuilder()
                .WithName("stop")
                .WithDescription("Stop profile(s)")
                .AddOption("profile", ApplicationCommandOptionType.String, "Profile name or 'all'", isRequired: true),

            new SlashCommandBuilder()
                .WithName("restart")
                .WithDescription("Restart profile(s)")
                .AddOption("profile", ApplicationCommandOptionType.String, "Profile name or 'all'", isRequired: true),

            new SlashCommandBuilder()
                .WithName("mule")
                .WithDescription("Trigger mule for profile(s)")
                .AddOption("profile", ApplicationCommandOptionType.String, "Profile name or 'all'", isRequired: true),

            new SlashCommandBuilder()
                .WithName("schedule")
                .WithDescription("Enable or disable schedule for profile(s)")
                .AddOption("action", ApplicationCommandOptionType.String, "Enable or disable", isRequired: true,
                    choices: [new ApplicationCommandOptionChoiceProperties { Name = "enable", Value = "enable" },
                              new ApplicationCommandOptionChoiceProperties { Name = "disable", Value = "disable" }])
                .AddOption("profile", ApplicationCommandOptionType.String, "Profile name or 'all'", isRequired: true),
            new SlashCommandBuilder()
                .WithName("identify")
                .WithDescription("Authenticate for privileged commands")
                .AddOption("password", ApplicationCommandOptionType.String, "Server password", isRequired: true),
        };

        try
        {
            foreach (var command in commands)
            {
                await guild.CreateApplicationCommandAsync(command.Build());
            }
            _logger.LogDebug("Registered {Count} slash commands for guild {GuildId}", commands.Count, guild.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register slash commands");
        }
    }

    private async Task OnSlashCommandExecuted(SocketSlashCommand command)
    {
        try
        {
            var settings = await _settingsRepository.GetAsync();
            var userId = command.User.Id.ToString();

            // Only help and identify are unauthenticated; all other commands require password (if one is set)
            if (command.Data.Name != "help" && command.Data.Name != "identify")
            {
                if (settings.Discord.HasPassword && !_authenticatedUsers.Contains(userId))
                {
                    await command.RespondAsync(embed: CreateErrorEmbed("Authentication Required",
                        "You must authenticate first. Use `/identify` with the bot password."), ephemeral: true);
                    return;
                }
            }

            // Commands that return multiple embeds
            if (command.Data.Name == "list")
            {
                var embeds = await HandleList();
                await SendEmbedPagesAsync(command, embeds);
                return;
            }

            if (command.Data.Name == "status")
            {
                var embeds = await HandleStatus(command);
                await SendEmbedPagesAsync(command, embeds);
                return;
            }

            var embed = command.Data.Name switch
            {
                "help" => await HandleHelp(settings.Discord.HasPassword),
                "start" => await HandleStart(command),
                "stop" => await HandleStop(command),
                "restart" => await HandleRestart(command),
                "mule" => await HandleMule(command),
                "schedule" => await HandleSchedule(command),
                "identify" => await HandleIdentify(command, userId),
                _ => CreateErrorEmbed("Unknown Command", "This command is not recognized.")
            };

            await command.RespondAsync(embed: embed, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling slash command: {Command}", command.Data.Name);
            try
            {
                await command.RespondAsync(embed: CreateErrorEmbed("Error", ex.Message), ephemeral: true);
            }
            catch
            {
                // Already responded or other error
            }
        }
    }

    private Task<Embed> HandleHelp(bool authRequired)
    {
        var embed = new EmbedBuilder()
            .WithTitle("D2BotNG Commands")
            .WithColor(ColorInfo)
            .AddField("/list", "List all profiles", inline: true)
            .AddField("/status <profile|all>", "Get profile status", inline: true)
            .AddField("/start <profile|all>", "Start profile(s)", inline: true)
            .AddField("/stop <profile|all>", "Stop profile(s)", inline: true)
            .AddField("/restart <profile|all>", "Restart profile(s)", inline: true)
            .AddField("/mule <profile|all>", "Trigger mule", inline: true)
            .AddField("/schedule <enable|disable> <profile|all>", "Control schedule", inline: true);

        if (authRequired)
        {
            embed.AddField("/identify <password>", "Authenticate for privileged commands", inline: true);
        }

        return Task.FromResult(embed.Build());
    }

    private async Task<List<Embed>> HandleList()
    {
        var profiles = await _profileRepository.GetAllAsync();

        if (!profiles.Any())
        {
            return [CreateInfoEmbed("Profiles", "No profiles configured.")];
        }

        return BuildPagedEmbeds("Profiles", ColorInfo, profiles.Select(profile =>
        {
            var instance = _profileEngine.GetInstance(profile.Name);
            var state = instance?.State.ToString() ?? "Stopped";
            var emoji = GetStateEmoji(instance?.State);
            return ($"{emoji} {profile.Name}", $"State: {state}", true);
        }));
    }

    private async Task<List<Embed>> HandleStatus(SocketSlashCommand command)
    {
        var profileArg = command.Data.Options.First().Value.ToString()!;
        var profiles = await GetTargetProfiles(profileArg);

        if (profiles.Count == 0)
        {
            return [CreateErrorEmbed("Not Found", $"Profile '{profileArg}' not found.")];
        }

        return BuildPagedEmbeds("Profile Status", ColorInfo, profiles.Select(profile =>
        {
            var instance = _profileEngine.GetInstance(profile.Name);
            var state = instance?.State.ToString() ?? "Stopped";
            var emoji = GetStateEmoji(instance?.State);
            var status = instance?.Status;

            var fieldValue = $"**State:** {state}\n" +
                             $"**Runs:** {profile.Runs} | **Chickens:** {profile.Chickens}\n" +
                             $"**Deaths:** {profile.Deaths} | **Crashes:** {profile.Crashes}";

            if (!string.IsNullOrWhiteSpace(status))
            {
                var truncated = status.Length > MaxStatusFieldLength
                    ? string.Concat(status.AsSpan(0, MaxStatusFieldLength - 1), "…")
                    : status;
                fieldValue += $"\n**Status:** {truncated}";
            }

            return ($"{emoji} {profile.Name}", fieldValue, false);
        }));
    }

    private static List<Embed> BuildPagedEmbeds(
        string baseTitle,
        Color color,
        IEnumerable<(string name, string value, bool inline)> fields)
    {
        var pages = new List<Embed>();
        EmbedBuilder? current = null;
        var currentFields = 0;
        var currentChars = 0;

        foreach (var (name, value, inline) in fields)
        {
            var addChars = name.Length + value.Length;

            if (current == null
                || currentFields >= MaxFieldsPerEmbed
                || currentChars + addChars > MaxEmbedContentChars)
            {
                if (current != null) pages.Add(current.Build());
                var title = pages.Count == 0 ? baseTitle : $"{baseTitle} (cont.)";
                current = new EmbedBuilder().WithTitle(title).WithColor(color);
                currentFields = 0;
                currentChars = title.Length;
            }

            current.AddField(name, value, inline);
            currentFields++;
            currentChars += addChars;
        }

        if (current != null && currentFields > 0) pages.Add(current.Build());
        return pages;
    }

    private static async Task SendEmbedPagesAsync(SocketSlashCommand command, IReadOnlyList<Embed> embeds)
    {
        if (embeds.Count == 0) return;
        await command.RespondAsync(embed: embeds[0], ephemeral: true);
        for (var i = 1; i < embeds.Count; i++)
        {
            await command.FollowupAsync(embed: embeds[i], ephemeral: true);
        }
    }

    private async Task<Embed> HandleStart(SocketSlashCommand command)
    {
        var profileArg = command.Data.Options.First().Value.ToString()!;
        var profiles = await GetTargetProfiles(profileArg);

        if (profiles.Count == 0)
        {
            return CreateErrorEmbed("Not Found", $"Profile '{profileArg}' not found.");
        }

        foreach (var profile in profiles)
        {
            await _profileEngine.StartProfileAsync(profile.Name);
        }

        return CreateSuccessEmbed("Started", $"Started {profiles.Count} profile(s).");
    }

    private async Task<Embed> HandleStop(SocketSlashCommand command)
    {
        var profileArg = command.Data.Options.First().Value.ToString()!;
        var profiles = await GetTargetProfiles(profileArg);

        if (profiles.Count == 0)
        {
            return CreateErrorEmbed("Not Found", $"Profile '{profileArg}' not found.");
        }

        foreach (var profile in profiles)
        {
            await _profileEngine.StopProfileAsync(profile.Name);
        }

        return CreateSuccessEmbed("Stopped", $"Stopped {profiles.Count} profile(s).");
    }

    private async Task<Embed> HandleRestart(SocketSlashCommand command)
    {
        var profileArg = command.Data.Options.First().Value.ToString()!;
        var profiles = await GetTargetProfiles(profileArg);

        if (profiles.Count == 0)
        {
            return CreateErrorEmbed("Not Found", $"Profile '{profileArg}' not found.");
        }

        foreach (var profile in profiles)
        {
            await _profileEngine.RestartProfileAsync(profile.Name);
        }

        return CreateSuccessEmbed("Restarted", $"Restarted {profiles.Count} profile(s).");
    }

    private async Task<Embed> HandleMule(SocketSlashCommand command)
    {
        var profileArg = command.Data.Options.First().Value.ToString()!;
        var profiles = await GetTargetProfiles(profileArg);

        if (profiles.Count == 0)
        {
            return CreateErrorEmbed("Not Found", $"Profile '{profileArg}' not found.");
        }

        foreach (var profile in profiles)
        {
            _profileEngine.SendMessage(profile.Name, MessageType.Mule, "mule");
        }

        return CreateSuccessEmbed("Mule Triggered", $"Sent mule command to {profiles.Count} profile(s).");
    }

    private async Task<Embed> HandleSchedule(SocketSlashCommand command)
    {
        var options = command.Data.Options.ToList();
        var action = options[0].Value.ToString()!;
        var profileArg = options[1].Value.ToString()!;
        var enabled = action == "enable";

        var profiles = await GetTargetProfiles(profileArg);

        if (profiles.Count == 0)
        {
            return CreateErrorEmbed("Not Found", $"Profile '{profileArg}' not found.");
        }

        foreach (var profile in profiles)
        {
            profile.ScheduleEnabled = enabled;
            await _profileRepository.UpdateAsync(profile);
            await _profileEngine.NotifyProfileStateChangedAsync(profile.Name, includeProfile: true);
        }

        var actionText = enabled ? "enabled" : "disabled";
        return CreateSuccessEmbed("Schedule Updated", $"Schedule {actionText} for {profiles.Count} profile(s).");
    }

    private async Task<Embed> HandleIdentify(SocketSlashCommand command, string userId)
    {
        var password = command.Data.Options.First().Value.ToString()!;
        var settings = await _settingsRepository.GetAsync();

        if (!settings.Discord.HasPassword)
        {
            return CreateInfoEmbed("Not Required", "No password is configured, authentication not required.");
        }

        if (settings.Discord.Password != password)
        {
            return CreateErrorEmbed("Authentication Failed", "Incorrect password.");
        }

        _authenticatedUsers.Add(userId);
        return CreateSuccessEmbed("Authenticated", "You have been authenticated successfully.");
    }

    private async Task<List<Profile>> GetTargetProfiles(string args)
    {
        var allProfiles = await _profileRepository.GetAllAsync();

        if (string.IsNullOrEmpty(args))
            return [];

        if (args.Equals("all", StringComparison.OrdinalIgnoreCase))
            return allProfiles.ToList();

        var profile = allProfiles.FirstOrDefault(p =>
            p.Name.Equals(args, StringComparison.OrdinalIgnoreCase));

        return profile != null ? [profile] : [];
    }

    private static string GetStateEmoji(RunState? state) => state switch
    {
        RunState.Running => "🟢",
        RunState.Starting => "🟡",
        RunState.Stopping => "🟡",
        RunState.Error => "🔴",
        _ => "⚫"
    };

    private static Embed CreateEmbed(string title, string description, Color color) =>
        new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithCurrentTimestamp()
            .Build();

    private static Embed CreateSuccessEmbed(string title, string description) =>
        CreateEmbed(title, description, ColorSuccess);

    private static Embed CreateErrorEmbed(string title, string description) =>
        CreateEmbed(title, description, ColorError);

    private static Embed CreateInfoEmbed(string title, string description) =>
        CreateEmbed(title, description, ColorInfo);
}
