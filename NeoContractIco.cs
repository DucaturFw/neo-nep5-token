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
		// public static readonly byte[] Owner = "AKQ8cCUoE99ncnRRbaYPit3pV3g58A6FJk".ToScriptHash();
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
		public static event MyAction<BigInteger, byte[], byte[]> Exchanged;
		
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
				if (operation == "deploy") return Deploy();
				if (operation == "totalSupply") return TotalSupply();
				if (operation == "name") return Name();
				if (operation == "symbol") return Symbol();
				if (operation == "decimals") return Decimals();
				if (operation == "transfer")
				{
					if (args.Length != 3)
						return false;
					
					byte[] from = (byte[])args[0];
					byte[] to = (byte[])args[1];
					BigInteger value = (BigInteger)args[2];
					return Transfer(from, to, value);
				}
				if (operation == "balanceOf")
				{
					if (args.Length != 1)
						return 0;
					
					byte[] account = (byte[])args[0];
					return BalanceOf(account);
				}
				
				if (operation == "exchange")
				{
					if (args.Length != 3)
						return false;
					
					BigInteger tokenAmount = (BigInteger)args[0];
					byte[] blockchainName = (byte[])args[1];
					byte[] receiver = (byte[])args[2];

					return Exchange(tokenAmount, blockchainName, receiver);
				}
				if (operation == "mintTokens")
				{
					if (args.Length != 4)
						return false;
					
					byte[] toAddress = (byte[])args[0];
					BigInteger tokenAmount = (BigInteger)args[1];
					byte[] fromBlockchain = (byte[])args[2];
					byte[] fromTxId = (byte[])args[3];
					
					return MintTokens(toAddress, tokenAmount, fromBlockchain, fromTxId);
				}
			}
			return false;
		}

		// initialization parameters, only once
		// 初始化参数
		public static bool Deploy()
		{
			byte[] total_supply = Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY_KEY);
			if (total_supply.Length != 0) return false;
			Storage.Put(Storage.CurrentContext, "owner", GetSender());
			Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY_KEY, 0);
			return true;
		}

		// Called by the contract owner from a trusted node after another blockchain transfer.
		private static bool MintTokens(byte[] toAddress, BigInteger tokenAmount, byte[] fromBlockchain, byte[] fromTxId)
		{
			byte[] owner = GetOwner();
			if (!Runtime.CheckWitness(owner))
				return false;
			
			byte[] sender = GetSender();
			// contribute asset is not neo
			if (sender.Length == 0)
			{
				return false;
			}
			
			BigInteger newBalance = BalanceOf(toAddress) + tokenAmount;
			Storage.Put(Storage.CurrentContext, toAddress, newBalance);
			
			BigInteger totalSupply = TotalSupply();
			Storage.Put(Storage.CurrentContext, TOTAL_SUPPLY_KEY, tokenAmount + totalSupply);
			Minted(toAddress, tokenAmount, fromBlockchain, fromTxId);
			return true;
		}
		private static bool Exchange(BigInteger tokenAmount, byte[] blockchainName, byte[] receiver)
		{
			byte[] sender = GetSender();
			if (!_tryReduceBalance(sender, tokenAmount))
				return false;
			
			Exchanged(tokenAmount, blockchainName, receiver);
			
			return true;
		}

		// get the total token supply
		// 获取已发行token总量
		public static BigInteger TotalSupply()
		{
			return Storage.Get(Storage.CurrentContext, TOTAL_SUPPLY_KEY).AsBigInteger();
		}

		// function that is always called when someone wants to transfer tokens.
		// 流转token调用
		public static bool Transfer(byte[] from, byte[] to, BigInteger value)
		{
			if (value <= 0) return false;
			if (!Runtime.CheckWitness(from)) return false;
			if (to.Length != 20) return false;
			
			if (from == to) return true;
			if (!_tryReduceBalance(from, value))
				return false;
			
			BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
			Storage.Put(Storage.CurrentContext, to, to_value + value);
			Transferred(from, to, value);
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
		// check whether asset is neo and get sender script hash
		private static byte[] GetSender()
		{
			Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
			TransactionOutput[] reference = tx.GetReferences();
			// you can choice refund or not refund
			foreach (TransactionOutput output in reference)
			{
				if (output.AssetId == neo_asset_id)
					return output.ScriptHash;
			}
			return new byte[]{};
		}

		// get smart contract script hash
		private static byte[] GetReceiver()
		{
			return ExecutionEngine.ExecutingScriptHash;
		}

		// get all you contribute neo amount
		private static ulong GetContributeValue()
		{
			Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
			TransactionOutput[] outputs = tx.GetOutputs();
			ulong value = 0;
			// get the total amount of Neo
			// 获取转入智能合约地址的Neo总量
			foreach (TransactionOutput output in outputs)
			{
				if (output.ScriptHash == GetReceiver() && output.AssetId == neo_asset_id)
				{
					value += (ulong)output.Value;
				}
			}
			return value;
		}
}
