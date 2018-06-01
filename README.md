# NEO NEP-5 Ducatur Token
NEP-5 compatible token with methods to exchange tokens with other blockchains (using external oracle).

### Exchange tokens:
Burns NEO tokens, mints equal amount on another blockchain.  
`Exchange(byte[] from, BigInteger tokenAmount, byte[] blockchainName, byte[] receiver)`  
- `from` — sender wallet address (for authorization checks)  
- `tokenAmount` — amount of tokens to send  
- `blockchainName` — name of the blockchain where tokens should be received  
- `receiver` — receiving wallet address (in target blockchain)

### Mint tokens (contract owner only):
Mints tokens after burning them in another blockchain.  
`MintTokens(byte[] toAddress, BigInteger tokenAmount, byte[] fromBlockchain, byte[] fromTxId)`  
- `toAddress` — receiving NEO wallet address  
- `tokenAmount` — amount of tokens to mint  
- `fromBlockchain` — name of the blockchain where tokens were burned  
- `fromTxId` — hash of the transaction where tokens werer burned (in initiating blockchain)
