using DSC.TLink;
using DSC.TLink.ITv2;
using TLinkWebPortal.Components;
using TLinkWebPortal.Services;
using TLinkWebPortal.Services.Settings;
using MudBlazor.Services;

namespace TLinkWebPortal
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Load user settings file (overrides appsettings.json)
            builder.Configuration.AddJsonFile(
                "userSettings.json", 
                optional: true, 
                reloadOnChange: true);

            // Register settings (will be auto-discovered)
            builder.Services.Configure<ITv2Settings>(
                builder.Configuration.GetSection(ITv2Settings.SectionName));

            // Register settings services
            builder.Services.AddSingleton<ISettingsDiscoveryService, SettingsDiscoveryService>();
            builder.Services.AddSingleton<ISettingsPersistenceService, SettingsPersistenceService>();

            // Add Blazor services
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            // Application services (singletons shared across handlers and UI)
            builder.Services.AddSingleton<IPartitionStatusService, PartitionStatusService>();
            builder.Services.AddSingleton<ISessionMonitor, SessionMonitor>();

            // TLink services + MediatR (TLink handlers only)
            builder.UseITv2();

            // Register web project MediatR handlers (additive to TLink's registration)
            builder.Services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

            // Add MudBlazor services
            builder.Services.AddMudServices();

            var app = builder.Build();

            // Force initialization of discovery service
            app.Services.GetRequiredService<ISettingsDiscoveryService>();

            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

            app.Run();
        }
    }
}
