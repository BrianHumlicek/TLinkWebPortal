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

namespace DSC.TLink
{
	public static class WebApplicationBuilderExtensions
	{
		public static WebApplicationBuilder UseITv2(this WebApplicationBuilder builder, int port = 3072)
		{
			builder.WebHost.ConfigureKestrel(options =>
			{
				options.ListenAnyIP(port, listenOptions =>
				{
					listenOptions.UseConnectionHandler<ITv2ConnectionHandler>();
				});
			});
            builder.Services.AddTransient<TLinkClient>();
			builder.Services.AddTransient<ITv2Session>();
			builder.Services.AddMediatR((configuration) =>
			{
				configuration.RegisterServicesFromAssemblyContaining<ITv2Session>();
			});
			builder.Services.AddLogging();
			return builder;
		}
	}
}
