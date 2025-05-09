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

using System.Security.Cryptography;

namespace DSC.TLink.ITv2
{
	internal abstract class ITv2Encryption : IDisposable
	{
		readonly Aes inboundAES = Aes.Create();
		readonly Aes outboundAES = Aes.Create();
		bool inboundActive;
		bool outboundActive;
		protected void activateInbound(byte[] key)
		{
			inboundAES.Key = key;
			inboundActive = true;
		}
		protected void activateOutbound(byte[] key)
		{
			outboundAES.Key = key;
			outboundActive = true;
		}
		protected byte[] encryptKeyData(byte[] key, byte[] plainText)
		{
			using (Aes aes = Aes.Create())
			{
				aes.Key = key;
				return aes.EncryptEcb(plainText, PaddingMode.Zeros);
			}
		}
		protected byte[] decryptKeyData(byte[] key, byte[] cipherText)
		{
			using (Aes aes = Aes.Create())
			{
				aes.Key = key;
				return aes.DecryptEcb(cipherText, PaddingMode.Zeros);
			}
		}
		public abstract byte[] ConfigureInboundEncryption();
		public abstract void ConfigureOutboundEncryption(byte[] remoteInitializer);
		public byte[] HandleInboundData(byte[] inboundData) => inboundActive ? inboundAES.DecryptEcb(inboundData, PaddingMode.Zeros) : inboundData;
		public byte[] HandleOutboundData(byte[] outboundData) => outboundActive ? outboundAES.EncryptEcb(outboundData, PaddingMode.Zeros) : outboundData;
		public void Dispose()
		{
			inboundAES.Dispose();
			outboundAES.Dispose();
		}
	}
}
