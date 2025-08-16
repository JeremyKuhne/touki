# Contributing

## Making Pull Requests (PRs)

They are most welcome! By submitting a PR you:

1. Confirm that you wrote the code (or otherwise have the right to contribute it), **and**
2. Agree that your contribution will be licensed under the MIT License that governs this project.

You retain copyright to your work; you simply grant Jeremy W. Kuhne and all downstream users a perpetual,
irrevocable MIT license to use, modify, and redistribute it.

### Coding Guidelines

See our [Coding Guidelines](docs/coding_guidelines.md) for details.

### Environment setup

For an IDE experience, once you have the [.NET SDK](https://dotnet.microsoft.com/download) installed, open the root folder in Visual Studio Code on any platform.

#### Unix

Run `setup.sh` once after cloning the repository to automatically install the .NET 9 SDK and configure your PATH. The script updates `~/.bashrc` so that the SDK is available in future sessions.

```bash
./setup.sh
```

#### Windows

Presumption is that you already have the relevant .NET SDK installed. To build the repo just run `dotnet build` in the root. If you want to use Visual Studio you can open the `touki.slnx` solution file.

```cmd
dotnet build
```