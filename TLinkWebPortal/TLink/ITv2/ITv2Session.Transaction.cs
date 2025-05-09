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

using DSC.TLink.ITv2.Messages;

namespace DSC.TLink.ITv2
{
	internal partial class ITv2Session
	{
		public abstract class Transaction
		{
			readonly protected ITv2Session session;
			readonly TaskCompletionSource tcs = new TaskCompletionSource();
			public Task Completion => tcs.Task;
			protected void completeTransaction() => tcs.SetResult();
			public Transaction(ITv2Session session)
			{
				this.session = session;
			}
			public abstract Task ProcessReceivedMessageAsync(ITv2Message message);
		}
	}
}
