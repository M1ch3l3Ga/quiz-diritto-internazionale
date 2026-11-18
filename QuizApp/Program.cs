using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QuizApp;
using QuizApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var config = new AppConfig();
builder.Configuration.GetSection("Supabase").Bind(config);
config.QuestionsPerSession = builder.Configuration.GetValue("QuestionsPerSession", 20);

builder.Services.AddSingleton(config);
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<SupabaseHttp>();
builder.Services.AddScoped<BrowserStorage>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<QuizService>();
builder.Services.AddScoped<SessionState>();
builder.Services.AddScoped<IStatsStore>(sp => config.IsLocalMode
    ? new LocalStatsStore(sp.GetRequiredService<AuthService>(), sp.GetRequiredService<BrowserStorage>())
    : new SupabaseStatsStore(config, sp.GetRequiredService<AuthService>(), sp.GetRequiredService<SupabaseHttp>()));

await builder.Build().RunAsync();
