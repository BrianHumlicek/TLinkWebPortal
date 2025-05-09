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

using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Messages;

namespace DSC.TLink.ITv2.Transactions
{
	internal class TransactionFactory
	{
		public static ITv2Session.Transaction Create(ITv2Command command, ITv2Session session) => command switch
		{
			//ITv2Command.Connection_Open_Session => new ITv2CommandResponseTransaction<OpenSessionMessage>(session),
			//ITv2Command.Connection_Request_Access => new ITv2CommandResponseTransaction<RequestAccess>(session),
			//ITv2Command.Notification_Time_Date_Broadcast => new ITv2CommandResponseTransaction<OpenSessionMessage>(session),
			_ => throw new NotImplementedException()
		};
	}
}
