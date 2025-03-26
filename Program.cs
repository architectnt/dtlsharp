/*
    This is a part of DigitalOut and is licenced under MIT.
*/

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
        static InteractionService interaction;
        public static IServiceProvider services;
        bool firststart;
        uint shr = 0;

        static readonly System.Timers.Timer memsave = new(3600000);

        static void Main() => new Program().RunAsync().GetAwaiter().GetResult();

        async Task RunAsync() {
            RandomProviders.InitializeAll();

            client = new DiscordShardedClient(new DiscordSocketConfig() {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
                MessageCacheSize = 128,
                AlwaysDownloadUsers = true,
            });
            interaction = new InteractionService(client, new()
            {
                DefaultRunMode = RunMode.Async,
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
                .AddSingleton(interaction)
                .BuildServiceProvider();
            client.Log += (msg) => {
                Console.WriteLine($"{DateTime.Now:h:mm:ss tt} | {(msg.Exception == null
                    ? $"\u001b[34m{msg.Source}\u001b[0m: {msg.Message}"
                    : $"\u001b[31m{msg.Source}*\u001b[0m: {msg.Exception.Message}\n\u001b[90m{msg.Exception.StackTrace}\u001b[0m")}");
                return Task.CompletedTask;
            };
            client.ShardReady += async (s) => {
                if (++shr == client.Shards.Count) await ClientReady();
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

        async Task ClientReady() {
            if (firststart) return;
            firststart = true;
            await RegisterCommandsAsync();
            await client.SetStatusAsync(UserStatus.Online);

            if(!Directory.Exists(API.tmpdir))
                Directory.CreateDirectory(API.tmpdir);

            Console.WriteLine($"\u001b[31mDTL:\u001b[0m core ready");
            DirectoryInfo[] fi = new DirectoryInfo(API.tmpdir).GetDirectories();
            if (fi.Length > 0) {
                for (int i = fi.Length; i-->0;) fi[i].Delete(true);
                Console.WriteLine($"\u001b[31mDTL:\u001b[0m {fi.Length} temp directories deleted");
            }
        }

        static async Task RegisterCommandsAsync() {
            try {
                await interaction.AddModulesAsync(Assembly.GetEntryAssembly(), services);
                await interaction.RegisterCommandsGloballyAsync();
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }

            client.SlashCommandExecuted += async (s) => {
                ShardedInteractionContext context = new(client, s);
                IResult r = await interaction.ExecuteCommandAsync(context, services);
                if(!r.IsSuccess){
                    EmbedBuilder embed = new()
                    {
                        Title = "error!",
                        Description = $"*{r.Error}*\n```{r.ErrorReason}```",
                        Color = API.RedColor,
                    };
                    await s.RespondAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
                }
            };

            client.AutocompleteExecuted += async (arg) => {
                InteractionContext context = new(client, arg, arg.Channel);
                IResult r = await interaction.ExecuteCommandAsync(context, services: services);
                if (!r.IsSuccess) Console.WriteLine(r.ErrorReason);
            };
        }
    }
}
