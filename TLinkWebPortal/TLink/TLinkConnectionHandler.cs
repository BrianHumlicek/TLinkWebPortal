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

using DSC.TLink.ITv2;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DSC.TLink
{
	internal class ITv2ConnectionHandler : ConnectionHandler
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<ITv2ConnectionHandler> _log;

		public ITv2ConnectionHandler(
			IServiceProvider serviceProvider, 
			ILogger<ITv2ConnectionHandler> log)
		{
			_serviceProvider = serviceProvider;
			_log = log;
		}

		public override async Task OnConnectedAsync(ConnectionContext connection)
		{
			_log.LogInformation("Connection request from {RemoteEndPoint}", connection.RemoteEndPoint);
			
			try
			{
				// Create a new scope per connection
				await using var scope = _serviceProvider.CreateAsyncScope();
				
				// Get scoped instances - these will be the same for this connection
				var session = scope.ServiceProvider.GetRequiredService<ITv2Session>();
				var client = scope.ServiceProvider.GetRequiredService<TLinkClient>();
				
				// Or create manually (simpler for connection-based lifecycle):
				// var settings = _serviceProvider.GetRequiredService<ITv2Settings>();
				// var mediator = _serviceProvider.GetRequiredService<IMediator>();
				// var logger = _serviceProvider.GetRequiredService<ILogger<ITv2Session>>();
				// var client = new TLinkClient(logger);
				// var session = new ITv2Session(client, mediator, settings, logger);
				
				await session.ListenAsync(connection.Transport, connection.ConnectionClosed);
			}
			catch (Exception ex)
			{
				_log.LogError(ex, "ITv2 connection error");
			}
			finally
			{
				_log.LogInformation("TLink disconnected from {RemoteEndPoint}", connection.RemoteEndPoint);
			}
		}
	}
}
