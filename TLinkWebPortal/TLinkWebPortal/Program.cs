using DSC.TLink;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using TLinkWebPortal.Client.Pages;
using TLinkWebPortal.Components;
using TLinkWebPortal.Services;

namespace TLinkWebPortal
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

            // Load an optional per-instance JSON file so we can persist user changes.
            // This file will be created/updated at runtime (content root).
            builder.Configuration.AddJsonFile(SettingsService.TLinkSettingsFilename, optional: true, reloadOnChange: true);

            // Bind TLinkSettings from configuration so IOptions/IOptionsMonitor are available
            builder.Services.Configure<TLinkSettings>(builder.Configuration.GetSection(SettingsService.TLinkSettingsSectionName));

            // register settings service that will bind to configuration and persist changes
            builder.Services.AddSingleton<SettingsService>();

            // Add services to the container.
            builder.Services.AddRazorComponents()
				.AddInteractiveServerComponents()
				.AddInteractiveWebAssemblyComponents();

            // read ServerPort from configuration and pass it to UseITv2
            var serverPort = builder.Configuration.GetServerPort();
			builder.UseITv2(serverPort);

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
			{
				app.UseWebAssemblyDebugging();
			}
			else
			{
				app.UseExceptionHandler("/Error");
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
