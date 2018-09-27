using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

public class NeoContractIco: SmartContract
{
	//Token Settings
	// public static string Name() => "Ducatur";
	// public static string Symbol() => "DUC";
	// public static readonly byte[] Owner = "AKQ8cCUoE99ncnRRbaYPit3pV3g58A6FJk".ToScriptHash();
	// public static byte Decimals() => 8;

	//ICO Settings

	public static readonly string TOTAL_SUPPLY_KEY = "total_supply";
	
	public delegate void MyAction<T>(T p0);
	public delegate void MyAction<T, T1>(T p0, T1 p1);
	public delegate void MyAction<T, T1, T2>(T p0, T1 p1, T2 p2);
	public delegate void MyAction<T, T1, T2, T3>(T p0, T1 p1, T2 p2, T3 p3);
	public delegate void MyAction<T, T1, T2, T3, T4>(T p0, T1 p1, T2 p2, T3 p3, T4 p4);

	[DisplayName("transfer")]
	public static event MyAction<string, byte[], byte[], BigInteger> Transferred;

	[DisplayName("exchange")]
	public static event MyAction<string, byte[], BigInteger, string, string> Exchanged; // from, tokenAmount, blockchainName, receiver
	
	[DisplayName("mint")]
	public static event MyAction<string, byte[], BigInteger, byte[], byte[]> Minted; //(toAddress, tokenAmount, fromBlockchain, fromTxId)

