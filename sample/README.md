# Configuring Your Project for Touki

The "sample" project is a sample "template" project to demonstrate how to configure
your own projects to leverage the features provided by Touki.

## Project File Setup

To use Touki effectively when targeting .NET Framework, you need to ensure your project file is set up correctly.

See [sample.csproj](sample.csproj) for a concrete example project for multi-targeting. The key points are:

- Target both .NET Framework (4.7.2 or later) and .NET (10.0 or later) with `<TargetFrameworks>`.
- Disable `<ImplicitUsings>` to allow redirecting `System.IO` to `Microsoft.IO`.

In addition to this, manually configure usings in [GlobalUsings.cs](GlobalUsings.cs) to seamlessly leverage "polyfill"
extensions and manually add the "normal" implicit usings (outside of "System.IO").
