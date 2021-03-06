using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

public class NeoContractIco: SmartContract
{
	//Token Settings
		public static string Name() => "Ducatur";
		public static string Symbol() => "DUC";
		public static readonly byte[] Owner = "AKQ8cCUoE99ncnRRbaYPit3pV3g58A6FJk".ToScriptHash();
		public static byte Decimals() => 8;
		private const ulong factor = 100000000; //decided by Decimals()
		private const ulong neo_decimals = 100000000;

		//ICO Settings
		private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

		public static readonly string TOTAL_SUPPLY_KEY = "total_supply";
		
		public delegate void MyAction<T>(T p0);
		public delegate void MyAction<T, T1>(T p0, T1 p1);
		public delegate void MyAction<T, T1, T2>(T p0, T1 p1, T2 p2);
		public delegate void MyAction<T, T1, T2, T3>(T p0, T1 p1, T2 p2, T3 p3);

		[DisplayName("transfer")]
		public static event MyAction<byte[], byte[], BigInteger> Transferred;

		[DisplayName("exchange")]
		public static event MyAction<byte[], BigInteger, string, string> Exchanged; // from, tokenAmount, blockchainName, receiver
		
		[DisplayName("mint")]
		public static event MyAction<byte[], BigInteger, byte[], byte[]> Minted; //(toAddress, tokenAmount, fromBlockchain, fromTxId)

		public static Object Main(string operation, params object[] args)
		{
			if (Runtime.Trigger == TriggerType.Verification)
			{
				byte[] owner = GetOwner();
				if (owner.Length == 20) // if param Owner is script hash
				{
					return Runtime.CheckWitness(owner);
				}
				else if (owner.Length == 33) // if param Owner is public key
				{
					byte[] signature = operation.AsByteArray();
					return VerifySignature(signature, owner);
				}
			}
			else if (Runtime.Trigger == TriggerType.Application)
			{
				// Runtime.Log(operation);
				if (operation == "deploy") return Deploy();
				if (operation == "totalSupply") return TotalSupply();
				if (operation == "name") return Name();
				if (operation == "symbol") return Symbol();
				if (operation == "decimals") return Decimals();
				if (operation == "owner") return GetOwner();
				if (operation == "transfer")
				{
					if (args.Length != 3)
					{
						// Runtime.Log("insufficient arguments length! "+args.Length);
						return false;
					}
					
					byte[] from = (byte[])args[0];
					byte[] to = (byte[])args[1];
					BigInteger value = (BigInteger)args[2];
					return Transfer(from, to, value);
				}
				if (operation == "balanceOf")
				{
					if (args.Length != 1)
					{
						// Runtime.Log("insufficient arguments length! "+args.Length);
						return 0;
					}
					
					byte[] account = (byte[])args[0];
					return BalanceOf(account);
				}
				
				if (operation == "exchange")
				{
					if (args.Length != 4)
					{
						// Runtime.Log("insufficient arguments length! "+args.Length);
						return false;
					}
					
					byte[] from = (byte[])args[0];
					BigInteger tokenAmount = (BigInteger)args[1];
					string blockchainName = (string)args[2];
					string receiver = (string)args[3];

					return Exchange(from, tokenAmount, blockchainName, receiver);
				}
				if (operation == "mintTokens")
				{
					if (args.Length != 4)
					{
						// Runtime.Log("insufficient arguments length! "+args.Length);
						return false;
					}
					
					byte[] toAddress = (byte[])args[0];
					BigInteger tokenAmount = (BigInteger)args[1];
					byte[] fromBlockchain = (byte[])args[2];
					byte[] fromTxId = (byte[])args[3];
					
					return MintTokens(toAddress, tokenAmount, fromBlockchain, fromTxId);
				}

				// Runtime.Log("operation not found: "+operation);
				return false;
			}
			// Runtime.Log("unknown Main() behaviour! "+operation);
			return false;
		}