	public static Object Main(string operation, string tokenName, params object[] args)
	{
		if (Runtime.Trigger == TriggerType.Verification)
		{
			byte[] owner = GetContractOwner();
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
			if (tokenName.Length == 0)
			{
				return false;
			}
			// Runtime.Log(operation);

			if (operation == "deploy") return Deploy(tokenName);
			if (operation == "totalSupply") return TotalSupply(tokenName);
			if (operation == "name") return Name(tokenName);
			if (operation == "symbol") return Symbol(tokenName);
			if (operation == "decimals") return Decimals(tokenName);
			if (operation == "owner") return GetContractOwner();
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
				return Transfer(tokenName, from, to, value);
			}
			if (operation == "balanceOf")
			{
				if (args.Length != 1)
				{
					// Runtime.Log("insufficient arguments length! "+args.Length);
					return 0;
				}
				
				byte[] account = (byte[])args[0];
				return BalanceOf(tokenName, account);
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

				return Exchange(tokenName, from, tokenAmount, blockchainName, receiver);
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
				
				return MintTokens(tokenName, toAddress, tokenAmount, fromBlockchain, fromTxId);
			}

			// Runtime.Log("operation not found: "+operation);
			return false;
		}
		// Runtime.Log("unknown Main() behaviour! "+operation);
		return false;
	}

	public static bool CreateToken(string tokenName, string name, string symbol, byte[] owner, byte decimals)
	{
		byte[] exists = Storage.Get(Storage.CurrentContext, tokenName);
		if (exists.Length != 0)
		{
			return false;
		}
		string prefix = tokenName + ".";
		Storage.Put(Storage.CurrentContext, tokenName, tokenName);
		Storage.Put(Storage.CurrentContext, prefix + "NAME", name);
		Storage.Put(Storage.CurrentContext, prefix + "SYM", symbol);
		Storage.Put(Storage.CurrentContext, prefix + "OWNER", owner);
		Storage.Put(Storage.CurrentContext, prefix + "DEC", decimals);
		return true;
	}
	public static string Name(string tokenName)
	{
		return Storage.Get(Storage.CurrentContext, tokenName + ".NAME").AsString();
	}
	public static string Symbol(string tokenName)
	{
		return Storage.Get(Storage.CurrentContext, tokenName + ".SYM").AsString();
	}
	public static byte Decimals(string tokenName)
	{
		return Storage.Get(Storage.CurrentContext, tokenName + ".DEC")[0];
	}
	// initialization parameters, only once
	public static bool Deploy(string tokenName)
	{
		// Runtime.Log("deploying contract...");
		string prefix = tokenName + ".";
		byte[] total_supply = Storage.Get(Storage.CurrentContext, prefix + TOTAL_SUPPLY_KEY);
		// Runtime.Log($"curent total_supply[{total_supply.Length}]: {total_supply.AsBigInteger()}");
		if (total_supply.Length != 0)
		{
			// Runtime.Log("contract was deployed earlier: total_supply["+total_supply.Length+"] = "+total_supply.AsBigInteger());
			return false;
		}
		byte[] owner = GetTokenOwner(tokenName);
		if (owner.Length != 0)
		{
			// Runtime.Log("contract was deployed earlier: owner["+owner.Length+"] = '"+owner.AsString()+"'");
			return false;
		}
		Storage.Put(Storage.CurrentContext, prefix + TOTAL_SUPPLY_KEY, 0);
		// Runtime.Log("deployed successfully! ");
		return true;
	}
	
	// Called by the contract owner from a trusted node after another blockchain transfer.
	private static bool MintTokens(string tokenName, byte[] toAddress, BigInteger tokenAmount, byte[] fromBlockchain, byte[] fromTxId)
	{
		string s = "";
		s += s + "MintTokens(): " + tokenAmount.ToByteArray().AsString();
		s += " for " + toAddress.AsString();
		s += " source: ["+fromBlockchain.AsString();
		s += "] " + fromTxId.AsString();
		// Runtime.Log(s);

		if (!Runtime.CheckWitness(GetTokenOwner(tokenName))) // only contract owner can mint tokens
		{
			// Runtime.Log("not owner! only owner can mint");
			return false;
		}
		string prefix = tokenName + ".";
		
		BigInteger newBalance = BalanceOf(tokenName, toAddress) + tokenAmount;
		// Runtime.Log("new balance: " + newBalance);
		Storage.Put(Storage.CurrentContext, prefix + toAddress.AsString(), newBalance);
		
		BigInteger totalSupply = TotalSupply(tokenName);
		// Runtime.Log("total supply: " + totalSupply);
		Storage.Put(Storage.CurrentContext, prefix + TOTAL_SUPPLY_KEY, tokenAmount + totalSupply);
		Minted(tokenName, toAddress, tokenAmount, fromBlockchain, fromTxId);
		// Runtime.Log("minted successfully!");
		return true;
	}
	private static bool Exchange(string tokenName, byte[] from, BigInteger tokenAmount, string blockchainName, string receiver)
	{
		// Runtime.Log("Exchange(): " + tokenAmount + " from " + from.AsString() + " to [" + blockchainName.AsBigInteger() + "] " + receiver);
		if (!Runtime.CheckWitness(from))
		{
			// Runtime.Log("not from address! only from can exchange: " + from.AsString());
			return false; // only tokens owner can exchange tokens for himself
		}
		
		if (!_tryReduceBalance(tokenName, from, tokenAmount))
		{
			// Runtime.Log("not enough tokens!");
			return false;
		}
		
		Exchanged(tokenName, from, tokenAmount, blockchainName, receiver);
		
		// Runtime.Log("exchanged successfully!");
		
		return true;
	}

	// get the total token supply
	public static BigInteger TotalSupply(string tokenName)
	{
		return Storage.Get(Storage.CurrentContext, tokenName + "." + TOTAL_SUPPLY_KEY).AsBigInteger();
	}

	// function that is always called when someone wants to transfer tokens.
	public static bool Transfer(string tokenName, byte[] from, byte[] to, BigInteger value)
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
		if (!_tryReduceBalance(tokenName, from, value))
		{
			// Runtime.Log("not enough tokens!");
			return false;
		}
		string pfrom = tokenName + "." + from.AsString();
		string pto = tokenName + "." + to.AsString();
		
		BigInteger to_value = Storage.Get(Storage.CurrentContext, pto).AsBigInteger();
		Storage.Put(Storage.CurrentContext, pto, to_value + value);
		Transferred(tokenName, from, to, value);
		// Runtime.Log("transferred successfully!");
		return true;
	}
	private static bool _tryReduceBalance(string tokenName, byte[] from, BigInteger amount)
	{
		BigInteger balance = BalanceOf(tokenName, from);
		if (balance < amount)
			return false;
		
		string prefixedFrom = tokenName + "." + from.AsString();
		
		if (balance == amount)
			Storage.Delete(Storage.CurrentContext, prefixedFrom);
		else
			Storage.Put(Storage.CurrentContext, prefixedFrom, balance - amount);
		
		return true;
	}

	// get the account balance of another account with address
	public static BigInteger BalanceOf(string tokenName, byte[] address)
	{
		return Storage.Get(Storage.CurrentContext, tokenName + "." + address.AsString()).AsBigInteger();
	}

	private static byte[] GetContractOwner()
	{
		return Storage.Get(Storage.CurrentContext, "owner");
	}
	private static byte[] GetTokenOwner(string tokenName)
	{
		return Storage.Get(Storage.CurrentContext, tokenName + ".owner");
	}
}
