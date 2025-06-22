# Touki (登器): Code for .NET and .NET Framework

[![Build](https://github.com/JeremyKuhne/touki/actions/workflows/dotnet.yml/badge.svg)](https://github.com/JeremyKuhne/touki/actions/workflows/dotnet.yml)

Provides useful functionality both for .NET and .NET Framework applications.

Tōki (登器) is the Japanese word for (Ninja) "climbing gear" or "climbing equipment". This library is designed to help
developers "climb" the challenges of cross framework .NET development by providing tools and utilities that enhance
performance and efficiency.

Some of the design goals include:

- Avoiding unnecessary allocations
- Avoiding code that prevents AOT compilation on .NET

Features:

- Non allocating interpolated string support on .NET Framework (`$"Age: {age}"`)

See [CONTRIBUTING.md](CONTRIBUTING) for more information on how to contribute to this project.

## Environment setup

Run `setup.sh` once after cloning the repository to automatically install the .NET 9 SDK and configure your PATH. The script updates `~/.bashrc` so that the SDK is available in future sessions.

```bash
./setup.sh
```
