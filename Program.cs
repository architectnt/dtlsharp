﻿/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using Discord.Commands;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using dtl.Internal;
using CatBox.NET;

namespace dtl {
    class Program {
        public static DiscordShardedClient client;
        static CommandService commands;
        static InteractionService interaction;
        public static IServiceProvider services;
        readonly HashSet<int> ashd = [];
        bool firststart;

        static readonly System.Timers.Timer memsave = new(3600000);

        static void Main() => new Program().RunAsync().GetAwaiter().GetResult();

        async Task RunAsync()
        {
            RandomProviders.InitializeAll();

            client = new DiscordShardedClient(new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
                MessageCacheSize = 128,
                AlwaysDownloadUsers = true,
            });
            commands = new CommandService(new()
            {
                DefaultRunMode = Discord.Commands.RunMode.Async,
                ThrowOnError = true,
            });
            interaction = new InteractionService(client, new()
            {
                DefaultRunMode = Discord.Interactions.RunMode.Async,
                ThrowOnError = true,
                UseCompiledLambda = true,
                AutoServiceScopes = true,
                EnableAutocompleteHandlers = true,
            });
            services = new ServiceCollection()
                .AddCatBoxServices(f => {
                    f.CatBoxUrl = new Uri("https://catbox.moe/user/api.php");
                    f.LitterboxUrl = new Uri("https://litterbox.catbox.moe/resources/internals/api.php");
                })
                .AddSingleton(client)
                .AddSingleton(commands)
                .AddSingleton(interaction)
                .BuildServiceProvider();
            client.Log += (msg) => {
                Console.WriteLine($"{DateTime.Now:h:mm:ss tt} | {(msg.Exception == null
                    ? $"\u001b[34m{msg.Source}\u001b[0m: {msg.Message}"
                    : $"\u001b[31m{msg.Source}*\u001b[0m: {msg.Exception.Message}\n\u001b[90m{msg.Exception.StackTrace}\u001b[0m")}");
                return Task.CompletedTask;
            };
            client.ShardReady += async (s) => {
                ashd.Add(s.ShardId);
                if (ashd.Count == client.Shards.Count) await ClientReady();
            };

            if(API.settings.token == "") {
                Console.WriteLine("you need to assign your bot token in .core/settings.json!");
                return;
            }

            await client.LoginAsync(TokenType.Bot, API.settings.token);
            await client.StartAsync();
            await client.SetCustomStatusAsync(API.settings.statuses[Random256.Range(API.settings.statuses.Length)]);


            memsave.Elapsed += async (_, _) => {
                await client.SetCustomStatusAsync(API.settings.statuses[Random256.Range(API.settings.statuses.Length)]);
                for(int i = API.modulecache.Count; i-->0;){
                    long sc = API.modulecache.ElementAt(i).Value[0].lastusetime; // uh
                    if((DateTimeOffset.Now.ToUnixTimeSeconds() - sc) >= 3600){
                        API.modulecache.Remove(API.modulecache.ElementAt(i).Key);
                    }
                }
            };
            memsave.Start();

            await Task.Delay(-1);
        }

        async Task ClientReady()
        {
            if (firststart) return;
            firststart = true;

            if(!Directory.Exists(API.tmpdir)){
                Directory.CreateDirectory(API.tmpdir);
            }

            await RegisterCommandsAsync();
            await client.SetStatusAsync(UserStatus.Online);
            Console.WriteLine($"\u001b[31mFUSIONSYSTEM:\u001b[0m core ready");
            DirectoryInfo[] fi = new DirectoryInfo(API.tmpdir).GetDirectories();
            if (fi.Length > 0)
            {
                for (int i = 0; i < fi.Length; i++) fi[i].Delete(true);
                Console.WriteLine($"\u001b[31mFUSIONSYSTEM:\u001b[0m {fi.Length} temp directories deleted");
            }
        }

        static async Task RegisterCommandsAsync()
        {
            client.MessageReceived += HandleCommandAsync;
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

            client.SlashCommandExecuted += async (s) => {
                try
                {
                    ShardedInteractionContext context = new(client, s);
                    await interaction.ExecuteCommandAsync(context, services);
                }
                catch (Exception ex)
                {
                    EmbedBuilder embed = new()
                    {
                        Title = "error!",
                        Description = $"*{ex.GetType().Name}*\n```{ex.Message}```",
                        Color = API.RedColor,
                    };
                    await s.RespondAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
                    Console.WriteLine($"FUSIONSYSTEM: Failed to execute {s.CommandName} with an {ex.GetType().Name} exception\n - {ex.Message}\n");
                }
            };

            client.AutocompleteExecuted += async (arg) => {
                InteractionContext context = new(client, arg, arg.Channel);
                Discord.Interactions.IResult r = await interaction.ExecuteCommandAsync(context, services: services);
                if (!r.IsSuccess)
                {
                    EmbedBuilder embed = new()
                    {
                        Title = "PREDEFINED ERROR_",
                        Description = "autocomplete err",
                        Color = API.RedColor,
                    };
                    embed.AddField($"Command ``'{arg.Data.CommandName}'`` with autocomplete attempted to execute, but an error occured",
                    $"_**Error caused:**_ *{r.Error}*\n```{r.ErrorReason}```");
                    await arg.RespondAsync(embed: embed.Build(), ephemeral: true);
                }
            };
            try
            {
                await interaction.AddModulesAsync(Assembly.GetEntryAssembly(), services);
                await interaction.RegisterCommandsGloballyAsync();
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        }

        static async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = (SocketUserMessage)arg;
            ShardedCommandContext ctx = new(client, msg);
            int apos = 0;
            if (msg.HasStringPrefix("%", ref apos, StringComparison.OrdinalIgnoreCase) || msg.HasStringPrefix("fdx!", ref apos, StringComparison.OrdinalIgnoreCase))
            {
                Discord.Commands.IResult result = await commands.ExecuteAsync(ctx, apos, services);
                if (result.Error == CommandError.UnknownCommand) Console.WriteLine("unknown cmd");
                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand) {
                    EmbedBuilder embed = new()
                    {
                        Title = "error!",
                        Description = $"*{result.Error}*\n```{result.ErrorReason}```",
                        Color = API.RedColor,
                    };
                    await ctx.Message.ReplyAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
                    Console.WriteLine($"FUSIONSYSTEM: Failed to execute {arg} with an {result.Error} exception\n - {result.ErrorReason}\n");
                }
            }
        }
    }
}
