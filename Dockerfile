FROM microsoft/dotnet:2.2-sdk

# Use baseimage-docker's init system.
CMD ["/sbin/my_init"]

ARG NEO_COMPILER_COMMIT=bdbaa79e75728ed1e32503ae971350d551f9f5cb
# ARG NEO_PLUGINS_COMMIT=10a348b94374e5ae92c5b2204ff908753b2273dd
# ARG NEO_CHAIN_ID=00746E41

####### INSTALL NEO #######

# 1. dotnet install
RUN apt-get update

# 2. git install
RUN apt-get -y install git libleveldb-dev sqlite3 libsqlite3-dev libunwind8

# 3. neo-cli download
# RUN git clone https://github.com/neo-project/neo-cli.git /neo-cli
RUN git clone https://github.com/neo-project/neo-compiler.git /neo-compiler

RUN mkdir /neo-contract

WORKDIR /neo-compiler
RUN git reset --hard ${NEO_COMPILER_COMMIT}

RUN dotnet restore
RUN dotnet publish -c release -r linux-x64 -o /neo-contract

WORKDIR /neo-contract
RUN ls -la

RUN mkdir /neo-contract-src
COPY ./NeoContractIco.cs /neo-contract-src/
COPY ./NeoContractIco.csproj /neo-contract-src/

WORKDIR /neo-contract-src
RUN dotnet restore
RUN dotnet publish -c release -r linux-x64 -o /neo-contract

WORKDIR /neo-contract
RUN ls -la
RUN dotnet neon.dll NeoContractIco.dll