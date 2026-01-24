// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using DSC.TLink.ITv2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DSC.TLink
{
	public static class StartupExtensions
	{
		public static WebApplicationBuilder UseITv2(this WebApplicationBuilder builder)
		{
            builder.Services.Configure<ITv2Settings>(builder.Configuration.GetSection(ITv2Settings.SectionName));
            builder.Services.AddSingleton(sp => 
                sp.GetRequiredService<IOptions<ITv2Settings>>().Value);

			builder.WebHost.ConfigureKestrel((context, options) =>
			{
                var listenPort = context.Configuration.GetValue($"{ITv2Settings.SectionName}:{nameof(ITv2Settings.ListenPort)}", ITv2Settings.DefaultListenPort);
                
                // Configure ITv2 panel connection port
                options.ListenAnyIP(listenPort, listenOptions =>
				{
					listenOptions.UseConnectionHandler<ITv2ConnectionHandler>();
				});
                
                // Re-add the default web UI port (since ConfigureKestrel disables defaults)
                options.ListenLocalhost(5181); // HTTP
                options.ListenLocalhost(7013, listenOptions => listenOptions.UseHttps()); // HTTPS
			});

            builder.Services.AddScoped<TLinkClient>();
            builder.Services.AddScoped<ITv2Session>();

			builder.Services.AddMediatR((configuration) =>
			{
				configuration.RegisterServicesFromAssemblyContaining<ITv2Session>();
			});
			builder.Services.AddLogging();
			return builder;
		}
	}
}
