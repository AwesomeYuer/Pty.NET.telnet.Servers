# Pty.NET telnet Server
- Too simple `telnet` server, it's `NOT` an complete implementation
- It's only a `too simple` `sample` or `toy` for using `Pty.NET`
- Support only for `Linux`
- Create By `AwesomeYuer`

## Usage:

### Run `Pty.NET.telnet.Server` command on `WSL` or other `Linux` as below:
```sh

dotnet build

dotnet test

# run the telnet Server
dotnet run --project Pty.telnet.TcpListener.Server/Pty.telnet.TcpListener.Server.csproj

```

### Run `telent` client command on `Linux/Windows/MacOS` as below:
```sh

# Connect to TcpListener server

telnet localhost 13000

# Then input any Linux command you want

whoami

pwd

```

# Original information as below:

# Pty.Net
[![NuGet package](https://img.shields.io/nuget/v/Pty.Net.svg)](https://nuget.org/packages/Pty.Net)

Pty.Net is a cross platform, .NET library providing idiomatic bindings for `forkpty()`.

Pty.Net supports Linux, macOS, and Windows. On versions of windows older than 1809 the [winpty](https://github.com/rprichard/winpty) is used. For windows 1809+ this library ships a side-by-side copy of conhost.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