		// initialization parameters, only once
		public static bool Deploy()
		{
			// Runtime.Log("deploying contract...");
			byte[] total_supply = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY_KEY);
			// Runtime.Log($"curent total_supply[{total_supply.Length}]: {total_supply.AsBigInteger()}");
			if (total_supply.Length != 0)
			{
				// Runtime.Log("contract was deployed earlier: total_supply["+total_supply.Length+"] = "+total_supply.AsBigInteger());
				return false;
			}
			byte[] owner = GetOwner();
			if (owner.Length != 0)
			{
				// Runtime.Log("contract was deployed earlier: owner["+owner.Length+"] = '"+owner.AsString()+"'");
				return false;
			}
			Storage.Put(Storage.CurrentContext, "owner", Owner);
			Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY_KEY, 0);
			// Runtime.Log("deployed successfully! ");
			return true;
		}
		
		// Called by the contract owner from a trusted node after another blockchain transfer.
		private static bool MintTokens(byte[] toAddress, BigInteger tokenAmount, byte[] fromBlockchain, byte[] fromTxId)
		{
			string s = "";
			s += s + "MintTokens(): " + tokenAmount.ToByteArray().AsString();
			s += " for " + toAddress.AsString();
			s += " source: ["+fromBlockchain.AsString();
			s += "] " + fromTxId.AsString();
			// Runtime.Log(s);

			if (!Runtime.CheckWitness(GetOwner())) // only contract owner can mint tokens
			{
				// Runtime.Log("not owner! only owner can mint");
				return false;
			}
			
			BigInteger newBalance = BalanceOf(toAddress) + tokenAmount;
			// Runtime.Log("new balance: " + newBalance);
			Storage.Put(Storage.CurrentContext, toAddress, newBalance);
			
			BigInteger totalSupply = TotalSupply();
			// Runtime.Log("total supply: " + totalSupply);
			Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY_KEY, tokenAmount + totalSupply);
			Minted(toAddress, tokenAmount, fromBlockchain, fromTxId);
			// Runtime.Log("minted successfully!");
			return true;
		}
		private static bool Exchange(byte[] from, BigInteger tokenAmount, string blockchainName, string receiver)
		{
			// Runtime.Log("Exchange(): " + tokenAmount + " from " + from.AsString() + " to [" + blockchainName.AsBigInteger() + "] " + receiver);
			if (!Runtime.CheckWitness(from))
			{
				// Runtime.Log("not from address! only from can exchange: " + from.AsString());
				return false; // only tokens owner can exchange tokens for himself
			}
			
			if (!_tryReduceBalance(from, tokenAmount))
			{
				// Runtime.Log("not enough tokens!");
				return false;
			}
			
			Exchanged(from, tokenAmount, blockchainName, receiver);
			
			// Runtime.Log("exchanged successfully!");
			
			return true;
		}

		// get the total token supply
		public static BigInteger TotalSupply()
		{
			return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY_KEY).AsBigInteger();
		}

		// function that is always called when someone wants to transfer tokens.
		public static bool Transfer(byte[] from, byte[] to, BigInteger value)
		{
			// Runtime.Log("Transfer(): " + value + " from " + from + " to " + to);

			if (value <= 0)
			{
				// Runtime.Log("value is too small!");
				return false;
			}
			if (!Runtime.CheckWitness(from))
			{
				// Runtime.Log("'from' address is not authorized!");
				return false;
			}
			if (to.Length != 20)
			{
				// Runtime.Log("'to' address is not 20 chars long (probably wallet address or smth)");
				return false;
			}
			if (from == to)
			{
				// Runtime.Log("sending to myself! (successfully)");
				return true;
			}
			if (!_tryReduceBalance(from, value))
			{
				// Runtime.Log("not enough tokens!");
				return false;
			}
			
			BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
			Storage.Put(Storage.CurrentContext, to, to_value + value);
			Transferred(from, to, value);
			// Runtime.Log("transferred successfully!");
			return true;
		}
		private static bool _tryReduceBalance(byte[] from, BigInteger amount)
		{
			BigInteger balance = BalanceOf(from);
			if (balance < amount)
				return false;
			
			if (balance == amount)
				Storage.Delete(Storage.CurrentContext, from);
			else
				Storage.Put(Storage.CurrentContext, from, balance - amount);
			
			return true;
		}

		// get the account balance of another account with address
		// 根据地址获取token的余额
		public static BigInteger BalanceOf(byte[] address)
		{
			return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
		}


		private static byte[] GetOwner()
		{
			return Storage.Get(Storage.CurrentContext, "owner");
		}
}
